using HabitTracker.Domain.Excepciones;

namespace HabitTracker.Domain.Planes;

public enum OrigenPlan
{
    Catalogo,
    Personal,
}

/// <summary>Máquina propia del plan de la persona, independiente de la del hábito (<c>TransicionesPlanUsuario</c>).</summary>
public enum EstadoPlanUsuario
{
    Activo,
    Pausado,
    /// <summary>Terminal: se llegó a la fecha de fin prevista.</summary>
    Completado,
    /// <summary>Terminal: la persona lo dejó.</summary>
    Cancelado,
}

/// <summary>Por qué se pausa. Una pausa justificada no corta la racha ni cuenta como días sin registro (P3).</summary>
public enum MotivoPausa
{
    Lesion,
    Viaje,
    Enfermedad,
    Trabajo,
    Otro,
}

/// <summary>Un tramo en que el plan estuvo pausado. La racha necesita saber qué días excluir.</summary>
public sealed class PeriodoPausa
{
    private PeriodoPausa() { } // EF Core

    internal PeriodoPausa(Guid planUsuarioId, DateTime inicioUtc, MotivoPausa motivo)
    {
        Id = Guid.NewGuid();
        PlanUsuarioId = planUsuarioId;
        InicioUtc = inicioUtc;
        Motivo = motivo;
    }

    public Guid Id { get; private set; }
    public Guid PlanUsuarioId { get; private set; }
    public DateTime InicioUtc { get; private set; }
    /// <summary>Nulo mientras la pausa sigue abierta.</summary>
    public DateTime? FinUtc { get; private set; }
    public MotivoPausa Motivo { get; private set; }

    internal void Cerrar(DateTime finUtc) => FinUtc = finUtc;
}

/// <summary>
/// El plan que una persona sigue (Fase 4 de HabitTracker v2). Agrega la máquina de estados del plan y sus ajustes; los
/// hábitos que lo componen son <c>Habito</c> con <c>PlanUsuarioId</c>. Las fechas son UTC; el día local lo resuelve la
/// calculadora de progreso con la zona horaria del perfil (RD-11).
/// </summary>
public sealed class PlanUsuario
{
    public const int MaximoExtensiones = 2;
    public const int DiasModoSuave = 7;
    /// <summary>Planes en curso (activos o en pausa) a la vez: más de tres dispersan en lugar de ayudar.</summary>
    public const int MaximoPlanesEnCurso = 3;
    public const int DiasPorSemana = 7;
    public const int LongitudNombre = 100;
    public const int LongitudSlug = 60;

    private readonly List<PeriodoPausa> _pausas = [];

    private PlanUsuario() { } // EF Core

    /// <summary>Empieza un plan del catálogo. La persona queda siguiéndolo desde <paramref name="ahoraUtc"/>.</summary>
    public static PlanUsuario Iniciar(Guid usuarioId, PlanPlantilla plantilla, TimeOnly? horaRecordatorio, DateTime ahoraUtc)
    {
        ArgumentNullException.ThrowIfNull(plantilla);
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("La identidad de la persona no es válida.", nameof(usuarioId));
        if (ahoraUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha debe ser UTC.", nameof(ahoraUtc));

        return new PlanUsuario
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            PlanPlantillaId = plantilla.Id,
            Slug = plantilla.Slug,
            Origen = OrigenPlan.Catalogo,
            Nombre = plantilla.Nombre,
            Semanas = plantilla.Semanas,
            FechaInicio = ahoraUtc,
            FechaFinPrevista = ahoraUtc.AddDays(plantilla.Semanas * DiasPorSemana),
            Estado = EstadoPlanUsuario.Activo,
            HoraRecordatorio = horaRecordatorio,
        };
    }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    /// <summary>Plantilla de origen. Nulo en un plan personal (Fase 8).</summary>
    public Guid? PlanPlantillaId { get; private set; }
    /// <summary>Slug de la plantilla, para enlazar el plan aunque esta se retire del catálogo.</summary>
    public string? Slug { get; private set; }
    public OrigenPlan Origen { get; private set; }
    /// <summary>Copia del nombre: un cambio en el catálogo no altera un plan en curso.</summary>
    public string Nombre { get; private set; } = string.Empty;
    public int Semanas { get; private set; }
    public DateTime FechaInicio { get; private set; }
    public DateTime FechaFinPrevista { get; private set; }
    public EstadoPlanUsuario Estado { get; private set; }
    /// <summary>Motivo de la pausa vigente; nulo si el plan no está en pausa.</summary>
    public MotivoPausa? MotivoPausa { get; private set; }
    public DateTime? ModoSuaveHasta { get; private set; }
    public int Extensiones { get; private set; }
    /// <summary>Hora local del recordatorio (Fase 5).</summary>
    public TimeOnly? HoraRecordatorio { get; private set; }
    public bool RecordatoriosActivos { get; private set; }
    public IReadOnlyList<PeriodoPausa> Pausas => _pausas;

