# Plan de prueba — Práctica 4 (contenedor, documentos y API externa)

Guion para comprobar la entrega **exactamente como la revisa el maestro** (`docs/practica-4-especificaciones.md` §4). Cada paso indica el resultado esperado. Existe una versión automática, descrita al final.

## 0. Preparación (clon limpio)

```bash
git clone https://github.com/J-bot-oss-code/HabitTracker-Arquitectura.git C:/p4rev   # ruta corta
cd C:/p4rev
git checkout practica-4
```

| Comprobación | Resultado esperado |
|---|---|
| Existen `Dockerfile`, `.dockerignore`, `docker-compose.yml`, `.env.example` | Presentes |
| Existe `.env` en el clon | **No** (`ls .env` falla) |
| `.gitignore` excluye `.env` | `git check-ignore -v .env` → regla `.env` |
| `.env` en el historial | `git log --all --oneline -- .env '*.env'` → vacío |

## 1. Arranque con un solo comando (1.5 pts)

```bash
copy .env.example .env          # cp en Linux/macOS
docker compose up --build
```

- Sin pasos manuales: migraciones, Administrador inicial y Meta de demostración se crean solos.
- Responde en **`http://localhost:8080`**; Swagger en `/swagger` → `200`.
- `docker compose ps`: solo `webapi` publica puerto (`0.0.0.0:8080->8080`); `db` no tiene mapeo y el `1433` no escucha en el equipo.

## 2. Revisión acumulada — los seis frentes (2.0 pts)

Swagger en `http://localhost:8080/swagger`. Los correos se leen con `docker compose logs webapi | findstr CORREO`.

| # | Frente | Pasos | Esperado |
|---|---|---|---|
| 1 | Registro con activación | `POST /api/auth/register` → leer `Código:` del log → `POST /api/auth/activate` | `201` y luego `200` |
| 2 | Sesión | `POST /api/auth/login` → `GET /api/auth/me` → `POST /api/auth/logout` → `GET /api/auth/me` | `200`, `200`, `200`, **`401`** |
| 3 | Estándar vs Administrador | Token Estándar en `GET /api/admin/usuarios` | **`403`** (con el Administrador: `200`) |
| 4 | Recuperación | `POST /api/auth/forgot-password` → código del log → `POST /api/auth/reset-password` | `200`; reusar el código → `400`; la clave vieja deja de servir |
| 5 | Permisos | Estándar crea (`201`); Administrador aprueba (`200`) / rechaza (`200`); aprobar otra vez, rechazar una aprobada, aprobar una rechazada | **`400`** en cada transición fuera de la tabla; Estándar aprobando → `403`; autoaprobación → `400` |
| 6 | Negocio (hábitos) | `POST /api/habits` con la Meta `5f1c0a00-0000-4000-8000-000000000002` → `transition` a `Activo` | `201`, `204`; `Planificado→Completado` y `Activo→Completado` sin meta cumplida → **`400`** |

## 3. Manejador de documentos (2.0 pts)

| Prueba | Esperado |
|---|---|
| Subir sin token | `401` |
| Subir sin archivo / archivo vacío / `.exe` | `400` controlado, sin traza de pila |
| Subir `informe.pdf` (dueño) | `201` con nombre, tipo, tamaño, fecha UTC y correo de quien lo subió |
| `GET /api/documents/{id}` y listado (dueño) | `200`, el documento aparece |
| Descargar | Bytes idénticos al original; `Content-Disposition: attachment`; `nosniff` |
| **Otro Estándar** lista | No ve el documento |
| **Otro Estándar** descarga / elimina / consulta | **`403`** en los tres |
| **Administrador** lista y descarga | Ve todos; `200` |
| Subir `nota.txt` declarado `text/html` | Se guarda como `text/plain` |
| Eliminar (dueño) | `204`; deja de listarse (también para el Administrador); consultarlo → `404` |
| Eliminación **lógica** | `SELECT Eliminado FROM Documentos` → `1`, la fila y el archivo siguen existiendo |

Comprobar la fila: `docker compose exec db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "<DB_PASSWORD>" -d HabitTracker -Q "SELECT NombreOriginal, Eliminado FROM Documentos"`.

## 4. Los datos sobreviven (1.0 pt)

1. Con un documento subido: `docker compose down` (**sin** `-v`).
2. `docker compose up -d`.
3. Esperado: el usuario inicia sesión con su contraseña, el documento sigue en el listado y se descarga con los mismos bytes.
4. `docker volume ls` muestra `documentos-archivos` y `sqlserver-datos` (dos volúmenes con nombre distintos).

## 5. API externa con la clave como secreto (1.5 pts)

| Prueba | Esperado |
|---|---|
| `.env` con la clave de ejemplo → `GET /api/habits/plan-del-dia?ciudad=Santo Domingo` | **`503`** `"El servicio de clima no está disponible…"`, sin la clave ni trazas |
| Mientras tanto `GET /api/habits` | `200`: el resto sigue funcionando |
| Sin `ciudad` / ciudad `<script>` | `400` |
| Sin sesión / con el Administrador | `401` / `403` |
| Con una clave real activa | `200` con clima, `lugarSugerido` y `aguaAdicionalMl` |
| `docker image inspect` / `docker history --no-trunc` | Sin el valor de ninguna variable del `.env` |
| `git log --all -p \| grep -i "appid=[a-z0-9]\{20,\}"` | Sin resultados |

## Versión automática

El recorrido completo (76 comprobaciones + 4 de persistencia) se automatizó en `scripts/revision-practica-4.sh` (`bash scripts/revision-practica-4.sh`, y `... persistencia` después de `down`/`up`), un script de `bash` + `curl` que imprime `PASA`/`FALLA` por cada punto; el resultado de la última corrida está en `docs/revision-practica-4.md`.
