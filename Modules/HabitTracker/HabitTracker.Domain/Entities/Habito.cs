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

    public Guid MetaId { get; private set; }
    public Meta Meta { get; private set; } = null!;

    public ICollection<RegistroDiario> Registros { get; private set; } = new List<RegistroDiario>();
    public ICollection<Etiqueta> Etiquetas { get; private set; } = new List<Etiqueta>();
    public DateTime FechaInicioCiclo { get; private set; }

    private Habito() { } // Constructor privado para EF Core

    public Habito(Guid usuarioId, string nombre, Periodicidad periodicidad,
                  decimal cantidadObjetivo, string unidad, Guid metaId)
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

    public void RegistrarAvance(Guid usuarioActualId, decimal cantidad, DateTime fechaUtc)
    {
        ValidarPropietario(usuarioActualId);
        if (Estado != EstadoHabito.Activo)
            throw new InvalidOperationException("Solo se puede registrar avance en estado Activo.");
        if (cantidad <= 0)
            throw new ArgumentException("La cantidad debe ser positiva.", nameof(cantidad));
        if (fechaUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha debe ser UTC.", nameof(fechaUtc));

        Registros.Add(new RegistroDiario(cantidad, fechaUtc, Unidad));
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
    }
}
