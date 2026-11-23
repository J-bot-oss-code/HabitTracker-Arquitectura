# Estado del proyecto — contexto para nuevas sesiones

> Última actualización: 20-11-2026, tras preparar el despliegue a un VPS (compose de producción, Caddy con HTTPS, respaldos y `docs/produccion.md`; rama `chore/produccion-compose`). Antes, el 19-11-2026, la auditoría de la Asignación 4 y, ese día, la corrección de la Asignación 3 (3 hallazgos de la P5) y la entrega de la Práctica 5 (tag `practica-5`). Antes, el 12-11-2026, la Fase 4b del plan HabitTracker v2 (frontend de Mis planes, Hoy y logros); ese mismo día, la Fase 4a (backend de Mis planes, check-in diario y adherencia) y la Fase 3b (frontend del catálogo) y, el 09-11-2026, la Fase 3a (backend del catálogo de 13 planes y del recomendador) y, el 08-11-2026, la Fase 2b (frontend de perfil y salud conectado) y la Fase 2a (backend de perfil, salud cifrada y progreso corporal). Antes, ese mismo día, la Fase 1 (cimientos), y el 07-11-2026 la Fase 0 (skill de contexto). Este archivo describe **solo el estado actual** (tope de 120 líneas, lo vigila `scripts/ci/verificar-contexto.sh`). La historia de cada fase está en `docs/historial/`.
> **Al iniciar una sesión, usa la skill `contexto-habittracker`** (`.claude/skills/contexto-habittracker/`): rutas, contratos, recetas y requisitos sin leer el repositorio entero. Reglas de trabajo en `AGENTS.md`; guía de uso y evaluación en `README.md`.

## 1. Resumen

HabitTracker Nexus es un monolito modular en .NET 10 con Clean Architecture. Tiene un solo host (`Host/WebApi`) y 7 módulos en `Modules/`, cada uno con `Domain ← Application ← Infrastructure` y su propio `DbContext`. Los cuatro contextos implementados comparten la cadena `ConnectionStrings:AccessControl` (SQL Server). El frontend React vive en `frontend/`.

| Módulo | Estado | Historia |
|---|---|---|
| AccessControl | Completo: registro + activación con código, JWT de 15 min + refresh en cookie, Google, bloqueo, contraseñas reforzadas, consentimientos, baja y descarga de datos, administración y cola de correos | `docs/historial/control-acceso-v2.md` |
| Permissions | Completo: `Pendiente → Aprobada → Aplicada`, `Pendiente → Rechazada`. Sin duplicados vigentes (409 `solicitud_duplicada`) y con la clave validada (nombre con puntos) | P2 |
| HabitTracker (negocio) | Hábitos (`Planificado → Activo ⇄ Pausado → Completado`, 6 endpoints en `api/habits`), perfil en dos niveles, cuestionario de salud cifrado y progreso corporal (`api/profile`, `api/progress`), catálogo de planes y recomendador (`api/plans`), Mis planes con pausas, ajustes, logros y mensajes (`api/my-plans`, `api/today`, `api/progress/summary`), y `plan-del-dia`. **En evolución:** plan v2, Fases 0 a 4 hechas; el perfil ya no guarda plan de interés (`MetaSlug` e `InicioPlan` salieron en la 4b) | `docs/historial/habittracker-v2.md` |
| DocumentManager | Completo para la P4 (RF-DOC-01 a 05). Un punto de extensión (`IGuardaEliminacionDocumento`) deja que el Host vete eliminar un archivo en uso (409 `documento_requiere_permiso`); sin guarda todo sigue igual | `docs/historial/practica-4.md` |
| Notifications | Completo para la P5 (RF-NOT-01 a 13): bandeja interna y correo por la cola, con ganchos opcionales en Permisos y en los logros de HabitTracker. Cola de correos con reclamo atómico, recuperación, máximo de 3 intentos y consulta del Administrador | `docs/historial/practica-5.md` |
| Reports | Completo para la P5 (RF-REP-01 a 05): permisos por estado (Core) y constancia por plan (negocio), con rango obligatorio y alcance por rol. Sin base de datos propia | `docs/historial/practica-5.md` |
| Auditing | Completo para la A4 (RF-AUD-01 a 07): historial inmutable (`RegistrosAuditoria`), consulta `GET api/audit` solo para el Administrador con filtros y paginación, y los eventos de Control de Acceso, Permisos y Documentos por puertos | `docs/bitacora-asignacion-4.md` |
| Frontend | Conectado a Control de Acceso, hábitos, permisos, usuarios, privacidad, perfil (onboarding de 3 toques y Perfil y salud), peso y medidas, el catálogo de planes con sus recomendados (landing, `/metas`, `/planes`) y Mis planes (empezar un plan, «Archivos del plan», Hoy desde `api/today`, `/mis-planes/:id`, celebración semanal y logros del servidor en Progreso), «Mis archivos» (`/documentos`), Configuración › Solicitudes y la P5: Notificaciones (bandeja, globo y preferencia de correo), Cola de correos y Reportes. Solo las métricas del Resumen (`GET /api/admin/metrics`) siguen como contrato propuesto; la auditoría y los mocks se comprobaron contra los controllers reales (20-11-2026) | `docs/historial/frontend.md` |
| CI | `ci-ok` y `seguridad-ok` obligatorios en `develop` y `main` | `docs/historial/ci.md` |

