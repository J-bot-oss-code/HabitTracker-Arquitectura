# Producción en un VPS (piloto)

Guía para desplegar HabitTracker Nexus en **un servidor con Docker Compose**: un proxy con HTTPS (Caddy) que sirve el frontend y reenvía `/api` a la API, la API y SQL Server. Es **una sola réplica**, pensada para un piloto de 10 a 20 personas. Los archivos: `docker-compose.prod.yml`, `Dockerfile.web`, `deploy/Caddyfile`, `.env.produccion.example` y `scripts/despliegue/`.

## 1. Qué necesitas
- **Un VPS** con Ubuntu 22.04 o 24.04, **2 vCPU y 4 GB de RAM como mínimo** (SQL Server Express usa hasta 1,5 GB) y 20 GB de disco.
- **Un dominio** con un registro `A` que apunte a la IP del servidor, antes del primer arranque (Let's Encrypt lo comprueba).
- **Un correo** para enviar: Gmail con «contraseña de aplicación» alcanza para el piloto (~500 correos al día).

## 2. Preparar el servidor (una vez)
```bash
# Docker y Compose v2 (https://docs.docker.com/engine/install/ubuntu/), git, y el firewall:
sudo ufw allow 22/tcp && sudo ufw allow 80/tcp && sudo ufw allow 443/tcp && sudo ufw enable
git clone https://github.com/J-bot-oss-code/HabitTracker-Arquitectura.git && cd HabitTracker-Arquitectura
cp .env.produccion.example .env && chmod 600 .env   # rellénalo: ver abajo
```
Solo se abren el 22, el 80 y el 443: la API y la base de datos **no** publican puertos.

**Rellenar `.env`** (los valores nunca se suben al repositorio):
- `DOMINIO`: el dominio, sin `https://`.
- `DB_PASSWORD`, `DB_APP_PASSWORD` y `ADMIN_CLAVE`: contraseñas largas (12 o más caracteres, con letras y números, 72 bytes como mucho). `JWT_CLAVE`: 32 o más caracteres aleatorios (`openssl rand -base64 48`). **Nunca los de `.env.example`**: la API no arranca con ellos.
- `DB_PASSWORD` es la de `sa` y solo la usan la base y el **migrador** (un contenedor de un solo uso que aplica las migraciones y crea el Administrador). La API entra con el usuario `habittracker_app` y `DB_APP_PASSWORD`, que solo lee y escribe datos: no puede borrar tablas ni administrar el servidor.
- Tras el **primer arranque**, entra como Administrador, cambia su contraseña desde la app y **borra `ADMIN_CLAVE` del `.env`** (solo sirve para crear la cuenta la primera vez).
- `ADMIN_CORREO`: el correo del Administrador inicial. **Cambia su contraseña al entrar la primera vez.**
- `SMTP_*`: para Gmail, `SMTP_HOST=smtp.gmail.com`, `SMTP_PORT=587`, `SMTP_SSL=true`, tu usuario y la contraseña de aplicación.
- `GOOGLE_CLIENT_ID` (opcional): añade `https://<DOMINIO>` a los orígenes autorizados en Google Cloud.

## 3. Primer arranque
```bash
docker compose -f docker-compose.prod.yml up -d --build
docker compose -f docker-compose.prod.yml ps -a     # db, webapi y web en "healthy" o "running"; migrador en "Exited (0)"
curl -fsS https://<DOMINIO>/health                  # "Healthy"
```
La primera vez tarda unos minutos (construye el frontend y la API; el migrador migra la base, crea el usuario de la API y el Administrador, y sale). Abre `https://<DOMINIO>` y entra con `ADMIN_CORREO`.

## 4. Respaldos
Tres cosas se respaldan: la **base de datos**, los **documentos** y las **claves de cifrado** de los datos de salud.
```bash
sudo scripts/despliegue/respaldar.sh
```
- Deja los respaldos en `/var/respaldos/habittracker` (cambia el destino con `DESTINO_RESPALDOS`) y borra los de más de 14 días.
- **Las claves van a otra carpeta** (`claves/`) distinta de la base (`datos/`): un respaldo con las dos juntas anula el cifrado, y sin las claves los cuestionarios de salud **no se pueden leer**.
- **Copia las dos carpetas fuera del servidor y a sitios distintos** (otro disco, otro proveedor, tu equipo). Un respaldo que solo está en el mismo servidor no sirve si el servidor se pierde.
- **Cifrarlos** (recomendado antes de copiarlos fuera): instala `age` (`apt install age`), crea un par de claves en **tu equipo** (`age-keygen -o respaldo.key`) y pon la pública (`age1…`) en `RESPALDO_CLAVE_PUBLICA` al llamar al script. Cada archivo queda como `….age` y solo se descifra con `respaldo.key`, que **no** se guarda en el servidor. Sin la variable, el script avisa y deja los archivos sin cifrar.
- **Programarlo** (cada noche a las 3:00, hora del servidor), con `sudo crontab -e`:
  ```
  0 3 * * * RESPALDO_CLAVE_PUBLICA=age1... /ruta/a/HabitTracker-Arquitectura/scripts/despliegue/respaldar.sh >> /var/log/habittracker-respaldo.log 2>&1
  ```
- **Probar la restauración** antes de necesitarla: en un servidor de prueba, `sudo scripts/despliegue/restaurar.sh <HabitTracker_….bak> <documentos_….tar.gz> <claves_….tar.gz>` (si están cifrados, con `RESPALDO_IDENTIDAD=/ruta/a/respaldo.key`), y comprueba que puedes entrar y abrir un documento.

## 5. Actualizar
```bash
sudo scripts/despliegue/actualizar.sh
```
Respalda, trae el código (`git pull`), reconstruye, arranca y espera a que la API esté sana. Las migraciones las aplica el migrador antes de arrancar la API. Si algo falla, el script indica cómo volver a la versión anterior y, si la migración ya cambió la base, cómo restaurar el respaldo previo.

## 6. Vigilar
- **Disponibilidad:** un monitor externo (UptimeRobot o similar) sobre `https://<DOMINIO>/health`, con aviso por correo. Configúralo como monitor de **palabra clave** que exige el texto `Healthy`: `/health` responde `Degraded` (también con 200) si el proceso de recordatorios dejó de completar pasadas, y `Unhealthy` (503) si no hay base de datos. Un monitor que solo mira el código 200 no vería el primer caso.
- **Logs:** `docker compose -f docker-compose.prod.yml logs -f webapi` (Docker los rota: 5 archivos de 10 MB por servicio).
- **Correo:** el Administrador ve la cola en `/admin/cola-correos`; un correo `Fallido` con su último error suele ser la contraseña de aplicación o el puerto.
- **Disco:** `df -h` de vez en cuando; los respaldos y los documentos crecen.

## 7. Lo que se acepta para el piloto
- **Una sola réplica** (la semilla del Administrador y las migraciones al arrancar así lo suponen).
- **Las claves de cifrado se guardan sin proteger** dentro de su volumen (`ProtectKeysWithCertificate` queda para el lanzamiento). Por eso se respaldan aparte de la base.
- **Gmail como proveedor de correo.** Para el lanzamiento hace falta un proveedor transaccional con SPF y DKIM; solo cambian las variables `SMTP_*`.
- **Sin despliegue continuo:** se actualiza a mano con `actualizar.sh`. La imagen se construye en el propio servidor.
- **Datos de personas reales:** necesitan consentimiento informado (Ley 172-13) y un acuerdo sobre su borrado al terminar el piloto (ver `docs/piloto.md`, cuando exista).

- **Contenedores endurecidos:** ningún servicio puede ganar privilegios (`no-new-privileges`, salvo el proxy `web`: Caddy necesita su capacidad de archivo para abrir el 80 y el 443, y se le quitan todas las demás); la API corre sin capacidades, con el sistema de archivos de solo lectura (escribe solo en sus volúmenes y en `/tmp`) y con un tope de procesos. Las imágenes van por versión exacta, no por digest.
- **`style-src 'unsafe-inline'` en la CSP (SEG-18):** lo usan los estilos dinámicos de la interfaz y el botón de Google; quitarlo obliga a rehacer componentes por un riesgo bajo, porque los scripts sí están acotados.

## 8. Si algo falla
| Síntoma | Qué mirar |
|---|---|
| Caddy no consigue el certificado | El DNS aún no apunta al servidor, o el 80 y el 443 están cerrados. `docker compose -f docker-compose.prod.yml logs web` |
| La API se detiene al arrancar | Falta una variable obligatoria (el mensaje lo dice) o la zona horaria `America/Santo_Domingo` no se resuelve. `logs webapi` |
| `502` en `/api` | La API no está sana todavía: espera, o `ps` y `logs webapi`. |
| No llegan correos | `SMTP_*` mal, o la contraseña de aplicación de Gmail. Revisa la cola del Administrador. |
| El límite de peticiones bloquea a todos | La API no ve la IP real: el proxy debe tener la IP `172.30.0.10` (red del compose) y `ProxiesConfiables__0` igual. |
