using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Excepciones;
using AccessControl.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AccessControl.Application.Service;

public sealed class ServicioPrivacidad : IServicioPrivacidad
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IConsentimientoRepository _consentimientos;
    private readonly ILoginExternoRepository _vinculos;
    private readonly ISesionRepository _sesionesRepo;
    private readonly IPasswordHasher _hasher;
    private readonly IValidadorIdTokenGoogle _validadorGoogle;
    private readonly IGestorSesiones _sesiones;
    private readonly IEnumerable<IParticipanteDatosUsuario> _participantes;
    private readonly IAuditoriaService _auditoria;
    private readonly OpcionesPrivacidad _opciones;
    private readonly ILogger<ServicioPrivacidad> _logger;

    public ServicioPrivacidad(
        IUsuarioRepository usuarios,
        IConsentimientoRepository consentimientos,
        ILoginExternoRepository vinculos,
        ISesionRepository sesionesRepo,
        IPasswordHasher hasher,
        IValidadorIdTokenGoogle validadorGoogle,
        IGestorSesiones sesiones,
        IEnumerable<IParticipanteDatosUsuario> participantes,
        IAuditoriaService auditoria,
        OpcionesPrivacidad opciones,
        ILogger<ServicioPrivacidad> logger)
    {
        _usuarios = usuarios;
        _consentimientos = consentimientos;
        _vinculos = vinculos;
        _sesionesRepo = sesionesRepo;
        _hasher = hasher;
        _validadorGoogle = validadorGoogle;
        _sesiones = sesiones;
        _participantes = participantes;
        _auditoria = auditoria;
        _opciones = opciones;
        _logger = logger;
    }

    public async Task<BajaSolicitadaDto> SolicitarBajaAsync(Guid usuarioId, string modo, string? password, string? idTokenGoogle)
    {
        // RD-07: el modo se valida contra la lista cerrada antes de tocar la base de datos. Lo que se usa después es la
        // constante, nunca el texto que llegó en la petición.
        var modoValido = ModoValido(modo);

        var usuario = await _usuarios.GetByIdAsync(usuarioId)
            ?? throw new UnauthorizedAccessException("Sesión inválida.");

        // Los administradores quedan fuera: darse de baja podría dejar al sistema sin nadie que lo administre.
        if (usuario.Rol == Rol.Administrador)
        {
            throw new OperacionNoPermitidaException();
        }

        await ConfirmarIdentidadAsync(usuario, password, idTokenGoogle);

        var ahora = DateTime.UtcNow;
        DateTime? borradoEn = null;

        if (modoValido == IServicioPrivacidad.ModoDesactivar)
        {
            usuario.SolicitarDesactivacion(ahora);
        }
        else
        {
            usuario.ProgramarBaja(ahora, _opciones.DiasGraciaBaja);
            borradoEn = FechasUtc.ComoUtc(usuario.BajaProgramadaPara);
        }

        await _usuarios.UpdateAsync(usuario);

        // Las sesiones abiertas dejan de servir en el momento: el cliente tiene que volver a iniciar sesión.
        await _sesiones.RevocarTodasAsync(usuario.Id, modoValido == IServicioPrivacidad.ModoDesactivar
            ? "desactivacion-por-el-usuario"
            : "baja-solicitada");

        await _auditoria.RegistroAuditoriaAsync(
            usuario.Id, usuario.Rol.ToString(), "Solicitud de baja de cuenta", "Usuario", usuario.Id.ToString(),
            "Cuenta activa", modoValido == IServicioPrivacidad.ModoDesactivar ? "Desactivada por su dueño" : "Eliminación programada");

        _logger.LogInformation("Baja solicitada por el usuario {UsuarioId} ({Modo}).", usuario.Id, modoValido);
        return new BajaSolicitadaDto(modoValido, borradoEn);
    }

    public async Task<bool> CancelarBajaAsync(Guid usuarioId)
    {
        var usuario = await _usuarios.GetByIdAsync(usuarioId)
            ?? throw new UnauthorizedAccessException("Sesión inválida.");

        if (!usuario.CancelarBaja())
        {
            return false;
        }

        await _usuarios.UpdateAsync(usuario);
        await _auditoria.RegistroAuditoriaAsync(
            usuario.Id, usuario.Rol.ToString(), "Cancelación de baja de cuenta", "Usuario", usuario.Id.ToString(),
            "Eliminación programada", "Cuenta activa");

        _logger.LogInformation("Baja cancelada por el usuario {UsuarioId}.", usuario.Id);
        return true;
    }

    public async Task<DateTime?> ObtenerBajaProgramadaAsync(Guid usuarioId)
    {
        var usuario = await _usuarios.GetByIdAsync(usuarioId)
            ?? throw new UnauthorizedAccessException("Sesión inválida.");

        return FechasUtc.ComoUtc(usuario.BajaProgramadaPara);
    }

    public async Task<ExportacionDatosDto> ExportarAsync(Guid usuarioId, CancellationToken cancelacion = default)
    {
        var usuario = await _usuarios.GetByIdAsync(usuarioId)
            ?? throw new UnauthorizedAccessException("Sesión inválida.");

        if (usuario.Rol == Rol.Administrador)
        {
            throw new OperacionNoPermitidaException();
        }

        var ahora = DateTime.UtcNow;
        var historial = await _consentimientos.ListarHistorialAsync(usuarioId);
        var vinculos = await _vinculos.ListarDeUsuarioAsync(usuarioId);
        var sesiones = await _sesionesRepo.ListarActivasAsync(usuarioId, ahora);

        // Si un módulo falla la descarga falla entera: un archivo incompleto que parece completo es peor que reintentar.
        var modulos = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var participante in _participantes)
        {
            modulos[participante.Seccion] = await participante.ExportarAsync(usuarioId, usuario.Correo.Valor, cancelacion);
        }

        await _auditoria.RegistroAuditoriaAsync(
            usuario.Id, usuario.Rol.ToString(), "Descarga de datos personales", "Usuario", usuario.Id.ToString(), "-", "-");

        // Nunca se incluye la contraseña, su hash, el código de recuperación ni los tokens de las sesiones.
        return new ExportacionDatosDto(
            ahora,
            new CuentaExportadaDto(
                usuario.Id, usuario.NombreCompleto, usuario.Correo.Valor, usuario.Rol.ToString(), usuario.Activo,
                FechasUtc.ComoUtc(usuario.FechaCreacion), usuario.TienePassword,
                FechasUtc.ComoUtc(usuario.DesactivadaPorUsuarioEn), FechasUtc.ComoUtc(usuario.BajaProgramadaPara)),
            historial.Select(c => new ConsentimientoExportadoDto(
                c.TerminosVersion, c.PrivacidadVersion, c.DatosSalud, c.Marketing, c.CompartirCumplimiento,
                c.EstadisticasAnonimas, FechasUtc.ComoUtc(c.AceptadoEn), FechasUtc.ComoUtc(c.ActualizadoEn), c.Origen)).ToList(),
            vinculos.Select(v => new VinculoDto(v.Proveedor, FechasUtc.ComoUtc(v.VinculadoEn))).ToList(),
            sesiones.Select(s => new SesionExportadaDto(
                s.Id, s.Dispositivo, s.IpCreacion, FechasUtc.ComoUtc(s.CreadaEn), FechasUtc.ComoUtc(s.UltimoUsoEn),
                FechasUtc.ComoUtc(s.ExpiraEn))).ToList(),
            modulos);
    }

    private static string ModoValido(string? modo)
    {
        if (string.Equals(modo, IServicioPrivacidad.ModoDesactivar, StringComparison.OrdinalIgnoreCase))
        {
            return IServicioPrivacidad.ModoDesactivar;
        }

        if (string.Equals(modo, IServicioPrivacidad.ModoEliminar, StringComparison.OrdinalIgnoreCase))
        {
            return IServicioPrivacidad.ModoEliminar;
        }

        throw new ArgumentException("El modo de baja no es válido.", nameof(modo));
    }

    /// <summary>
    /// Confirma que quien pide la baja es el dueño de la cuenta, con la contraseña o, si la cuenta no tiene, con Google.
    /// Un fallo es siempre un 400 (nunca un 401, que cerraría la sesión en el cliente).
    /// </summary>
    private async Task ConfirmarIdentidadAsync(Usuario usuario, string? password, string? idTokenGoogle)
    {
        if (usuario.TienePassword)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length > 1024)
            {
                throw new ClaveActualIncorrectaException();
            }

            // Mismo bloqueo por intentos fallidos que el inicio de sesión (RF-CA-19): quien robó un token no puede
            // adivinar la contraseña para borrar la cuenta.
            if (!_hasher.Verificar(usuario.PasswordHash!, password))
            {
                usuario.RegistrarIntentoFallido();
                await _usuarios.UpdateAsync(usuario);

                throw usuario.EstaBloqueado() ? new CuentaBloqueadaException() : new ClaveActualIncorrectaException();
            }

            if (usuario.EstaBloqueado())
            {
                throw new CuentaBloqueadaException();
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(idTokenGoogle) || idTokenGoogle.Length > 4096)
        {
            throw new ConfirmacionIncorrectaException();
        }

        IdentidadExterna identidad;
        try
        {
            identidad = await _validadorGoogle.ValidarAsync(idTokenGoogle);
        }
        catch (TokenExternoInvalidoException)
        {
            // El validador responde 401 para un token falso; aquí sería cerrar la sesión de quien solo se equivocó.
            throw new ConfirmacionIncorrectaException();
        }

        var vinculo = identidad.CorreoVerificado
            ? await _vinculos.ObtenerPorSubjectAsync(LoginExterno.ProveedorGoogle, identidad.Subject)
            : null;

        // El token tiene que ser de la cuenta de Google vinculada a ESTA cuenta, no de cualquiera.
        if (vinculo is null || vinculo.UsuarioId != usuario.Id)
        {
            throw new ConfirmacionIncorrectaException();
        }
    }
}
