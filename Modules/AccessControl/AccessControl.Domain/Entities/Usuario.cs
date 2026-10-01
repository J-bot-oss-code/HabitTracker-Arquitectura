using AccessControl.Domain.Entities.ValueObjects;

namespace AccessControl.Domain.Entities
{
    public class Usuario
    {
        public Guid Id { get; private set; }
        public string NombreCompleto { get; private set; } = null!; // se coloca private set para que solo se pueda modificar desde la clase Usuario
        public Email Correo { get; private set; } = null!;
        public string PasswordHash { get; private set; } = null!;
        public bool Activo { get; private set; }
        public DateTime FechaCreacion { get; private set; }
        public TokenActivacion? TokenAcceso { get; private set; }
        public CodigoRecuperacion? Recuperacion { get; private set; }
        public BloqueoCuenta Bloqueo { get; private set; } = new BloqueoCuenta();

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
                throw new ArgumentException(nameof(passwordHash), "Un usuario debe tener un password para ser creado");
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

        public void CambiarRol(Rol nuevoRol)
        {
            if (Rol == Rol.Estandar)
            {
                throw new InvalidOperationException("Un usuario Estándar no puede cambiar de rol.");
            }

            if (Rol == nuevoRol)
            {
                throw new InvalidOperationException("El usuario ya tiene este rol.");
            }
            Rol = nuevoRol;
        }

        public void EstablecerTokenActivacion(TokenActivacion token)
        {
            TokenAcceso = token ?? throw new ArgumentNullException(nameof(token));
        }

        public void EstablecerTokenActivacion(string valor, DateTime vencimiento)
        {
            TokenAcceso = new TokenActivacion(valor, vencimiento);
        }

        public void CompletarActivacion(string token)
        {
            if (TokenAcceso == null || !TokenAcceso.EsValido(token))
            {
                throw new InvalidOperationException("El enlace de activacion es incorrecto o ha expirado");
            }

            Activo = true;
            TokenAcceso = null;
        }

        public void ComopletarActivacion(string token)
        {
            CompletarActivacion(token);
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
            // RF-CA-13: Asignamos un valor imposible de hashear/hacer match para invalidar la clave actual inmediatamente
            PasswordHash = $"INVALIDADO_{Guid.NewGuid()}";
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
        }
    }
}
