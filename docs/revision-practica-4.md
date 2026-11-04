# Revisión de la Práctica 4 — simulación del maestro

Se revisó la entrega **como lo indica la especificación (§4)**: un clon limpio en una ruta corta (`C:\p4rev`), `.env.example` copiado a `.env`, el comando de la sección «Contenedor» del README al pie de la letra y, sobre el proyecto levantado, las comprobaciones de la rúbrica. El resultado de cada punto sale de `scripts/revision-practica-4.sh` y de comandos de `docker` y `git` ejecutados sobre ese clon.

**Resultado de la corrida:** 76 comprobaciones del recorrido completo + 4 de persistencia tras `down`/`up` = **80 de 80 pasan**. Además, `dotnet test` ejecuta **308 pruebas** (106 antes de la Práctica 4), todas en verde.

## 1. Lo que se revisa, paso a paso

| Paso del maestro (§4) | Cómo se comprobó | Resultado |
|---|---|---|
| `git clone` y `git checkout` | Clon de GitHub de la rama de la práctica | Sin `.env` en el clon |
| Presencia de archivos | `ls -a` | `Dockerfile`, `.dockerignore`, `docker-compose.yml`, `.env.example`, `.gitignore` con `.env` |
| `.env` en el historial | `git log --all -- .env '*.env'` y lista de todos los archivos que existieron | Vacío; solo existen `.env.example` y `frontend/.env.example` |
| Claves en el historial | `git log --all -p` buscando `appid=` con valor y cadenas de clave | Sin coincidencias reales |
| `.env.example` → `.env` y el comando del README | `docker compose up --build` | Levanta y responde en `http://localhost:8080` (`/swagger` → `200`) |
| Solo la app publica puerto | `docker compose ps`, `netstat` | `webapi: 0.0.0.0:8080->8080`; `db` sin mapeo; el 1433 no escucha en el equipo |
| Documento: subir, listar, descargar, eliminar | Script, dueño | `201` con los cinco datos, listado, descarga byte a byte, `204` |
| Otro Estándar rechazado | Script, Beto | Descargar, eliminar y consultar → `403`; no lo ve en su listado |
| `down` sin `-v` y volver a levantar | Script, fase de persistencia | Inicia sesión, el documento se lista y se descarga con los mismos bytes |
| API externa con la clave de ejemplo | `GET /api/habits/plan-del-dia` | `503` controlado; `GET /api/habits` sigue en `200` |
| Historial completo | Ver arriba | Sin `.env` ni clave |
| Seis frentes | Script, secciones 1–6 | Todos pasan |

## 2. Rúbrica

| Criterio | Peso | Nivel | Evidencia |
|---|---|---|---|
| **Manejador de documentos RF-DOC-01 a 04** | 2.0 | **100 %** | Subir guarda nombre, tipo, tamaño, fecha UTC y quién lo subió. El listado excluye eliminados y ajenos (el Administrador ve todos). Solo dueño o Administrador descargan o eliminan; otro Estándar recibe `403`. La eliminación es lógica: tras eliminar, `SELECT Eliminado FROM Documentos` devuelve `1` y la fila y el archivo siguen existiendo. Sin sesión → `401`; archivo ausente, vacío o de tipo no permitido → `400` controlado |
| **Contenedor con un solo comando** | 1.5 | **100 %** | Desde un clon limpio, `copy .env.example .env` + `docker compose up --build` levanta todo y responde en el puerto 8080 que dice el README. Hay `Dockerfile`, `.dockerignore`, `docker-compose.yml` y la sección «Contenedor». Sin pasos manuales: migraciones y semillas se aplican al arrancar |
| **Volumen persistente RF-DOC-05** | 1.0 | **100 %** | Dos volúmenes con nombre (`documentos-archivos` y `sqlserver-datos`). Tras `down` sin `-v` y `up`: el documento se lista y se descarga y el usuario inicia sesión |
| **API externa con la clave como secreto** | 1.5 | **100 % si la sección «API externa» incluye una respuesta real** (hoy: *pendiente de la clave*) | OpenWeatherMap alimenta `plan-del-dia` con reglas de negocio propias (no es un proxy). La clave llega por `OPENWEATHER_API_KEY` desde el `.env` no versionado; `.env.example` trae la variable. Sin clave, con clave inválida, `401`/`429`/`5xx`, timeout o JSON inesperado → `503` controlado y el resto sigue. La imagen y el historial no contienen la clave; los logs y mensajes tampoco |
| **Revisión acumulada (6 frentes)** | 2.0 | **100 %** | Registro con activación por correo; inicio/cierre de sesión; Estándar rechazado en una operación de Administrador; recuperación de contraseña; permisos (crear, aprobar, rechazar y transiciones fuera de la tabla rechazadas); hábitos (operación válida y transición prohibida rechazada) |