## 2. Tags de entrega (no mover ni reescribir)

| Tag | Commit | Notas |
|---|---|---|
| `asignacion-1` | `0056600` | |
| `practica-1` | `171d0fd` | |
| `asignacion-2` | `6433209` | |
| `practica-2` | `86f7e0a` | Defectos `practica-2-defecto-1..6`, hijos directos |
| `practica-3` | `318daab` | Defectos `practica-3-defecto-1..4` (`485a0a1`, `dcd2bea`, `f1d3335`, `f802c65`), hijos directos |
| `practica-4` | `0403df5` | Contenedor, documentos, API externa y activación con código |

- Un ruleset de GitHub impide actualizar, borrar o mover `practica-*` y `asignacion-*`. Crear tags nuevos sigue permitido.
- Los tags de la P3 se rehicieron el 28-10 (antes de entregar). Quien tenga clones antiguos debe ejecutar `git fetch --tags --force`.
- Cada commit de defecto cambia un solo archivo de producción. La tabla de defectos y pruebas está en `README.md` › `## Defectos — Práctica 2/3`.
- Para entregar en Moodle: URL del repositorio + nombre del tag base.

## 3. Pruebas

- **Backend:** `dotnet test` ejecuta **1544** pruebas (medido el 20-11-2026; entre ellas, 61 del check-in por correo, 5 de la verificación del entorno de producción, 5 de regresión `Hallazgo{N}_*` de la A3 y 42 de la auditoría de la A4) sin API ni BD externa (SQLite en memoria). El desglose por proyecto está en `.claude/skills/contexto-habittracker/referencias/inventario.generado.md`.
- **Frontend:** `cd frontend && npm test` ejecuta **1065** pruebas en 70 archivos (medido el 20-11-2026) sin backend (MSW).
- `tests/Permissions.Api.Tests` es una carpeta sin proyecto (solo `bin/obj`, no versionada).

## 4. Entorno local

- SQL Express `.\SQLEXPRESS`, base `HabitTracker` (desarrollo). Las bases `*_Prueba*` son desechables.
- **User Secrets** de `Host/WebApi`: `JwtSettings:Clave` y `SMTP_HOST/PORT/USER/PASS` (son **reales**). La cadena de conexión está en `appsettings.json` (autenticación de Windows, sin contraseña). El `.env` de la raíz solo lo usa `docker compose`: si cambias la clave de aplicación de Gmail, actualiza los dos.
- **Probar sin tocar la base de desarrollo ni enviar correos:** `ConnectionStrings__AccessControl=Server=.\SQLEXPRESS;Database=<BD_PRUEBA>;Trusted_Connection=True;TrustServerCertificate=True` y `SMTP_HOST=` vacío. Los correos quedan en `CorreosEnCola`.
- **API:** `http://localhost:5246` y Swagger en `/swagger`. Para promover a Administrador: `UPDATE usuarios SET Rol = 2 WHERE Correo = ...` y volver a iniciar sesión.
- **Frontend contra la API local:** `VITE_API_URL=http://localhost:5246 npx vite --port 5174` (el puerto 5174 está en el CORS y en los orígenes de Google).
- **Docker:** `copy .env.example .env` + `docker compose up --build` → `http://localhost:8080`. Con `CORREO_MODO=consola` y `SMTP_HOST` vacío, los correos salen en `docker compose logs webapi`. Las claves de cifrado de los datos de salud van en el volumen `dataprotection-claves`: respáldalo con la base.
- Tras traer cambios del backend, aplica las migraciones pendientes de cada contexto (`dotnet ef migrations list ... --context <Ctx>`). Sin ellas la API responde 500.

