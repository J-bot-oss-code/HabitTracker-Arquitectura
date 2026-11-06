using AccessControl.Domain.Entities.ValueObjects;

namespace AccessControl.Domain.Entities
{
    public class Usuario
    {
        public Guid Id { get; private set; }
        public string NombreCompleto { get; private set; } = null!; // se coloca private set para que solo se pueda modificar desde la clase Usuario
        public Email Correo { get; private set; } = null!;
        /// <summary>Nulo en las cuentas creadas con un proveedor externo (Google): no hay contraseña que guardar.</summary>
        public string? PasswordHash { get; private set; }

        private const string PrefijoInvalidado = "INVALIDADO_";

        /// <summary>La cuenta puede iniciar sesión con contraseña (no es solo de Google ni se la invalidó un administrador).</summary>
        public bool TienePassword => !string.IsNullOrEmpty(PasswordHash) && !PasswordHash.StartsWith(PrefijoInvalidado, StringComparison.Ordinal);
        public bool Activo { get; private set; }
        public DateTime FechaCreacion { get; private set; }
        public TokenActivacion? TokenAcceso { get; private set; }
        public CodigoRecuperacion? Recuperacion { get; private set; }
        public BloqueoCuenta Bloqueo { get; private set; } = new BloqueoCuenta();

        /// <summary>Cuándo la propia persona desactivó su cuenta. Nulo si no lo hizo (o si ya volvió a entrar).</summary>
        public DateTime? DesactivadaPorUsuarioEn { get; private set; }

        /// <summary>Cuándo pidió eliminar la cuenta. Nulo si no hay una baja programada.</summary>
        public DateTime? BajaSolicitadaEn { get; private set; }

        /// <summary>Desde cuándo se puede borrar la cuenta definitivamente (fin del periodo de gracia).</summary>
        public DateTime? BajaProgramadaPara { get; private set; }

        public Rol Rol { get; private set; }

        private Usuario() {} // Constructor privado para EF Core

        public Usuario(string nombreCompleto, Email correo, string passwordHash, Rol rol = Rol.Estandar)
        {
            if(correo == null)
            {
                throw new ArgumentNullException(nameof(correo), "El correo no puede estar vacio");
            }

            if(string.IsNullOrWhiteSpace(passwordHash))
            {
                throw new ArgumentException("Un usuario debe tener un password para ser creado", nameof(passwordHash));
            }

            if (string.IsNullOrWhiteSpace(nombreCompleto))
            {
                throw new ArgumentException("El nombre completo no puede estar vacío.", nameof(nombreCompleto));
            }

            Id = Guid.NewGuid();
            NombreCompleto = nombreCompleto;
            Correo = correo;
            PasswordHash = passwordHash;
            // El usuario nace inactivo: debe activar su cuenta con el token (RF-CA-15).
            Activo = false;
            FechaCreacion = DateTime.UtcNow;
            Rol = rol;
        }

        /// <summary>
        /// Cuenta creada con un proveedor externo (Google). Nace ACTIVA y sin contraseña: el proveedor ya verificó que la
        /// persona controla el correo, que es lo mismo que prueba el código de activación en el registro con correo (RF-CA-15).
        /// </summary>
        public static Usuario CrearConProveedorExterno(string nombreCompleto, Email correo)
        {
            var usuario = new Usuario(nombreCompleto, correo, "externo");
            usuario.PasswordHash = null;
            usuario.Activo = true;
            return usuario;
        }

        public void CambiarRol(Rol nuevoRol)
        {
            if (Rol == nuevoRol)
            {
                throw new InvalidOperationException("El usuario ya tiene este rol.");
            }
            Rol = nuevoRol;
        }

        /// <summary>
        /// Cuenta creada y todavía sin activar (tiene un código de activación pendiente). Se distingue de una cuenta
        /// desactivada por un administrador, que no tiene código pendiente (RF-CA-15 frente a RF-CA-20).
        /// </summary>
        public bool PendienteDeActivacion => !Activo && TokenAcceso != null;

        /// <summary>
        /// La persona desactivó su propia cuenta (RF-CA-20 habla de la desactivación por un administrador, que es otra cosa):
        /// puede volver a entrar cuando quiera y la cuenta se reactiva. Una desactivada por un administrador no.
        /// </summary>
        public bool DesactivadaPorElUsuario => !Activo && DesactivadaPorUsuarioEn != null;

        public bool TieneBajaProgramada => BajaProgramadaPara != null;

