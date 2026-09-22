namespace AccessControl.Application.Interfaces
{
    public interface IEmailQueue
    {
        Task EncolarCorreoRecuperacionAsync(string correoDestino, string codigoGenerado);
        Task EncolarCorreoActivacionAsync(string correo, string token);
    }
}