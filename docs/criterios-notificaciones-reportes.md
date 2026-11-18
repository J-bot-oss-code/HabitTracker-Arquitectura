# Criterios de aceptación — Notificaciones, cola de correos y reportes (Práctica 5)

Escritos **antes** de delegar la construcción al agente. Cada criterio es una condición que cualquiera puede comprobar sin
preguntarme: tiene un «dado / cuando / entonces» y **cómo se comprueba** (una llamada a la API o una prueba automática).
Todas las operaciones piden sesión válida (`Authorization: Bearer <token>`); sin sesión responden 401 (RD-06).

**Cuentas de ejemplo** (el Administrador se crea al arrancar con `ADMIN_CORREO` y `ADMIN_CLAVE`; el resto se registra):
`admin` y `admin2` son Administradores activos; `ana` y `beto` son Estándar activos.
**Correo de prueba:** el contenedor incluye Mailpit; los correos enviados se ven en `http://localhost:8025`.

Rutas que cubren estos criterios: `api/notifications`, `api/notifications/{id}/read`, `api/notifications/preferences`,
`api/admin/email-queue`, `api/admin/email-queue/process`, `api/reports/permissions` y `api/reports/adherence`.

## 1. Notificaciones (pieza 4)

### RF-NOT-01 — Crear una solicitud notifica a los Administradores
- **Dado** que `admin` y `admin2` están activos y `ana` es Estándar,
- **cuando** `ana` crea una solicitud de permiso (`POST api/permissions`),
- **entonces** `admin` y `admin2` tienen cada uno 1 notificación nueva sin leer de tipo `SolicitudCreada`, y ni `ana` ni `beto` tienen ninguna.
- **Se comprueba:** `GET api/notifications?soloNoLeidas=true` con el token de cada uno. Un Administrador desactivado no recibe nada.

### RF-NOT-02 — Resolver una solicitud notifica al solicitante
- **Dado** una solicitud pendiente de `ana`,
- **cuando** `admin` la aprueba (`POST api/permissions/{id}/approve`), **entonces** `ana` recibe una notificación `SolicitudResuelta` cuyo texto dice que fue **aprobada**.
- **Cuando** `admin` la rechaza con motivo «Falta justificación» (`POST .../reject`), **entonces** `ana` recibe una notificación que dice **rechazada** y contiene el motivo.
- **Se comprueba:** `GET api/notifications` con el token de `ana`; `beto` y los demás Administradores no reciben la de resolución.

### RF-NOT-03 — Un evento del módulo de negocio notifica
- **Dado** que `ana` no ha obtenido ningún logro,
- **cuando** registra su primer avance de un hábito (`POST api/habits/{id}/progress`) y consulta Hoy (`GET api/today`),
- **entonces** tiene una notificación `LogroObtenido` con el nombre del logro («Primer avance»); si consulta Hoy otra vez, **no** se crea una segunda.
- **Se comprueba:** `GET api/notifications` antes y después. Los pasos exactos están en el README, sección `Notificaciones`. El texto no contiene datos de salud.

### RF-NOT-04 — Consultar solo las propias, con leídas y no leídas
- **Dado** que `ana` y `beto` tienen notificaciones distintas,
- **cuando** `ana` pide `GET api/notifications`, **entonces** ve solo las suyas, cada una con `leida` verdadero o falso; con `?soloNoLeidas=true` solo las no leídas.
- **Se comprueba:** ninguna respuesta de `ana` incluye un id de `beto`. Sin token → 401.

### RF-NOT-05 — Marcar como leída solo la propia
- **Cuando** `ana` hace `POST api/notifications/{id}/read` sobre una suya, **entonces** responde 204 y deja de salir en `soloNoLeidas=true`.
- **Cuando** `ana` lo hace sobre una de `beto`, **entonces** se rechaza (403) y la de `beto` sigue sin leer.
- **Se comprueba:** `GET api/notifications` de `beto` después del intento.

### RF-NOT-06 — Dos canales: interno y correo
- **Dado** que `ana` tiene el correo activado (valor por defecto),
- **cuando** se genera una notificación para ella, **entonces** aparece en `GET api/notifications` **y** hay un correo para su dirección en la cola (`GET api/admin/email-queue`) que pasa a `Enviado` y se ve en Mailpit.
- **Se comprueba:** Mailpit muestra 1 correo para `ana` con el asunto de la notificación.

### RF-NOT-07 — Desactivar el correo sin perder la notificación interna
- **Dado** que `ana` desactiva el correo (`PUT api/notifications/preferences` con `{ "correoActivo": false }`),
- **cuando** se genera una notificación para ella, **entonces** aparece en `GET api/notifications` y **no** se encola ningún correo nuevo para su dirección.
- **Se comprueba:** `GET api/admin/email-queue` no suma correos para `ana`; `GET api/notifications/preferences` devuelve `correoActivo: false`. Al volver a activarlo, las siguientes sí se encolan.

## 2. Cola de correos

### RF-NOT-08 — Los correos se encolan; la operación no espera al servidor de correo
- **Dado** que el servidor de correo está apagado (`docker compose stop mailpit`),
- **cuando** `ana` crea una solicitud de permiso, **entonces** la API responde 201 sin esperar al envío y el correo queda `Pendiente` en la cola.
- **Se comprueba:** respuesta 201 y `GET api/admin/email-queue?estado=Pendiente` con el correo.

### RF-NOT-09 — Un proceso independiente envía los pendientes
- **Dado** un correo `Pendiente` y el servidor de correo encendido,
- **cuando** corre el procesador (el servicio en segundo plano cada 15 s, o `POST api/admin/email-queue/process` como Administrador) **sin que nadie repita la operación que lo creó**,
- **entonces** el correo pasa a `Enviado`, con `fechaEnvio`, y llega a Mailpit.

