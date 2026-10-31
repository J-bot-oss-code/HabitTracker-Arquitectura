# Estado del proyecto — contexto para nuevas sesiones

> Última actualización: 28-10-2026, después de integrar la Práctica 3 en `main` (PR #32 → `develop`, PR #33 → `main`).
> Lee este archivo al iniciar una sesión. Las reglas de trabajo están en `AGENTS.md` y la guía de uso y evaluación en `README.md`.

## 1. Resumen

HabitTracker Nexus es un monolito modular en .NET 10 con Clean Architecture: un solo host (`Host/WebApi`) y 7 módulos en `Modules/`. Cada módulo tiene `Domain ← Application ← Infrastructure` y su propio `DbContext`. Los tres contextos implementados comparten la cadena `ConnectionStrings:AccessControl` (SQL Server).

| Módulo | Estado | Entrega |
|---|---|---|
| AccessControl (Control de Acceso) | Completo: registro + activación por correo, JWT, logout con blacklist, bloqueo 15 min, roles `Estandar`/`Administrador`, recuperación de contraseña, cola de correos en BD, administración de usuarios | Práctica 1 |
| Permissions (Gestión de Permisos) | Completo: `Pendiente → Aprobada → Aplicada`, `Pendiente → Rechazada`, autoresolución prohibida, acción protegida por permiso aplicado | Asignación 2 / Práctica 2 |
| HabitTracker (módulo de negocio) | Completo para la P3: `Planificado → Activo ⇄ Pausado → Completado` (terminal), 5 endpoints en `api/habits`, reconfiguración con permiso `Habits.AdvancedReconfig` | Práctica 3 |
| Auditing, DocumentManager, Notifications, Reports | Solo esqueleto (proyectos vacíos) | Pendiente |

## 2. Tags de entrega (no mover ni reescribir)

| Tag | Commit | Notas |
|---|---|---|
| `asignacion-1` | `0056600` | |
| `practica-1` | `171d0fd` | |
| `asignacion-2` | `6433209` | |
| `practica-2` | `86f7e0a` | 39 pruebas. Defectos `practica-2-defecto-1..6`, hijos directos. |
| `practica-3` | `318daab` | 106 pruebas. Defectos `practica-3-defecto-1..4` (`485a0a1`, `dcd2bea`, `f1d3335`, `f802c65`), hijos directos. |

- Los tags de la Práctica 3 se rehicieron el 28-10 (antes de entregar) para incluir dos correcciones (ver §5). Quien tenga clones antiguos debe ejecutar `git fetch --tags --force`.
- Cada commit de defecto cambia un solo archivo de producción y su mensaje indica archivo y línea. La tabla con la prueba que detecta cada defecto está en `README.md` › `## Defectos — Práctica 2/3`.
- Para la entrega en Moodle: URL del repositorio + nombre del tag base.

## 3. Pruebas

`dotnet test` ejecuta 106 pruebas sin levantar la API ni usar una BD externa:

| Proyecto | Pruebas |
|---|---|
| HabitTracker.Domain.Tests | 43 |
| HabitTracker.Application.Tests | 21 (con dobles en memoria) |
| HabitTracker.Integration.Tests | 3: 1 de integración real con Permisos + 2 de persistencia de avances, con EF Core/SQLite |
| Permissions.Domain / Application / Infrastructure | 18 / 6 / 3 |
| AccessControl.Application.Tests | 12 |

`tests/Permissions.Api.Tests` es una carpeta sin proyecto (solo `bin/obj`, no versionada).

## 4. Entorno local

- SQL Express `.\SQLEXPRESS`, base `HabitTracker` (desarrollo) y base aislada `HabitTracker_PruebaP3` (prueba E2E; se puede borrar).
- Secrets (User Secrets de `Host/WebApi`): `JwtSettings:Clave` y `SMTP_HOST/PORT/USER/PASS`, que son **reales**. La cadena de conexión está en `appsettings.json` (Windows Auth, sin contraseña).
- Para probar sin tocar la base de desarrollo ni enviar correos:
  `ConnectionStrings__AccessControl=Server=.\SQLEXPRESS;Database=<BD_PRUEBA>;Trusted_Connection=True;TrustServerCertificate=True` y `SMTP_HOST=` vacío. Los correos quedan en `CorreosEnCola`.
- La API corre en `http://localhost:5246` y Swagger en `/swagger`. Para promover a Administrador: `UPDATE usuarios SET Rol = 2 WHERE Correo = ...` y volver a iniciar sesión.
- Plan de prueba manual verificado (83/83): `docs/plan-prueba-swagger-practica-3.md`.

## 5. Decisiones y correcciones relevantes

- **`RegistroDiario.Id` con `ValueGeneratedNever`** (migración vacía `RegistroDiarioIdAsignadoPorDominio`). El dominio asigna el Id. Sin esto, EF trataba el avance nuevo como existente (`UPDATE` en vez de `INSERT`) y la API respondía 500. Aplica a cualquier entidad hija nueva con Id asignado en el constructor.
- **`JsonStringEnumConverter`** en `Program.cs`: los enums viajan como texto (`"Diaria"`, `"Activo"`) y también se aceptan números.
- **Middleware de excepciones:** `UnauthorizedAccessException` → 403 si el usuario está autenticado y 401 si no. `InvalidOperationException` → 400. `ArgumentException` → 400. Los mensajes al cliente son genéricos (RD-08).
- **Transiciones** en un solo punto: `TransicionesHabito.EsTransicionValida` y `TransicionesSolicitudPermiso`. `Habito.Completar` no duplica la validación de la meta.
- **Reconfigurar** inicia un ciclo nuevo (`FechaInicioCiclo`) y conserva el historial; se rechaza si el hábito está `Completado`.

## 6. Deuda técnica conocida

1. `HabitsController` y `PermissionsController` deciden el 404 buscando `"no existe"` en el mensaje (`EsNoEncontrado`). Es frágil: conviene una excepción de dominio específica.
2. `HabitsController` depende de la clase concreta `HabitoService` y no de una interfaz (RD-01).
3. `RegistrarAvance` acepta fechas futuras.
4. Las fechas leídas de SQL vuelven con `DateTimeKind.Unspecified`, y la API devuelve `fechaInicioCiclo` y `fecha` sin `Z`.
5. `.github/PULL_REQUEST_TEMPLATE.md` contiene una PR ya rellenada en lugar de una plantilla vacía con las 4 secciones.
6. El README mezcla secciones de las prácticas 1–3: se podría reorganizar por pieza.

## 7. Flujo de trabajo

- Commits en español con los RF/RD (`feat: ... [RF-NEG-02]`) y revisión RD-01..RD-12 antes de cada commit (ver `AGENTS.md`).
- Ramas: trabajo en `feature/...`, `fix/...` o `docs/...`; PR hacia `develop`; luego PR `develop → main` (`Promover a main — ...`). Cuerpo de PR en 4 secciones: Qué cambia, Por qué, Cómo probarlo, Qué NO incluye. Nunca push directo a `main` ni a `develop`. Las PRs las fusiona el usuario.
- El usuario tiene la skill `flujo-pr` (a nivel de usuario, en `~/.claude/skills/flujo-pr`) con este flujo.
- La bitácora de cada práctica (`docs/bitacora-*.md`) registra las solicitudes reales al agente, sus errores y las correcciones.

## 8. Problemas de entorno conocidos

- **Rutas largas en Windows:** con más de 260 caracteres, SQLite no carga `e_sqlite3` y las pruebas de persistencia fallan. Hay que clonar o copiar el repo en una ruta corta.
- **`sqlcmd`:** no admite `-W` junto con `-y`, ni `-h` con `-y 0`. Con `-W`, las columnas largas (p. ej. `CorreosEnCola.Cuerpo`) se truncan; extraer el fragmento necesario con `SUBSTRING(... CHARINDEX(...))`.
- **Git Bash + curl:** los acentos dentro de JSON pueden llegar mal codificados; usar ASCII en los datos de prueba.

## 9. Próximos pasos posibles

- Módulo de negocio: RF-NEG-07, RF-NEG-08 y RF-NEG-10 (fuera del alcance de la P3).
- Piezas del Core pendientes: Notificaciones, Auditoría, Reportes y Manejador de Documentos (ver `docs/WORKFLOW_STATUS.md`).
- Atender la deuda técnica de §6.

## 10. Frontend (monorepo)

> Añadido el 30-10-2026 en la rama `feature/frontend-base`. No modifica el backend: los tags de entrega y los archivos de `Host/`, `Modules/` y `tests/` quedan intactos.

- Ubicación: `frontend/`. Stack: React 19, Vite 8, TypeScript estricto, React Router 7, TanStack Query 5, react-hook-form + zod, Tailwind 4, MSW 2 y Vitest + Testing Library. Guía: `frontend/README.md`. Reglas: `frontend/AGENTS.md`.
- Pantallas funcionales (lo que ya entrega el backend):
  - Control de acceso: registro, activación, login, logout, recuperación, restablecimiento y cambio de contraseña.
  - Hábitos: lista con filtros, creación, transiciones, avance, historial y reconfiguración con permiso.
  - Permisos: solicitud y bandeja del Administrador con aprobación y rechazo.
  - Administración de usuarios.
- Pantallas "Próximamente" con sus RF: Documentos, Notificaciones, Cola de correos, Reportes y Auditoría, que incluyen RF-NEG-07/08/10 (`frontend/src/features/coming-soon/piezas.ts`).
- Datos: API simulada con MSW (`frontend/src/mocks`) que replica el contrato y las reglas de `Host/WebApi`. Para conectar el backend real: `VITE_USE_MOCKS=false` más **CORS en `Program.cs`**, que hoy no está configurado. Este último es un cambio del backend pendiente.
- Pruebas: `cd frontend && npm test` ejecuta 84 pruebas (unitarias, flujos de UI por rol y contrato de la API simulada) sin backend.
- Agentes del frontend en `.claude/agents/frontend-*.md`: architect, api-contract, ui-builder, test-writer y reviewer. El flujo está en `frontend/AGENTS.md`.
- Contrato observado al construirlo:
  - `GET /api/permissions` usa `soloPendientes=true` por defecto;
  - el login usa `email` y el resto de endpoints `correo`;
  - el rol llega en el claim URI largo;
  - no existe `GET /api/habits/{id}` ni un listado de metas, así que el `metaId` se escribe a mano.
