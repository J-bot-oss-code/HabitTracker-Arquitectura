# Estado del proyecto — contexto para nuevas sesiones

> Última actualización: 08-11-2026, Fase 2a del plan HabitTracker v2 (backend de perfil, salud cifrada y progreso corporal). Antes, ese mismo día, la Fase 1 (cimientos), y el 07-11-2026 la Fase 0 (skill de contexto). Este archivo describe **solo el estado actual** (tope de 120 líneas, lo vigila `scripts/ci/verificar-contexto.sh`). La historia de cada fase está en `docs/historial/`.
> **Al iniciar una sesión, usa la skill `contexto-habittracker`** (`.claude/skills/contexto-habittracker/`): rutas, contratos, recetas y requisitos sin leer el repositorio entero. Reglas de trabajo en `AGENTS.md`; guía de uso y evaluación en `README.md`.

## 1. Resumen

HabitTracker Nexus es un monolito modular en .NET 10 con Clean Architecture. Tiene un solo host (`Host/WebApi`) y 7 módulos en `Modules/`, cada uno con `Domain ← Application ← Infrastructure` y su propio `DbContext`. Los cuatro contextos implementados comparten la cadena `ConnectionStrings:AccessControl` (SQL Server). El frontend React vive en `frontend/`.

| Módulo | Estado | Historia |
|---|---|---|
| AccessControl | Completo: registro + activación con código, JWT de 15 min + refresh en cookie, Google, bloqueo, contraseñas reforzadas, consentimientos, baja y descarga de datos, administración y cola de correos | `docs/historial/control-acceso-v2.md` |
| Permissions | Completo: `Pendiente → Aprobada → Aplicada`, `Pendiente → Rechazada` | P2 |
| HabitTracker (negocio) | Hábitos (`Planificado → Activo ⇄ Pausado → Completado`, 6 endpoints en `api/habits`), perfil en dos niveles, cuestionario de salud cifrado y progreso corporal (`api/profile`, `api/progress`), y `plan-del-dia`. **En evolución:** plan v2, Fases 0, 1 y 2a hechas | `docs/historial/habittracker-v2.md` |
| DocumentManager | Completo para la P4 (RF-DOC-01 a 05) | `docs/historial/practica-4.md` |
| Auditing, Notifications, Reports | Solo esqueleto (proyectos vacíos) | — |
| Frontend | Conectado a Control de Acceso, hábitos, permisos, usuarios y privacidad. Perfil, peso y medidas ya tienen backend real, pero el frontend aún usa sus mocks (Fase 2b). Las métricas siguen como contrato propuesto | `docs/historial/frontend.md` |
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

- **Backend:** `dotnet test` ejecuta **946** pruebas (medido el 08-11-2026) sin API ni BD externa (SQLite en memoria). El desglose por proyecto está en `.claude/skills/contexto-habittracker/referencias/inventario.generado.md`.
- **Frontend:** `cd frontend && npm test` ejecuta **634** pruebas en 49 archivos (medido el 08-11-2026) sin backend (MSW).
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
- **Errores:** `ProblemDetails` con mensaje genérico (RD-08) y una extensión `codigo` estable en Control de Acceso (tabla en el README) y en HabitTracker (`habito_no_encontrado` 404, `fecha_futura` y `meta_no_encontrada` 400). Las transiciones prohibidas del hábito siguen siendo un 400 genérico, para no tocar las suites de la P3. `UnauthorizedAccessException` responde 403 con sesión y 401 sin ella.
- **Transiciones en un solo punto:** `TransicionesHabito.EsTransicionValida` y `TransicionesSolicitudPermiso`. Reconfigurar un hábito inicia un ciclo nuevo y conserva el historial.
- **Módulos conectados por puertos:** la interfaz vive en el `Application` de quien la usa y la implementa un adaptador en `Host/WebApi/Adapters/` (`PermisoServiceAdapter`, `IParticipanteDatosUsuario` para la baja y la descarga). Ningún módulo lee tablas de otro.
- **Plan HabitTracker v2** (07-11-2026):
  - Básico y Regular se distinguen por su estructura.
  - Los planes no aptos se muestran y se bloquean (`plan_no_apto`).
  - Cifrado en reposo con Data Protection.
  - Principios P1–P8.
  - El piloto (Fase 6) decide las Fases 7–9.

