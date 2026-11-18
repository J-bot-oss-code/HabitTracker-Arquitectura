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
    /// <summary>Intentos de envío antes de rendirse; el worker lo usa para no volver a leer los correos agotados.</summary>
    public const int MaxIntentos = 3;

    public const int LongitudMaximaError = 500;

    public Guid Id { get; private set; }
    public string Destinatario { get; private set; } = string.Empty;
    public string Asunto { get; private set; } = string.Empty;
    public string Cuerpo { get; private set; } = string.Empty;
    public EstadoCorreo Estado { get; private set; }
    public int Intentos { get; private set; }
    public string? UltimoError { get; private set; }
    public DateTime? FechaUltimoIntento { get; private set; }
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
        RegistrarFallo("Error de envío no especificado.");
    }

    public void RegistrarFallo(string error)
    {
        if (Estado != EstadoCorreo.Procesando)
        {
            throw new InvalidOperationException("Solo se puede marcar como fallido un correo en estado Procesando.");
        }

        Intentos++;
        // Un mensaje de error largo (o con detalles del servidor) no debe llenar la tabla ni mostrarse entero.
        UltimoError = error.Length <= LongitudMaximaError ? error : error[..LongitudMaximaError];
        FechaUltimoIntento = DateTime.UtcNow;
        Estado = EstadoCorreo.Fallido;
    }

    public void Reintentar()
    {
        if (Estado != EstadoCorreo.Fallido)
        {
            throw new InvalidOperationException("Solo se puede reintentar un correo en estado Fallido.");
        }

        if (Intentos >= MaxIntentos)
        {
            throw new InvalidOperationException("El correo agotó sus reintentos.");
        }

        Estado = EstadoCorreo.Pendiente;
    }

    /// <summary>
    /// Backoff exponencial: tras cada fallo se espera <c>esperaBase × 2^intentos</c> antes de reintentar (1 minuto de base por
    /// defecto: 2 y 4 minutos). Con una base de 0 los reintentos son inmediatos (pruebas y demostraciones).
    /// </summary>
    public bool PuedeReintentarse(DateTime ahora, TimeSpan? esperaBase = null)
    {
        if (Estado == EstadoCorreo.Pendiente)
        {
            return true;
        }

        if (Estado == EstadoCorreo.Fallido && Intentos < MaxIntentos && FechaUltimoIntento.HasValue)
        {
            var espera = (esperaBase ?? TimeSpan.FromMinutes(1)) * Math.Pow(2, Intentos);
            return FechaUltimoIntento.Value.Add(espera) <= ahora;
        }

        return false;
    }
}
