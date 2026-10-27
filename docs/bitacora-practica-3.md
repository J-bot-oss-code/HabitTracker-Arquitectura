# Bitácora — Práctica 3: Módulo de Negocio e Integración

## Sesión de trabajo

### Solicitud 1: Revisión del estado actual antes de implementar

**Prompt del usuario:**
> Antes de escribir código, revisa el estado actual de esta práctica. Lee AGENTS.md, feutures/PRACTICA3/_PLAN.md y feutures/PRACTICA3/practica-3-modulo-negocio-requisitos.md. Inspecciona el módulo HabitTracker existente... Confirma la rama activa y que el commit local 09e215e está presente. Compara el código actual con cada requisito y fase.

**Respuesta del agente:**
Identificó que el módulo HabitTracker tenía 5 estados (`Pendiente`, `Activo`, `Pausado`, `Completado`, `Abandonado`) en lugar de los 4 requeridos (`Planificado`, `Activo`, `Pausado`, `Completado`). Detectó que `HabitTracker.Application` estaba vacío, no había repositorios ni controlador de hábitos, y no existía integración con Permisos. Confirmó rama `feature/practica-3` y commit `09e215e`. Presentó plan de 7 fases con commits atómicos.

**Evidencia:** `git log --oneline -1` mostró `09e215e docs: precisar plan y requisitos de Práctica 3`.

### Solicitud 2: Implementación de Fase 1 (Dominio)

**Prompt del usuario:**
> Implementa primero el dominio y sus pruebas unitarias según las fases y los documentos.

**Respuesta del agente:**
Creó 6 commits atómicos:
1. `ee1d1fa` — EstadoHabito renombra Pendiente a Planificado y elimina Abandonado
2. `668f391` — TransicionesHabito define 4 transiciones permitidas y 3 prohibidas explícitas
3. `e6dcd6e` — enum Periodicidad (Diaria/Semanal)
4. `33cbd02` — Habito con UsuarioId, Periodicidad, CantidadObjetivo, Unidad, CumpleMetaFinal, FechaInicioCiclo
5. `62e8f2b` — RegistroDiario con Cantidad decimal, Fecha UTC y Unidad
6. `1637290` — suite de dominio con 43 pruebas

**Error del agente detectado:**
Las pruebas de `CalculoMetaTests` fallaron porque `RegistrarAvance` requiere estado `Activo`, pero las pruebas intentaban registrar avance en estado `Planificado`. Se detectó al ejecutar `dotnet test` (9 fallos).

**Corrección:**
Se agregó `habito.Activar(propietario)` antes de `RegistrarAvance` en las pruebas de cálculo de meta. También se corrigió el uso de reflexión para establecer `CumpleMetaFinal` (propiedad de solo lectura) reemplazándolo por registro de avances reales.

**Evidencia:** `dotnet test` → 43 passed, 0 failed después de la corrección.

### Solicitud 3: Cierre de Fase 1 (commits atómicos)

**Prompt del usuario:**
> Las 74 pruebas pasan, pero no empieces aún la Fase 2. Primero cierra la Fase 1: alinea Reconfigurar con los requisitos (nuevo ciclo, conservar historial, excluir avances anteriores), agrega pruebas, revisa cálculo UTC, organiza commits atómicos.

**Respuesta del agente:**
Agregó `FechaInicioCiclo` a `Habito` para rastrear el inicio del ciclo de medición. El cálculo de `CumpleMetaFinal` ahora filtra registros con `r.Fecha >= FechaInicioCiclo`. Se agregaron 8 pruebas nuevas para reconfiguración y límites UTC. Se reorganizaron todos los cambios en 6 commits atómicos en español.

**Error del agente detectado:**
La prueba `RF_NEG_02_Reconfigurar_IniciaNuevoCiclo_RegistrosAnterioresNoCuentan` falló porque esperaba 2 registros pero había 1. La causa fue que el test registraba avance antes del reconfigurar y luego intentaba verificar que no contaba, pero el registro se hacía en un solo paso.

**Corrección:**
Se ajustó la prueba para registrar dos avances antes del reconfigurar y verificar que después del reconfigurar (con nueva meta más alta) `CumpleMetaFinal` es false, pero los 2 registros se conservan en el historial.

