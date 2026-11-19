# Bitácora — Asignación 3

Auditoría de notificaciones, cola de correos y reportes de la Práctica 5. Base: `develop` en `b393c03`.

**Cómo lo hice.** Le pedí al agente que auditara contra mis criterios y que ejecutara el sistema: clon limpio, `docker compose up --build`, cuentas del README. Cada hallazgo se reprodujo antes de corregirlo; cada corrección se aceptó al ver su prueba fallar sin ella y pasar con ella (suite completa: 1436 pruebas en verde). Tras corregir, comprobé en Docker los seis frentes de la revisión acumulada de la P5 y los tres hallazgos.

**Qué le pedí.**
- *P5, lo único que dije sobre estas piezas:* «elabora el plan para implementarla, no olvides la verdadera vicion del proyecto pero por el momento debemos cumplir con la rubrica del maestro» y, al final, «cuando termines actua como el maestro y al final dame los resultados de las prubebas contra la rubrica de evaluacion y si pasa con 8/8». No di instrucciones detalladas por pieza; el plan y los criterios los armó el agente.
- *Esta asignación:* «crea el plan para su implementacion … al final debes actuar como el maestro y comparar todo con la rubrica … usar la skill ponytail … kiss, yagni» y «Recuerda evitar una sobreingenieria». Aprobé el plan de 3 hallazgos: su columna «Corrección mínima» es la instrucción de corrección que se cita abajo.

| # | Pieza | Gravedad | Hallazgo | Incumple | Commit | Prueba |
|---|---|---|---|---|---|---|
| 1 | Reportes | Media | Un plan cancelado desaparece del reporte de constancia | RF-REP-02 | `c7d8f67` | `Hallazgo1_*` (2) |
| 2 | Cola | Baja | Un correo enviado muestra `intentos: 0` | RF-NOT-11 | `28cf6a4`, `c20008d` | `Hallazgo2_*` (2) |
| 3 | Notificaciones | Baja | Dos guardados simultáneos de la preferencia dan 500 | RF-NOT-07 | `701aa7a`, `63a8dee` | `Hallazgo3_*` (1) |

## Hallazgo 1 — Plan cancelado fuera del reporte (media)

- **Qué se pidió:** RF-REP-02 y mi criterio: «días del rango en que el plan estuvo en curso». No decía qué pasa con un plan cancelado.
- **Qué entregó el agente:** el reporte filtra `Estado != Cancelado` en el servicio y en la consulta del Administrador, y una prueba suya da esa omisión por correcta (`1ef78be`). Lo dejó escrito como «se deja fuera a propósito» en la PR, el historial y el ESTADO, pero no en el chat.
- **Evidencia:** con un avance hoy, `GET api/reports/adherence` trae 1 fila (100 %); tras cancelar el plan trae `"filas":[]`, con el token de la persona y con el del Administrador. La prueba nueva falla antes con «The collection was empty».
- **Por qué pasó:** criterio ausente y revisión insuficiente. Vi el aviso y lo tomé como decisión de producto; además la «revisión como el maestro» de la P5 (30/30) la hizo el propio agente y no incluía ningún plan cancelado.
- **Corrección:** el agente, con la instrucción «`PlanUsuario.FechaCancelacion`, que fija `Cancelar` y va en una migración nueva; en los dos filtros, un cancelado con fecha cuenta hasta esa fecha». Commit `c7d8f67`. Límite: los cancelados antes de la migración no tienen fecha y siguen fuera.
- **Qué cambio:** criterios §5 («un plan cancelado cuenta hasta el día en que se canceló») y `AGENTS.md` («lo que se deja fuera a propósito se vuelve criterio o se corrige»).

## Hallazgo 2 — `intentos: 0` en un correo enviado (baja)

