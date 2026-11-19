using HabitTracker.Domain.Excepciones;
using HabitTracker.Domain.Planes;

namespace HabitTracker.Domain.Entities;

public class Habito
{
    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public Periodicidad Periodicidad { get; private set; }
    public decimal CantidadObjetivo { get; private set; }
    public string Unidad { get; private set; } = string.Empty;
    public EstadoHabito Estado { get; private set; }

    /// <summary>Meta del catálogo a la que pertenece (opcional desde HabitTracker v2: un hábito puede ir suelto).</summary>
    public Guid? MetaId { get; private set; }
    public Meta? Meta { get; private set; }

    /// <summary>Plan de la persona al que pertenece (Fase 4). Nulo: un hábito suelto, como todos los anteriores.</summary>
    public Guid? PlanUsuarioId { get; private set; }
    /// <summary>Instrucciones copiadas de la tarea del plan.</summary>
    public string? Descripcion { get; private set; }
    /// <summary>"Hoy solo lo mínimo": lo que cuenta en un mal día. Nulo en un hábito suelto.</summary>
    public string? VersionMinima { get; private set; }
    public Ancla? Ancla { get; private set; }

    public ICollection<RegistroDiario> Registros { get; private set; } = new List<RegistroDiario>();
    public ICollection<Etiqueta> Etiquetas { get; private set; } = new List<Etiqueta>();
    public DateTime FechaInicioCiclo { get; private set; }

    private Habito() { } // Constructor privado para EF Core

    public Habito(Guid usuarioId, string nombre, Periodicidad periodicidad,
                  decimal cantidadObjetivo, string unidad, Guid? metaId = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre no puede estar vacío.", nameof(nombre));

        if (string.IsNullOrWhiteSpace(unidad))
            throw new ArgumentException("La unidad no puede estar vacía.", nameof(unidad));

        if (cantidadObjetivo <= 0)
            throw new ArgumentException("La cantidad objetivo debe ser positiva.", nameof(cantidadObjetivo));

        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Nombre = nombre;
        Periodicidad = periodicidad;
        CantidadObjetivo = cantidadObjetivo;
        Unidad = unidad;
        MetaId = metaId;
        Estado = EstadoHabito.Planificado;
        FechaInicioCiclo = DateTime.UtcNow;
    }

    /// <summary>
    /// El hábito que nace de una tarea del plan: copia sus valores, para que un cambio en el catálogo no altere un plan en
    /// curso, y arranca Activo (la persona acaba de elegir empezar).
    /// </summary>
    public static Habito DesdeTareaDePlan(Guid usuarioId, Guid planUsuarioId, TareaPlantilla tarea)
    {
        ArgumentNullException.ThrowIfNull(tarea);
        var habito = new Habito(usuarioId, tarea.Nombre, tarea.Periodicidad, tarea.Cantidad, tarea.Unidad)
        {
            PlanUsuarioId = planUsuarioId,
            Descripcion = tarea.Instrucciones,
            VersionMinima = tarea.VersionMinima,
            Ancla = tarea.AnclaSugerida,
        };
        habito.Activar(usuarioId);
        return habito;
    }

    /// <summary>
    /// Verdadero si lo pausó el plan al pausarse, no la persona. Así, al reanudar el plan solo vuelven los que el plan pausó:
    /// un hábito que la persona dejó en pausa a propósito no se reactiva solo.
    /// </summary>
    public bool PausadoPorPlan { get; private set; }

    /// <summary>El plan se pausa (o termina sin conservar los hábitos) y pausa este hábito.</summary>
    public void PausarPorPlan(Guid usuarioActualId)
    {
        Pausar(usuarioActualId);
        PausadoPorPlan = true;
    }

    /// <summary>El plan se reanuda: vuelve solo si lo pausó el plan.</summary>
    public void ReanudarPorPlan(Guid usuarioActualId)
    {
        if (Estado == EstadoHabito.Pausado && PausadoPorPlan)
            Activar(usuarioActualId);
    }

    public bool CumpleMetaFinal => CalcularCumpleMetaFinal();

