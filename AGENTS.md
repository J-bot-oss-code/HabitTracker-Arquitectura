# AGENTS.md — HabitTracker-Arquitectura

.NET 10 modular monolith (Clean Architecture). Single host `Host/WebApi` + modules under `Modules/`.

## Propósito del proyecto

**HabitTracker Nexus** ayuda a una persona a **mantener hábitos con constancia, sin culpa**: planes guiados, check-in diario con versión mínima y mensajes de motivación. La app protege los datos de salud (cifrado en reposo, consentimiento) y nunca mide ni recompensa el cuerpo.

- **Lenguaje y framework:** C# / .NET 10 (ASP.NET Core Web API, EF Core, SQL Server) y un frontend React + Vite + TypeScript en `frontend/`.
- **Módulos del Core:** Control de Acceso, Permisos, Manejador de documentos, Notificaciones (con la cola de correos), Reportes y Auditoría. **Módulo de negocio:** HabitTracker.

**Al iniciar una sesión, usar la skill `contexto-habittracker`** (`.claude/skills/contexto-habittracker/SKILL.md`). Es un mapa corto del repositorio (módulos, endpoints, puertos, pruebas, frontend, requisitos RF/RD, recetas y principios de producto) que evita leer el repo entero o los PDF. Después, `docs/ESTADO-DEL-PROYECTO.md` (estado actual, ≤120 líneas). La historia por fases está en `docs/historial/` y solo se lee bajo demanda.

Al cerrar cada entrega:
- actualizar el ESTADO;
- regenerar el inventario (`bash scripts/contexto/generar-inventario.sh`);
- ajustar la referencia afectada de la skill;
- comprobar con `bash scripts/ci/verificar-contexto.sh`, que también corre en el CI.

## Solution / projects

- Solution is `HabitTrackerSystem.slnx` (new XML format, not `.sln`).
- Each module has `<Module>.Domain` ← `<Module>.Application` ← `<Module>.Infrastructure`. Wiring: AccessControl in `Host/WebApi/Extensions/ServiceExtensions.cs`, HabitTracker in `Host/WebApi/Extensions/HabitTrackerModuleExtensions.cs`, Permissions and DocumentManager in their own `Infrastructure/DependencyInjection/`.
- Implemented modules: `AccessControl` (Práctica 1, extended by Control de Acceso v2), `Permissions` (Práctica 2), `HabitTracker` (Práctica 3; v2 plan in `docs/plan-habittracker-v2.md`), `DocumentManager` (Práctica 4). `Auditing`, `Notifications`, `Reports` are empty skeletons.
- Each module owns its DbContext/migrations (SQL Server); no cross-module table access — modules talk through Application interfaces (e.g. HabitTracker `IPermisoService` → `PermisoServiceAdapter`).
- All projects: `net10.0`, `Nullable enable`, `ImplicitUsings enable`. Requires .NET 10 SDK.

## Commands

- `dotnet build HabitTrackerSystem.slnx`
- `dotnet test` — xUnit + Moq suites in `tests/`; no API or external DB needed (SQLite in memory).
- `dotnet run --project Host/WebApi/WebApi.csproj` (`http://localhost:5246`, Swagger at `/swagger`). Secrets (connection string, `JwtSettings:*`, SMTP) via User Secrets/env vars — see README.
- Migrations: `dotnet ef database update --project Modules/<M>/<M>.Infrastructure --startup-project Host/WebApi --context <Ctx>` (`ApplicationDbContext`, `PermissionsDbContext`, `HabitTrackerDbContext`, `DocumentManagerDbContext`).
- **Contenedor (todo el sistema):** `copy .env.example .env` y `docker compose up --build` → `http://localhost:8080`. Los datos de los documentos y las claves de cifrado viven en volúmenes con nombre: `docker compose down` (sin `-v`) no los borra.
- **Frontend:** `cd frontend && npm ci && npm test` (sin backend, API simulada con MSW), `npm run lint`, `npx tsc -b`, `npm run build`. Contra la API real: `VITE_API_URL=http://localhost:5246 npx vite --port 5174`.
- Tags `practica-N` / `practica-N-defecto-M` are graded deliverables — never move or rewrite them.
- CI (`.github/workflows/ci.yml` and `seguridad.yml`): required checks are `ci-ok` and `seguridad-ok`; high/critical findings block the merge. Run locally with `bash scripts/ci/verificar-reglas-rd.sh`, `bash scripts/ci/verificar-commits.sh <base> HEAD` and `bash scripts/ci/pruebas-verificar-commits.sh` (RD-03/05/08/10/11 and the commit format are checked automatically). `Directory.Build.props` turns the .NET security analyzers (`analizadores-seguridad.globalconfig`) and NuGetAudit NU1903/NU1904 into build errors — fix the cause; never lower the severity of a whole category. Exceptions need a written justification (see README › Integración continua).