## 5. Decisiones vigentes

- **`ValueGeneratedNever`** en toda entidad hija con Id asignado por el dominio (como `RegistroDiario.Id`). Sin esto, EF emite UPDATE en vez de INSERT y la API responde 500.
- **Enums como texto** (`JsonStringEnumConverter`); también se aceptan números.
- **Errores:** `ProblemDetails` con mensaje genérico (RD-08) y una extensión `codigo` estable en Control de Acceso (tabla en el README) y en HabitTracker (`habito_no_encontrado`, `plan_usuario_no_encontrado` y `plan_no_encontrado` 404; `fecha_futura`, `plan_no_apto`, `limite_planes`, `plan_ya_activo`, `transicion_plan_invalida`… 400). Las transiciones prohibidas del hábito siguen siendo un 400 genérico, para no tocar las suites de la P3. `UnauthorizedAccessException` responde 403 con sesión y 401 sin ella.
- **Transiciones en un solo punto:** `TransicionesHabito.EsTransicionValida`, `TransicionesPlanUsuario` y `TransicionesSolicitudPermiso`. Reconfigurar un hábito inicia un ciclo nuevo y conserva el historial.
- **Módulos conectados por puertos:** la interfaz vive en el `Application` de quien la usa y la implementa un adaptador en `Host/WebApi/Adapters/` (`PermisoServiceAdapter`, `IParticipanteDatosUsuario` para la baja y la descarga). Ningún módulo lee tablas de otro.
- **Plan HabitTracker v2** (07-11-2026):
  - Básico y Regular se distinguen por su estructura.
  - Los planes no aptos se muestran y se bloquean (`plan_no_apto`).
  - Cifrado en reposo con Data Protection.
  - Principios P1–P8.
  - El piloto (Fase 6) decide las Fases 7–9.
- **Adherencia (Fase 4a):** `CalculadoraProgreso` es la única definición de racha y cumplimiento y mide por el **día local** del perfil (`CumpleMetaFinal` sigue en UTC, regla de la P3). Un plan con tareas diarias se mide en días y uno solo semanal, en semanas; un día sin registro no corta la racha y dos seguidos sí; las pausas no cuentan. Los ajustes se sugieren, nunca se aplican solos, y los logros no reciben peso ni medidas.
- **Producción (14-11-2026):** los endpoints con sesión tienen límite de peticiones **por persona** (`usuario` 120/min y `subidas` 10/min para documentos; `RateLimit:*`), el limitador corre después de autenticar, `GET /health` (anónimo, sin detalles) comprueba la base de datos y el compose lo usa como healthcheck. Sin `PROXY_CONFIABLE` no se confía en `X-Forwarded-For`.
- **Archivos y permisos (13-11-2026):** la persona controla sus archivos y los elimina cuando quiera; **solo los vinculados a un plan en curso** (Activo o En pausa) piden un permiso **por archivo** (`Documents.DeleteLinked.{id sin guiones}`), que se pide en la propia pantalla y aprueba un Administrador. Quitar el vínculo con el plan en curso pide el mismo permiso (si no, sería la forma de esquivarlo). Los archivos se vinculan a **planes**, no a hábitos (`AdjuntoPlan`, tope de 10 por plan, en cascada con el plan). La regla la compone el Host (`GuardaEliminacionDocumentoAdapter`) y el Manejador de documentos sigue con una sola responsabilidad: no lee archivos ni recomienda; las recomendaciones siguen con el método actual. «Mis permisos» salió del menú: el permiso se pide donde se necesita y el historial vive en Configuración › Solicitudes. Sin fotos del cuerpo en Progreso (P4).

## 6. Deuda técnica conocida