    public int SemanasTotales => Semanas + Extensiones;
    public bool EstaEnCurso => Estado is EstadoPlanUsuario.Activo or EstadoPlanUsuario.Pausado;
    public bool EnModoSuave(DateTime ahoraUtc) => ModoSuaveHasta is { } hasta && ahoraUtc <= hasta;
    public bool LlegoFechaFin(DateTime ahoraUtc) => ahoraUtc >= FechaFinPrevista;

    public void Pausar(MotivoPausa motivo, DateTime ahoraUtc)
    {
        if (!Enum.IsDefined(motivo))
            throw new ArgumentException("El motivo de la pausa no es válido.", nameof(motivo));
        TransicionesPlanUsuario.Validar(Estado, EstadoPlanUsuario.Pausado, LlegoFechaFin(ahoraUtc));
        // Un plan que ya llegó a su fin se completa: pausarlo (y extenderlo al reanudar) sería una forma de no terminarlo.
        if (LlegoFechaFin(ahoraUtc))
            throw new ReglaNegocioException(ReglaNegocioException.TransicionPlanInvalida,
                "El plan ya llegó a su final: puedes completarlo o extenderlo.");

        _pausas.Add(new PeriodoPausa(Id, ahoraUtc, motivo));
        MotivoPausa = motivo;
        Estado = EstadoPlanUsuario.Pausado;
    }

    /// <summary>
    /// Reanuda. Los días de la pausa se agregan al final: la persona no pierde semanas del programa. Se cuentan en días
    /// LOCALES (del día en que empezó a antes del día en que reanuda), los mismos que la calculadora de progreso no toma en
    /// cuenta, para que lo que se quita y lo que se agrega coincidan.
    /// </summary>
    public void Reanudar(DateTime ahoraUtc, TimeZoneInfo zona)
    {
        ArgumentNullException.ThrowIfNull(zona);
        TransicionesPlanUsuario.Validar(Estado, EstadoPlanUsuario.Activo, LlegoFechaFin(ahoraUtc));

        if (PausaAbierta() is { } pausa)
        {
            pausa.Cerrar(ahoraUtc);
            var dias = DiasDePausa(pausa.InicioUtc, ahoraUtc, zona);
            if (dias > 0)
                FechaFinPrevista = FechaFinPrevista.AddDays(dias);
        }

        MotivoPausa = null;
        Estado = EstadoPlanUsuario.Activo;
    }

    /// <summary>Solo se completa un plan activo que llegó a su fecha de fin (<c>plan_aun_no_termina</c>).</summary>
    public void Completar(DateTime ahoraUtc)
    {
        TransicionesPlanUsuario.Validar(Estado, EstadoPlanUsuario.Completado, LlegoFechaFin(ahoraUtc));
        Estado = EstadoPlanUsuario.Completado;
    }

    public void Cancelar(DateTime ahoraUtc)
    {
        TransicionesPlanUsuario.Validar(Estado, EstadoPlanUsuario.Cancelado, LlegoFechaFin(ahoraUtc));
        PausaAbierta()?.Cerrar(ahoraUtc);
        MotivoPausa = null;
        Estado = EstadoPlanUsuario.Cancelado;
    }

    /// <summary>
    /// +1 semana, como máximo <see cref="MaximoExtensiones"/> veces. No es una transición: el estado no cambia. Nunca se
    /// aplica sola: la propone <c>SugeridorAjuste</c> y la persona decide.
    /// </summary>
    public void Extender()
    {
        ExigirActivo();
        if (Extensiones >= MaximoExtensiones)
            throw new ReglaNegocioException(ReglaNegocioException.LimiteExtensiones,
                $"Un plan se puede extender {MaximoExtensiones} veces como máximo.");

        Extensiones++;
        FechaFinPrevista = FechaFinPrevista.AddDays(DiasPorSemana);
    }

