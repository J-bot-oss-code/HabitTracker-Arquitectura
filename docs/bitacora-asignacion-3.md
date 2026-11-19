# Bitácora — Asignación 3: auditoría de notificaciones, cola de correos y reportes

Tag: `asignacion-3`. Base auditada: `develop` en `b393c03` (19-11-2026), con la Práctica 5 ya entregada (`practica-5`).

## Cómo se auditó

- **Contra qué:** `docs/criterios-notificaciones-reportes.md`, los RF-NOT y RF-REP de `docs/practica_5/practica-5.md` y los RD-02, 03, 06, 07, 08, 10 y 11.
- **Cómo:** se leyó el código de los tres módulos y se **ejecutó el sistema**. Clon limpio de `develop` en `C:\src\ht-a3`, `copy .env.example .env` y `docker compose up --build` (Mailpit, sin correo real). Cuentas del README: `admin@ejemplo.com` y una Estándar `ana@ejemplo.com`, activada con el código leído en Mailpit. Cada hallazgo se reprodujo **antes** de corregirlo.
- **Quién:** la auditoría y las correcciones las hizo el agente (Claude) a partir de mis instrucciones. Una corrección se dio por buena solo tras ver su prueba fallar sin ella, pasar con ella y la suite completa en verde (1436 pruebas).

**Instrucciones que le di al agente en esta sesión** (literales):

1. «ponte al corriente del proyecto con los md: estado del proyecto y el readmi, luego que estes al corriente iniciaremos con la asignacion 3.»
2. «aqui esta el md de la asignacion 3, crea el plan para su implementacion incluyendo en el plan que al final debes actuar como el maestro y comparar todo con la rubrica de evaluacion y asegurar que todo pasa bien, tambien usar la skill ponytail para ahorrar token y el plan debe ser ordenado e incluir pruebas para ver que todo esta bien, recuerda kiss, yagni. no olvides espesificar en que modelo se debe hacer cada cosa para ahorrar token.»
3. «Recuerda evitar una sobreingenieria» (rechacé el primer plan, que tenía 5 hallazgos y fases de más).
4. Aprobé el plan reducido (3 hallazgos, una corrección mínima por cada uno). **Ese plan es la instrucción de corrección**: la columna «Corrección mínima» de cada hallazgo es lo que se le ordenó al agente, y se cita literal en cada «Corrección» de abajo.

Lo que **no** está en el repositorio son los prompts literales de la sesión de la Práctica 5. Lo que sí quedó escrito de aquella delegación son `AGENTS.md`, los criterios (`17e95db`, anteriores a todo el código del motor) y `docs/historial/practica-5.md`; los campos «Qué se pidió» citan esos textos.

## Resumen

| # | Pieza | Gravedad | Título | Incumple | Corrección | Pruebas |
|---|---|---|---|---|---|---|
| 1 | Reportes | Media | Un plan cancelado desaparece del reporte de constancia | RF-REP-02 | `c7d8f67` | `Hallazgo1_*` (2) |
| 2 | Cola de correos | Baja | Un correo enviado muestra `intentos: 0` | RF-NOT-11 | `28cf6a4` | `Hallazgo2_*` (2; la segunda en `c20008d`) |
| 3 | Notificaciones | Baja | Dos guardados simultáneos de la preferencia dan 500 | RF-NOT-07 | `701aa7a` | `Hallazgo3_*` (1; reforzada en `63a8dee`) |

## Hallazgo 1 — Un plan cancelado desaparece del reporte de constancia (media)