1. Archivos vinculados, decisiones de producto que se dejan así: el permiso `Documents.DeleteLinked.{id}` no se consume (queda Aplicada y un duplicado se rechaza) y el Administrador elimina sin pasar por la guarda.
2. Notificaciones, cola y reportes (P5), detalles por pulir: `POST api/admin/email-queue/process` no distingue «nada que hacer» de «esperando el reintento»; no hay un endpoint de conteo de no leídas (el globo del menú pide la lista cada 60 s); el agregado del Administrador no tiene cohorte mínima y mide por día UTC (el personal, por día local). Un plan cancelado antes de la migración `AgregarFechaCancelacionAlPlan` no tiene fecha y no se mide; la API simulada del frontend (`mocks/handlers/reportes.ts`) todavía excluye los cancelados.
3. Detrás de un proxy inverso, `RemoteIpAddress` es la IP del proxy y los endpoints anónimos (`catalogo`, `login`…) comparten el cupo por IP: se resuelve con `PROXY_CONFIABLE` (`ProxiesConfiables__0`, ver README). Los endpoints con sesión ya se limitan por persona.
4. Sin E2E en Edge (puppeteer-core), sin `claude-security` sobre lo nuevo y sin el «latido del worker» en el healthcheck (no hay worker de recordatorios). *(Fase 6 y Fase 5.)*
5. El límite de 3 planes en curso se comprueba en el servicio: dos peticiones simultáneas podrían pasarlo (solo el mismo plan repetido lo impide la base). `MarcarLogrosVistos` marca todos los logros y una zona horaria que el sistema no conoce cae a UTC sin avisar (al guardar el perfil sí se valida; en Docker falta tzdata, Fase 6).
6. «Leer y, si no existe, insertar» sin tolerar la carrera (dos peticiones simultáneas de una persona nueva darían 500): `PerfilSaludServices` (crear el perfil) y `PerfilSaludRepositories.GuardarAsync` (el cuestionario). Igual la solicitud duplicada de Permisos (`ExisteVigenteAsync` y luego insertar, sin índice único; su repositorio tiene un defecto plantado y no se toca). La preferencia de notificaciones ya lo tolera (Asignación 3).
7. Auditoría (A4), por pulir: no se audita `cambiar-password` (cambio con la contraseña actual) y el cambio y su registro no comparten transacción.
8. Mis planes, detalles del backend por pulir: `celebracionSemana` no tiene marca de «visto» (el frontend lo recuerda en `sessionStorage`) y `ResumenProgresoDto.racha` mezcla días y semanas (cambiarlos cambia el contrato del frontend: con el piloto).

## 7. Flujo de trabajo

- **Commits** en español con IDs RF/RD **de 2 dígitos** (`feat: ... [RF-NEG-02, RD-07]`) y revisión RD-01..RD-12 antes de cada commit (ver `AGENTS.md`).
- **Ramas:** `feature/`, `fix/`, `docs/`, `refactor/` o `chore/` → PR a `develop`. Solo PRs a `main` (`Promover a main — ...`) cuando el plan v2 esté listo y funcional en `develop`.
  - El cuerpo de la PR lleva 4 secciones: Qué cambia, Por qué, Cómo probarlo y Qué NO incluye (skill `flujo-pr`).
  - `develop` y `main` rechazan el push directo. Si se trabajó en `develop` local: `git push origin develop:feature/...` (sin `-u`).
  - Las PR las fusiona el usuario.
- **Versiones fijas (desde el 09-11-2026):** las dependencias no se actualizan solo por ser más nuevas, sino por seguridad o por necesidad. El detalle está en `docs/historial/ci.md` y en la skill.
- La bitácora de cada práctica (`docs/bitacora-*.md`) registra las solicitudes reales al agente, sus errores y las correcciones.
- **Al cerrar cada entrega:** actualiza este archivo (≤120 líneas), mueve el detalle de la fase a `docs/historial/`, regenera el inventario de la skill (`bash scripts/contexto/generar-inventario.sh`) y comprueba con `bash scripts/ci/verificar-contexto.sh`.

## 8. Problemas de entorno conocidos

