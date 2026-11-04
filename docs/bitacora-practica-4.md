# Bitácora — Práctica 4: Contenedor, Manejador de documentos y API externa

## Sesión de trabajo

### Solicitud 1: Plan de acción y cierre de la rama anterior

**Prompt del usuario:**
> vamos a implementar la practica numero 4, realiza un plan de accion siguiendo las pautas: plan de implementacion y al final las pruebas realizando un test a ver si cumple perfectamente con la rubrica del maestro y luego actuar como el maestro y realizando el como se revisa exactamente. no olvides realizar commits atomicos y trabajar en la rama indicada (nueva rama), no olvides explicarme todo el codigo que vallas implenetando y en el plan explicame como lo vas a realizar para yo aprender y no ser simplemente un vide coding. antes de iniciar: antes estabamos trabajando en la rama de feuture/frontend-nexus, guerda todos los cambios y subelos al repo y luego realiza la pr contra develop y tu mismo apruebala contra develop, luego a partir de hay inicia a trabajar al final subimos todo a main.

**Respuesta del agente:**
Leyó la especificación, el estado del proyecto y el backend; propuso un plan de 12 commits atómicos y preguntó tres decisiones (API de clima OpenWeatherMap, correos en modo consola, solo API + Swagger en el contenedor). Verificó 106 pruebas del backend y 452 del frontend, subió `feature/frontend-nexus`, abrió la PR #39 hacia `develop` con el cuerpo de cuatro secciones y la fusionó a petición del usuario. Creó `feature/practica-4-documentos-contenedor` desde `develop`.

**Evidencia:** PR #39 fusionada; `dotnet test` → 106 pasan; `npm test` → 452 pasan.

### Solicitud 2: Manejador de documentos (dominio, aplicación, infraestructura, API)

**Respuesta del agente:**
Implementó el módulo en cuatro commits con el patrón de Permissions: `Documento` con borrado lógico y regla de acceso en un solo punto (`bf9513b`), `DocumentoService` con validación y autorización centralizada (`69a3f8f`), persistencia y almacenamiento en disco (`f4a694c`) y los endpoints con el mapeo de errores en el middleware (`6bb858c`). Antes de cada commit se pasó la revisión RD-01..12.

