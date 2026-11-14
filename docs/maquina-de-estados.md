# Máquina de Estados: Hábito (Módulo de Negocio)

La entidad central del negocio es el `Habito`. Sus estados y transiciones se gestionan de manera centralizada en el dominio mediante la clase `TransicionesHabito`. La máquina de Habit Tracker es independiente de la máquina de solicitudes de Gestión de Permisos.

## Estados

| Estado | Tipo | Descripción |
| :--- | :--- | :--- |
| `Planificado` | Inicial | Estado al crear el hábito. |
| `Activo` | Intermedio | Hábito en seguimiento. |
| `Pausado` | Intermedio | Hábito suspendido temporalmente. |
| `Completado` | Terminal único | Hábito finalizado con éxito. Sin transiciones de salida. |

## Transiciones permitidas

| Desde | Hacia | Quién la ejecuta | Condición de aceptación |
| :--- | :--- | :--- | :--- |
| `Planificado` | `Activo` | Usuario propietario | `Habito.UsuarioId == usuarioActualId` (identidad autenticada). |
| `Activo` | `Pausado` | Usuario propietario | `Habito.UsuarioId == usuarioActualId`. |
| `Pausado` | `Activo` | Usuario propietario | `Habito.UsuarioId == usuarioActualId`. |
| `Activo` | `Completado` | Usuario propietario | `Habito.UsuarioId == usuarioActualId` y `CumpleMetaFinal == true`. |

## Transiciones prohibidas explícitas

El orden de esta tabla es intencional. La primera fila es el defecto 1; la salida del estado terminal es el defecto 2.

| Desde | Hacia | Rechazo esperado |
| :--- | :--- | :--- |
| `Planificado` | `Completado` | No se puede saltar el estado `Activo`; permanece `Planificado`. |
| `Pausado` | `Completado` | Primero debe volver a `Activo`; permanece `Pausado`. |
| `Completado` | Cualquiera | `Completado` es terminal; permanece `Completado`. |

Cualquier otro par no enumerado también se rechaza y conserva el estado actual. No existe transición de salida desde `Completado`.

## Medición del avance y significado de completar

- Un hábito tiene periodicidad `Diaria` o `Semanal`, una cantidad objetivo positiva y una unidad.
- `CumpleMetaFinal` es verdadero cuando la suma de los avances del propietario para el período vigente alcanza o supera la cantidad objetivo.
- Para `Diaria`, el período es el día UTC.
- Para `Semanal`, la semana ISO en UTC, de lunes a lunes.
- Solo se registra avance mientras el hábito está `Activo`.
- Solo se completa desde `Activo`, siendo su propietario y después de alcanzar la meta del período.

## Significado de reconfigurar/resetear

`advanced-reconfig` reemplaza la periodicidad, cantidad objetivo y/o unidad con valores válidos y empieza un nuevo ciclo de medición desde la fecha UTC de reconfiguración. El progreso histórico se conserva y puede consultarse; los registros anteriores al nuevo ciclo no cuentan para la meta nueva. La operación no cambia el estado de la máquina y se rechaza si el hábito está `Completado`. Requiere el permiso aplicado `Habits.AdvancedReconfig` para el mismo usuario.
---

# Máquina de Estados: Plan del usuario (HabitTracker v2)

Máquina **propia del plan que una persona sigue** (`PlanUsuario`), independiente de la del hábito: cambiar una no obliga a cambiar la otra (RF-NEG-09). Vive en un solo punto: `TransicionesPlanUsuario` (`Modules/HabitTracker/HabitTracker.Domain/Planes/PlanUsuario.cs`). Los hábitos del plan se pausan y reanudan con él por `Habito.PausarPorPlan` / `ReanudarPorPlan`, sin cambiar las transiciones del hábito.

## Estados

| Estado | Tipo | Significado |
|---|---|---|
| `Activo` | Inicial | El plan está en marcha. |
| `Pausado` | Intermedio | Suspendido con un motivo; los días de pausa no cortan la racha y se suman al final del plan. |
| `Completado` | Terminal | Se llegó a la fecha de fin prevista. |
| `Cancelado` | Terminal | La persona lo dejó. |

## Transiciones

| Desde | Hacia | Condición |
|---|---|---|
| `Activo` | `Pausado` | Con motivo (`motivo_pausa_requerido`); no si el plan ya llegó a su fin. |
| `Pausado` | `Activo` | Reanudar: la fecha de fin se corre los días de pausa. |
| `Activo` | `Completado` | Solo al llegar a la fecha de fin (`plan_aun_no_termina`). |
| `Activo` / `Pausado` | `Cancelado` | Siempre. |

**Prohibidas explícitas** (400 `transicion_plan_invalida`, el estado no cambia): `Pausado → Completado` (primero se reanuda) y cualquier salida de `Completado` o `Cancelado`.

Al terminar un plan, los hábitos se conservan activos o quedan en pausa según lo que elija la persona; el historial y el récord del plan no se pierden. Los ajustes (modo suave, extender) no son transiciones: solo con el plan `Activo` y nunca se aplican solos.
