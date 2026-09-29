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
            Activo = true;
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

        public void Desactivar()
        {
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