| Campo | |
|---|---|
| **Qué se pidió** | RF-REP-02 (P5): «Un reporte sobre tu módulo de negocio. Responde una pregunta real del dominio». Mi criterio lo concretó: «días del rango en que el plan estuvo en curso» (`criterios-notificaciones-reportes.md`, RF-REP-02). El criterio **no decía qué pasa con un plan que se cancela**. |
| **Qué entregó el agente** | `ReporteConstanciaService` y `ReporteConstanciaRepository` filtran `Estado != Cancelado`, así que un plan que estuvo en curso dentro del rango deja de contar en cuanto se cancela, y con él sus avances. El agente lo anotó como «se deja fuera a propósito» (`docs/historial/practica-5.md`, Reportes) y escribió una prueba que da esa omisión por correcta (`RF_REP_02_UnPlanCancelado_NoEntraEnElReporte`). Ambos nacieron en `1ef78be` (18-11-2026). |
| **Por qué pasó** | **Criterio ausente + revisión insuficiente.** Mi criterio no cubría el estado terminal «cancelado». El defecto estaba a la vista: el historial lo llamaba «a propósito» y el ESTADO §6.2 lo listaba como detalle por pulir («el reporte de constancia no incluye los planes cancelados»), pero lo traté como decisión de producto y no como incumplimiento de RF-REP-02. Además, la prueba del agente confirmaba el defecto, así que `dotnet test` en verde me daba una falsa seguridad. |
| **Corrección** | La hizo el agente. **Instrucción (plan aprobado):** «`PlanUsuario.FechaCancelacion` (`DateTime?`), que fija `Cancelar` y que va en una migración nueva. En los dos filtros, un cancelado con fecha cuenta hasta esa fecha.» **Devolvió** el commit `c7d8f67` (`FechaCancelacion` + `FinEfectivo`, migración `AgregarFechaCancelacionAlPlan` anulable, y los dos filtros) y reemplazó la prueba que afirmaba el defecto por dos `Hallazgo1_*`. Verificado: la causa repetida se corrigió en los dos sitios (con solo uno revertido falla solo la prueba de ese sitio). **Límite declarado:** un plan cancelado *antes* de la migración no tiene fecha y sigue fuera del reporte; no hay de dónde sacarla. |
| **Qué cambias** | En `docs/criterios-notificaciones-reportes.md`, sección 5: *un plan cancelado cuenta hasta el día en que se canceló*. En `AGENTS.md`: *lo que se «deja fuera a propósito» se convierte en un criterio escrito o se corrige; una deuda que contradice un RF es un hallazgo.* |

Evidencia contra la API real (sobre `b393c03`, antes de corregir):

```text
GET  api/reports/adherence?desde=2026-11-19&hasta=2026-11-19      (ana, con el plan en curso y 1 avance)
  -> "filas":[{"plan":"Mi día de oficina saludable","personas":1,"diasConAvance":1,"diasSoloMinimo":0,"diasEnRango":1,"porcentajeConstancia":100}]
POST api/my-plans/{id}/transition {"nuevoEstado":"Cancelado","conservarHabitos":false}   -> 200
GET  el mismo reporte (token de ana, alcance "propio")            -> "filas":[]
GET  el mismo reporte (token del Administrador, alcance "todos")  -> "filas":[]
```

Prueba, sobre la producción anterior: `Hallazgo1_...EnElReportePersonal` y `...EnElReporteDelAdministrador` → `Assert.Single() Failure: The collection was empty`; sobre el tag, pasan.

## Hallazgo 2 — Un correo enviado muestra `intentos: 0` (baja)

| Campo | |
|---|---|
| **Qué se pidió** | RF-NOT-11 (P5): «Cada correo registra estado (pendiente, enviado o fallido), intentos, fechas y último error». Mi criterio repite «intentos» y lo usa en RF-NOT-10 (`intentos = 3` tras fallar), pero **no define qué cuenta como intento**. |
| **Qué entregó el agente** | `CorreoEnCola.MarcarEnviado` cambia el estado y la fecha pero no incrementa `Intentos`: solo cuentan los fallos. El defecto es anterior a la P5 (cola mínima, `bf20c46`, 22-09-2026); en la P5 el agente reutilizó la cola y no contrastó la regla con RF-NOT-11. |
| **Por qué pasó** | **Criterio ambiguo.** «Intentos» no estaba definido, y los criterios solo probaban el camino de fallo (RF-NOT-10). Además lo había visto: `ESTADO-DEL-PROYECTO.md` §6.2 decía «`Intentos` de un correo solo cuenta los fallos» como detalle por pulir; lo dejé como deuda en vez de tratarlo como incumplimiento de RF-NOT-11. |
| **Corrección** | La hizo el agente. **Instrucción (plan aprobado):** «`Intentos++` en `MarcarEnviado`.» **Devolvió** `28cf6a4` (una línea + `Hallazgo2_UnCorreoEnviadoAlPrimerIntento_RegistraEseIntento`) y, al revisar el criterio nuevo, `c20008d` con `Hallazgo2_UnCorreoQueFallaUnaVezYLuegoSale_CuentaLosDosIntentos`. El frontend ya mostraba «Intentos: N de 3» sin suponer cero, y RF-NOT-10 no cambia (los fallos se cuentan igual). |
| **Qué cambias** | En los criterios: *`intentos` cuenta cada envío intentado, también el que sale bien*. En `AGENTS.md`: *los criterios definen cada número que se muestra, no solo el camino de fallo; una deuda que contradice un RF es un hallazgo.* |

Evidencia contra la API real (sobre `b393c03`, antes de corregir):

```text
POST api/auth/register (ana) -> 201
POST api/admin/email-queue/process -> {"enviados":1,"fallidos":0,"recuperados":0}
GET  api/admin/email-queue?estado=Enviado ->
  [{"destinatario":"ana@ejemplo.com","estado":"Enviado","intentos":0,"fechaUltimoIntento":"2026-11-19T18:31:04.0995272","fechaEnvio":"2026-11-19T18:31:04.2273977","ultimoError":null}]
```