## 6. Deuda técnica conocida

1. `PermissionsController` decide el 404 buscando `"no existe"` en el mensaje (`EsNoEncontrado`). En HabitTracker ya se resolvió con `RecursoNoEncontradoException`; queda para cuando se trabaje el módulo de Permisos.
2. Un correo que queda en `Procesando` tras una caída nunca se reintenta, y `CorreosEnCola` no tiene índices. *(Fase 5.)*
3. Dentro de Docker, `RemoteIpAddress` es la IP de la pasarela, así que todos los clientes comparten el cupo del limitador de peticiones (hasta configurar `ForwardedHeaders` con `KnownProxies`).
4. `webapi` no tiene healthcheck. *(Fase 6.)*
5. `.github/PULL_REQUEST_TEMPLATE.md` contiene una PR rellenada en lugar de una plantilla con las 4 secciones.

## 7. Flujo de trabajo

- **Commits** en español con IDs RF/RD **de 2 dígitos** (`feat: ... [RF-NEG-02, RD-07]`) y revisión RD-01..RD-12 antes de cada commit (ver `AGENTS.md`).
- **Ramas:** `feature/`, `fix/`, `docs/`, `refactor/` o `chore/` → PR a `develop` → PR `develop → main` (`Promover a main — ...`).
  - El cuerpo de la PR lleva 4 secciones: Qué cambia, Por qué, Cómo probarlo y Qué NO incluye (skill `flujo-pr`).
  - `develop` y `main` rechazan el push directo. Si se trabajó en `develop` local: `git push origin develop:feature/...` (sin `-u`).
  - Las PR las fusiona el usuario.
- La bitácora de cada práctica (`docs/bitacora-*.md`) registra las solicitudes reales al agente, sus errores y las correcciones.
- **Al cerrar cada entrega:** actualiza este archivo (≤120 líneas), mueve el detalle de la fase a `docs/historial/`, regenera el inventario de la skill (`bash scripts/contexto/generar-inventario.sh`) y comprueba con `bash scripts/ci/verificar-contexto.sh`.

## 8. Problemas de entorno conocidos

- **Rutas largas en Windows:** con más de 260 caracteres, SQLite no carga `e_sqlite3` y las pruebas de persistencia fallan. Clona el repo en una ruta corta.
- **`sqlcmd`:** no admite `-W` junto con `-y`, ni `-h` con `-y 0`. Con `-W` se truncan las columnas largas; usa `SUBSTRING(... CHARINDEX(...))`.
- **Git Bash + curl:** los acentos dentro de JSON pueden llegar mal codificados; usa ASCII en los datos de prueba. Con `docker exec` y rutas absolutas, antepón `MSYS_NO_PATHCONV=1`.
- **CRLF:** con `core.autocrlf=true`, `npm run format:check` falla en local, pero en el CI pasa. `.gitattributes` fuerza LF en los `.sh`.
- **PDF de requisitos:** no se renderizan. Usa el resumen de la skill (`referencias/requisitos.md`) o `pdftotext -raw -enc UTF-8`.

## 9. Próximos pasos

- **Plan HabitTracker v2** (`docs/plan-habittracker-v2.md`):
  - Fases 0, 1 y 2a (backend) hechas → Fase 2b (frontend: onboarding de 3 toques, «Tu salud», Perfil y salud, Progreso conectado) → Fases 3–5, el corte vertical;
  - Fase 6, producción, métricas y piloto;
  - las Fases 7–9 dependen del informe del piloto.
- **Pendiente de decidir antes de la Fase 6:** dónde se hospeda el piloto. El CD está pendiente y Vercel no hospeda .NET.
- **Después del módulo de negocio:** Permisos, Documentos, Notificaciones (bandeja interna y RF-NOT-06/07), Reportes (RF-NEG-08) y Auditoría.