    /// <summary>Modo suave: durante 7 días la versión mínima cuenta como día completo.</summary>
    public void ActivarModoSuave(DateTime ahoraUtc)
    {
        ExigirActivo();
        if (EnModoSuave(ahoraUtc))
            throw new ReglaNegocioException(ReglaNegocioException.AjusteNoDisponible, "El modo suave ya está activo.");
        ModoSuaveHasta = ahoraUtc.AddDays(DiasModoSuave);
    }

    public void ConfigurarRecordatorio(TimeOnly? hora, bool activos)
    {
        if (!EstaEnCurso)
            throw new ReglaNegocioException(ReglaNegocioException.TransicionPlanInvalida,
                "Solo un plan en curso tiene recordatorio.");
        if (activos && hora is null && HoraRecordatorio is null)
            throw new ArgumentException("Elige a qué hora quieres el recordatorio.", nameof(hora));

        if (hora is { } nueva)
            HoraRecordatorio = nueva;
        RecordatoriosActivos = activos;
    }

    /// <summary>Días locales completos de una pausa: del día en que empezó a antes del día en que se reanudó.</summary>
    public static int DiasDePausa(DateTime inicioUtc, DateTime finUtc, TimeZoneInfo zona)
    {
        var inicio = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(inicioUtc, DateTimeKind.Utc), zona));
        var fin = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(finUtc, DateTimeKind.Utc), zona));
        return Math.Max(0, fin.DayNumber - inicio.DayNumber);
    }

    private PeriodoPausa? PausaAbierta() => _pausas.LastOrDefault(p => p.FinUtc is null);

    private void ExigirActivo()
    {
        if (Estado != EstadoPlanUsuario.Activo)
            throw new ReglaNegocioException(ReglaNegocioException.AjusteNoDisponible,
                "Solo se puede ajustar un plan activo.");
    }
}

/// <summary>
/// Única definición de las transiciones del plan (RD-04). Prohibidas explícitas: Pausado → Completado y salir de un
/// estado terminal. Completar exige haber llegado a la fecha de fin.
/// </summary>
public static class TransicionesPlanUsuario
{
    public static bool EsTransicionValida(EstadoPlanUsuario actual, EstadoPlanUsuario nuevo, bool llegoFechaFin = false) =>
        (actual, nuevo) switch
        {
            (EstadoPlanUsuario.Activo, EstadoPlanUsuario.Pausado) => true,
            (EstadoPlanUsuario.Pausado, EstadoPlanUsuario.Activo) => true,
            (EstadoPlanUsuario.Activo, EstadoPlanUsuario.Completado) => llegoFechaFin,
            (EstadoPlanUsuario.Activo, EstadoPlanUsuario.Cancelado) => true,
            (EstadoPlanUsuario.Pausado, EstadoPlanUsuario.Cancelado) => true,
            // Prohibidas explícitas.
            (EstadoPlanUsuario.Pausado, EstadoPlanUsuario.Completado) =>
                throw Invalida("Primero reanuda el plan; un plan en pausa no se puede completar."),
            (EstadoPlanUsuario.Completado or EstadoPlanUsuario.Cancelado, _) =>
                throw Invalida("Un plan completado o cancelado ya no cambia."),
            _ => false,
        };

    /// <summary>Lanza <c>transicion_plan_invalida</c> (o <c>plan_aun_no_termina</c>) si la transición no se permite.</summary>
    public static void Validar(EstadoPlanUsuario actual, EstadoPlanUsuario nuevo, bool llegoFechaFin)
    {
        if (EsTransicionValida(actual, nuevo, llegoFechaFin))
            return;

        if (actual == EstadoPlanUsuario.Activo && nuevo == EstadoPlanUsuario.Completado)
            throw new ReglaNegocioException(ReglaNegocioException.PlanAunNoTermina,
                "Todavía no llegaste al final del plan.");

        throw Invalida($"No se puede pasar de {actual} a {nuevo}.");
    }

    private static ReglaNegocioException Invalida(string mensaje) =>
        new(ReglaNegocioException.TransicionPlanInvalida, mensaje);
}
