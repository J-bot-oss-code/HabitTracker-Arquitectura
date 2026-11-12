# Frontend (`frontend/`)

> Reglas completas en `frontend/AGENTS.md`. Features, conteo de archivos y contratos propuestos: `inventario.generado.md`.

## Stack y comandos

- React 19, Vite, TypeScript estricto, React Router 7, TanStack Query 5, react-hook-form + zod, Tailwind 4, MSW 2 y Vitest + Testing Library.
- En Windows, ejecuta los comandos desde una terminal con la unidad en mayúscula (`C:\…`): con `c:\…` Vitest falla en todas las suites («failed to find the runner»).
- Comandos: `npm test`, `npm run lint`, `npm run typecheck`, `npm run build` y `npm run format:check`. Este último falla en Windows por CRLF; en el CI pasa.
- Variables (`src/shared/config/env.ts`):
  - `VITE_USE_MOCKS`: `false` para usar el backend real;
  - `VITE_API_URL`;
  - `VITE_GOOGLE_CLIENT_ID`.
- Contra la API local sin tocar `.env.local`: `VITE_API_URL=http://localhost:5246 npx vite --port 5174`. El puerto 5174 está en el CORS y en los orígenes de Google.

## Estructura

- `src/features/<feature>/` con `api.ts`, `types.ts`, `schemas.ts` (zod), `components/`, `pages/` y sus pruebas. Una feature solo importa el `api.ts` o el `types.ts` de otra; `shared/` nunca importa `features/`.
- `src/shared/api/http.ts` hace los fetch con `credentials: 'include'`. Ante un 401 renueva una vez (`sesion.ts`, una renovación compartida con la cabecera `X-HabitTracker-Csrf: 1`) y repite la petición.
- `src/shared/api/problem.ts`:
  - `mensajeDeError` traduce `ProblemDetails.codigo` a un mensaje;
  - `esNoDisponible` toma un 405 o un **404 sin cuerpo** como "endpoint inexistente" y muestra "llegará pronto". Por eso un 404 real del backend siempre lleva `ProblemDetails`.
- `src/shared/auth/tokenStorage.ts` guarda el token de acceso **en memoria**; la sesión larga la sostiene la cookie HttpOnly.
- `src/shared/lib/fechas.ts`: fechas UTC. Las de los hábitos llegan con `Z`; `parsearFechaUtc` acepta también fechas sin zona.
- `src/app/guards.tsx`: `RequierePerfil` lleva a `/bienvenida` si `GET /api/profile` responde 404 **con** ProblemDetails. Envuelve `/hoy`, `/progreso`, `/habitos` y `/permisos`.
- `src/app/navigation.ts` define el menú (Estándar: Hoy, Mis hábitos, **Planes**, Progreso, Mis permisos) y las secciones de Configuración (`/ajustes`: Cuenta, Contraseña y seguridad, Inicio de sesión, Privacidad y datos, Apariencia). No hay sección de Notificaciones ni de Perfil todavía.

## Features relevantes para el negocio

| Feature | Qué hay | Backend |
|---|---|---|
| `habits` | Lista, crear (sin meta), detalle (`GET /api/habits/{id}`), transiciones, avance, reconfigurar, **Hoy** (`hoy.ts`) y métricas de racha y logros calculadas en el navegador (`estados.ts`, `progreso.ts`) | Real: `/api/habits` |
| `perfil` | Onboarding de **3 toques** `/bienvenida` (jornada → zona horaria → aviso de salud; no pide datos de salud), Configuración › **Perfil y salud** `/ajustes/perfil-salud` (perfil nivel 2 y cuestionario por bloques, aviso para activar `datosSalud`) | Real: `/api/profile`, `/api/profile/health/*` |
| `progreso` | Peso, medidas, constancia, récords y logros | Real: `/api/progress/weights` y `/measurements` |
| `planes` | Catálogo de 13 planes (`CatalogoPlanes` con filtros por nivel y área), detalle `/metas/:slug` y `/planes/:slug` (aviso de seguridad, tareas con versión mínima, guía semanal, «Elegir este plan»), página **Planes** `/planes` (recomendados con sus motivos y «No recomendado para ti» con su razón) y `usePlanResumen(slug)` para quien solo necesita nombre y duración | Real: `/api/plans`, `/api/plans/{slug}`, `/api/plans/recommended` |
| `publico` | Landing (el catálogo le llega por una ranura: `LandingPage catalogo={…}`), calculadora de calorías y textos legales (`legal.ts`: términos `v2.1` y privacidad `v1.4`) | Ninguno |
| `privacidad` | Consentimientos (incluido `datosSalud`), baja y descarga | Real: `/api/privacy/*` |
| `auth` | Registro con consentimientos, activación con código, login, Google y sesiones | Real: `/api/auth/*` |
| `auditoria` | Resumen y auditoría del Administrador | **Propuesto**: `/api/admin/metrics` y `/api/audit` |
| `coming-soon` | Notificaciones, cola de correos, reportes y documentos (`piezas.ts`, con sus RF) | — |