**Puntaje esperado: 8.0 / 8.0**, condicionado a completar el ejemplo real de la respuesta del clima (ver §4).

## 3. Cómo se verificaron los defectos que importan

- **Autorización en el servidor (RD-06):** las pruebas de unidad del servicio y del controlador, y el script contra el contenedor, comprueban que la identidad sale de los claims y que un Estándar no obtiene el documento de otro por el `id`.
- **Eliminación lógica:** consulta SQL directa a la base dentro del contenedor.
- **Que la clave no se filtra:** pruebas del cliente HTTP que fuerzan errores con la clave dentro del mensaje de la excepción y comprueban que no llega al mensaje, a `ToString()` ni a los logs; `docker history --no-trunc` y `docker image inspect` sin valores del `.env`; la imagen no contiene ningún archivo `.env` y corre como el usuario `app`.

## 4. Riesgos y pendientes (sin maquillar)

1. **Ejemplo real de la API.** El README exige «un ejemplo real de la respuesta que obtuviste (sin la clave)». Hace falta una clave gratuita de OpenWeatherMap (tarda hasta 2 h en activarse) en el `.env` local; con ella se ejecuta la llamada y se pega la respuesta en la sección «API externa». **Mientras no esté, el criterio puede quedar en 50 %.**
2. **Primer arranque lento.** Descargar SQL Server (~2.3 GB) y restaurar paquetes tarda varios minutos. En una prueba desde clon limpio el primer `up --build` falló por un corte de red al restaurar NuGet y el segundo levantó bien; por eso el `Dockerfile` reintenta `dotnet restore` tres veces.
3. **Requisitos del equipo de revisión.** Docker con Compose v2, el puerto 8080 libre y acceso a internet para las imágenes y los paquetes. En Mac con chip Apple, SQL Server corre emulado.
4. **Una sola réplica.** La siembra inicial no está protegida contra dos instancias arrancando a la vez (`Usuario.Correo` no tiene índice único; añadirlo exige una migración en AccessControl). La composición usa una réplica.
5. **Solo demostración.** El contenedor escucha HTTP sin TLS y los correos con enlaces/códigos se escriben en el log en modo consola. Está documentado en el README.
6. **Caché del clima.** Cualquier usuario autenticado puede consumir la cuota gratuita (60 llamadas/min) y provocar `503` para todos. Mejora futura: caché por ciudad o limitación de tasa.
7. **RF-DOC-06** (auditoría de subidas y eliminaciones) no se califica en esta práctica; llega con la Pieza 6.

## 5. Revisiones de diseño (RD-01 a RD-12)

Antes de cada commit se ejecutó la revisión del agente `core-transversal-reviewer` sobre los archivos del commit. Hallazgos que cambiaron el código: tamaño y tipo del archivo tomados del cliente (ahora se miden y derivan en el servidor), `\` como separador en Linux, raíz de almacenamiento con barra final, archivo parcial tras un fallo, ruta por defecto dentro de `bin/`, reintentos que ocultaban errores de migración, clave de Administrador débil, Swagger encendido por defecto, redirecciones y tamaño de respuesta del cliente HTTP, y el registro de la URL con la clave. Las observaciones no críticas que se dejaron como deuda están en la sección anterior.
