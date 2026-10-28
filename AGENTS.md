# AGENTS.md — HabitTracker-Arquitectura

.NET 10 modular monolith (Clean Architecture). Single host `Host/WebApi` + modules under `Modules/`.

## Solution / projects

- Solution is `HabitTrackerSystem.slnx` (new XML format, not `.sln`).
- Each module has `<Module>.Domain` ← `<Module>.Application` ← `<Module>.Infrastructure`, wired in `Host/WebApi/Extensions/ServiceExtensions.cs`.
- Implemented modules: `AccessControl` (Práctica 1), `Permissions` (Práctica 2), `HabitTracker` (Práctica 3). `Auditing`, `DocumentManager`, `Notifications`, `Reports` are empty skeletons.
- Each module owns its DbContext/migrations (SQL Server); no cross-module table access — modules talk through Application interfaces (e.g. HabitTracker `IPermisoService` → `PermisoServiceAdapter`).
- All projects: `net10.0`, `Nullable enable`, `ImplicitUsings enable`. Requires .NET 10 SDK.

## Commands

- `dotnet build HabitTrackerSystem.slnx`
- `dotnet test` — xUnit + Moq suites in `tests/`; no API or external DB needed (SQLite in memory).
- `dotnet run --project Host/WebApi/WebApi.csproj` (`http://localhost:5246`, Swagger at `/swagger`). Secrets (connection string, `JwtSettings:*`, SMTP) via User Secrets/env vars — see README.
- Migrations: `dotnet ef database update --project Modules/<M>/<M>.Infrastructure --startup-project Host/WebApi --context <Ctx>` (`ApplicationDbContext`, `PermissionsDbContext`, `HabitTrackerDbContext`).
- Tags `practica-N` / `practica-N-defecto-M` are graded deliverables — never move or rewrite them.

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
