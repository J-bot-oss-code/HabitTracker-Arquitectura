using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Excepciones;
using AccessControl.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AccessControl.Application.Service;

public sealed class ServicioLoginExterno : IServicioLoginExterno
{
    private const int LongitudMaximaNombre = 200;

    private readonly IValidadorIdTokenGoogle _validador;
    private readonly IUsuarioRepository _usuarios;
    private readonly ILoginExternoRepository _vinculos;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtProvider _jwtProvider;
    private readonly IVersionesLegales _versiones;
    private readonly ILogger<ServicioLoginExterno> _logger;

    public ServicioLoginExterno(
        IValidadorIdTokenGoogle validador,
        IUsuarioRepository usuarios,
        ILoginExternoRepository vinculos,
        IPasswordHasher hasher,
        IJwtProvider jwtProvider,
        IVersionesLegales versiones,
        ILogger<ServicioLoginExterno> logger)
    {
        _validador = validador;
        _usuarios = usuarios;
        _vinculos = vinculos;
        _hasher = hasher;
        _jwtProvider = jwtProvider;
        _versiones = versiones;
        _logger = logger;
    }

    public async Task<ResultadoLoginExterno> IniciarConGoogleAsync(string idToken, ConsentimientosRegistroDto? consentimientos)
    {
        var identidad = await ValidarIdentidadAsync(idToken);

        // 1. Identidad ya vinculada: es un inicio de sesión normal, con las reglas del login.
        var vinculo = await _vinculos.ObtenerPorSubjectAsync(LoginExterno.ProveedorGoogle, identidad.Subject);
        if (vinculo is not null)
        {
            var existente = await _usuarios.GetByIdAsync(vinculo.UsuarioId)
                ?? throw new TokenExternoInvalidoException();
            await ExigirPuedeEntrarAsync(existente);

            _logger.LogInformation("Inicio de sesión con Google del usuario {UsuarioId}.", existente.Id);
            return new ResultadoLoginExterno(Respuesta(existente), CuentaNueva: false);
        }

        var correo = new Email(identidad.Correo);

        // 2. El correo ya tiene cuenta: no se vincula sola. Hay que confirmar la contraseña (POST /google/vincular).
        if (await _usuarios.CorreoExistsAsync(correo))
        {
            throw new VinculacionRequeridaException();
        }

        // 3. Correo nuevo: se crea la cuenta, activa porque Google ya verificó el correo. Sin la aceptación de las
        // versiones vigentes de los documentos legales no se crea (antes de tocar la base de datos).
        if (consentimientos is null)
        {
            throw new ConsentimientoRequeridoException();
        }

        var aceptados = AceptacionLegal.Validar(_versiones, consentimientos);

        var nuevo = Usuario.CrearConProveedorExterno(NombreDe(identidad), correo);
        var ahora = DateTime.UtcNow;
        var consentimiento = ConsentimientoUsuario.DelRegistro(
            nuevo.Id, aceptados.TerminosVersion, aceptados.PrivacidadVersion,
            aceptados.Marketing.GetValueOrDefault(), ahora, ConsentimientoUsuario.OrigenGoogle);
        var nuevoVinculo = new LoginExterno(nuevo.Id, LoginExterno.ProveedorGoogle, identidad.Subject, ahora);

        await _usuarios.SaveAsync(nuevo, consentimiento, nuevoVinculo);

        _logger.LogInformation("Cuenta creada con Google: {UsuarioId}.", nuevo.Id);
        return new ResultadoLoginExterno(Respuesta(nuevo), CuentaNueva: true);
    }

    public async Task<ResultadoLoginExterno> VincularGoogleAsync(string idToken, string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length > 1024)
        {
            throw new ArgumentException("Las credenciales no son válidas.", nameof(password));
        }

        var identidad = await ValidarIdentidadAsync(idToken);
        var usuario = await _usuarios.GetByCorreoAsync(new Email(identidad.Correo));

        // Sin cuenta con contraseña no hay nada que confirmar: se responde igual que una clave incorrecta y se gasta el
        // mismo tiempo, para no revelar qué correos tienen cuenta.
        if (usuario is null || usuario.PasswordHash is null)
        {
            _hasher.SimularVerificacion(password);
            throw new CredencialesInvalidasException();
        }

        // Mismo bloqueo por intentos fallidos que el inicio de sesión (RF-CA-19).
        if (!_hasher.Verificar(usuario.PasswordHash, password))
        {
            usuario.RegistrarIntentoFallido();
            await _usuarios.UpdateAsync(usuario);

            throw usuario.EstaBloqueado() ? new CuentaBloqueadaException() : new CredencialesInvalidasException();
        }

        if (usuario.EstaBloqueado())
        {
            throw new CuentaBloqueadaException();
        }

        // Quien desactivó su propia cuenta la reactiva al demostrar que es el dueño; una desactivada por un administrador no.
        if (usuario.DesactivadaPorElUsuario)
        {
            usuario.ReactivarTrasDesactivacion();
        }