    private bool CalcularCumpleMetaFinal()
    {
        var ahora = DateTime.UtcNow;
        decimal suma = Periodicidad switch
        {
            Periodicidad.Diaria => Registros
                .Where(r => r.Fecha >= FechaInicioCiclo && r.Fecha.Date == ahora.Date)
                .Sum(r => r.Cantidad),
            Periodicidad.Semanal => Registros
                .Where(r => r.Fecha >= FechaInicioCiclo && EstaEnSemanaISOActual(r.Fecha, ahora))
                .Sum(r => r.Cantidad),
            _ => 0m
        };
        return suma >= CantidadObjetivo;
    }

    private static bool EstaEnSemanaISOActual(DateTime fecha, DateTime referenciaUtc)
    {
        var inicioSemana = ObtenerInicioSemanaISO(referenciaUtc);
        var finSemana = inicioSemana.AddDays(7);
        return fecha >= inicioSemana && fecha < finSemana;
    }

    private static DateTime ObtenerInicioSemanaISO(DateTime fechaUtc)
    {
        var diff = fechaUtc.DayOfWeek - DayOfWeek.Monday;
        if (diff < 0) diff += 7;
        return fechaUtc.Date.AddDays(-diff).Date;
    }

    public void Activar(Guid usuarioActualId)
    {
        ValidarPropietario(usuarioActualId);
        TransicionarA(EstadoHabito.Activo);
    }

    public void Pausar(Guid usuarioActualId)
    {
        ValidarPropietario(usuarioActualId);
        TransicionarA(EstadoHabito.Pausado);
    }

    public void Completar(Guid usuarioActualId)
    {
        ValidarPropietario(usuarioActualId);
        TransicionarA(EstadoHabito.Completado);
    }

    public void Reconfigurar(Guid usuarioActualId, Periodicidad nuevaPeriodicidad,
                             decimal nuevaCantidadObjetivo, string nuevaUnidad)
    {
        ValidarPropietario(usuarioActualId);
        if (Estado == EstadoHabito.Completado)
            throw new InvalidOperationException("No se puede reconfigurar un hábito completado.");

        if (string.IsNullOrWhiteSpace(nuevaUnidad))
            throw new ArgumentException("La unidad no puede estar vacía.", nameof(nuevaUnidad));
        if (nuevaCantidadObjetivo <= 0)
            throw new ArgumentException("La cantidad objetivo debe ser positiva.", nameof(nuevaCantidadObjetivo));

        Periodicidad = nuevaPeriodicidad;
        CantidadObjetivo = nuevaCantidadObjetivo;
        Unidad = nuevaUnidad;
        FechaInicioCiclo = DateTime.UtcNow;
    }

    /// <param name="esVersionMinima">
    /// "Hoy solo lo mínimo": cuenta como un día cumplido en la constancia, y solo existe en un hábito de un plan.
    /// </param>
    public void RegistrarAvance(Guid usuarioActualId, decimal cantidad, DateTime fechaUtc, bool esVersionMinima = false)
    {
        ValidarPropietario(usuarioActualId);
        if (Estado != EstadoHabito.Activo)
            throw new InvalidOperationException("Solo se puede registrar avance en estado Activo.");
        if (cantidad <= 0)
            throw new ArgumentException("La cantidad debe ser positiva.", nameof(cantidad));
        if (fechaUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha debe ser UTC.", nameof(fechaUtc));

        if (esVersionMinima && VersionMinima is null)
            throw new ReglaNegocioException(ReglaNegocioException.VersionMinimaNoDisponible,
                "Este hábito no tiene una versión mínima.");

        Registros.Add(new RegistroDiario(cantidad, fechaUtc, Unidad, esVersionMinima));
    }

    private void ValidarPropietario(Guid usuarioActualId)
    {
        if (UsuarioId != usuarioActualId)
            throw new UnauthorizedAccessException("El usuario no es el propietario del hábito.");
    }

    private void TransicionarA(EstadoHabito nuevoEstado)
    {
        bool cumpleMeta = (Estado == EstadoHabito.Activo && nuevoEstado == EstadoHabito.Completado)
            ? CumpleMetaFinal : false;

        if (!TransicionesHabito.EsTransicionValida(Estado, nuevoEstado, cumpleMeta))
        {
            throw new InvalidOperationException($"La transición de {Estado} a {nuevoEstado} está prohibida.");
        }

        Estado = nuevoEstado;
        // Cualquier cambio de estado hecho directamente lo decide la persona: el plan ya no es quien lo pausó.
        PausadoPorPlan = false;
    }
}