- **Vitest «failed to find the runner» en Windows:** si la terminal abre `frontend/` con la unidad en minúscula (`c:\Users\…`), Vitest carga `vitest` dos veces y falla en todas las suites con más de un archivo. Ejecuta `npm test` desde una terminal con `C:\…` en mayúscula (PowerShell con `Set-Location 'C:\…'`). En el CI no ocurre.
- **Rutas largas en Windows:** con más de 260 caracteres, SQLite no carga `e_sqlite3` y las pruebas de persistencia fallan. Clona el repo en una ruta corta.
- **`sqlcmd`:** no admite `-W` junto con `-y`, ni `-h` con `-y 0`. Con `-W` se truncan las columnas largas; usa `SUBSTRING(... CHARINDEX(...))`.
- **Git Bash + curl:** los acentos dentro de JSON pueden llegar mal codificados; usa ASCII en los datos de prueba. Con `docker exec` y rutas absolutas, antepón `MSYS_NO_PATHCONV=1`.
- **CRLF:** con `core.autocrlf=true`, `npm run format:check` falla en local, pero en el CI pasa. `.gitattributes` fuerza LF en los `.sh`.
- **PDF de requisitos:** no se renderizan. Usa el resumen de la skill (`referencias/requisitos.md`) o `pdftotext -raw -enc UTF-8`.

## 9. Próximos pasos

- **Práctica 5 entregada (19-11-2026):** tag `practica-5` sobre `main` (35d5ae8); revisión acumulada desde Docker con un clon limpio, 30 de 30 (rúbrica 8/8). No se toca el tag.
- **Asignación 3 corregida (19-11-2026):** 3 hallazgos de la P5 (plan cancelado fuera del reporte, `intentos` del correo enviado, 500 por guardados simultáneos de la preferencia), un commit y una prueba cada uno; detalle y cómo verificarlos en `docs/bitacora-asignacion-3.md`. **Falta:** la PR a `develop` y, cuando se ordene, el tag `asignacion-3`.
- **Asignación 4 construida (19-11-2026):** historial de auditoría (módulo `Auditing`, migraciones `CrearRegistrosAuditoria` y `AgregarOrigenDelCodigoDeRecuperacion`); sección `Auditoría` del README y `docs/bitacora-asignacion-4.md`. **Falta:** la PR a `develop` y, cuando se ordene, el tag `asignacion-4`.
- **Siguiente:** terminar el plan v2 (Fase 5 y siguientes).
- **Fase 5 (correo como canal de acción), mínimo para el piloto:** check-in en un clic (`api/reminders/checkin`, enlace firmado de 36 h, GET sin efectos y POST idempotente) y **recordatorios** (20-11-2026): `RecordatoriosWorker` cada 5 min, opt-in por plan, un correo por plan y día a su hora local (reclamo atómico), cadencia 3/7/21 que solo reduce, botones «Ya lo hice» y baja en un clic (`api/reminders/unsubscribe`). Migración `AgregarUltimoRecordatorioDelPlan`. **Queda para después del piloto:** resumen semanal aparte, horario silencioso, agrupar planes en un correo, «¿retomamos?» tras una pausa y `POST .../test`.
- **Mi actividad (20-11-2026):** `GET /api/me/activity?dias=7|30` (ambos roles, solo lo propio, lectura) y pantalla `/ajustes/actividad` con descarga en CSV y JSON. Nada se borra (RF-AUD-06): es una ventana sobre el historial.
- **Quejas y mejoras (20-11-2026):** `api/feedback` (Estándar envía y ve las suyas, 5 en 24 h) y `api/admin/feedback` (bandeja paginada y estado Nueva, Leída o Resuelta); migración `AgregarSugerencias`; entra en la descarga de datos y en la baja de cuenta. Pantallas: Estándar en Configuración › Quejas y mejoras (`/ajustes/sugerencias`) y Administrador en `/admin/sugerencias`, con globo de nuevas en el menú.
- **Métricas del Resumen (21-11-2026):** `GET api/admin/metrics` real (solo Administrador), con cohorte mínima de 5 para lo que cuenta personas; sin datos de salud ni identificadores. **Decisión:** la consulta de las métricas no se audita (es solo lectura y agregada). **Pantalla (21-11-2026):** el Resumen del Administrador ya usa este contrato real (`features/auditoria/metricas.ts`, `mocks/handlers/metricas.ts`); lo oculto por la cohorte muestra «—» y la razón, no un cero; ya no queda ningún contrato propuesto en el frontend.
- **Latido de los recordatorios (21-11-2026):** `/health` responde `Degraded` (200) si el proceso de recordatorios lleva más de 3 pasadas + 2 min sin completar una; apagado a propósito (`Recordatorios:Habilitado=false`) cuenta como sano. En producción, monitor de palabra clave `Healthy` (`docs/produccion.md`).
- **Verificado contra el sistema real (21-11-2026):** compose de producción con SQL Server, Caddy y claves reales; las 4 migraciones nuevas se aplican; activación con sesión, Mi actividad, quejas y mejoras, métricas, recordatorio y «Ya lo hice» funcionan de punta a punta. **Carga:** 800 usuarios simultáneos, 0 % de errores, con API y base a 1 CPU cada una (el límite real es iniciar sesión: ~4 por segundo y CPU). Detalle, qué falta y opciones de hosting en `docs/informe-produccion.md`; plan del piloto en `docs/piloto.md`; guiones en `scripts/carga/`. **Falta antes de abrir al público:** prueba de navegador con Edge contra el sistema real y un escaneo de seguridad automático.
- **Auditoría de seguridad (21-11-2026), correcciones en curso:** S1 (cuentas y correo) corrige SEG-01 (reserva atómica del intento de recuperación y tope de 5 códigos por cuenta y día), SEG-02 (re-registro de una cuenta pendiente: la reemplaza y activarla ya no abre sesión; purga de pendientes a las 48 h), SEG-03 (IPv6 por /64), SEG-05 (topes de la cola), SEG-11 (72 bytes), SEG-12 (purga de la cola a 14 días), SEG-15, SEG-16 y SEG-19. SEG-04 se acepta (RF-CA-01 exige rechazar el correo duplicado). Migración `LimitarCodigosPorCuenta`. S2 (sesiones y datos): SEG-08 (correo de aviso en cada inicio de sesión de un Administrador), SEG-09 (cuota de 200 MB y 200 archivos por persona, contando los eliminados), SEG-10 (`no-store` en toda respuesta de `/api` sin caché propia) y SEG-17 (el registro pendiente ya no guarda el correo en claro). S3 (infraestructura): SEG-06 (la API de producción entra con `habittracker_app`, solo lectura y escritura; un servicio `migrador` de un solo uso migra con `sa`), SEG-07 (Trivy también sobre la imagen web), SEG-13 (respaldos cifrados con `age` si hay `RESPALDO_CLAVE_PUBLICA`), SEG-14 (contenedores sin privilegios ni capacidades, API de solo lectura, sin `-P` en `sqlcmd`) y SEG-21 (`npm audit` vuelve a bloquear con todas las dependencias). Se aceptan SEG-18 y SEG-20 (`docs/produccion.md`).
- **Plan HabitTracker v2** (`docs/plan-habittracker-v2.md`):
  - Fases 0 a 4 hechas → Fase 5, el corte vertical (siguiente: recordatorios y check-in por correo, `RegistroDiario.Origen` y `ClaveIdempotencia`);
  - Fase 6, producción, métricas y piloto;
  - las Fases 7–9 dependen del informe del piloto.