Slugs del catálogo (13, de la API): los 6 Regulares `perder-grasa-sostenible`, `reducir-estres`, `dormir-mejor`, `primeros-5-km`, `ganar-masa-muscular` y `fuerza-en-casa`, y los 7 Básicos `mi-dia-oficina`, `mi-dia-turno-nocturno`, `mi-dia-trabajo-fisico`, `hidratacion-con-calor`, `caminar-para-empezar`, `respiracion-anti-estres` y `desconexion-digital-nocturna`.
- **No hay catálogo estático:** un slug que llega de la URL o de `localStorage` se valida por formato (`esSlugPlan`) y, para mostrar su nombre, contra la lista del servidor (`usePlanResumen`). Un hook nunca va después de un `return` anticipado (`HoyPage` lo rompió una vez).
- **El mock de los planes** (`mocks/handlers/planes.ts`) replica `RecomendadorPlanes` y `ReglasSeguridadPlanes`; sus datos (`planesData.ts`) son una copia de `catalogo-planes.json` que `mocks/planes.test.ts` compara con el JSON del backend. Si cambia el contenido allí, se regenera la copia (no se edita a mano).

## Contratos propuestos (sin backend)

- Se declaran en `src/mocks/handlers/propuestas/*.ts`, se registran en `CONTRATOS_PROPUESTOS` (`propuestas/index.ts`) y se apuntan en "Pendientes para el backend" de `frontend/docs/PLAN-FRONTEND-NEXUS.md`.
- Una prueba (`src/mocks/propuestas.test.ts`) exige que los handlers y el registro coincidan y que ninguna propuesta pise una ruta real.
- Cuando el backend implementa un contrato, sale del registro y su handler pasa a `src/mocks/handlers/<feature>.ts`, replicando las reglas del backend.
- Perfil y progreso ya son handlers reales (`src/mocks/handlers/perfil.ts` y `progreso.ts`): 409 `consentimiento_salud_requerido` (`mensajeDeError` explica dónde dar el permiso), enums del perfil en PascalCase y la evaluación de salud replicada de `EvaluadorSalud`. La descarga de datos es solo la del servidor (sección `perfilYSalud`).

## Peculiaridades del contrato

- El login usa `email` y el resto de los endpoints, `correo`.
- Los enums viajan como texto.
- El rol llega en el claim URI largo.
- `GET /api/permissions` usa `soloPendientes=true` por defecto.
- `/api/habits` es solo para el rol Estandar.
- El registro exige `consentimientos` con las versiones vigentes.
- La contraseña debe tener 12 caracteres o más.

## Agentes del frontend (`.claude/agents/`)

Orden: `frontend-architect` (dónde va) → `frontend-api-contract` (tipos y mocks contra el backend) → `frontend-ui-builder` ⇄ `frontend-design-critic` → `frontend-test-writer` → `frontend-reviewer` (antes del commit). `ciberseguridad-pentester` queda reservado para una auditoría antes de producción.

Calidad que se mide: contraste WCAG 2.2 AA en los tres temas (`src/shared/theme/contraste.test.ts`), sin desborde a 360 px y confirmación en las acciones de alto impacto.
