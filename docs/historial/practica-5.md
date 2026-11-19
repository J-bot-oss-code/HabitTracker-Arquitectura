# Práctica 5 — Notificaciones, cola de correos y reportes

> Requisitos: `docs/practica_5/practica-5.md` (RF-NOT-01 a 13, RF-REP-01 a 05). Criterios de aceptación, escritos **antes** del primer commit del motor: `docs/criterios-notificaciones-reportes.md`. Archivo de contexto del agente: `AGENTS.md`.

## Orden de entrega (commits atómicos)

1. PR #114: `AGENTS.md` (propósito, cómo correr y probar, restricciones sobre secretos, tags y defectos) y los criterios. Fusionada antes de cualquier código del motor.
2. PR #115: el `npm audit` del CI bloquea solo producción mientras `picomatch` (GHSA-fc5q-39xc-vjpf) no tenga parche; sin esto fallaban todas las PR.
3. Notificaciones y cola (esta rama): ver abajo.

## Notificaciones (pieza 4)

- **`Modules/Notifications`** (Domain, Application, Infrastructure + `NotificationsDbContext`, migración `CrearNotificaciones`): `Notificacion` (solo su dueña la marca como leída), `PreferenciaNotificaciones` (correo activo por defecto) y `NotificacionService`. Canal interno siempre; correo solo si la preferencia lo permite (RF-NOT-06 y 07). Un fallo al encolar el correo se registra y no pierde la interna.
- **Puertos** (Notifications.Application): `IColaCorreos`, `IDirectorioUsuarios`. Adaptadores del Host: `ColaCorreosAdapter` (sobre `IEmailQueue`), `DirectorioUsuariosAdapter` (sobre `IUsuarioRepository`, con el nuevo `ListarAdministradoresActivosAsync`).
- **Ganchos opcionales** (parámetro del constructor con `null` por defecto, como `IGuardaEliminacionDocumento`, para no tocar las suites de las prácticas): `INotificadorPermisos` en `SolicitudPermisoService` (crear → todos los Administradores; aprobar o rechazar → solo el solicitante, con el motivo) e `INotificadorUsuario` en `LogrosService` (solo cuando `AgregarSiNoExisteAsync` devuelve `true`: un aviso por logro). El aviso va **después** de guardar y un fallo no deshace la operación. Adaptadores: `NotificadorPermisosAdapter` y `NotificadorUsuarioAdapter` (textos que celebran la conducta, sin peso, cuerpo ni culpa; una prueba lo vigila).
- **API:** `GET api/notifications`, `POST api/notifications/{id}/read` (ajena → 403, inexistente → 404 `notificacion_no_encontrada`), `GET/PUT api/notifications/preferences` (`correoActivo` obligatorio). `ParticipanteNotificaciones` entra en la descarga de datos y en la baja de cuenta.

## Cola de correos (RF-NOT-08 a 13, en Control de Acceso)

- El ciclo del worker se extrajo a `ProcesadorColaCorreos` y el envío a `IEnviadorCorreo`/`EnviadorCorreoSmtp`; `EmailSenderWorker` solo lo llama cada 15 s.
- **Reclamo atómico** (`ExecuteUpdateAsync` condicionado al estado): dos pasadas no duplican (RF-NOT-12). **Recuperación** de los `Procesando` de más de `Correo:MinutosParaRecuperar` (10) como un intento fallido. **Espera** `base × 2^intentos` con `Correo:EsperaReintentoSegundos` (60). Máximo **3 intentos** (`CorreoEnCola.MaxIntentos`). `UltimoError` se acota a 500 caracteres.
- **SMTP** (`LeerOpciones`): `SMTP_HOST` y `SMTP_PORT` obligatorios, `SMTP_SSL` (true), `SMTP_USER`/`SMTP_PASS` opcionales pero juntos, `SMTP_FROM`, tiempo máximo de 10 s.
- `IEmailQueue.EncolarCorreoAsync` (correo informativo con la plantilla sin la caja del código; el texto se escapa).
- **Admin:** `GET api/admin/email-queue?estado=` (sin el cuerpo, tope de 200) y `POST api/admin/email-queue/process`; migración `IndiceCorreosPorEstado`.
- **Mailpit** en el compose (`axllent/mailpit:v1.32.0`, interfaz en `:8025`): el `.env.example` apunta a él; `docker compose stop mailpit` provoca el fallo.
- Los endpoints con sesión llevan la política de límite `usuario` (RD-06).

## Verificación

- **Pruebas:** dominio, servicio con dobles, repositorios con SQLite, controllers, adaptadores y el procesador (dos ejecuciones, fallo hasta `Fallido`, recuperación, reclamo atómico).
- **Contra la API real** (SQL Server aislado, ya borrado, y un SMTP simulado que hace de Mailpit): 37 de 37 comprobaciones sobre RF-NOT-01 a 13 (dos Administradores, Estándar sin acceso a la cola, preferencia, SMTP apagado → 201 rápido → `Fallido` con 3 intentos → vuelve a salir al reencender).
- **Lecciones:** (1) `generar-inventario.sh` fallaba con exit 123 cuando `xargs` partía los archivos en dos tandas y una no tenía coincidencias; (2) con el correo por Mailpit, el README y la revisión acumulada tienen que decir dónde leer el código de activación; (3) el SMTP codifica el HTML en quoted-printable: para leer un código en una prueba hay que decodificar el cuerpo.