Prueba, sobre la producción anterior: `Expected: 1  Actual: 0` y `Expected: 2  Actual: 1`; sobre el tag, pasan.

## Hallazgo 3 — Dos guardados simultáneos de la preferencia dan 500 (baja)

| Campo | |
|---|---|
| **Qué se pidió** | RF-NOT-07 (P5): desactivar el correo sin perder el canal interno. Mis criterios probaban el efecto (no se encola, la interna queda), con una persona y una petición. **No había ningún caso de concurrencia**, que el enunciado pedía explícitamente («dos solicitudes simultáneas»). |
| **Qué entregó el agente** | `NotificacionService.GuardarPreferenciasAsync` lee la preferencia y, si no existe, la inserta (`80dead2`). La clave primaria es `UsuarioId`, así que dos peticiones a la vez de una persona sin preferencia insertan las dos y una falla con 500. La prueba del servicio usa dobles de un solo hilo y no puede ver la carrera. |
| **Por qué pasó** | **Criterio ausente.** Escribí los criterios pensando en un usuario y una petición y no trasladé la concurrencia que el enunciado nombraba. |
| **Corrección** | La hizo el agente. **Instrucción (plan aprobado):** «En `PreferenciaNotificacionesRepository.AgregarAsync`: si la fila ya existe (`DbUpdateException`), se actualiza esa fila.» **Devolvió** `701aa7a` (el cambio, sin tocar ninguna interfaz, y la prueba con la carrera simulada con dos contextos) y `63a8dee`, que la hace fallar por aserción (`Assert.Null`) en vez de por excepción. Revisado: la preferencia es la única entidad de Notificaciones con clave natural; el mismo patrón existe en el perfil de salud de HabitTracker, fuera del alcance auditado (queda como deuda en el ESTADO). |
| **Qué cambias** | En los criterios: *dos peticiones simultáneas de la misma persona nunca dan 500*. En `AGENTS.md`: *toda operación «leer y, si no existe, insertar» nueva tolera la carrera, y los criterios nuevos llevan un caso de dos peticiones simultáneas.* |

Evidencia contra la API real (sobre `b393c03`, antes de corregir):

```text
GET /api/notifications/preferences (ana, nunca guardada) -> {"correoActivo":true}
12 x PUT /api/notifications/preferences {"correoActivo":false} en paralelo -> códigos registrados: 3 x 200, 1 x 500
log de webapi: DbUpdateException ... SqlException: Violation of PRIMARY KEY constraint 'PK_PreferenciasNotificaciones'.
               Cannot insert duplicate key in object 'dbo.PreferenciasNotificaciones'.
```

Prueba, sobre la producción anterior: `Assert.Null() Failure: Value is not null ... DbUpdateException ... UNIQUE constraint failed`; sobre el tag, pasa.

## Después de corregir: la API real y los seis frentes

Con un clon limpio de esta rama (`docker compose up --build`, la migración se aplica al arrancar) y el mismo script para todo:

- **Los seis frentes de la revisión acumulada de la P5 pasan:** registro con activación por el código de Mailpit (y el código reutilizado se rechaza), sesión y 403 de un Estándar en una operación de Administrador, logout con `me` → 401, recuperación de contraseña con login con la clave nueva, permisos (aprobar, rechazar con motivo, aprobar dos veces → 400, aprobar una rechazada → 400, el Administrador no resuelve su propia solicitud → 400), hábito con `Planificado → Completado` rechazado (400) y `Planificado → Activo` (204), y un documento que sigue en el listado y se descarga tras `docker compose down` (sin `-v`) y volver a levantar.
- **Los tres hallazgos ya no se reproducen:** ningún correo `Enviado` con `intentos: 0` (los hay con `1`); el plan cancelado sigue en el reporte personal y en el del Administrador; 12 `PUT` simultáneos de la preferencia → 12 respuestas y ninguna 500.
- Un primer intento del frente de permisos falló por mi propio script (usé claves con espacios como «Acceso reportes»: la clave se valida y responde 400, que es lo correcto); con claves válidas (`Reports.Ver`) pasó entero.

## Revisión como el maestro

Un subagente (Opus) calificó la rama desde un clon limpio, con la rúbrica. Resultado: **4,5/5 estimado**; Correcciones, Auditoría y Pruebas «Logrado», y la bitácora «Parcial» por no mostrar la instrucción de corrección ni salidas pegadas. Lo que encontró y lo que se hizo:

| Observación | Qué se hizo |
|---|---|
| Falta la instrucción de corrección y las salidas reales | Añadidas (campo «Corrección» y los bloques de evidencia de arriba) |
| La prueba del hallazgo 3 fallaba por excepción, no por aserción | `63a8dee` |
| El criterio «falló una vez y salió → 2» no tenía prueba | `c20008d` |
| Dos lecciones de `AGENTS.md` prometían más de lo que cumple el código (`Completar` no guarda fecha; el patrón leer-insertar sigue en el perfil de salud) | Lecciones reescritas a lo que sí se cumple; lo existente, como deuda en el ESTADO |
| H1 y H2 ya constaban como deuda | Reconocido en «Por qué pasó» de cada uno |

## Casos que se revisaron y no son hallazgo

| Caso | Qué se ejecutó | Resultado |
|---|---|---|
| Rango sin datos | `GET api/reports/permissions?desde=2020-01-01&hasta=2020-01-31` | 200, `filas: []`, `total: 0` |
| `desde` posterior a `hasta`, o sin fechas | `GET api/reports/adherence` con ese rango, y sin parámetros | 400 en los dos |
| Un Estándar en la cola | `GET api/admin/email-queue` con el token de `ana` | 403 |
| Marcar la notificación de otra persona | `ana` crea una solicitud (201); luego `POST api/notifications/{id del Administrador}/read` con el token de `ana` | 403, y la del Administrador sigue `leida:false` |
| Usuario sin ningún Administrador activo | No se puede provocar: lo impide `ultimo_administrador` | n/a |
| Destinatario inválido | No se puede provocar por la API: el correo se valida al registrarse | n/a |
| Errores con trazas, rutas o credenciales | `GlobalExceptionMiddleware` responde `ProblemDetails` genérico; no hay claves en el historial (`gitleaks` pasa) | Sin hallazgo |

## Hallazgos que declaro y no corrijo (gravedad baja)

- **El agregado del Administrador mide por día UTC y el personal por día local** (RD-11): un avance de noche puede caer en días distintos según el reporte. Es una decisión documentada (README, Reportes), el agregado no tiene una zona única, y corregirlo toca el repositorio y su prueba; queda para la Fase 6 del plan v2.
- **Sin cohorte mínima en el agregado** (RF-REP-04): con una sola persona en un plan, el Administrador ve su constancia exacta, aunque sin nombre ni correo. Se resuelve con las métricas de la Fase 6.
- **Seguimiento:** la API simulada del frontend (`mocks/handlers/reportes.ts`) todavía excluye los planes cancelados; el contrato no cambia y la app real no se ve afectada.

## Cómo verificar cada hallazgo

Desde un clon limpio, para cada uno: se vuelve **solo el archivo de producción** al estado anterior a su commit, las pruebas del hallazgo deben fallar, y al restaurarlo deben pasar. (Los comandos usan el tag `asignacion-3`: existe en el punto entregado.)

```bash
git clone https://github.com/J-bot-oss-code/HabitTracker-Arquitectura.git && cd HabitTracker-Arquitectura
git checkout asignacion-3

# Hallazgo 1  (commit c7d8f67)
git checkout c7d8f67~1 -- Modules/HabitTracker/HabitTracker.Domain/Planes/PlanUsuario.cs Modules/HabitTracker/HabitTracker.Application/Services/ReporteConstanciaService.cs Modules/HabitTracker/HabitTracker.Infrastructure/Repositories/ReporteConstanciaRepository.cs
dotnet test tests/HabitTracker.Integration.Tests --filter "FullyQualifiedName~Hallazgo1"   # FALLAN (2)
git checkout asignacion-3 -- . && dotnet test tests/HabitTracker.Integration.Tests --filter "FullyQualifiedName~Hallazgo1"   # PASAN

# Hallazgo 2  (commit 28cf6a4)
git checkout 28cf6a4~1 -- Modules/AccessControl/AccessControl.Domain/Entities/CorreoEnCola.cs
dotnet test tests/AccessControl.Infrastructure.Tests --filter "FullyQualifiedName~Hallazgo2"   # FALLAN (2)
git checkout asignacion-3 -- . && dotnet test tests/AccessControl.Infrastructure.Tests --filter "FullyQualifiedName~Hallazgo2"   # PASAN

# Hallazgo 3  (commit 701aa7a)
git checkout 701aa7a~1 -- Modules/Notifications/Notifications.Infrastructure/Repositories/NotificacionRepository.cs
dotnet test tests/Notifications.Infrastructure.Tests --filter "FullyQualifiedName~Hallazgo3"   # FALLA
git checkout asignacion-3 -- . && dotnet test tests/Notifications.Infrastructure.Tests --filter "FullyQualifiedName~Hallazgo3"   # PASA
```

Para reproducirlos contra la API real: `docker compose up --build` y los pasos de los bloques de evidencia de cada hallazgo.