- **Hosting decidido (20-11-2026): un VPS con Docker Compose.** Listo en el repositorio: `docker-compose.prod.yml`, `Dockerfile.web` y `deploy/Caddyfile` (HTTPS, cabeceras y el SPA), `.env.produccion.example`, `scripts/despliegue/` (respaldar, restaurar y actualizar) y `docs/produccion.md`. Se verificó en local (HTTPS, cookie, puertos cerrados, IP real y una restauración completa tras destruir los volúmenes, con el cuestionario cifrado descifrándose). **Falta, y es tuyo:** contratar el VPS y el dominio, rellenar el `.env` y desplegar siguiendo el runbook. Sin CD (los workflows no se tocan): se actualiza con `actualizar.sh`.
- **Antes de producción** (detalle en la memoria del proyecto): proveedor SMTP real en lugar de Mailpit, claves reales (`JWT_CLAVE`, `DB_PASSWORD`, `ADMIN_CLAVE`), `SWAGGER_HABILITADO=false`, `PROXY_CONFIABLE` con el proxy, respaldo aparte de la base y de `dataprotection-claves`, revertir la excepción de `npm audit` cuando haya parche de `picomatch` (`docs/historial/ci.md`), auditoría, métricas, E2E en Edge y el piloto con consentimiento (Ley 172-13).