- **Qué se pidió:** RF-NOT-11 («estado, intentos, fechas y último error»). Mi criterio usa «intentos» sin definirlo.
- **Qué entregó el agente:** `CorreoEnCola.MarcarEnviado` no cuenta el intento; el defecto viene de la cola de la P1 (`bf20c46`) y el agente la reutilizó. Lo vio dos veces (su consulta SQL y el informe de otro agente) y lo anotó en el ESTADO como detalle.
- **Evidencia:** `POST api/admin/email-queue/process` → `{"enviados":1}`; `GET api/admin/email-queue?estado=Enviado` → `"estado":"Enviado","intentos":0`. La prueba falla antes con `Expected: 1  Actual: 0`.
- **Por qué pasó:** criterio ambiguo. Solo probé el camino de fallo y dejé como deuda algo que contradecía RF-NOT-11.
- **Corrección:** el agente, con «`Intentos++` en `MarcarEnviado`». Commits `28cf6a4` y `c20008d` (el caso «falló una vez y luego salió → 2»).
- **Qué cambio:** criterios §5 («`intentos` cuenta cada envío intentado, también el exitoso») y `AGENTS.md` («una deuda que contradice un RF es un hallazgo»).

## Hallazgo 3 — 500 por dos guardados simultáneos (baja)

- **Qué se pidió:** RF-NOT-07. Mis criterios probaban una petición de una persona; ninguno de concurrencia, que el enunciado pedía.
- **Qué entregó el agente:** `GuardarPreferenciasAsync` lee y, si no existe, inserta; con clave primaria `UsuarioId`, dos peticiones a la vez chocan. Su prueba guarda dos veces en secuencia. Conocía el riesgo (`ReclamarAsync`, `LogrosService`) y no lo aplicó aquí.
- **Evidencia:** 12 `PUT api/notifications/preferences` en paralelo → al menos un `500`, y el log: `Violation of PRIMARY KEY constraint 'PK_PreferenciasNotificaciones'`. La prueba falla antes con `DbUpdateException`.
- **Por qué pasó:** criterio ausente: no trasladé «dos solicitudes simultáneas» a mis criterios.
- **Corrección:** el agente, con «en `AgregarAsync`, si la fila ya existe se actualiza esa fila». Commits `701aa7a` y `63a8dee` (la prueba falla por aserción, no por excepción).
- **Qué cambio:** criterios §5 («dos peticiones simultáneas nunca dan 500») y `AGENTS.md` («todo leer-e-insertar nuevo tolera la carrera»). Lección general añadida: *el agente que construye no es el único que verifica*.

## Revisado sin hallazgo
Rango sin datos (200, `filas: []`), `desde > hasta` y sin fechas (400), Estándar en la cola (403), notificación ajena (403 y sigue sin leer). «Sin Administrador activo» y «destinatario inválido» no se pueden provocar por la API.

## No corregido (baja)
El agregado del Administrador mide por día UTC y el personal por día local (RD-11), y no tiene cohorte mínima (RF-REP-04). Ambos están documentados y quedan para la Fase 6 del plan v2.

## Cómo verificar
Desde un clon con `git checkout asignacion-3`: se vuelve solo el archivo de producción al commit anterior, la prueba falla; se restaura y pasa.

```bash
git checkout c7d8f67~1 -- Modules/HabitTracker/HabitTracker.Domain/Planes/PlanUsuario.cs Modules/HabitTracker/HabitTracker.Application/Services/ReporteConstanciaService.cs Modules/HabitTracker/HabitTracker.Infrastructure/Repositories/ReporteConstanciaRepository.cs
dotnet test tests/HabitTracker.Integration.Tests --filter "FullyQualifiedName~Hallazgo1"   # falla (2)
git checkout asignacion-3 -- . && dotnet test tests/HabitTracker.Integration.Tests --filter "FullyQualifiedName~Hallazgo1"   # pasa

git checkout 28cf6a4~1 -- Modules/AccessControl/AccessControl.Domain/Entities/CorreoEnCola.cs
dotnet test tests/AccessControl.Infrastructure.Tests --filter "FullyQualifiedName~Hallazgo2"   # falla (2)
git checkout asignacion-3 -- . && dotnet test tests/AccessControl.Infrastructure.Tests --filter "FullyQualifiedName~Hallazgo2"   # pasa

git checkout 701aa7a~1 -- Modules/Notifications/Notifications.Infrastructure/Repositories/NotificacionRepository.cs
dotnet test tests/Notifications.Infrastructure.Tests --filter "FullyQualifiedName~Hallazgo3"   # falla
git checkout asignacion-3 -- . && dotnet test tests/Notifications.Infrastructure.Tests --filter "FullyQualifiedName~Hallazgo3"   # pasa
```
