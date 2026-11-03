# AGENTS.md — frontend/

SPA de HabitTracker Nexus: React 19 + Vite 8 + TypeScript estricto. Vive en el mismo repo que el
backend (.NET), pero es independiente: **no se modifica nada fuera de `frontend/` y
`.claude/agents/frontend-*.md`** sin que el usuario lo pida. Las reglas generales (commits en
español con RF/RD, flujo de ramas y PRs) están en el `AGENTS.md` de la raíz.

## Comandos (desde `frontend/`)

- `npm install`: requiere Node 20 o superior.
- `npm run dev`: http://localhost:5173 con la API simulada (MSW).
- `npm test`: Vitest + Testing Library contra MSW; no necesita backend.
- `npm run lint`, `npx tsc -b`, `npm run format:check`, `npm run build`.

## Estructura

```
src/
  app/        router (routes.tsx), guards (RequireAuth / SoloInvitados), layouts, navigation, providers
  shared/     api (http + ProblemDetails), auth (JWT, sesión, roles), config (env), lib, ui
  features/   auth · habits · permissions · admin-users · coming-soon
              cada una con types.ts · api.ts · schemas.ts · components/ · pages/ · *.test.ts(x)
  mocks/      API simulada: db.ts (memoria), http.ts (auth, ProblemDetails), handlers/, contrato.test.ts
  test/       setup.ts y render.tsx (renderApp, iniciarSesionComo)
```

- **Dependencias:** `shared` no importa de `features` ni de `app`. Una feature solo usa de otra
  su `api.ts`/`types.ts`.
- **Contrato:** `features/*/types.ts` + `mocks/handlers` replican `Host/WebApi`. El backend es la
  fuente de verdad. Particularidades:
  - el login usa `email`;
  - los enums viajan como texto;
  - el rol llega en el claim URI largo;
  - `soloPendientes` vale `true` por defecto;
  - las fechas de hábitos vienen sin `Z`;
  - `api/habits` es solo para `Estandar`.

## Reglas

- La lógica de negocio está en el servidor (RD-02/RD-06). El frontend refleja reglas solo para
  orientar (p. ej. `habits/estados.ts`) y siempre maneja el rechazo de la API.
- Entradas validadas con zod con los límites del backend (RD-07); errores mostrados con
  `mensajeDeError` (RD-08).
- Nada sensible en `VITE_*` ni en el código (RD-10). Fechas UTC con `shared/lib/fechas` (RD-11).
- Cada feature con pruebas que corren sin backend (RD-12).
- UI e identificadores en español.

## Piezas pendientes del backend

Documentos, Notificaciones, Cola de correos, Reportes y Auditoría (más RF-NEG-07/08/10) son
pantallas "Próximamente" definidas en `features/coming-soon/piezas.ts`. Cuando el backend entregue
una:

1. Ejecutar `frontend-api-contract` para tipar el contrato y crear los handlers MSW.
2. Ejecutar `frontend-architect` para planificar la feature.
3. Implementarla con `frontend-ui-builder`, registrar la ruta perezosa en `app/routes.tsx` y
   quitar `proximamente` en `app/navigation.ts` y la entrada de `piezas.ts`.
4. Pasar siempre por `frontend-design-critic` y atender sus quejas (el builder lo hace como
   parte de su ciclo).
5. Escribir las pruebas con `frontend-test-writer`.
6. Revisar con `frontend-reviewer` antes del commit.

## Equipo de agentes (`.claude/agents/`)

| Agente                     | Rol                                                                | Edita                 |
| -------------------------- | ------------------------------------------------------------------ | --------------------- |
| `frontend-architect`       | Plan de la feature: archivos, capas, rutas, roles, pruebas         | No                    |
| `frontend-api-contract`    | Alinear tipos y mocks con los controllers reales                   | Sí (solo `frontend/`) |
| `frontend-ui-builder`      | Páginas y componentes según la especificación de diseño y temas    | Sí                    |
| `frontend-design-critic`   | Crítica UI/UX tras cada cambio; el builder atiende sus quejas      | No                    |
| `frontend-test-writer`     | Pruebas Vitest/Testing Library                                     | Sí                    |
| `frontend-reviewer`        | Revisión RD + seguridad + aislamiento del backend antes del commit | No                    |
| `ciberseguridad-pentester` | Auditoría de seguridad con ataques locales; solo antes de entregar | No                    |

Flujo: **architect → api-contract → ui-builder ⇄ design-critic → test-writer → reviewer**.
El crítico se ejecuta siempre tras implementar o cambiar una funcionalidad; el pentester y el
plugin `frontend-design` se reservan para el final por su coste en tokens. Los agentes del
backend (`.github/copilot/agents/`) siguen aplicando a `Host/` y `Modules/`.

## Identidad visual

La fuente de verdad es la especificación `docs/mejoras/diseno-ui-web-movil.md`: diseño sobrio
(fondo gris claro, tarjetas blancas con borde sutil, un solo acento índigo, sin degradados ni
sombras pesadas), Plus Jakarta Sans para títulos y cifras e Inter para el cuerpo. Temas Claro,
Oscuro y Sistema sobre tokens semánticos (`src/index.css`); la equivalencia con los tokens de la
especificación y las desviaciones por accesibilidad están en `docs/PLAN-FRONTEND-NEXUS.md`.

## Backend congelado y contratos propuestos

Hasta terminar las prácticas del maestro **no se modifica el backend** (ni CORS). Las pantallas
que necesitan endpoints inexistentes usan contratos propuestos simulados en
`src/mocks/handlers/propuestas/` (registro `CONTRATOS_PROPUESTOS`, verificado por
`mocks/propuestas.test.ts`). Validaciones: idénticas al backend donde ya existen; si no, límites
marcados como "Límite propuesto para el backend (mejora futura)". Cada necesidad se anota en
`docs/PLAN-FRONTEND-NEXUS.md` › "Pendientes para el backend".

## Conectar con el backend real (después de las prácticas)

1. Habilitar CORS en `Host/WebApi/Program.cs` para el origen del frontend. Es un cambio del
   backend y se hace en su propia PR.
2. Crear `frontend/.env.local` con `VITE_USE_MOCKS=false` y `VITE_API_URL=http://localhost:5246`.
3. Ejecutar `frontend-api-contract` para confirmar que no hay diferencias.
