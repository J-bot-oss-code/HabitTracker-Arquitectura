namespace AccessControl.Domain.Entities
{
    public class RevocacionSesion
    {
        public Guid Id { get; private set; }
        public Guid UsuarioId { get; private set; }
        public DateTime FechaCorte { get; private set; }
        public DateTime FechaExpiracion { get; private set; }

        private RevocacionSesion() {} // Constructor privado para EF Core

        public RevocacionSesion(Guid usuarioId, DateTime fechaCorte, DateTime fechaExpiracion)
        {
            Id = Guid.NewGuid();
            UsuarioId = usuarioId;
            FechaCorte = fechaCorte;
            FechaExpiracion = fechaExpiracion;
        }

        public void ActualizarCorte(DateTime fechaCorte, DateTime fechaExpiracion)
        {
            FechaCorte = fechaCorte;
            FechaExpiracion = fechaExpiracion;
        }
    }
}
