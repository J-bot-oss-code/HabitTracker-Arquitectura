namespace AccessControl.Domain.Entities.ValueObjects;

public sealed class BloqueoCuenta
{
    private const int MaxIntentos = 5;
    private static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

    public int IntentosFallidos { get; private set; }
    public DateTime? BloqueadoHasta { get; private set; }

    public BloqueoCuenta()
    {
        IntentosFallidos = 0;
        BloqueadoHasta = null;
    }

    public bool EstaBloqueado() =>
        BloqueadoHasta.HasValue && BloqueadoHasta.Value > DateTime.UtcNow;

    public void RegistrarFallo()
    {
        // Un bloqueo vigente no se prolonga por intentos adicionales.
        if (EstaBloqueado())
        {
            return;
        }

        // Al vencer el bloqueo, un ciclo nuevo comienza desde cero.
        if (BloqueadoHasta.HasValue)
        {
            Restablecer();
        }

        IntentosFallidos++;

        // Los cinco primeros fallos reciben 401; el sexto activa el bloqueo.
        if (IntentosFallidos > MaxIntentos)
        {
            BloqueadoHasta = DateTime.UtcNow.Add(DuracionBloqueo);
        }
    }

    public void Restablecer()
    {
        IntentosFallidos = 0;
        BloqueadoHasta = null;
    }
}
