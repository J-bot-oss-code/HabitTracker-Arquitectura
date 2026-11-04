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

## Resultados finales

- 308 pruebas en verde (106 antes de la práctica).
- 80 de 80 comprobaciones de la simulación del maestro.
- Pendiente: ejemplo real de la respuesta del clima (requiere la clave de OpenWeatherMap del estudiante).