        /// <summary>La persona desactiva su cuenta: no podrá entrar con ella hasta que inicie sesión de nuevo.</summary>
        public void SolicitarDesactivacion(DateTime ahora)
        {
            if (!Activo)
            {
                throw new InvalidOperationException("El usuario ya está desactivado.");
            }

            Activo = false;
            DesactivadaPorUsuarioEn = ahora;
        }

        /// <summary>Un inicio de sesión correcto reactiva la cuenta que la propia persona había desactivado.</summary>
        public void ReactivarTrasDesactivacion()
        {
            if (!DesactivadaPorElUsuario)
            {
                throw new InvalidOperationException("La cuenta no fue desactivada por su dueño.");
            }

            Activo = true;
            DesactivadaPorUsuarioEn = null;
        }

        /// <summary>
        /// Programa el borrado de la cuenta para dentro de <paramref name="diasGracia"/> días. Mientras tanto la cuenta
        /// sigue funcionando y puede cancelarse. Pedirlo otra vez no mueve la fecha: se conserva la original.
        /// </summary>
        public void ProgramarBaja(DateTime ahora, int diasGracia)
        {
            if (diasGracia < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(diasGracia), "El periodo de gracia no puede ser negativo.");
            }

            if (TieneBajaProgramada)
            {
                return;
            }

            BajaSolicitadaEn = ahora;
            BajaProgramadaPara = ahora.AddDays(diasGracia);
        }

        /// <summary>Cancela la baja programada. False si no había ninguna.</summary>
        public bool CancelarBaja()
        {
            if (!TieneBajaProgramada)
            {
                return false;
            }

            BajaSolicitadaEn = null;
            BajaProgramadaPara = null;
            return true;
        }

        /// <summary>¿Ya terminó el periodo de gracia?</summary>
        public bool BajaVencida(DateTime ahora) => BajaProgramadaPara != null && BajaProgramadaPara <= ahora;

        public void EstablecerTokenActivacion(string valor, DateTime vencimiento)
        {
            TokenAcceso = new TokenActivacion(valor, vencimiento);
        }

        /// <summary>Cuenta un código de activación incorrecto; a los 5 el código queda invalidado.</summary>
        public void RegistrarFalloActivacion() => TokenAcceso?.RegistrarFallo();

        public void CompletarActivacion(string token)
        {
            if (TokenAcceso == null || !TokenAcceso.EsValido(token))
            {
                throw new InvalidOperationException("El código de activación es incorrecto o ha expirado");
            }

            Activo = true;
            TokenAcceso = null;
        }

        public void RegistrarIntentoFallido() => Bloqueo.RegistrarFallo();

        public void RestablecerIntentos() => Bloqueo.Restablecer();

        public bool EstaBloqueado() => Bloqueo.EstaBloqueado();

        public void GenerarCodigoRecuperacion(string codigo, int horasValidez)
        {
            Recuperacion = new CodigoRecuperacion(codigo, horasValidez);
        }

        public void RestablecerPassword(string nuevoHash, string codigo)
        {
            if (Recuperacion == null || !Recuperacion.EsValido(codigo))
            {
                throw new InvalidOperationException("El código de recuperación es incorrecto, ya fue usado o ha expirado.");
            }

            if (string.IsNullOrWhiteSpace(nuevoHash))
            {
                throw new ArgumentException("La nueva contraseña no puede estar vacía.", nameof(nuevoHash));
            }

            PasswordHash = nuevoHash;
            Recuperacion.MarcarComoUsado();
        }

        public void CambiarPassword(string nuevoHash)
        {
            if (string.IsNullOrWhiteSpace(nuevoHash)) throw new ArgumentException("El hash no puede estar vacío.");
            PasswordHash = nuevoHash;
        }

        public void InvalidarPassword()
        {
            // Una cuenta sin contraseña (solo Google) no tiene clave que invalidar: sigue sin tenerla.
            if (PasswordHash is null)
            {
                return;
            }

            // RF-CA-13: Asignamos un valor imposible de hashear/hacer match para invalidar la clave actual inmediatamente
            PasswordHash = $"{PrefijoInvalidado}{Guid.NewGuid()}";
        }

        public void Desactivar()        {
            if (!Activo)
            {
                throw new InvalidOperationException("El usuario ya está desactivado.");
            }
            Activo = false;
        }

        public void Activar()
        {
            if (Activo)
            {
                throw new InvalidOperationException("El usuario ya está activo.");
            }
            Activo = true;
            DesactivadaPorUsuarioEn = null;
            // Una cuenta activada por un administrador ya no necesita (ni acepta) el código de activación pendiente.
            TokenAcceso = null;
        }
    }
}
