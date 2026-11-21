# Informe para producción (21-11-2026)

Estado de HabitTracker Nexus antes de publicarlo: qué está alineado, qué se probó contra el sistema real, cuánta gente aguanta, qué falta y dónde alojarlo. **No se desplegó nada.**

## 1. Resumen

| Pregunta | Respuesta |
|---|---|
| ¿Frontend alineado con el backend? | **Sí.** No queda ningún contrato «propuesto»: todas las pantallas llaman a endpoints reales. |
| ¿Funciona contra el sistema real? | **Sí**, con SQL Server, el proxy HTTPS y las claves de cifrado reales (sección 3). |
| ¿Cuánta gente aguanta? | **800 usuarios simultáneos probados, 0 % de errores**, en una configuración de 2 vCPU (sección 5). |
| ¿Se puede alojar gratis? | **No de forma realista con este stack** (SQL Server). Lo más barato: un VPS de unos 5 a 7 USD al mes, o créditos de estudiante unos meses (sección 7). |
| ¿Qué falta? | Dominio, servidor, correo real, secretos y fusionar las PR (sección 6). |

## 2. Alineación frontend ↔ backend

- Se revisaron **todas** las llamadas del frontend contra los controllers reales (rutas, verbos, campos, enums, códigos de estado). El único desajuste real era la pantalla de Auditoría del Administrador (ya corregido); el resto eran detalles de la API simulada.
- **Candado permanente:** `frontend/src/mocks/contratoForma.test.ts` compara la forma de cada respuesta simulada con los DTO reales de C#. Si el backend cambia un campo y el mock no, esa prueba falla. Es la defensa contra el fallo que viste («eventos.map is not a function»: las pruebas pasaban contra una API simulada que no se parecía a la real).
- **Métricas del Resumen:** pasaron de «contrato propuesto» a endpoint real (`GET /api/admin/metrics`). Con ello el inventario ya indica «ninguno: todo está conectado al backend».

## 3. Verificado contra el sistema real

Compose de producción (`docker-compose.prod.yml`) con SQL Server Express, Caddy con HTTPS y las claves de Data Protection reales. Las 4 migraciones nuevas se aplicaron sin problema.

| Funcionalidad | Resultado |
|---|---|
| `/health` | `Healthy` (incluye el latido del proceso de recordatorios) |
| Activar con el código del correo | Abre la sesión (token y cookie Secure/HttpOnly); el código no se reutiliza |
| Dispositivo en Sesiones activas | Se guarda corto, sin el User-Agent completo |
| Mi actividad | Muestra lo propio; `dias` distinto de 7 o 30 da 400; sin sesión da 401 |
| Auditoría | Registra la activación de la cuenta |
| Quejas y mejoras | Estándar envía (201), el texto corto da 400, la sexta del día da `limite_sugerencias`, el Estándar no ve la bandeja (403) |
| Bandeja del Administrador | Ve la queja con tildes, marca leída (200), volver a «Nueva» da 400, una inexistente 404 |
| Métricas | 200 solo al Administrador (403 al Estándar), con cohorte mínima 5 y **sin ningún identificador** |
| Recordatorio por correo | Llega a la hora elegida con «Abrir la app» y enlace de baja; la baja en un clic apaga el recordatorio |
| «Ya lo hice» desde el correo | El GET no tiene efectos, el POST registra el avance (la tarea queda «Hecha»), repetirlo es inofensivo, un enlace manipulado o el de baja dan 400 |

**No verificado (a propósito, por costo):**
- Una prueba de navegador completa con Edge sobre el sistema real. Lo cubren las 1155 pruebas del frontend más la comparación de formas contra los DTO reales, pero no es lo mismo que ver las pantallas en un navegador contra el backend real. Recomendado antes del piloto.
- El inicio con Google real (hace falta un `GOOGLE_CLIENT_ID` verdadero) y el envío con un SMTP real (se usó Mailpit).

## 4. Seguridad

Se revisaron las superficies nuevas, sobre todo las anónimas:

| Superficie | Protecciones |
|---|---|
| Activación (enlace del correo) | El GET no activa nada; código de 6 dígitos, un solo uso, 24 h, 5 intentos y límite por IP. Tras activar, la página redirige solo a una URL http(s) configurada (`App:FrontendUrl`), nunca a otra. |
| «Ya lo hice» y baja del correo | Token firmado y con caducidad (36 h y 60 días); el GET no tiene efectos; el POST es idempotente; CSP sin scripts; límite por IP; un token de un propósito no sirve para el otro. |
| Correos | Los botones solo aceptan URL http(s); el texto se escapa; el correo nunca lleva datos de salud (hay una prueba). |
| Quejas y mejoras | Se muestran siempre como texto; tope de 5 en 24 h; texto de 10 a 2000 caracteres. |
| Métricas | Solo Administrador, sin parámetros; lo que cuenta personas se oculta con menos de 5. |

**Pendiente (no es un fallo conocido, es lo que falta por mirar):**
- Un escaneo de seguridad automático del código completo (`claude-security`): no se ejecutó por costo. Recomendado antes de abrir al público.
- Las claves de cifrado (Data Protection) viven en un volumen respaldado, sin cifrar con un certificado. Aceptado para el piloto (`docs/produccion.md`).
- La excepción de `npm audit` por `picomatch` sigue vigente hasta que haya parche (`docs/historial/ci.md`).
- Un detalle de la regla del límite diario de quejas: contar y luego insertar deja un hueco; dos envíos simultáneos podrían dejar 6 en vez de 5. Es un tope contra abuso, no un dato que se muestre (queda anotado en el código).

## 5. Cuánta gente aguanta

**Cómo se midió:** k6 contra la API real, con la API limitada a 1 CPU y SQL Server a 1 CPU (como un VPS de 2 vCPU). Cada usuario simulado inicia sesión una vez y luego consulta su día, su progreso y sus avisos con pausas de 3 a 8 segundos, y guarda algo de vez en cuando. Entran a 1,5 personas por segundo y la latencia se mide cuando ya están todos conectados. Es una carga **más intensa que la real**: una persona de verdad hace una acción cada minuto, no cada 5 segundos.

| Usuarios simultáneos | Errores | Leer (mediana / p95) | Escribir (mediana / p95) | CPU API | CPU base | Memoria (API / base) |
|---|---|---|---|---|---|---|
| 200 | 0 % | 4,2 ms / 6,4 ms | 4,9 ms / 7,4 ms | 8 % | 10 % | 237 MiB / 826 MiB |
| 400 | 0 % | 4,0 ms / 6,2 ms | 4,6 ms / 7,1 ms | 17 % | 10 % | 202 MiB / 829 MiB |
| **800** | **0 %** | **3,7 ms / 7,7 ms** | **4,3 ms / 7,5 ms** | **26 %** | **26 %** | **218 MiB / 832 MiB** |

Con 800 usuarios simultáneos sobra más del 70 % de CPU. **Estimación sin medir** (extrapolando de forma lineal): la configuración llegaría a unos 2000 o 3000 usuarios simultáneos antes de saturar la CPU. Como cada persona real hace mucho menos, eso equivale a muchos miles de personas registradas.

**El verdadero límite es iniciar sesión, no usar la app.** El hash seguro de contraseñas cuesta unos 0,23 s de CPU por inicio de sesión. Con 1 CPU caben unos 4 inicios de sesión por segundo. En una primera prueba, donde 200 y 400 usuarios entraban casi a la vez (un pico irreal), la API se saturó (hasta 84 % de errores con 400). En la vida real las sesiones se distribuyen en el tiempo (y el refresco usa la cookie, sin repetir el hash), y el límite de 10 inicios por minuto por IP protege contra abusos.

**Limitaciones de la medición** (por eso la cifra es una guía y no una garantía):
- Se usaron 5 cuentas compartidas, con poca información cada una; con meses de uso las consultas leerán más datos.
- Se llamó a la API directamente, sin pasar por Caddy, y k6 corría en la misma máquina (compite por CPU, así que el resultado es conservador).
- SQL Server Express limita la memoria útil a 1,4 GB de caché y la base a 10 GB.

**Para el piloto de 10 a 20 personas la capacidad sobra por un factor de más de 40.**

## 6. Qué falta para desplegar

