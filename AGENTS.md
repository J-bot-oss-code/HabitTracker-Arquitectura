# AGENTS.md — HabitTracker-Arquitectura

Greenfield .NET 10 Clean Architecture skeleton. Only template code exists (`Core.Api/Program.cs` weatherforecast + empty `Class1.cs` placeholders).

## Solution / projects

- Solution is `HabitTrackerSystem.slnx` (new XML format, not `.sln`).
- Projects: `core.Domain` (no deps) ← `Core.Application` ← `Core.Infraestructure`; `Core.Api` is the entrypoint (`Core.Api/Program.cs`).
- Preserve exact names: `core.Domain` (lowercase `c`), `Core.Infraestructure` (misspelled — do not "fix").
- All projects: `net10.0`, `Nullable enable`, `ImplicitUsings enable`. Requires .NET 10 SDK.

## Commands

- `dotnet build HabitTrackerSystem.slnx`
- `dotnet run --project Core.Api` (`http://localhost:5031`, `https://localhost:7002` per `Core.Api/Properties/launchSettings.json`)
- No test projects, lint, or CI configured yet — do not invent commands.

## Architecture rules (from `.github/copilot/agents/`)

- Dependency flow only inward: `Api → Infrastructure → Application → Domain`. `core.Domain` must have zero `ProjectReference`s; business logic never references EF.
- `Core.Api` currently has no `ProjectReference`s (only `Microsoft.AspNetCore.OpenApi 10.0.9`) — add references to Application/Infrastructure when wiring DI. OpenAPI via `AddOpenApi()` / `MapOpenApi()` (dev only).
- Intended stack (not yet implemented): EF Core + Fluent API + migrations, Repository + Unit of Work in Infrastructure implementing Application ports; CQRS/Mediator-style use cases in Application; JWT Bearer + roles/claims, FluentValidation, `ProblemDetails` middleware, RESTful status codes in Api.

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
