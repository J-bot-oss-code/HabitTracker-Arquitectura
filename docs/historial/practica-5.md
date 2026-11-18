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
