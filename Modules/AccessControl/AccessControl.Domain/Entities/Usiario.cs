namespace AccessControl.Domain.Entities
{
    public class Usuario
    {
        public Guid Id {get; private set;}
        public string NombreCompleto {get; private set;} = null!; // se coloca private set para que solo se pueda modificar desde la clase Usuario
        public string Correo {get; private set;} = null!;
        public string PasswordHash {get; private set;} = null!;
        public bool Activo {get; private set;} 
        public DateTime FechaCreacion {get; private set;}

        public Rol Rol {get; private set;} 

        private Usuario() {} // Constructor privado para EF Core

        public Usuario(string nombreCompleto, string correo, string passwordHash, Rol rol = Rol.Estandar)
        {
            if (string.IsNullOrWhiteSpace(nombreCompleto))
            {
                throw new ArgumentException("El nombre completo no puede estar vacío.", nameof(nombreCompleto)); // se utiliza el nameof para obtener el nombre del parámetro y evitar errores de tipeo
            }
            if (string.IsNullOrWhiteSpace(correo))
            {
                throw new ArgumentException("El correo no puede estar vacío.", nameof(correo));
            }
            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                throw new ArgumentException("La contraseña no puede estar vacía.", nameof(passwordHash));
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
            if (Rol == nuevoRol)
            {
                throw new InvalidOperationException("El usuario ya tiene este rol.");
            }
            Rol = nuevoRol;
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