using System;
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

    public bool EstaBloqueado() => BloqueadoHasta.HasValue && BloqueadoHasta.Value > DateTime.UtcNow;

    public void RegistrarFallo()
    {
        IntentosFallidos++;
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