**Evidencia:** `dotnet test` → 43 passed después de la corrección.

### Solicitud 4: Fase 2 (Application layer)

**Prompt del usuario:**
> Implementa únicamente la Fase 2 de Práctica 3: capa Application para HabitTracker.

**Respuesta del agente:**
Creó puertos (`IHabitoRepository`, `IUnitOfWork`, `IPermisoService`), DTOs (6 comandos/consultas/respuestas), `HabitoService` con 5 casos de uso y 17 pruebas de aplicación con dobles in-memory.

### Solicitud 5: Corrección de brechas en Fase 2

**Prompt del usuario:**
> Corrige únicamente las brechas encontradas en la Fase 2: identidad del permiso (correo vs GUID), MetaId ficticio, validación de Periodicidad, eliminación de comprobación duplicada de CumpleMetaFinal.

**Respuesta del agente:**
1. `ReconfigurarHabitoAsync` ahora recibe `correoSolicitante` como parámetro separado del `usuarioActualId` (GUID).
2. `CrearHabitoCommand` requiere `MetaId` real (eliminado `Guid.NewGuid()`).
3. Agregado `Enum.IsDefined` para `Periodicidad` en creación y reconfiguración.
4. Eliminada comprobación duplicada de `CumpleMetaFinal` en `CambiarEstadoAsync` (el dominio ya la valida).
5. Agregado `IMetaRepository` para validar existencia de Meta.
6. Agregado `if (usuarioActualId == Guid.Empty)` en todos los métodos.

**Error del agente detectado:**
Al agregar `IMetaRepository` al constructor de `HabitoService`, las 21 pruebas existentes fallaron con CS7036 (parámetro faltante). Se detectó al compilar.

**Corrección:**
Se agregó `InMemoryMetaRepository` a las pruebas y se actualizó la instanciación de `HabitoService` en todas las pruebas. También se corrigió la prueba `RF_NEG_02_AdvancedReconfig_HabitoCompletado_Rechaza` que esperaba `InvalidOperationException` pero recibía `UnauthorizedAccessException` porque el permiso se consultaba antes de verificar el estado. Se reordenó: primero verificar estado Completado, luego consultar permiso.

**Evidencia:** `dotnet test` → 21 passed después de la corrección.

### Solicitud 6: Fase 3 (Persistencia, Integración, API)

**Prompt del usuario:**
> Implementa la Fase 3 de Práctica 3: persistencia EF Core, integración con Permisos, endpoints API, prueba de integración real.

**Respuesta del agente:**
Creó:
- `HabitoRepository`, `MetaRepository`, `HabitTrackerUnitOfWork` con EF Core
- `HabitTrackerDbContext` con Fluent API para nuevos campos
- Migración `ActualizarEsquemaHabitos` (0 filas heredadas, sin pérdida)
- `PermisoServiceAdapter` conectando `IPermisoService` con `ISolicitudPermisoService`
- `HabitsController` con 5 endpoints protegidos con JWT y rol `Estandar`
- Prueba de integración real con SQLite en memoria (rechazo sin permiso + éxito tras aprobación)

**Error del agente detectado:**
La prueba de integración falló con `SQLite Error 1: 'no such table: SolicitudesPermisos'` porque ambas conexiones (HabitTracker y Permissions) compartían la misma conexión SQLite en memoria, y `EnsureCreatedAsync` de la primera context impedía la creación de tablas de la segunda.

**Corrección:**
Se separaron las conexiones SQLite (`habitConnection` y `permConnection`) para que cada contexto tenga su propia base en memoria.

**Segundo error detectado:**
`InvalidOperationException: The instance of entity type 'SolicitudPermiso' cannot be tracked because another instance with the same key value is already being tracked.` Causado por usar un solo contexto para `CrearAsync` (que trackea la entidad) y luego `AprobarAsync` (que hace `GetByIdAsync` con `AsNoTracking` y luego `Update` que intenta re-trackear).

**Corrección:**
Se usaron contextos separados para cada operación de permisos (`permContext1`, `permContext2`, `permContext3`), ya que los datos persisten en SQLite y los nuevos contextos ven los datos commiteados.

