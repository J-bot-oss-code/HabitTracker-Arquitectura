using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Excepciones;
using AccessControl.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AccessControl.Application.Service;

public sealed class GestorSesiones : IGestorSesiones
{
    private const int LongitudMaximaToken = 128;

    // Un refresh token ya vencido se conserva un tiempo para poder detectar que alguien lo reutiliza.
    private static readonly TimeSpan RetencionDeVencidas = TimeSpan.FromDays(30);

    private readonly ISesionRepository _sesiones;
    private readonly IUsuarioRepository _usuarios;
    private readonly ITokenBlacklist _blacklist;
    private readonly IJwtProvider _jwtProvider;
    private readonly OpcionesSesion _opciones;
    private readonly ILogger<GestorSesiones> _logger;

    public GestorSesiones(
        ISesionRepository sesiones,
        IUsuarioRepository usuarios,
        ITokenBlacklist blacklist,
        IJwtProvider jwtProvider,
        OpcionesSesion opciones,
        ILogger<GestorSesiones> logger)
    {
        _sesiones = sesiones;
        _usuarios = usuarios;
        _blacklist = blacklist;
        _jwtProvider = jwtProvider;
        _opciones = opciones;
        _logger = logger;
    }

    public async Task<SesionEmitida> IniciarAsync(Guid usuarioId, string? dispositivo, string? ip)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("El usuario no es válido.", nameof(usuarioId));
        }

        var ahora = DateTime.UtcNow;
        var (token, hash) = NuevoToken();
        var sesion = new SesionUsuario(usuarioId, hash, ahora, _opciones.DuracionRefresh, dispositivo, ip);

        await _sesiones.AgregarAsync(sesion);

        // Limpieza oportunista: no hace falta un proceso aparte para que la tabla no crezca sin límite.
        await _sesiones.EliminarVencidasAsync(ahora - RetencionDeVencidas);

        _logger.LogInformation("Sesión iniciada para el usuario {UsuarioId}.", usuarioId);
        return new SesionEmitida(token, sesion.ExpiraEn);
    }

    public async Task<SesionRenovada> RenovarAsync(string? refreshToken)
    {
        if (!TokenConFormatoValido(refreshToken))
        {
            throw new SesionInvalidaException();
        }

        var ahora = DateTime.UtcNow;
        var actual = await _sesiones.ObtenerPorHashAsync(Hash(refreshToken!));
        if (actual is null)
        {
            throw new SesionInvalidaException();
        }

        // Un token revocado o ya reemplazado que vuelve a aparecer lo tiene alguien que no debería: o el dueño o el
        // ladrón ya usó la copia. No se sabe cuál, así que se cierra toda la familia y hay que iniciar sesión otra vez.
        if (actual.RevocadaEn is not null)
        {
            await _sesiones.RevocarFamiliaAsync(actual.FamiliaId, ahora);
            _logger.LogWarning("Se reutilizó un refresh token ya revocado del usuario {UsuarioId}: se revocó su familia de sesiones.", actual.UsuarioId);
            throw new SesionInvalidaException();
        }

        if (actual.ExpiraEn <= ahora)
        {
            throw new SesionInvalidaException();
        }

        var usuario = await _usuarios.GetByIdAsync(actual.UsuarioId);
        if (usuario is null || !usuario.Activo)
        {
            // La cuenta se desactivó o desapareció: la sesión no puede seguir renovándose.
            await _sesiones.RevocarFamiliaAsync(actual.FamiliaId, ahora);
            throw new SesionInvalidaException();
        }

        if (usuario.EstaBloqueado())
        {
            throw new SesionInvalidaException();
        }

        var (nuevoToken, nuevoHash) = NuevoToken();
        var siguiente = actual.SiguienteEnLaFamilia(nuevoHash, ahora, _opciones.DuracionRefresh);

        // Atómico: si otra petición ya canjeó este mismo token entre la lectura y ahora, aquí no se gana la carrera.
        if (!await _sesiones.RotarAsync(actual.Id, siguiente, ahora))
        {
            await _sesiones.RevocarFamiliaAsync(actual.FamiliaId, ahora);
            _logger.LogWarning("Se canjeó dos veces un mismo refresh token del usuario {UsuarioId}: se revocó su familia de sesiones.", actual.UsuarioId);
            throw new SesionInvalidaException();
        }

        var acceso = _jwtProvider.Generar(usuario);
        var respuesta = new UsuarioResponseDto(
            usuario.Id, usuario.NombreCompleto, usuario.Correo.Valor, usuario.Rol.ToString(), usuario.Activo, acceso,
            BajaProgramadaPara: FechasUtc.ComoUtc(usuario.BajaProgramadaPara));

        return new SesionRenovada(respuesta, nuevoToken, siguiente.ExpiraEn);
    }

    public async Task CerrarAsync(Guid usuarioId, string? refreshToken)
    {
        if (!TokenConFormatoValido(refreshToken))
        {
            return;
        }

        var sesion = await _sesiones.ObtenerPorHashAsync(Hash(refreshToken!));

        // Solo se cierra la sesión del propio usuario: una cookie ajena no cierra la sesión de otra persona.
        if (sesion is not null && sesion.UsuarioId == usuarioId)
        {
            await _sesiones.RevocarFamiliaAsync(sesion.FamiliaId, DateTime.UtcNow);
        }
    }

    public async Task RevocarTodasAsync(Guid usuarioId, string motivo)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("El usuario no es válido.", nameof(usuarioId));
        }

        // Los refresh tokens se revocan en la tabla de sesiones y los tokens de acceso ya emitidos con la hora de corte.
        await _sesiones.RevocarTodasAsync(usuarioId, DateTime.UtcNow);
        _blacklist.RevocarSesionesAnteriores(usuarioId);

        _logger.LogInformation("Sesiones del usuario {UsuarioId} revocadas ({Motivo}).", usuarioId, motivo);
    }

    public async Task<IReadOnlyList<SesionActivaDto>> ListarAsync(Guid usuarioId, string? refreshActual)
    {
        var hashActual = TokenConFormatoValido(refreshActual) ? Hash(refreshActual!) : null;
        var activas = await _sesiones.ListarActivasAsync(usuarioId, DateTime.UtcNow);

        // SQL Server devuelve las fechas sin zona (Unspecified): se marcan como UTC para que el JSON lleve la "Z".
        return activas
            .Select(s => new SesionActivaDto(
                s.Id,
                s.Dispositivo,
                DateTime.SpecifyKind(s.CreadaEn, DateTimeKind.Utc),
                DateTime.SpecifyKind(s.UltimoUsoEn, DateTimeKind.Utc),
                hashActual is not null && s.TokenHash == hashActual))
            .ToList();
    }

    public async Task<bool> RevocarAsync(Guid usuarioId, Guid sesionId)
    {
        var ahora = DateTime.UtcNow;
        var sesion = await _sesiones.ObtenerAsync(usuarioId, sesionId);
        if (sesion is null || !sesion.EstaActiva(ahora))
        {
            return false;
        }

        await _sesiones.RevocarFamiliaAsync(sesion.FamiliaId, ahora);
        _logger.LogInformation("Sesión {SesionId} del usuario {UsuarioId} revocada por su dueño.", sesionId, usuarioId);
        return true;
    }

    private static bool TokenConFormatoValido(string? token) =>
        !string.IsNullOrWhiteSpace(token) && token.Length <= LongitudMaximaToken;

    /// <summary>32 bytes de un generador criptográfico en Base64Url (43 caracteres) y su hash SHA-256 en hexadecimal.</summary>
    private static (string Token, string Hash) NuevoToken()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
