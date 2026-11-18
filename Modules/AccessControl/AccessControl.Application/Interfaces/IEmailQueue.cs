namespace AccessControl.Application.Interfaces
{
    public interface IEmailQueue
    {
        Task EncolarCorreoRecuperacionAsync(string correoDestino, string codigoGenerado);
        Task EncolarCorreoActivacionAsync(string correo, string codigo);

        /// <summary>
        /// Encola un correo informativo con la misma plantilla (RF-NOT-06, RF-NOT-08): no se envía aquí, lo envía el
        /// procesador de la cola. El título y el mensaje se tratan como texto (se escapa el HTML).
        /// </summary>
        Task EncolarCorreoAsync(string destinatario, string asunto, string titulo, string mensaje);
    }
}