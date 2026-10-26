namespace HabitTracker.Domain.Entities;

public enum EstadoHabito
{
    Planificado, // Estado inicial
    Activo,      // En progreso
    Pausado,     // Suspendido temporalmente
    Completado   // Estado terminal único
}
