# Bitácora — Asignación 3: auditoría de notificaciones, cola de correos y reportes

Tag: `asignacion-3`. Base auditada: `develop` en `b393c03` (19-11-2026), con la Práctica 5 ya entregada (`practica-5`).

## Cómo se auditó

- **Contra qué:** `docs/criterios-notificaciones-reportes.md`, los RF-NOT y RF-REP de `docs/practica_5/practica-5.md` y los RD-02, 03, 06, 07, 08, 10 y 11.
- **Cómo:** se leyó el código de los tres módulos y se **ejecutó el sistema**. Clon limpio de `develop` en `C:\src\ht-a3`, `copy .env.example .env` y `docker compose up --build` (Mailpit, sin correo real). Cuentas del README: `admin@ejemplo.com` y una Estándar `ana@ejemplo.com`, activada con el código leído en Mailpit.
- **Quién:** la auditoría y las correcciones las hizo el agente (Claude) a partir de mis instrucciones (abajo). Cada hallazgo se reprodujo **antes** de corregirlo, y cada corrección se dio por buena solo tras ver su prueba fallar sin ella, pasar con ella y toda la suite en verde (1435 pruebas).

**Instrucciones que le di al agente en esta sesión** (literales):

1. «ponte al corriente del proyecto con los md: estado del proyecto y el readmi, luego que estes al corriente iniciaremos con la asignacion 3.»
2. «aqui esta el md de la asignacion 3, crea el plan para su implementacion incluyendo en el plan que al final debes actuar como el maestro y comparar todo con la rubrica de evaluacion y asegurar que todo pasa bien, tambien usar la skill ponytail para ahorrar token y el plan debe ser ordenado e incluir pruebas para ver que todo esta bien, recuerda kiss, yagni. no olvides espesificar en que modelo se debe hacer cada cosa para ahorrar token.»
3. «Recuerda evitar una sobreingenieria» (rechacé el primer plan, que tenía 5 hallazgos y fases de más; el aprobado tiene 3).

Lo que **no** está en el repositorio son los prompts literales de la sesión de la Práctica 5. Lo que sí quedó escrito de aquella delegación son `AGENTS.md`, los criterios (`17e95db`, anteriores a todo el código del motor) y `docs/historial/practica-5.md`; los campos «Qué se pidió» citan esos textos.

## Resumen

| # | Pieza | Gravedad | Título | Incumple | Commit de la corrección | Prueba |
|---|---|---|---|---|---|---|
| 1 | Reportes | Media | Un plan cancelado desaparece del reporte de constancia | RF-REP-02 | `c7d8f67` | `Hallazgo1_*` (2) |
| 2 | Cola de correos | Baja | Un correo enviado muestra `intentos: 0` | RF-NOT-11 | `28cf6a4` | `Hallazgo2_*` |
| 3 | Notificaciones | Baja | Dos guardados simultáneos de la preferencia dan 500 | RF-NOT-07, RD-07 | `701aa7a` | `Hallazgo3_*` |

## Hallazgo 1 — Un plan cancelado desaparece del reporte de constancia (media)

