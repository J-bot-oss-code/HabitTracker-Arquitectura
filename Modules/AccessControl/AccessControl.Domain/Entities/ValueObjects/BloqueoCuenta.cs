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
            // Si el bloqueo anterior ya venció, el contador se reinicia:
            // un nuevo ciclo de intentos empieza desde cero.
            if (BloqueadoHasta.HasValue && BloqueadoHasta.Value <= DateTime.UtcNow)
            {
                Restablecer();
            }

            IntentosFallidos++;

            // El bloqueo se activa al superar los 5 fallos: los 5 primeros
            // responden 401 y el 6to ya encuentra la cuenta bloqueada.
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