Lo que necesitas tú (el runbook paso a paso está en `docs/produccion.md`):
1. **Un servidor** con Docker y al menos **2 GB de RAM** (SQL Server + API usan ~1,1 GB; con 4 GB hay holgura).
2. **Un dominio** apuntando al servidor (Caddy saca el certificado HTTPS solo).
3. **Correo real** (`SMTP_*`; para el piloto sirve el de Gmail, ~500 correos al día).
4. **Secretos nuevos** en el `.env` del servidor (`JWT_CLAVE`, `DB_PASSWORD`, `ADMIN_CLAVE`); nunca los de prueba.
5. **Un monitor externo** (UptimeRobot o similar) por **palabra clave `Healthy`** sobre `/health`, para enterarte también si el proceso de recordatorios se detiene.
6. **Respaldos programados** con `scripts/despliegue/respaldar.sh` (base y claves en carpetas distintas).
7. Opcional: `GOOGLE_CLIENT_ID` para «Continuar con Google».

Lo que falta de mi lado / del repositorio:
- **Fusionar las PR en orden.** La #128 está abierta con el CI en verde y todo lo demás está apilado encima de ella (14 ramas). Hay que abrir una PR por rama y fusionarlas en cadena.
- Antes de abrir al público: la prueba de navegador con Edge y el escaneo de seguridad de la sección 4.

## 7. Dónde alojarlo

**Realidad:** el stack usa **SQL Server**, que necesita unos 2 GB de RAM y, hasta donde sé, **no corre en procesadores ARM** (verifícalo antes de apostar por una máquina ARM). Eso descarta casi todos los planes gratuitos.

| Opción | Costo | Encaja | Comentario |
|---|---|---|---|
| **VPS pequeño** (Hetzner, DigitalOcean, etc.) con 2 a 4 GB | ~5 a 7 USD/mes | **Sí, es lo recomendado** | Es lo que prepara `docs/produccion.md`. Los precios de Hetzner subieron en junio de 2026 y hay restricciones de disponibilidad: [consulta su página de ajuste de precios](https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/) y la consola antes de decidir. |
| **Azure for Students** | $100 de crédito (verifica la vigencia en tu región) | Sí, unos meses | Las VM gratuitas (B1s, etc.) tienen poca RAM; una máquina con 2 GB y más consume el crédito. [Oferta](https://azure.microsoft.com/en-us/free/students) y [preguntas frecuentes](https://learn.microsoft.com/en-us/azure/education-hub/faq). Hay que comprobarlo con la calculadora de precios. |
| **Oracle Cloud Always Free** | Gratis | **No** con SQL Server | Las dos VM AMD gratuitas tienen 1 GB de RAM (insuficiente). Las ARM se recortaron a 2 OCPU y 12 GB según [lo reportado](https://www.linuxtoday.com/blog/oracle-quietly-cuts-free-tier-ampere-a1-resources-in-half/); la [documentación de Oracle](https://docs.oracle.com/iaas/Content/FreeTier/resourceref.htm) no es consistente. |
| **Fly.io, Render, Railway** | Sin plan gratuito real | **No** | Fly.io ya no ofrece plan gratuito a cuentas nuevas; Render duerme los contenedores y no maneja Compose; ninguno ofrece SQL Server gratis. Comparativa: [Render](https://render.com/articles/platforms-with-a-real-free-tier-for-developers-in-2026). |
| **Cambiar a PostgreSQL** | Abre opciones gratuitas | Requiere trabajo | Habría que cambiar el proveedor de EF Core y regenerar las migraciones (hoy son de SQL Server). Con ello cabría en la capa ARM gratuita de Oracle o en una base Postgres gratuita. Es una decisión de varios días de trabajo, no hay que hacerla para el piloto. |

**Recomendación:** para el piloto, un VPS de 2 a 4 GB (~5 a 7 USD/mes) o los créditos de Azure for Students; si el costo importa a largo plazo, evaluar PostgreSQL después del piloto, con el informe en la mano.

*Los precios y condiciones de proveedores cambian con frecuencia: se consultaron el 21-11-2026 y conviene confirmarlos en cada sitio antes de pagar.*

## 8. Cómo repetir las mediciones

- Verificación contra el sistema real: levantar `docker-compose.prod.yml` con `DOMINIO=localhost` y un Mailpit en la red (ver `docs/produccion.md`) y recorrer los casos de la sección 3.
- Carga: `scripts/carga/carga.js` (k6, imagen `grafana/k6:0.57.0`) y `scripts/carga/carga.override.yml` (1 CPU por servicio y límites de peticiones subidos **solo para la prueba**). Variables: `BASE`, `USUARIOS`, `CLAVE`, `VUS`, `RITMO`, `ESTABLE`. Nunca subir esos límites en producción.