| Campo | |
|---|---|
| **Qué se pidió** | RF-REP-02 (P5): «Un reporte sobre tu módulo de negocio. Responde una pregunta real del dominio». Mi criterio lo concretó: «días del rango en que el plan estuvo en curso» (`criterios-notificaciones-reportes.md`, RF-REP-02). El criterio **no decía qué pasa con un plan que se cancela**. |
| **Qué entregó el agente** | `ReporteConstanciaService` y `ReporteConstanciaRepository` filtran `Estado != Cancelado`, así que un plan que estuvo en curso dentro del rango deja de contar en cuanto se cancela, y con él sus avances. El agente lo anotó como «se deja fuera a propósito» (`docs/historial/practica-5.md`, Reportes) y escribió una prueba que da esa omisión por correcta (`RF_REP_02_UnPlanCancelado_NoEntraEnElReporte`). Ambos nacieron en `1ef78be` (18-11-2026). |
| **Evidencia** | `POST api/my-plans` → avance → `GET api/reports/adherence?desde=2026-11-19&hasta=2026-11-19` devuelve 1 fila (`diasConAvance:1`, `porcentajeConstancia:100`). Tras `POST api/my-plans/{id}/transition` con `Cancelado`, la misma consulta devuelve `"filas":[]`, tanto con el token de `ana` (`alcance:"propio"`) como con el del Administrador (`alcance:"todos"`). La causa estaba **repetida en dos sitios** (servicio personal y consulta global). |
| **Por qué pasó** | **Criterio ausente + revisión insuficiente.** Mi criterio no cubría el estado terminal «cancelado». Vi el aviso «se deja fuera a propósito» en el historial y lo acepté sin convertirlo en un criterio. Peor: la prueba del agente confirmaba el defecto, así que `dotnet test` en verde me daba una falsa seguridad. |
| **Corrección** | La hizo el agente con las instrucciones 2 y 3 de arriba (más el plan aprobado); la prueba se vio fallar antes y pasar después. Commit `c7d8f67`: `PlanUsuario.FechaCancelacion` (migración nueva `AgregarFechaCancelacionAlPlan`, columna anulable) y `FinEfectivo`; el servicio personal y la consulta del agregado miden hasta esa fecha. La prueba vieja, que afirmaba el defecto, se reemplazó por dos `Hallazgo1_*` (personal y Administrador). **Límite declarado:** un plan cancelado *antes* de la migración no tiene fecha y sigue fuera del reporte; no hay de dónde sacarla. |
| **Qué cambias** | En `docs/criterios-notificaciones-reportes.md`, sección «Casos límite (Asignación 3)»: *un plan cancelado cuenta hasta el día en que se canceló*. En `AGENTS.md`: *un estado terminal guarda su fecha; y todo lo que el agente «deja fuera a propósito» necesita un criterio escrito, no un aviso en el historial.* |

## Hallazgo 2 — Un correo enviado muestra `intentos: 0` (baja)

| Campo | |
|---|---|
| **Qué se pidió** | RF-NOT-11 (P5): «Cada correo registra estado (pendiente, enviado o fallido), intentos, fechas y último error». Mi criterio repite «intentos» y lo usa en RF-NOT-10 (`intentos = 3` tras fallar), pero **no define qué cuenta como intento**. |
| **Qué entregó el agente** | `CorreoEnCola.MarcarEnviado` cambia el estado y la fecha pero no incrementa `Intentos`: solo cuentan los fallos. El defecto es anterior a la P5 (cola mínima, `bf20c46`, 22-09-2026); en la P5 el agente reutilizó la cola y no contrastó la regla con RF-NOT-11. |
| **Evidencia** | `POST api/auth/register` → `POST api/admin/email-queue/process` (`{"enviados":1,...}`) → `GET api/admin/email-queue?estado=Enviado` devuelve `"estado":"Enviado"`, con `fechaUltimoIntento` y `fechaEnvio` rellenas, pero `"intentos":0`. |
| **Por qué pasó** | **Criterio ambiguo.** «Intentos» no estaba definido, y los criterios solo probaban el camino de fallo (RF-NOT-10). Además lo había visto: `ESTADO-DEL-PROYECTO.md` §6.2 decía «`Intentos` de un correo solo cuenta los fallos» como detalle por pulir; lo dejé como deuda en vez de tratarlo como incumplimiento de RF-NOT-11. |
| **Corrección** | La hizo el agente (instrucciones 2 y 3). Commit `28cf6a4`: `Intentos++` en `MarcarEnviado`. Prueba `Hallazgo2_UnCorreoEnviadoAlPrimerIntento_RegistraEseIntento`. El frontend ya mostraba «Intentos: N de 3» sin suponer cero, y RF-NOT-10 no cambia (los fallos se siguen contando igual). |
| **Qué cambias** | En los criterios: *`intentos` cuenta cada envío intentado, también el que sale bien*. En `AGENTS.md`: *una deuda anotada que contradice un RF no es deuda: es un hallazgo, y se corrige.* |

## Hallazgo 3 — Dos guardados simultáneos de la preferencia dan 500 (baja)