**Tercer error detectado:**
La prueba `RF_NEG_06_AdvancedReconfig_HabitoCompletado_Rechaza` esperaba `InvalidOperationException` pero recibía `UnauthorizedAccessException` porque el permiso se consultaba antes de verificar el estado Completado.

**Corrección:**
Se reordenó `ReconfigurarHabitoAsync` para verificar primero el estado Completado y luego consultar el permiso.

**Evidencia:** `dotnet test` → 104 passed, 0 failed.

### Solicitud 7: Fase 4 (Defectos y tags)

**Prompt del usuario:**
> Implementa la Fase 4... crea tags de defectos.

**Respuesta del agente:**
Creó tag `practica-3` en el commit base. Creó 4 commits de defectos, cada uno hijo directo de `practica-3`, cada uno cambiando un solo archivo de producción:
1. `practica-3-defecto-1`: permite `Planificado → Completado` (TransicionesHabito.cs)
2. `practica-3-defecto-2`: permite `Completado → Activo` (TransicionesHabito.cs)
3. `practica-3-defecto-3`: omite validación de propietario en `Activar()` (Habito.cs)
4. `practica-3-defecto-4`: omite consulta de permiso en `ReconfigurarHabitoAsync` (HabitoService.cs)

Cada defecto se verificó haciendo fallar al menos una prueba. Se volvió a la rama `feature/practica-3` después de cada tag.

**Error del agente detectado:**
Al crear el defecto 1, se intentó usar `git checkout practica-3` pero el HEAD estaba en detached state. Se manejó correctamente volviendo a la rama.

### Solicitud 8: Auditoría de Fase 3 y mejoras

**Prompt del usuario:**
> Realiza una auditoría revisando que todo cumpla... aplica las mejoras que consideres necesarias.

**Respuesta del agente:**
Identificó 3 hallazgos de seguridad y aplicó mejoras:
1. Agregó validación `if (usuarioActualId == Guid.Empty)` en todos los métodos de `HabitoService`.
2. Agregó manejo de 404 para hábitos inexistentes en `HabitsController` (catch `InvalidOperationException` cuando contiene "no existe").
3. Eliminó DTO muerto `ObtenerHabitosQuery` que no se usaba.

**Evidencia:** `dotnet build` → 0 warnings, 0 errores. `dotnet test` → 104 passed, 0 failed.

### Solicitud 9: Fase 5 (Documentación)

**Prompt del usuario:**
> Procede con la Fase 5 cumpliendo estrictamente con lo especificado.

**Respuesta del agente:**
Actualizó `docs/maquina-de-estados.md` con los 4 estados, tablas de transiciones permitidas/prohibidas, condiciones, estado terminal y semántica de reconfigurar. Actualizó `README.md` con sección de Práctica 3, guía de verificación de las 5 funcionalidades, comando `dotnet test`, comprobación desde clon limpio y sección `## Defectos` con los 4 tags. Creó `docs/bitacora-practica-3.md` con esta bitácora.

### Errores del agente registrados

1. **Pruebas de dominio fallaron por estado incorrecto** (Solicitud 2): Se intentó registrar avance en estado `Planificado` en lugar de `Activo`. Detectado por `dotnet test`. Corregido agregando `Activar()` antes de `RegistrarAvance()`.

2. **Prueba de reconfiguración con conteo incorrecto** (Solicitud 3): Se esperaban 2 registros pero había 1. Detectado por `dotnet test`. Corregido ajustando la secuencia de registros en la prueba.

3. **Pruebas de aplicación fallaron por parámetro faltante** (Solicitud 5): Al agregar `IMetaRepository` al constructor de `HabitoService`, 21 pruebas fallaron con CS7036. Detectado por `dotnet build`. Corregido agregando `InMemoryMetaRepository` a las pruebas.

4. **Prueba de integración falló por tabla inexistente** (Solicitud 6): SQLite en memoria compartida no creaba tablas de Permissions. Detectado por `dotnet test`. Corregido usando conexiones SQLite separadas.

5. **Prueba de integración falló por tracking conflict** (Solicitud 6): EF Core no permitía trackear la misma entidad en el mismo contexto. Detectado por `dotnet test`. Corregido usando contextos separados por operación.