**Errores del agente detectados:**
1. *Confianza en el cliente (RD-07).* El primer servicio guardaba el tamaño y el tipo MIME que **declaraba el cliente**, lo que permitía subir más de 10 MB declarando 1 byte y servir un `.txt` como HTML (XSS almacenado). Lo detectó el revisor.
2. *`Path.GetFileName` en Linux.* No trata `\` como separador, y el contenedor corre en Linux; una prueba habría fallado fuera de Windows.
3. *Raíz de almacenamiento con barra final* (`/app/data/documentos/`) rompía todas las rutas; y la ruta por defecto estaba dentro de `bin/`, que un `clean` borra.
4. *Filtro de excepción enrevesado* al limpiar archivos parciales: habría podido borrar un archivo existente. Se simplificó con una bandera `creado`.

**Corrección:** el servicio mide los bytes reales con tope de 10 MB, deriva el MIME de una lista blanca de extensiones, normaliza `\`, el almacenamiento recorta la barra final y rechaza `/` y `\`, y la ruta por defecto sale de `bin/`.

**Evidencia:** 19 + 37 + 19 + 22 pruebas nuevas en los proyectos de documentos.

### Solicitud 3: Arranque sin pasos manuales y correos en consola

**Respuesta del agente:**
`InicializacionBaseDeDatos` aplica las migraciones de los cuatro módulos y siembra una Meta de demostración y el Administrador inicial desde variables de entorno (`9df8be0`). El `EmailSenderWorker` escribe los correos en el log con `CORREO_MODO=consola` (`dc9d9ab`).

**Errores del agente detectados:**
- Los reintentos capturaban cualquier excepción, así que una migración rota tardaba un minuto en fallar. Ahora solo se reintentan errores de conexión.
- Se aceptaba una clave de Administrador de cualquier longitud; ahora exige 12 caracteres y detiene el arranque si no los cumple.
- *Usabilidad del modo consola.* El cuerpo del correo es HTML de muchas líneas, y filtrar el log por «CORREO» no mostraba el enlace. Se encontró al preparar el README y se corrigió con un resumen de una línea (`0f4b35e`).

### Solicitud 4: API externa de clima

**Respuesta del agente:**
`RecomendadorClima` (dominio puro) y `PlanDelDiaService` (`302f93d`); `OpenWeatherServicioClima` con `HttpClient` tipado y `GET /api/habits/plan-del-dia` (`2f76376`). Se creó `PlanDelDiaService` aparte para no tocar el constructor de `HabitoService` ni sus pruebas.

**Decisión de diseño:** como la clave viaja en la URL, el cliente nunca registra ni excepciones de red ni URLs; se quitó el logging automático de `HttpClient`, se desactivaron las redirecciones y se acotó el tamaño de la respuesta.

### Solicitud 5: Contenedor, README y simulación del maestro

**Respuesta del agente:**
`Dockerfile` de dos etapas sin root, `.dockerignore`, `docker-compose.yml` con dos volúmenes con nombre y `.env.example` (`994d6a4`); secciones del README (`689834c`); script de revisión con 80 comprobaciones.

**Errores del agente detectados:**
1. El primer script de revisión tenía un fallo de `bash` (`local` evaluaba el arreglo antes de asignar las variables) y Git Bash convertía rutas de `docker exec`; se corrigieron.
2. Una prueba desde un **clon limpio** falló en el primer `docker compose up --build` por un corte de red al restaurar NuGet; el segundo intento levantó. Se añadieron reintentos de `dotnet restore` al `Dockerfile` (`df31f3d`).
3. Un `rm -rf` sobre el directorio del clon fue bloqueado por una verificación de seguridad; no se esquivó, se usó un directorio nuevo.

**Evidencia:** 80 de 80 comprobaciones; 308 pruebas en verde. Detalle en `docs/revision-practica-4.md`.

### Solicitud 6: clave real del clima, SMTP real y código de activación de 6 dígitos

**Prompt del usuario:**
> en los archivos .env y .env.example coloque valores reales y mi correo real y mi clave de acceso real, no hay problema con eso ?
>
> puedes hacer que en ves de llegarle un token al usuario para activar su cuenta le llegue mejor un codigo aleatorio de maximo 6 digitos.

**Respuesta del agente:**
1. *Secretos en un archivo versionado.* Detectó, sin imprimir valores, que `.env.example` (versionado) tenía la clave del clima, el usuario y la contraseña SMTP y contraseñas nuevas. Antes de que llegara a un commit guardó una copia en `mis-valores-reales.env` (ignorado por Git) y restauró `.env.example`. Comprobó que ningún valor real estuvo nunca en el historial.
2. *Llamada real al clima.* Con la clave del estudiante en `.env`, `GET /api/habits/plan-del-dia` respondió `200` (31.4 °C, lluvia moderada, «bajo techo», +500 ml de agua) y la clave no apareció en la respuesta ni en los logs. La respuesta real se documentó en el README.
3. *Código de 6 dígitos* (`45ad3f9`). El registro envía 6 dígitos de `RandomNumberGenerator`; la activación pasó a `POST /api/auth/activate` con `{correo, codigo}`; migración `AgregarIntentosActivacion`.

**Errores detectados:**
- *Del estudiante:* valores reales en `.env.example`. Corregido antes de cualquier commit.
- *Del agente:* la primera versión contaba los intentos con leer, incrementar y guardar. La revisión RD señaló un **crítico**: con peticiones en paralelo todas leen el mismo contador y una ráfaga costaría un solo intento. Se cambió a una reserva atómica en SQL (`UPDATE ... OUTPUT`) hecha antes de comparar.
- *Del agente:* faltaba un límite por IP (se añadió una política de 30 peticiones por minuto) y el formato del código solo comprobaba la longitud (ahora exige exactamente 6 dígitos `0-9`, sin aceptar dígitos de otros alfabetos).
- *Del agente:* el script de revisión leía el `.env` con `source` (una contraseña con espacios lo rompe), usaba una carpeta temporal que `curl` no entiende y contaba mal las respuestas paralelas (varios procesos que hacen `>>` al mismo archivo se pisan en Windows).

**Evidencia:** 347 pruebas en verde; en SQL Server real, una ráfaga de 12 intentos simultáneos dejó el contador en 12, el código correcto quedó bloqueado y el límite por IP respondió `429`; 95 comprobaciones del recorrido completo y 4 de persistencia.

## Resumen de commits

| Commit | Mensaje |
|---|---|
| `7ce929d` | docs: agrega la especificación de la práctica 4 |
| `bf9513b` | feat: entidad Documento con borrado lógico y regla de acceso |
| `69a3f8f` | feat: servicio de documentos con validación de archivos |
| `f4a694c` | feat: persistencia y almacenamiento en disco de documentos |
| `6bb858c` | feat: endpoints del manejador de documentos |
| `9df8be0` | feat: migraciones automáticas y datos semilla al iniciar |
| `dc9d9ab` | feat: modo consola para la cola de correos |
| `0f4b35e` | fix: el modo consola registra el correo en una sola línea |
| `302f93d` | feat: recomendación del plan del día según el clima |
| `2f76376` | feat: integración con OpenWeatherMap con rechazo controlado |
| `994d6a4` | feat: contenedor con Dockerfile, compose y volúmenes con nombre |
| `689834c` | docs: secciones Contenedor, API externa y Documentos en el README |
| `df31f3d` | fix: reintentar dotnet restore en el Dockerfile |
| `0877a2e` | test: script de revisión extremo a extremo de la práctica 4 |
| `6c02e08` | docs: plan de prueba, revisión del maestro, bitácora y estado |
| `45ad3f9` | feat: el correo de activación trae un código de 6 dígitos con límite de intentos |
| `280f9f0` | test: el script de revisión cubre el código de 6 dígitos, la ráfaga y la clave real |

## Resultados finales

- 347 pruebas en verde (106 antes de la práctica).
- 99 de 99 comprobaciones de la simulación del maestro (95 del recorrido completo + 4 de persistencia).
- API externa probada con una clave real (`200`) y con la de ejemplo (`503` controlado).