## Restricciones para el agente (obligatorias)

- **Secretos:** nunca abras, leas ni imprimas `.env` ni `mis-valores-reales.env` (están fuera de git a propósito). Nunca escribas credenciales, claves ni contraseñas en el código, los commits, los logs, las pruebas ni la documentación; usa variables de entorno y `.env.example` con valores vacíos (RD-10).
- **No tocar:** los tags `practica-*` y `asignacion-*` (ni sus ramas), las migraciones ya creadas (se añaden migraciones nuevas), `.github/workflows/`, `analizadores-seguridad.globalconfig`, `Directory.Build.props` y los archivos con defectos plantados de las prácticas (`SolicitudPermiso.cs`, `TransicionesSolicitudPermiso.cs`, `HabitoService.cs`, `Habito.cs`, `TransicionesHabito.cs`).
- **Alcance:** cada módulo se comunica con otro solo por interfaces de `Application` y adaptadores en `Host/WebApi/Adapters`; ningún módulo lee las tablas de otro. Un cambio nuevo no puede romper las pruebas existentes: `dotnet test` debe seguir en verde.
- **Commits y PR:** en español, con identificadores RF/RD de 2 dígitos; nunca push directo a `develop` ni a `main` (rama de trabajo y PR). No fusiones ni etiquetes sin que el usuario lo pida.
- **Criterios primero:** el trabajo de Notificaciones y Reportes se acepta contra `docs/criterios-notificaciones-reportes.md`, que se escribió antes del código.

## Architecture rules (from `.github/copilot/agents/`)

- Dependency flow only inward: `Api → Infrastructure → Application → Domain`. Domain projects have zero `ProjectReference`s; business logic never references EF.
- State machines live in a single domain point (`TransicionesSolicitudPermiso`, `TransicionesHabito`).
- Api: JWT Bearer + roles (`Estandar`, `Administrador`), identity from claims, `GlobalExceptionMiddleware` → `ProblemDetails`, RESTful status codes.

## Reglas Obligatorias para Commits y Código

### Formato de Commits
- **Idioma:** Todos los mensajes de commit DEBEN estar en español.
- **Estructura:** Deben seguir el formato de la sección "Cómo leer este documento" del archivo `docs/requeriments/Requerimientos_Core_PIII.pdf`. Usar identificadores de requisito (ej. `feat: hashear contraseña en registro [RF-CA-02]`).
- **Contenido:** Antes de hacer commit, leer el PDF de requerimientos para asegurar que el commit refleja correctamente las piezas del Core que se modificaron.
- **Prohibido:** Commits con mensajes en inglés, genéricos sin contexto, o que no mencionen el RF/RD correspondiente.

### Validación de Código
- **Antes de cada commit,** ejecutar el agente `core-transversal-reviewer` para validar que el código cumple los 12 requisitos de diseño transversales (RD-01 a RD-12).
- **Revisión obligatoria del archivo** `.github/copilot/agents/core-transversal-reviewer.agent.md`:
  - RD-01: Componentes con responsabilidad única e interfaz explícita.
  - RD-02: Lógica de negocio en capa de servicio/dominio, no en handler HTTP.
  - RD-03: Core no depende del módulo de negocio.
  - RD-04: Transiciones de estado en un único punto del código.
  - RD-05: Contraseñas con hash (bcrypt/argon2/scrypt).
  - RD-06: Autorización verificada en servidor en cada operación protegida.
  - RD-07: Toda entrada externa validada antes de tocar BD o lógica.
  - RD-08: Errores con mensajes genéricos, sin trazas de pila ni rutas.
  - RD-09: Datos persisten en almacenamiento externo.
  - RD-10: Credenciales y claves en variables de entorno, nunca hardcodeadas.
  - RD-11: Fechas con DateTime.UtcNow en todo el sistema.
  - RD-12: Cada pieza con al menos una prueba que corre sin la app completa.

## Lecciones de la Asignación 3 (auditoría de la P5)

- **Un estado terminal guarda su fecha** (cancelado, rechazado, completado) y los reportes lo cuentan hasta ahí; no desaparece de lo que se mide.
- **Lo que se «deja fuera a propósito» no se acepta con un aviso en el historial:** se convierte en un criterio escrito o se corrige. Una prueba que da por buena esa omisión no es evidencia.
- **Una deuda anotada que contradice un RF o un criterio es un hallazgo:** se corrige, no se deja en el ESTADO.
- **Todo «leer y, si no existe, insertar» tolera la carrera** (clave única y, si choca, actualizar), y los criterios nuevos llevan un caso de dos peticiones simultáneas y uno de borde (estado terminal, rango vacío).
- Los criterios definen cada número que se muestra (p. ej. `intentos` cuenta cada envío intentado) y no solo el camino de fallo.
