# 🔄 Máquina de Estados del Negocio — Hábito

Entidad central: `Habito` (`Modules/HabitTracker/HabitTracker.Domain/Entities/Habito.cs`), con atributo de estado `Estado` (`EstadoHabito`).

Los 5 estados están declarados en un solo lugar (`EstadoHabito.cs`) — RF-NEG-03. Las transiciones permitidas están declaradas en un solo lugar (`TransicionesHabito.EsTransicionValida`) — RD-04.

## Tabla de transiciones

| Desde | Hacia | Quién la ejecuta | Condición |
|---|---|---|---|
| Pendiente | Activo | Usuario | El hábito está en Pendiente. |
| Pendiente | Abandonado | Usuario | El hábito está en Pendiente. |
| Activo | Pausado | Usuario | El hábito está en Activo. |
| Activo | Completado | Usuario | El hábito está en Activo. |
| Activo | Abandonado | Usuario | El hábito está en Activo. |
| Pausado | Activo | Usuario | El hábito está en Pausado. |
| Pausado | Abandonado | Usuario | El hábito está en Pausado. |
| Cualquier otra combinación | — | — | Prohibida (RF-NEG-04). El sistema la rechaza con `InvalidOperationException` y el estado no cambia. |

## Estados terminales (RF-NEG-05)

`Completado` y `Abandonado`: ninguna transición parte de ellos.

## Transición prohibida explícita (RF-NEG-04)

`Pausado → Completado`: para completar hay que reanudar (`Pausado → Activo`) primero. Intentarla se rechaza y el estado no cambia.