        // Una cuenta que sigue esperando su código de activación sí puede vincularse: Google acaba de verificar el correo.
        if (!usuario.Activo && !usuario.PendienteDeActivacion)
        {
            throw new CuentaDesactivadaException();
        }

        var existente = await _vinculos.ObtenerPorSubjectAsync(LoginExterno.ProveedorGoogle, identidad.Subject);
        if (existente is not null && existente.UsuarioId != usuario.Id)
        {
            throw new ProveedorYaVinculadoException();
        }

        if (existente is null)
        {
            if (await _vinculos.ObtenerDeUsuarioAsync(usuario.Id, LoginExterno.ProveedorGoogle) is not null)
            {
                throw new ProveedorYaVinculadoException();
            }

            await _vinculos.AgregarAsync(new LoginExterno(usuario.Id, LoginExterno.ProveedorGoogle, identidad.Subject, DateTime.UtcNow));
            _logger.LogInformation("Google vinculado al usuario {UsuarioId}.", usuario.Id);
        }

        if (usuario.PendienteDeActivacion)
        {
            usuario.Activar();
        }

        usuario.RestablecerIntentos();
        await _usuarios.UpdateAsync(usuario);

        return new ResultadoLoginExterno(Respuesta(usuario), CuentaNueva: false);
    }

    public async Task<VinculosDto> ListarVinculosAsync(Guid usuarioId)
    {
        var usuario = await _usuarios.GetByIdAsync(usuarioId)
            ?? throw new UnauthorizedAccessException("Sesión inválida.");
        var vinculos = await _vinculos.ListarDeUsuarioAsync(usuarioId);

        return new VinculosDto(
            usuario.TienePassword,
            vinculos.Select(v => new VinculoDto(v.Proveedor, DateTime.SpecifyKind(v.VinculadoEn, DateTimeKind.Utc))).ToList());
    }

    public async Task<bool> DesvincularAsync(Guid usuarioId, string proveedor)
    {
        var usuario = await _usuarios.GetByIdAsync(usuarioId)
            ?? throw new UnauthorizedAccessException("Sesión inválida.");

        if (await _vinculos.ObtenerDeUsuarioAsync(usuarioId, proveedor) is null)
        {
            return false;
        }

        // Sin contraseña, Google es la única forma de entrar: quitarla dejaría la cuenta inaccesible.
        if (!usuario.TienePassword)
        {
            throw new UltimoMetodoAccesoException();
        }

        var eliminado = await _vinculos.EliminarAsync(usuarioId, proveedor);
        if (eliminado)
        {
            // El proveedor llega en la ruta de la petición: no se escribe en el log (solo el identificador del usuario).
            _logger.LogInformation("Proveedor externo desvinculado del usuario {UsuarioId}.", usuarioId);
        }

        return eliminado;
    }

    private async Task<IdentidadExterna> ValidarIdentidadAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken) || idToken.Length > 4096)
        {
            throw new TokenExternoInvalidoException();
        }

        var identidad = await _validador.ValidarAsync(idToken);

        // Un correo que Google no verificó no demuestra que la persona lo controle: no sirve para entrar ni vincular.
        if (!identidad.CorreoVerificado
            || string.IsNullOrWhiteSpace(identidad.Subject)
            || string.IsNullOrWhiteSpace(identidad.Correo))
        {
            throw new TokenExternoInvalidoException();
        }

        return identidad;
    }

    /// <summary>Las mismas reglas del inicio de sesión con contraseña para una cuenta ya vinculada (RF-CA-15, RF-CA-19, RF-CA-20).</summary>
    private async Task ExigirPuedeEntrarAsync(Usuario usuario)
    {
        if (usuario.EstaBloqueado())
        {
            throw new CuentaBloqueadaException();
        }

        if (!usuario.Activo)
        {
            if (!usuario.DesactivadaPorElUsuario)
            {
                throw usuario.PendienteDeActivacion ? new CuentaNoActivaException() : new CuentaDesactivadaException();
            }

            // La propia persona la había desactivado y acaba de demostrar con Google que es el dueño: se reactiva.
            usuario.ReactivarTrasDesactivacion();
            await _usuarios.UpdateAsync(usuario);
        }
    }

    private static string NombreDe(IdentidadExterna identidad)
    {
        var nombre = identidad.Nombre?.Trim();
        if (string.IsNullOrEmpty(nombre))
        {
            nombre = identidad.Correo.Split('@')[0];
        }

        return nombre.Length > LongitudMaximaNombre ? nombre[..LongitudMaximaNombre] : nombre;
    }

    private UsuarioResponseDto Respuesta(Usuario usuario) => new(
        usuario.Id, usuario.NombreCompleto, usuario.Correo.Valor, usuario.Rol.ToString(), usuario.Activo,
        _jwtProvider.Generar(usuario), BajaProgramadaPara: FechasUtc.ComoUtc(usuario.BajaProgramadaPara));
}
