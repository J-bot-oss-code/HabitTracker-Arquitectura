---
name: contexto-habittracker
description: Punto de entrada de toda sesión en HabitTracker Nexus. Úsala al iniciar, antes de planificar, de explorar el código o de lanzar un agente Explore, y cuando necesites saber dónde vive algo (módulos, endpoints, puertos entre módulos, pruebas, frontend, requisitos RF/RD, reglas de commits, principios de producto). Evita leer el ESTADO completo, el PDF de requisitos o barrer el repositorio.
---

# Contexto rápido de HabitTracker Nexus

Monolito modular .NET 10 (Clean Architecture) + frontend React en `frontend/`. Un host (`Host/WebApi`) y módulos en `Modules/`. Esta skill te pone al día con pocos tokens: **lee solo lo que necesites de la tabla de ruteo**.

## Reglas de ahorro (obligatorias)

1. No leas `docs/ESTADO-DEL-PROYECTO.md` entero si basta con esta skill; su historia está en `docs/historial/`, que solo se abre bajo demanda.
2. No abras los PDF de `docs/requeriments/`: el resumen de cada RF/RD está en `referencias/requisitos.md`.
3. Busca con `Grep` y un `path` acotado (un módulo o una carpeta), no en todo el repo.
4. Lanza un agente Explore **solo** si la pregunta no está cubierta aquí, y dile exactamente qué carpeta mirar.
5. Para el estado actual (endpoints, migraciones, número de pruebas, contratos propuestos) usa `referencias/inventario.generado.md`, que sale del código.
6. Si algo de aquí resulta falso, corrígelo en la misma rama: la skill vale lo que valga su exactitud.

## Tabla de ruteo

| Si necesitas… | Abre |
|---|---|
| Estado actual, tags, pruebas, entorno local, deuda, próximos pasos | `docs/ESTADO-DEL-PROYECTO.md` |
| Qué pide un RF o RD, y qué ID usar en un commit | `referencias/requisitos.md` |
| Módulos, cableado, middleware de errores, puertos, workers, configuración, pruebas del backend | `referencias/backend.md` |
| Features, contratos reales y propuestos, mocks, guards, agentes del frontend | `referencias/frontend.md` |
| Pasos para tareas repetidas (endpoint, entidad, migración, puerto, worker, feature, PR) | `referencias/recetas.md` |
| Principios de producto P1–P8, lo prohibido en motivación, contenido local | `referencias/producto.md` |
| Endpoints, migraciones, conteo de pruebas, contratos propuestos (generado) | `referencias/inventario.generado.md` |
| Plan vigente del módulo de negocio (fases 0–9) | `docs/plan-habittracker-v2.md` |
| Máquina de estados del hábito | `docs/maquina-de-estados.md` |
| Historia de fases anteriores (Control de Acceso v2, Práctica 4, frontend) | `docs/historial/` |
| Reglas del frontend | `frontend/AGENTS.md` |

## Reglas de oro

- **Tags de entrega** (`practica-*`, `asignacion-*`, `*-defecto-*`): nunca se mueven ni se reescriben; GitHub lo impide con un ruleset.
- **Ramas:** trabajo en `feature/`, `fix/`, `docs/`, `refactor/` o `chore/` → PR a `develop` → PR `develop → main`. `develop` y `main` rechazan el push directo. PR con la skill `flujo-pr` (4 secciones). Las PR las fusiona el usuario.
- **Commits** en español: `tipo(ámbito opcional): descripción [RF-XX-NN, RD-NN]`.
  - Regex del CI (`scripts/ci/verificar-commits.sh`): IDs `RF-[A-Z]+-[0-9]{2}` o `RD-[0-9]{2}`, **exactamente 2 dígitos** (`RF-NEG-02.1` falla), entre corchetes al final y separados por `, `.
  - La primera palabra no puede ser inglesa. Usa IDs **existentes** (`referencias/requisitos.md`), nunca inventados.
- **Antes de cada commit:** revisión RD-01..RD-12 con `.github/copilot/agents/core-transversal-reviewer.agent.md`. En el frontend: `frontend-reviewer` y `frontend-design-critic`.
- **CI obligatorio:** `ci-ok` y `seguridad-ok`. Los analizadores de seguridad de .NET y NuGetAudit NU1903/NU1904 son errores: se arregla la causa, nunca se baja la severidad de una categoría.
- **Capas:** `Api → Infrastructure → Application → Domain`. El dominio no tiene `ProjectReference` ni EF. Ningún módulo del Core referencia a HabitTracker (lo verifica `scripts/ci/verificar-reglas-rd.sh`).
- **Entre módulos:** solo por puertos. La interfaz vive en el `Application` de quien la necesita y la implementa un adaptador en `Host/WebApi/Adapters/`. Ningún módulo lee tablas de otro.
- **Fechas:** `DateTime.UtcNow` o `TimeProvider`; `DateTime.Now`/`Today` rompen el CI (RD-11).
- **Secretos:** User Secrets o variables de entorno, nunca en `appsettings*.json` (RD-10).

## Comandos

```bash
dotnet build HabitTrackerSystem.slnx          # 0 avisos: las reglas de seguridad son errores
dotnet test                                   # sin API ni BD externa (SQLite en memoria)
dotnet run --project Host/WebApi/WebApi.csproj   # http://localhost:5246, Swagger en /swagger
cd frontend && npm test && npm run lint && npm run typecheck && npm run build
bash scripts/ci/verificar-reglas-rd.sh
bash scripts/ci/verificar-commits.sh origin/develop HEAD
bash scripts/ci/verificar-contexto.sh         # esta skill sigue siendo cierta
bash scripts/contexto/generar-inventario.sh   # regenerar el inventario tras cambiar el código
```

## Al cerrar una fase o una PR

1. Regenera el inventario (`bash scripts/contexto/generar-inventario.sh`).
2. Actualiza la referencia afectada (backend, frontend, recetas o producto) y el ESTADO, que debe quedar en ≤120 líneas; la historia va a `docs/historial/`.
3. Comprueba con `bash scripts/ci/verificar-contexto.sh`.
