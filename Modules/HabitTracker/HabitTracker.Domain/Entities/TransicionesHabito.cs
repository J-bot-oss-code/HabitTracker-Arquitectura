using System;
namespace HabitTracker.Domain.Entities;

public static class TransicionesHabito
{
    public static bool EsTransicionValida(EstadoHabito actual, EstadoHabito nuevo, bool cumpleMetaFinal = false) =>
        (actual, nuevo) switch
        {
            (EstadoHabito.Planificado, EstadoHabito.Activo) => true,
            (EstadoHabito.Activo, EstadoHabito.Pausado) => true,
            (EstadoHabito.Pausado, EstadoHabito.Activo) => true,
            (EstadoHabito.Activo, EstadoHabito.Completado) => cumpleMetaFinal,
            // Prohibidas explícitas (orden intencional: defecto 1, defecto 2, terminal)
            (EstadoHabito.Planificado, EstadoHabito.Completado) =>
                throw new InvalidOperationException("No se puede saltar el estado Activo; permanece Planificado."),
            (EstadoHabito.Pausado, EstadoHabito.Completado) =>
                throw new InvalidOperationException("Primero debe volver a Activo; permanece Pausado."),
            (EstadoHabito.Completado, EstadoHabito.Activo) => true,
            (EstadoHabito.Completado, _) =>
                throw new InvalidOperationException("Completado es terminal; permanece Completado."),
            _ => false
        };
}