### RF-NOT-10 — Reintentos limitados y luego fallido
- **Dado** que el servidor de correo está apagado y `Correo__EsperaReintentoSegundos=0` (para no esperar entre intentos en la prueba),
- **cuando** se ejecuta el procesador 4 veces seguidas, **entonces** el correo tiene `intentos = 3` (el máximo, declarado en el README), estado `Fallido` y `ultimoError` no vacío; las ejecuciones siguientes no lo reintentan ni lo borran.
- **Se comprueba:** `GET api/admin/email-queue?estado=Fallido`.

### RF-NOT-11 — Estado de cada correo, consultable por el Administrador
- **Cuando** `admin` consulta `GET api/admin/email-queue`, **entonces** cada correo trae destinatario, asunto, estado (`Pendiente`, `Enviado` o `Fallido`), intentos, fecha de creación, fecha del último intento, fecha de envío y último error; **no** trae el cuerpo del correo (puede incluir códigos de activación).
- **Cuando** `ana` (Estándar) hace la misma consulta o `POST .../process`, **entonces** se rechaza con 403.

### RF-NOT-12 — Un correo enviado no se reenvía
- **Dado** un correo `Pendiente`,
- **cuando** se ejecuta el procesador dos veces seguidas, **entonces** Mailpit recibe **un solo** correo y la segunda ejecución informa `enviados: 0`.
- **También:** un correo que quedó en `Procesando` por una caída (más de 10 minutos) vuelve a considerarse y se envía una sola vez. Se comprueba con la prueba automática del procesador (SQLite en memoria).

### RF-NOT-13 — Las credenciales del correo salen de variables de entorno
- **Entonces** el servidor de correo se configura solo con `SMTP_HOST`, `SMTP_PORT`, `SMTP_SSL`, `SMTP_USER`, `SMTP_PASS` y `SMTP_FROM` (variables de entorno); `.env.example` las lista **sin valores secretos**.
- **Se comprueba:** `git log -p | grep -i "SMTP_PASS="` no muestra ninguna clave real, y el escaneo de secretos del CI (gitleaks) pasa sobre todo el historial.

## 3. Reportes con agregación (pieza 5)

Ambos reportes exigen `desde` y `hasta` (fechas `AAAA-MM-DD`, UTC, ambas inclusivas, rango máximo de 366 días). Sin ellas o con `desde > hasta` → 400.

### RF-REP-01 — Reporte del Core: solicitudes de permiso por estado y período
- **Dado** 6 solicitudes creadas en el rango (2 pendientes, 2 aplicadas, 2 rechazadas),
- **cuando** `admin` consulta `GET api/reports/permissions?desde=...&hasta=...`,
- **entonces** recibe **una fila por estado** con su cantidad (por ejemplo `Pendiente: 2`, `Aplicada: 2`, `Rechazada: 2`), no el listado de solicitudes.

### RF-REP-02 — Reporte del negocio que responde una pregunta real
- **Pregunta:** «¿Qué tan constante fui en cada plan durante este período?».
- **Cuando** `ana` consulta `GET api/reports/adherence?desde=...&hasta=...`, **entonces** recibe una fila por plan con: días con avance, días en que solo hizo la versión mínima, días del rango en que el plan estuvo en curso y el porcentaje de constancia. Los días se miden por el **día local** de `ana`.

### RF-REP-03 — Agregación real
- **Dado** que `ana` registró 10 avances en 2 planes,
- **entonces** el reporte del negocio tiene **2 filas** (menos que los 10 registros), y el de permisos tiene como máximo 4 filas (una por estado) aunque haya más solicitudes.
- **Se comprueba:** contar filas de la respuesta frente a los registros creados. La agrupación la hace la base de datos o una función propia del módulo (`ReportesService`), no la interfaz.

### RF-REP-04 — Los reportes respetan el rol
- **Cuando** `ana` (Estándar) consulta `api/reports/permissions`, **entonces** los totales cuentan **solo sus** solicitudes (la suma coincide con las suyas, no con las de `beto`); `admin` ve las de todos.
- **Cuando** `ana` consulta `api/reports/adherence`, **entonces** ve solo sus planes; **cuando** lo consulta `admin`, ve el agregado por plan del catálogo (cuántas personas y promedio de días con avance) **sin nombres ni correos**.
- **Se comprueba:** crear datos de `ana` y de `beto` y comparar los totales de cada token.

### RF-REP-05 — Filtro por rango de fechas
- **Dado** los mismos datos, con solicitudes creadas en dos semanas distintas,
- **cuando** se consulta el reporte con el rango de la primera semana y luego con el de la segunda, **entonces** los resultados son **distintos**; un rango sin datos devuelve cero filas (no un error).
- **Se comprueba:** las dos respuestas difieren en las cantidades de al menos un estado (o un plan).

## 4. Revisión acumulada (nada de lo anterior se rompe)
Desde un clon limpio, con `copy .env.example .env` y `docker compose up --build`:
1. registro con activación por correo, saliendo por la cola (Mailpit);
2. inicio y cierre de sesión, y rechazo (403) a un Estándar que invoca una operación de Administrador;
3. recuperación de contraseña;
4. permisos: crear, aprobar, rechazar y rechazo de transiciones fuera de la tabla;
5. módulo de negocio: una operación válida funciona y la transición prohibida de la máquina de estados se rechaza (400);
6. un documento subido sobrevive a `docker compose down` (sin `-v`) y a volver a levantar.
- **Se comprueba:** con `dotnet test` en verde (nada previo falla) y siguiendo el README al pie de la letra.
