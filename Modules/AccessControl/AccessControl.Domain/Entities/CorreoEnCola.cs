namespace AccessControl.Domain.Entities;

public enum EstadoCorreo
{
    Pendiente = 1,
    Procesando = 2,
    Enviado = 3,
    Fallido = 4
}

public class CorreoEnCola
{
    public Guid Id { get; private set; }
    public string Destinatario { get; private set; } = string.Empty;
    public string Asunto { get; private set; } = string.Empty;
    public string Cuerpo { get; private set; } = string.Empty;
    public EstadoCorreo Estado { get; private set; }
    public DateTime FechaCreacion { get; private set; }
    public DateTime? FechaEnvio { get; private set; }

    private CorreoEnCola() { } // Constructor privado para EF Core

    public CorreoEnCola(string destinatario, string asunto, string cuerpo)
    {
        if (string.IsNullOrWhiteSpace(destinatario))
        {
            throw new ArgumentException("El destinatario no puede estar vacío.", nameof(destinatario));
        }

        if (string.IsNullOrWhiteSpace(asunto))
        {
            throw new ArgumentException("El asunto no puede estar vacío.", nameof(asunto));
        }

        if (string.IsNullOrWhiteSpace(cuerpo))
        {
            throw new ArgumentException("El cuerpo no puede estar vacío.", nameof(cuerpo));
        }

        Id = Guid.NewGuid();
        Destinatario = destinatario;
        Asunto = asunto;
        Cuerpo = cuerpo;
        Estado = EstadoCorreo.Pendiente;
        FechaCreacion = DateTime.UtcNow;
        FechaEnvio = null;
    }

    public void MarcarProcesando()
    {
        if (Estado != EstadoCorreo.Pendiente)
        {
            throw new InvalidOperationException("Solo se puede procesar un correo en estado Pendiente.");
        }

        Estado = EstadoCorreo.Procesando;
    }

    public void MarcarEnviado()
    {
        if (Estado != EstadoCorreo.Procesando)
        {
            throw new InvalidOperationException("Solo se puede marcar como enviado un correo en estado Procesando.");
        }

        Estado = EstadoCorreo.Enviado;
        FechaEnvio = DateTime.UtcNow;
    }

    public void MarcarFallido()
    {
        if (Estado != EstadoCorreo.Procesando)
        {
            throw new InvalidOperationException("Solo se puede marcar como fallido un correo en estado Procesando.");
        }

        Estado = EstadoCorreo.Fallido;
    }
}