6. **Prueba de integración falló por orden de validaciones** (Solicitud 6): `ReconfigurarHabitoAsync` consultaba permiso antes de verificar estado Completado. Detectado por `dotnet test`. Corregido reordenando las validaciones.

### Resumen de commits (25 en feature/practica-3)

| Fase | Commits | Descripción |
|------|---------|-------------|
| 0 | 3 | Plan y requisitos (preexistentes) |
| 1 | 6 | Dominio: EstadoHabito, TransicionesHabito, Periodicidad, Habito, RegistroDiario, tests |
| 2 | 4 | Application: puertos, DTOs, HabitoService, tests |
| 2b | 5 | Corrección de brechas: identidad permiso, MetaId real, validaciones, tests |
| 3 | 7 | Infraestructura: repositorios, DbContext, migración, adaptador, controller, tests |
| 3b | 1 | Mejoras post-auditoría |
| 4 | 4 | Defectos y tags |
| 5 | 3 | Documentación: maquina-de-estados.md, README.md, bitácora |

### Resultados finales

- `dotnet build HabitTrackerSystem.slnx`: 0 warnings, 0 errores
- `dotnet test`: 104 passed, 0 failed
- Tags: `practica-3`, `practica-3-defecto-1` a `practica-3-defecto-4`
- Rama: `feature/practica-3` (25 commits adelante de origin)
- Push: no realizado (pendiente de instrucción del usuario)

### Solicitud 10: Auditoría general y verificación de la rúbrica oficial

**Prompt del usuario:**
> Quiero que realices una auditoría general de todas las fases y requisitos, resuelvas los problemas con base en los requisitos del maestro y, al final, ejecutes `dotnet test` y la API para validar que no haya errores. También verifica la rúbrica oficial de 8.0 puntos.

**Hallazgos y correcciones:**
1. `RF_NEG_02_Reconfigurar_RegistroAntesDeCicloNoCuentaAunqueCumplaCantidad` usaba una hora fija de las 08:00 UTC, que podía ser futura al ejecutar la prueba. Se cambió a `DateTime.UtcNow.AddMinutes(-1)` para garantizar que el registro precede la reconfiguración.
2. `Habito.Completar` comprobaba la meta antes de llamar a la máquina central, duplicando la regla de transición (**RD-04**). Se eliminó la validación duplicada; la aceptación de `Activo → Completado` permanece en `TransicionesHabito`. Se actualizaron las aserciones de dominio y aplicación para verificar el rechazo y que el estado no cambie.
3. El middleware convertía todo `UnauthorizedAccessException` en `401`, incluso cuando el usuario ya estaba autenticado y no tenía autorización. Ahora devuelve `403` para identidad autenticada y conserva `401` para identidad no autenticada, según la guía HTTP de la práctica.
4. La migración no añadía `Habitos.Unidad`, aunque el modelo y el snapshot la requieren. La migración incluye la columna y su reversión. Una consulta SQL de solo lectura al entorno local confirmó cero filas en `Habitos` y `RegistrosDiarios`; el esquema local contiene `Habitos.Unidad`.

**Evidencia de validación:**
- `dotnet test --artifacts-path C:\Temp2\HabitTrackerAudit`: 104 pruebas pasaron, 0 fallaron. Se usó un directorio de artefactos aislado porque otra instancia de WebApi mantenía bloqueado el ejecutable del proyecto.
- `dotnet run --project Host\WebApi\WebApi.csproj --artifacts-path C:\Temp2\HabitTrackerApiRun --no-launch-profile -- --urls http://localhost:5256`: la API arrancó en Development sin errores de aplicación; `GET /swagger/index.html` respondió HTTP 200. La instancia de auditoría se detuvo al terminar la comprobación.
- `sqlcmd` en modo de solo lectura: 0 hábitos y 0 registros diarios heredados.

La suite de integración con Gestión de Permisos también forma parte de las 104 pruebas y pasó. La validación interactiva de los cinco frentes acumulados requiere solicitudes HTTP autenticadas y datos de prueba; no se modificaron cuentas ni hábitos de la base compartida.