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

        // El quinto fallo activa el bloqueo; el siguiente inicio se rechaza aunque la clave sea correcta.
        if (IntentosFallidos >= MaxIntentos)
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