| Campo | |
|---|---|
| **Qué se pidió** | RF-NOT-07 (P5): desactivar el correo sin perder el canal interno. Mis criterios probaban el efecto (no se encola, la interna queda), con una persona y una petición. **No había ningún caso de concurrencia.** |
| **Qué entregó el agente** | `NotificacionService.GuardarPreferenciasAsync` lee la preferencia y, si no existe, la inserta (`80dead2`). La clave primaria es `UsuarioId`, así que dos peticiones a la vez de una persona sin preferencia insertan las dos y una falla. La prueba del servicio usa dobles de un solo hilo y no puede ver la carrera. |
| **Evidencia** | 12 `PUT api/notifications/preferences` con `{"correoActivo":false}` en paralelo (usuario sin preferencia guardada): entre los códigos registrados hubo un **500**, y el log de `webapi` muestra `DbUpdateException ... SqlException: Violation of PRIMARY KEY constraint 'PK_PreferenciasNotificaciones'. Cannot insert duplicate key`. La preferencia terminó en `false` (la otra petición sí la guardó). |
| **Por qué pasó** | **Criterio ausente.** Escribí los criterios pensando en un usuario y una petición; el enunciado pedía explícitamente «dos solicitudes simultáneas» y no lo trasladé a los criterios. |
| **Corrección** | La hizo el agente (instrucciones 2 y 3). Commit `701aa7a`: en `PreferenciaNotificacionesRepository.AgregarAsync`, si el insert choca y la fila ya existe, se actualiza con el último valor pedido; si el fallo es otro, se relanza. Sin cambiar ninguna interfaz. Prueba `Hallazgo3_AgregarLaPreferenciaCuandoOtraPeticionYaLaCreo_ActualizaEnVezDeFallar` (carrera simulada de forma determinista con dos contextos). Revisado: la preferencia es la única entidad de Notificaciones con clave natural. |
| **Qué cambias** | En los criterios: *dos peticiones simultáneas de la misma persona nunca dan 500*. En `AGENTS.md`: *toda operación «leer y, si no existe, insertar» debe tolerar la carrera, y los criterios llevan un caso de concurrencia.* |

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

Desde un clon limpio de la rama, para cada uno: se vuelve **solo el archivo de producción** al estado anterior a su commit, la prueba debe fallar, y al restaurarlo debe pasar.

```bash
git clone https://github.com/J-bot-oss-code/HabitTracker-Arquitectura.git && cd HabitTracker-Arquitectura
git checkout asignacion-3

# Hallazgo 1  (commit c7d8f67)
git checkout c7d8f67~1 -- Modules/HabitTracker/HabitTracker.Domain/Planes/PlanUsuario.cs Modules/HabitTracker/HabitTracker.Application/Services/ReporteConstanciaService.cs Modules/HabitTracker/HabitTracker.Infrastructure/Repositories/ReporteConstanciaRepository.cs
dotnet test tests/HabitTracker.Integration.Tests --filter "FullyQualifiedName~Hallazgo1"   # FALLA (2)
git checkout asignacion-3 -- . && dotnet test tests/HabitTracker.Integration.Tests --filter "FullyQualifiedName~Hallazgo1"   # PASA

# Hallazgo 2  (commit 28cf6a4)
git checkout 28cf6a4~1 -- Modules/AccessControl/AccessControl.Domain/Entities/CorreoEnCola.cs
dotnet test tests/AccessControl.Infrastructure.Tests --filter "FullyQualifiedName~Hallazgo2"   # FALLA
git checkout asignacion-3 -- . && dotnet test tests/AccessControl.Infrastructure.Tests --filter "FullyQualifiedName~Hallazgo2"   # PASA

# Hallazgo 3  (commit 701aa7a)
git checkout 701aa7a~1 -- Modules/Notifications/Notifications.Infrastructure/Repositories/NotificacionRepository.cs
dotnet test tests/Notifications.Infrastructure.Tests --filter "FullyQualifiedName~Hallazgo3"   # FALLA
git checkout asignacion-3 -- . && dotnet test tests/Notifications.Infrastructure.Tests --filter "FullyQualifiedName~Hallazgo3"   # PASA
```

Para reproducirlos contra la API real: `docker compose up --build` y los pasos de la columna «Evidencia» de cada hallazgo.
