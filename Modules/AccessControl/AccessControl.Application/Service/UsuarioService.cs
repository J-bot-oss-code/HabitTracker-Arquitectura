using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Domain.Interfaces;
using AccessControl.Domain.Entities.ValueObjects;
using Microsoft.Extensions.Logging;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Excepciones;
using System.Diagnostics;
using System.Security.Cryptography;

namespace AccessControl.Application.Service;

public class UsuarioService : IUsuarioService
{
    private const int HorasValidezActivacion = 24;
    private static readonly TimeSpan EnfriamientoReenvioActivacion = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan EnfriamientoRecuperacion = TimeSpan.FromSeconds(60);

    private readonly IUsuarioRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailQueue _emailQueue;
    private readonly IAuditoriaService _auditoria;
    private readonly IJwtProvider _jwtProvider;
    private readonly IGestorSesiones _sesiones;
    private readonly ILogger<UsuarioService> _logger;
    private readonly IPoliticaContrasena _politica;
    private readonly IVersionesLegales _versiones;
    public UsuarioService(
        IUsuarioRepository repository, 
        IPasswordHasher passwordHasher, 
        IEmailQueue emailQueue,
        IAuditoriaService auditoria,
        IJwtProvider jwtProvider,
        IGestorSesiones sesiones,
        ILogger<UsuarioService> logger,
        IPoliticaContrasena? politica = null,
        IVersionesLegales? versiones = null)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _emailQueue = emailQueue;
        _auditoria = auditoria;
        _jwtProvider = jwtProvider;
        _sesiones = sesiones;
        _logger = logger;
        // En la aplicación siempre se inyecta la política completa; sin ella (pruebas) solo se comprueba el formato.
        _politica = politica ?? PoliticaContrasena.SoloFormato;
        _versiones = versiones ?? VersionesLegales.PorDefecto;
    }

   

    public async Task<UsuarioResponseDto> AutenticarUsuarioAsync(LoginRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var email = new Email(request.Email);
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 1024)
        {
            throw new ArgumentException("Las credenciales no son válidas.", nameof(request));
        }

        var usuario = await _repository.GetByCorreoAsync(email);

        // El correo es un dato personal que viene de la petición: no se escribe en el log (ni siquiera enmascarado).
        // Se registra solo el identificador del usuario, que sale de la base de datos.
        // 1. RF-CA-03: sin revelar qué dato falló. Si el correo no existe se gasta el mismo tiempo que una verificación
        // real: sin esto, la respuesta tardaría ~250 ms menos y delataría qué correos están registrados.
        if (usuario == null)
        {
            _logger.LogInformation("Intento de login con un correo no registrado.");
            _passwordHasher.SimularVerificacion(request.Password);
            throw new CredencialesInvalidasException();
        }

        _logger.LogInformation("Intento de login para el usuario {UsuarioId}.", usuario.Id);

        // 2. Se verifica la clave ANTES de revelar si la cuenta está inactiva:
        // una clave incorrecta en cuenta activa o inactiva responde 401 genérico.
        // Solo si ya está bloqueada (el atacante provocó el bloqueo) se informa el bloqueo.
        if (!_passwordHasher.Verificar(usuario.PasswordHash, request.Password))
        {
            usuario.RegistrarIntentoFallido();
            await _repository.UpdateAsync(usuario);

            if (usuario.EstaBloqueado())
            {
                throw new CuentaBloqueadaException();
            }

            throw new CredencialesInvalidasException();
        }

        // 3. RF-CA-19: cuenta bloqueada por intentos fallidos (solo con clave correcta).
        if (usuario.EstaBloqueado())
        {
            throw new CuentaBloqueadaException();
        }

        // 4. RF-CA-15 / RF-CA-20: la cuenta debe estar activa (solo con clave correcta). El código distingue
        // "falta activarla con el código del correo" de "un administrador la desactivó".
        if (!usuario.Activo)
        {
            throw usuario.PendienteDeActivacion ? new CuentaNoActivaException() : new CuentaDesactivadaException();
        }

        // 5. Éxito: se restablecen intentos, se persiste y se emite el JWT.
        usuario.RestablecerIntentos();
        await _repository.UpdateAsync(usuario);

        var token = _jwtProvider.Generar(usuario);

        return new UsuarioResponseDto(
            usuario.Id,
            usuario.NombreCompleto,
            usuario.Correo.Valor,
            usuario.Rol.ToString(),
            usuario.Activo,
            token
        );
    }

    public async Task CambiarRolAsync(Guid usuarioId, string nuevoRol, Guid adminId)
    {
        if (usuarioId == Guid.Empty || adminId == Guid.Empty || string.IsNullOrWhiteSpace(nuevoRol))
        {
            throw new ArgumentException("Los datos para cambiar el rol no son válidos.");
        }

       _logger.LogInformation("Cambio de rol para el usuario {usuarioId} a {nuevoRol} por el admin {adminId}", usuarioId, nuevoRol, adminId);

        // RF-CA-05 / RF-CA-06: solo un Administrador puede cambiar roles,
        // verificado en el servidor contra el ejecutor (adminId), no el destino.
        await ExigirAdministradorAsync(adminId);

        var usuario = await _repository.GetByIdAsync(usuarioId);
        if(usuario == null)
        {
            throw new InvalidOperationException("El usuario no existe");
        }

        if (!Enum.TryParse<Rol>(nuevoRol, ignoreCase: true, out var rol) || !Enum.IsDefined(rol))
        {
            throw new InvalidOperationException("El rol especificado no es válido");
        }

        // Quitar el rol al último administrador activo dejaría el sistema sin nadie que pueda administrarlo.
        if (usuario.Rol == Rol.Administrador && rol != Rol.Administrador && usuario.Activo
            && await _repository.ContarAdministradoresActivosAsync() <= 1)
        {
            throw new UltimoAdministradorException();
        }

        var rolAnterior = usuario.Rol;

        usuario.CambiarRol(rol);

        await _repository.UpdateAsync(usuario);

        // RF-CA-12: el rol viaja dentro del JWT. Sin cortar las sesiones abiertas, un administrador degradado
        // conservaba sus permisos hasta que su token venciera.
        await _sesiones.RevocarTodasAsync(usuarioId, "cambio-de-rol");

         // RF-CA-08: Auditoría estricta
        await _auditoria.RegistroAuditoriaAsync(
            adminId,
            "Administrador",
            "Cambio de rol",
            "Usuario",
            usuario.Id.ToString(),
            rolAnterior.ToString(),
            rol.ToString()
        );
    }

    public async Task<IEnumerable<UsuarioResponseDto>> ObtenerTodosAsync()
    {
        _logger.LogInformation("Consultando lista de usuarios.");

        var usuarios = await _repository.ObtenerTodosAsync();

        return usuarios.Select(u => new UsuarioResponseDto(
            u.Id,
            u.NombreCompleto,
            u.Correo.Valor,
            u.Rol.ToString(),
            u.Activo
        ));
    }

    public async Task CambiarEstadoAsync(Guid usuarioId, bool nuevoEstado, Guid adminId)
    {
        if (usuarioId == Guid.Empty || adminId == Guid.Empty)
        {
            throw new ArgumentException("Los identificadores de usuario no son válidos.");
        }

        _logger.LogInformation("Cambio de estado para el usuario {usuarioId} a {nuevoEstado} por el admin {adminId}", usuarioId, nuevoEstado, adminId);

        // RF-CA-05 / RF-CA-06: la autorización se verifica también aquí, no solo en el atributo del controlador (RD-06).
        await ExigirAdministradorAsync(adminId);

        if (usuarioId == adminId)
        {
            throw new InvalidOperationException("Un administrador no puede desactivarse a sí mismo.");
        }

        var usuario = await _repository.GetByIdAsync(usuarioId);
        if (usuario == null)
        {
            throw new InvalidOperationException("El usuario no existe");
        }

        var estadoAnterior = usuario.Activo;

        if (nuevoEstado)
        {
            usuario.Activar();
        }
        else
        {
            if (usuario.Rol == Rol.Administrador && usuario.Activo
                && await _repository.ContarAdministradoresActivosAsync() <= 1)
            {
                throw new UltimoAdministradorException();
            }

            usuario.Desactivar();

            // RF-CA-20: las sesiones abiertas del usuario desactivado dejan de ser válidas.
            await _sesiones.RevocarTodasAsync(usuarioId, "cuenta-desactivada");
        }

        await _repository.UpdateAsync(usuario);

        // RF-CA-08: Auditoría estricta
        await _auditoria.RegistroAuditoriaAsync(
            adminId,
            "Administrador",
            "Cambio de estado",
            "Usuario",
            usuario.Id.ToString(),
            estadoAnterior.ToString(),
            nuevoEstado.ToString()
        );
    }

    public async Task IniciarRecuperacionAsync(string correo)
    {
        var inicio = Stopwatch.GetTimestamp();
        var Email = new Email(correo);

        var usuario = await _repository.GetByCorreoAsync(Email);

        // RF-CA-09: respuesta idéntica exista o no el correo, sin revelar registros.
        if (usuario == null)
        {
            _logger.LogInformation("Recuperación de contraseña solicitada para un correo no registrado.");
            await EsperarRespuestaMinimaAsync(inicio);
            return;
        }

        _logger.LogInformation("Recuperación de contraseña iniciada para el usuario {UsuarioId}.", usuario.Id);

        // Enfriamiento: sin él, cada solicitud emitía un código nuevo con el contador de fallos en cero (se podían
        // probar códigos sin fin) y se podía inundar de correos a la víctima. Dentro del minuto se responde igual
        // (idéntico a un correo inexistente, RF-CA-09) pero no se emite ni se envía nada.
        if (usuario.Recuperacion is { Usado: false } vigente
            && DateTime.UtcNow - vigente.FechaEmision < EnfriamientoRecuperacion)
        {
            await EsperarRespuestaMinimaAsync(inicio);
            return;
        }

        var codigoGenerado = GenerarCodigoRecuperacion();

        usuario.GenerarCodigoRecuperacion(codigoGenerado, 1);

        await _repository.UpdateAsync(usuario);

        // Se envía al correo normalizado de la cuenta, no al texto que llegó en la petición.
        await _emailQueue.EncolarCorreoRecuperacionAsync(usuario.Correo.Valor, codigoGenerado);
        await EsperarRespuestaMinimaAsync(inicio);
    }

    public async Task RestablecerPasswordAsync(string correo, string codigo, string nuevaPassword)
    {
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length > 64)
        {
            throw new ArgumentException("El código de recuperación no es válido.", nameof(codigo));
        }

        _logger.LogInformation("Restablecimiento de contraseña solicitado.");

        _politica.ValidarFormato(nuevaPassword);

        var usuario = await _repository.GetByCorreoAsync(new Email(correo));

        // Mensaje uniforme para no revelar qué correos están registrados.
        // Cada código incorrecto cuenta: a los 5 fallos el código se invalida.
        if (usuario == null || usuario.Recuperacion == null || !usuario.Recuperacion.EsValido(codigo))
        {
            if (usuario?.Recuperacion != null)
            {
                usuario.Recuperacion.RegistrarFallo();
                await _repository.UpdateAsync(usuario);
            }

            throw new CodigoInvalidoException();
        }

        // Solo con el código ya comprobado se consulta el servicio externo de filtraciones; si la clave está filtrada el
        // código NO se consume y la persona puede elegir otra.
        await _politica.ValidarNoFiltradaAsync(nuevaPassword);

        var nuevoHash = _passwordHasher.Hash(nuevaPassword);

        // RF-CA-11: valida el código (un solo uso + vencimiento) y lo marca como usado.
        usuario.RestablecerPassword(nuevoHash, codigo);

        // Quien demostró controlar el correo recupera el acceso: se levanta el bloqueo por intentos fallidos (RF-CA-19).
        usuario.RestablecerIntentos();

        // RF-CA-12: las sesiones abiertas antes del cambio dejan de ser válidas.
        await _sesiones.RevocarTodasAsync(usuario.Id, "restablecimiento-de-contrasena");

        await _repository.UpdateAsync(usuario);
    }

    public async Task CambiarPasswordAsync(Guid usuarioId, string actual, string nueva)
    {
        if (usuarioId == Guid.Empty || string.IsNullOrWhiteSpace(actual) || actual.Length > 1024)
        {
            throw new ArgumentException("Los datos para cambiar la contraseña no son válidos.");
        }
        _politica.ValidarFormato(nueva);

        _logger.LogInformation("Cambio de contraseña para el usuario {usuarioId}.", usuarioId);

        var usuario = await _repository.GetByIdAsync(usuarioId);
        if (usuario == null)
        {
            throw new InvalidOperationException("El usuario no existe");
        }

        if (!_passwordHasher.Verificar(usuario.PasswordHash, actual))
        {
            // Quien tiene un token robado podría adivinar la clave actual: cada fallo cuenta para el mismo bloqueo del
            // login (RF-CA-19). Responde 400 y no 403: un 403 aquí parecería un problema de permisos, no de la clave.
            usuario.RegistrarIntentoFallido();
            await _repository.UpdateAsync(usuario);

            throw usuario.EstaBloqueado() ? new CuentaBloqueadaException() : new ClaveActualIncorrectaException();
        }

        // RF-CA-14: la clave nueva no puede estar en filtraciones conocidas (el formato ya se comprobó al principio).
        await _politica.ValidarNoFiltradaAsync(nueva);

        usuario.CambiarPassword(_passwordHasher.Hash(nueva));

        // RF-CA-12: las sesiones abiertas antes del cambio dejan de ser válidas.
        await _sesiones.RevocarTodasAsync(usuario.Id, "cambio-de-contrasena");

        await _repository.UpdateAsync(usuario);
    }

    public async Task ForzarRestablecimientoAsync(Guid usuarioId, Guid adminId)
    {
        if (usuarioId == Guid.Empty || adminId == Guid.Empty)
        {
            throw new ArgumentException("Los identificadores de usuario no son válidos.");
        }

        _logger.LogInformation("Restablecimiento forzado para el usuario {usuarioId} por el admin {adminId}.", usuarioId, adminId);

        // RF-CA-13: solo un Administrador puede forzarlo; se verifica en el servidor (RD-06).
        await ExigirAdministradorAsync(adminId);

        var usuario = await _repository.GetByIdAsync(usuarioId);
        if (usuario == null)
        {
            throw new InvalidOperationException("El usuario no existe");
        }

        // RF-CA-13: la contraseña anterior deja de servir de inmediato.
        usuario.InvalidarPassword();

        // Las sesiones abiertas antes del forzado dejan de ser válidas.
        await _sesiones.RevocarTodasAsync(usuario.Id, "restablecimiento-forzado");

        var codigoGenerado = GenerarCodigoRecuperacion();
        usuario.GenerarCodigoRecuperacion(codigoGenerado, 24);

        await _repository.UpdateAsync(usuario);

        await _emailQueue.EncolarCorreoRecuperacionAsync(usuario.Correo.Valor, codigoGenerado);

        // RF-AUD-05: la acción queda auditada.
        await _auditoria.RegistroAuditoriaAsync(
            adminId,
            "Administrador",
            "Restablecimiento forzado de contraseña",
            "Usuario",
            usuario.Id.ToString(),
            "Contraseña anterior activa",
            "Contraseña invalidada (restablecimiento forzado por administrador)"
        );
    }

    private ConsentimientosRegistroDto ValidarConsentimientosDelRegistro(ConsentimientosRegistroDto? consentimientos)
    {
        if (consentimientos is null
            || consentimientos.Marketing is null
            || consentimientos.TerminosVersion != _versiones.Terminos
            || consentimientos.PrivacidadVersion != _versiones.Privacidad)
        {
            throw new ConsentimientoNoValidoException();
        }

        return consentimientos;
    }

    /// <summary>Código de 6 dígitos aleatorios (000000-999999) de un generador criptográfico.</summary>
    private static string GenerarCodigoNumerico() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    // Mismo generador para activación y recuperación (antes la recuperación usaba 6 caracteres de un Guid).
    private static string GenerarCodigoActivacion() => GenerarCodigoNumerico();

    private static string GenerarCodigoRecuperacion() => GenerarCodigoNumerico();

    /// <summary>
    /// RF-CA-05 / RD-06: comprueba en el servidor que quien ejecuta la operación existe, está activo y es Administrador.
    /// No se confía en el rol del token ni en que el controlador ya lo filtró.
    /// </summary>
    private async Task ExigirAdministradorAsync(Guid adminId)
    {
        var ejecutor = await _repository.GetByIdAsync(adminId);
        if (ejecutor == null || !ejecutor.Activo || ejecutor.Rol != Rol.Administrador)
        {
            throw new OperacionNoPermitidaException();
        }
    }

    private static async Task EsperarRespuestaMinimaAsync(long inicio)
    {
        var restante = TimeSpan.FromMilliseconds(100) - Stopwatch.GetElapsedTime(inicio);
        if (restante > TimeSpan.Zero)
        {
            await Task.Delay(restante);
        }
    }

    public async Task ReenviarEnlaceActivacionAsync(string correo)
    {
        _logger.LogInformation("Solicitud de reenvio de correo para activacion de la cuenta");

        var email = new Email(correo);
        var usuario = await _repository.GetByCorreoAsync(email);

        if(usuario != null && !usuario.Activo)
        {
            // Enfriamiento: sin él, pedir un código nuevo reiniciaría el contador de intentos y permitiría adivinar
            // el código de otra persona sin límite. La emisión se deduce del vencimiento (siempre emisión + 24 h).
            if (usuario.TokenAcceso != null)
            {
                var emision = usuario.TokenAcceso.Vencimiento.AddHours(-HorasValidezActivacion);
                if (DateTime.UtcNow - emision < EnfriamientoReenvioActivacion)
                {
                    return;
                }
            }

            string nuevoCodigo = GenerarCodigoActivacion();
            usuario.EstablecerTokenActivacion(nuevoCodigo, DateTime.UtcNow.AddHours(HorasValidezActivacion));

            await _repository.UpdateAsync(usuario);
            await _emailQueue.EncolarCorreoActivacionAsync(usuario.Correo.Valor, nuevoCodigo);
        }
    }

    public async Task ActivarCuentaAsync(string correo, string codigo)
    {
        // RD-07: un código mal formado se rechaza ANTES de consultar la base de datos y no consume intentos.
        if (codigo is null || codigo.Length != 6 || !codigo.All(c => c >= '0' && c <= '9'))
        {
            throw new ArgumentException("El código de activación debe tener 6 dígitos.", nameof(codigo));
        }

        _logger.LogInformation("iniciando activacion de cuenta");

        // El código se busca SIEMPRE junto al correo: con 6 dígitos no es único entre usuarios.
        var usuario = await _repository.GetByCorreoAsync(new Email(correo));

        // Mensaje uniforme (CodigoInvalidoException): no revela si el correo existe, si ya está activo o si el código expiró.
        if (usuario == null || usuario.Activo || usuario.TokenAcceso == null)
        {
            throw new CodigoInvalidoException();
        }

        // Fuerza bruta (RD-06): el intento se RESERVA de forma atómica en la base de datos antes de comparar.
        // Así, aunque lleguen mil peticiones a la vez, solo las 5 primeras llegan a comparar el código;
        // el resto recibe el mismo rechazo sin comparar. Un intento reservado cuenta aunque el código sea correcto.
        var intentos = await _repository.ReservarIntentoActivacionAsync(usuario.Id);
        if (intentos == 0 || intentos > TokenActivacion.MaxIntentos || !usuario.TokenAcceso.EsValido(codigo))
        {
            throw new CodigoInvalidoException();
        }

        usuario.CompletarActivacion(codigo);

        await _repository.UpdateAsync(usuario);
    }

    public async Task<UsuarioResponseDto> RegistrarUsuarioAsync(RegistroRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Nombre) || request.Nombre.Trim().Length > 200)
        {
            throw new ArgumentException("El nombre completo no es válido.", nameof(request));
        }
        _politica.ValidarFormato(request.Password);

        // Sin la aceptación de las versiones vigentes de los documentos legales no se crea la cuenta (antes de la base de datos).
        var consentimientos = ValidarConsentimientosDelRegistro(request.Consentimientos);

        // 1. Instancias el Value Object (esto además validará que el correo tenga un formato correcto)
        var Email = new Email(request.Correo);

        // 2. verificar correo unico (RF-CA-01)
        var existe = await _repository.CorreoExistsAsync(Email);

        if(existe)
        {
            throw new CorreoYaRegistradoException();
        }

        // 2b. RF-CA-14: la contraseña no puede estar en filtraciones conocidas (solo si el correo es nuevo).
        await _politica.ValidarNoFiltradaAsync(request.Password);

        // 3. Hashear password antes de crear la entidad (RF-CA-02)
        var passwordhash = _passwordHasher.Hash(request.Password);

        // 4. Crear la entidad
        var nuevoUsuario = new Usuario(request.Nombre, Email, passwordhash );

        string codigoActivacion = GenerarCodigoActivacion();
        nuevoUsuario.EstablecerTokenActivacion(codigoActivacion, DateTime.UtcNow.AddHours(HorasValidezActivacion));


        // 5. guardar
        // El usuario y la constancia de lo que aceptó se guardan juntos: no puede existir una cuenta sin su consentimiento.
        var consentimiento = ConsentimientoUsuario.DelRegistro(
            nuevoUsuario.Id, consentimientos.TerminosVersion, consentimientos.PrivacidadVersion,
            consentimientos.Marketing.GetValueOrDefault(), DateTime.UtcNow);
        await _repository.SaveAsync(nuevoUsuario, consentimiento);

        _logger.LogInformation("Usuario registrado: {UsuarioId}.", nuevoUsuario.Id);

        await _emailQueue.EncolarCorreoActivacionAsync(nuevoUsuario.Correo.Valor, codigoActivacion);

        // 6. mappeamos al dto
        return new UsuarioResponseDto(
            nuevoUsuario.Id, 
            nuevoUsuario.NombreCompleto, 
            nuevoUsuario.Correo.Valor,
            nuevoUsuario.Rol.ToString(),
            nuevoUsuario.Activo
            );
    }
}