## Reportes (pieza 5, RF-REP-01 a 05)

- **`Modules/Reports`** (Application, sin base de datos propia): `ReportesService` valida el rango (`desde` y `hasta` obligatorios, `hasta ≥ desde`, máximo 366 días, fechas desde el año 2000) y decide el alcance por el **rol del token**: un Estándar solo obtiene lo suyo, un Administrador lo de todas las personas (RF-REP-04). Pide los datos por puertos (`IFuenteSolicitudes`, `IFuenteConstancia`); los adaptadores del Host (`FuenteSolicitudesAdapter`, `FuenteConstanciaAdapter`) los conectan con Permisos y HabitTracker (RD-03).
- **Core (RF-REP-01):** `GET api/reports/permissions` → solicitudes por estado en el rango. `ISolicitudPermisoReportes` (Permisos) hace el `GROUP BY` del estado en la base de datos (una fila por estado, nunca el listado); el rango es de `FechaCreacion` con el límite superior exclusivo. No se tocó `SolicitudPermisoRepository.cs` (tiene un defecto plantado en su tag): es una clase nueva.
- **Negocio (RF-REP-02 y RF-NEG-08):** `GET api/reports/adherence` responde **«¿Qué tan constante fui en cada plan durante este período?»**: por plan, días con avance, días solo con la versión mínima, días del rango en marcha y porcentaje. `CalculadoraConstancia` (dominio, pura, sin peso ni medidas) cuenta del inicio del plan hasta su fin, hoy o el final del rango y descarta los días de pausa. Personal: por el día local de la persona. Administrador: agregado por plan del catálogo (personas, promedios y porcentaje ponderado), por día UTC y **sin nombres ni correos**; lee con un JOIN de las columnas que usa (`ReporteConstanciaRepository`).
- **Se deja fuera a propósito:** los planes cancelados (no guardan la fecha en que se dejaron) y una cohorte mínima en el agregado (se anota para la Fase 6, con las métricas).
- **Verificación:** 14 pruebas del servicio, 8 de persistencia con SQLite, 9 de la calculadora, 6 de repositorio de permisos, 6 del Host. **Contra la API real:** 20 de 20 comprobaciones (una fila por estado, Estándar solo lo suyo, otro rango da otro resultado, rangos inválidos → 400, sin token → 401, agregado del Administrador sin datos personales).
- **Lección:** `SelectMany` con un `Where` anidado necesita `APPLY` y SQLite no lo traduce; un JOIN explícito funciona en las dos bases.

## Frontend

- **Contrato** (`frontend-api-contract`): tipos y hooks en `features/notificaciones`, `features/cola-correos` y `features/reportes` (rango con zod espejo de `ValidarRango`), y handlers MSW que replican los controllers: avisos al crear y resolver solicitudes, logros con el texto de `NotificadorUsuarioAdapter`, cola con reintentos y tope de 3, reportes con la misma agregación (redondeo de .NET).
- **Pantallas** (`frontend-ui-builder` con `frontend-design`, «la hoja del día»): bandeja `/notificaciones` con globo de sin leer en el menú, Configuración › Notificaciones (interruptor de correo), `/admin/cola-correos` (tabla en escritorio y tarjetas en móvil, «Procesar ahora» que explica el resultado) y `/reportes` (atajos de 7 y 30 días, el 0 % sin culpa, el Administrador con titular neutro y sin datos personales). Sale `coming-soon`.
- **Revisión:** `frontend-design-critic` (1 bloqueante, «hasta3 veces», y 5 importantes: foco al marcar, titular del Administrador, cifras en el Resumen, «recuperados» en lenguaje claro, conservar los datos al cambiar el rango), `frontend-test-writer` (de 993 a 1033 pruebas) y `frontend-reviewer` (los mocks no felicitaban logros ni recortaban textos; referencias colgadas en los agentes).
- **Lección:** una prueba que cuenta «7 días» falla de noche en Santo Domingo, porque el rango es UTC y la constancia se mide por el día local.

## Revisión acumulada como el maestro (18-11-2026)

- Clon limpio de `develop` en una ruta corta, `copy .env.example .env` y `docker compose up --build`: levanta con `db`, `mailpit` y `webapi` en estado *healthy* (el healthcheck de `/health` funciona).
- **30 de 30 comprobaciones** siguiendo el README: los seis frentes (registro con activación cuyo código se lee en Mailpit, sesión y 403 de un Estándar, recuperación de contraseña, permisos con transiciones prohibidas, hábito con la transición prohibida, y un documento que sobrevive a `docker compose down` sin `-v`), RF-NOT-01 a 12 (dos Administradores reciben la solicitud y nadie más; con Mailpit apagado la operación responde 201 al instante y el correo queda `Fallido` con 3 intentos a los 6,9 min; dos pasadas no duplican) y RF-REP-01 a 05. RF-NOT-13: el historial no tiene ninguna clave (`git log -p`) y gitleaks pasa.
- **Encontrado y corregido:** el README decía que `Correo__EsperaReintentoSegundos=0` en el `.env` acortaba la espera, pero el compose no pasaba esa variable al contenedor. Ahora es `CORREO_ESPERA_REINTENTO_SEGUNDOS` (en el `.env.example`, 60 por defecto): con 0, el correo queda `Fallido` en segundos.
- Criterio 1: los criterios (`17e95db`) son anteriores y ancestros del primer commit del motor (`18909b4`, la cola; `d247f68`, el dominio de Notificaciones).
