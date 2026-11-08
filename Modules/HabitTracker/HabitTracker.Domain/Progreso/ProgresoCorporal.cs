namespace HabitTracker.Domain.Progreso;

/// <summary>Zonas de medición del frontend (<c>ZONAS</c>). Grasa es el porcentaje de grasa corporal.</summary>
public enum ZonaMedicion
{
    Cintura,
    Pecho,
    Brazo,
    Grasa,
}

/// <summary>Reglas comunes a los registros corporales: fechas UTC, no futuras y desde el año 2000.</summary>
public static class FechasCorporales
{
    public static readonly DateTime FechaMinima = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly TimeSpan ToleranciaReloj = TimeSpan.FromMinutes(5);

    public static void Validar(DateTime fechaUtc, DateTime ahoraUtc)
    {
        if (fechaUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha debe ser UTC.", nameof(fechaUtc));
        if (fechaUtc < FechaMinima)
            throw new ArgumentException("La fecha es demasiado antigua.", nameof(fechaUtc));
        if (fechaUtc > ahoraUtc + ToleranciaReloj)
            throw new Excepciones.ReglaNegocioException(Excepciones.ReglaNegocioException.FechaFutura,
                "La fecha no puede ser futura.");
    }

    /// <summary>Día UTC al que pertenece un registro: hay como máximo uno por día (el nuevo reemplaza al anterior).</summary>
    public static DateTime DiaUtc(DateTime fechaUtc) => DateTime.SpecifyKind(fechaUtc.Date, DateTimeKind.Utc);
}

/// <summary>Peso de un día. Es un dato de salud: guardarlo exige el consentimiento <c>datosSalud</c>.</summary>
public sealed class RegistroPeso
{
    public const decimal Minimo = 30;
    public const decimal Maximo = 300;

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public DateTime Fecha { get; private set; }
    public decimal PesoKg { get; private set; }

    private RegistroPeso() { } // EF Core

    public RegistroPeso(Guid usuarioId, DateTime fechaUtc, decimal pesoKg, DateTime ahoraUtc)
    {
        if (usuarioId == Guid.Empty)
            throw new UnauthorizedAccessException("Identidad de usuario inválida.");
        FechasCorporales.Validar(fechaUtc, ahoraUtc);
        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Fecha = fechaUtc;
        Corregir(pesoKg);
    }

    /// <summary>Un segundo peso del mismo día corrige el registro de ese día.</summary>
    public void Corregir(decimal pesoKg)
    {
        if (pesoKg < Minimo || pesoKg > Maximo)
            throw new ArgumentException($"El peso debe estar entre {Minimo} y {Maximo} kg.", nameof(pesoKg));
        PesoKg = pesoKg;
    }
}

/// <summary>Medida corporal de un día y una zona. Es un dato de salud.</summary>
public sealed class MedicionCorporal
{
    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public DateTime Fecha { get; private set; }
    public ZonaMedicion Zona { get; private set; }
    public decimal Valor { get; private set; }

    private MedicionCorporal() { } // EF Core

    public MedicionCorporal(Guid usuarioId, DateTime fechaUtc, ZonaMedicion zona, decimal valor, DateTime ahoraUtc)
    {
        if (usuarioId == Guid.Empty)
            throw new UnauthorizedAccessException("Identidad de usuario inválida.");
        if (!Enum.IsDefined(zona))
            throw new ArgumentException("La zona no es válida.", nameof(zona));
        FechasCorporales.Validar(fechaUtc, ahoraUtc);
        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Fecha = fechaUtc;
        Zona = zona;
        Corregir(valor);
    }

    /// <summary>Límites del frontend: cintura y pecho 40–250 cm, brazo 10–100 cm, grasa 3–60 %.</summary>
    public static (decimal Minimo, decimal Maximo) Limites(ZonaMedicion zona) => zona switch
    {
        ZonaMedicion.Cintura or ZonaMedicion.Pecho => (40, 250),
        ZonaMedicion.Brazo => (10, 100),
        ZonaMedicion.Grasa => (3, 60),
        _ => throw new ArgumentException("La zona no es válida.", nameof(zona)),
    };

    public void Corregir(decimal valor)
    {
        var (minimo, maximo) = Limites(Zona);
        if (valor < minimo || valor > maximo)
            throw new ArgumentException($"El valor debe estar entre {minimo} y {maximo}.", nameof(valor));
        Valor = valor;
    }
}
