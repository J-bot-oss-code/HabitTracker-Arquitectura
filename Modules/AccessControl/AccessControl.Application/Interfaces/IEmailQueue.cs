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

        /// <summary>
        /// Igual que <see cref="EncolarCorreoAsync"/>, con botones de acción y un enlace para darse de baja. Los textos se
        /// escapan y cada URL debe ser absoluta y http(s): una URL de otro esquema se rechaza (<see cref="ArgumentException"/>).
        /// </summary>
        Task EncolarCorreoConAccionesAsync(
            string destinatario, string asunto, string titulo, string mensaje,
            IReadOnlyList<AccionCorreo> acciones, string? enlaceBaja);
    }

    /// <summary>Un botón del correo: el texto que se lee y la URL a la que lleva.</summary>
    public sealed record AccionCorreo(string Texto, string Url);
}