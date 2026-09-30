namespace HabitTracker.Domain.Entities;

public enum EstadoHabito
{
    Pendiente,   // Estado inicial
    Activo,      // En progreso
    Pausado,     // Suspendido temporalmente
    Completado,  // Estado terminal exitoso
    Abandonado   // Estado terminal fallido
}
