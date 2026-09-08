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
