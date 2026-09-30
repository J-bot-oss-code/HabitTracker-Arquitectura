using System;
namespace HabitTracker.Domain.Entities;

public static class TransicionesHabito
{
    public static bool EsTransicionValida(EstadoHabito actual, EstadoHabito nuevo) =>
        (actual, nuevo) switch
        {
            (EstadoHabito.Pendiente, EstadoHabito.Activo) => true,
            (EstadoHabito.Activo, EstadoHabito.Pausado) => true,
            (EstadoHabito.Activo, EstadoHabito.Completado) => true,
            (EstadoHabito.Activo, EstadoHabito.Abandonado) => true,
            (EstadoHabito.Pausado, EstadoHabito.Activo) => true,
            (EstadoHabito.Pausado, EstadoHabito.Abandonado) => true,
            // Transiciones prohibidas explícitas desde estados terminales (RF-NEG-04, RF-NEG-05):
            (EstadoHabito.Completado, _) => throw new InvalidOperationException("Un hábito completado no puede cambiar de estado."),
            (EstadoHabito.Abandonado, _) => throw new InvalidOperationException("Un hábito abandonado no puede cambiar de estado."),
            _ => false
        };
}
