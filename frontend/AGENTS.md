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
  features/   auth · habits · permissions · admin-users · perfil · progreso · privacidad · planes · publico · auditoria · coming-soon
              publico · perfil · progreso · privacidad · auditoria (pantallas de producto)
              cada una con types.ts · api.ts · schemas.ts · components/ · pages/ · *.test.ts(x)
  mocks/      API simulada: db.ts (memoria), http.ts (auth, ProblemDetails), handlers/, contrato.test.ts
  test/       setup.ts y render.tsx (renderApp, iniciarSesionComo)
```

- **Dependencias:** `shared` no importa de `features` ni de `app`. Una feature solo usa de otra
  su `api.ts`/`types.ts`. Única excepción: `BotonGoogle` (`features/auth/google/`), que también usa
  el centro de privacidad para confirmar la baja de una cuenta sin contraseña.
- **Contrato:** `features/*/types.ts` + `mocks/handlers` replican `Host/WebApi`. El backend es la
  fuente de verdad. Particularidades:
  - el login usa `email`;
  - los enums viajan como texto;
  - el rol llega en el claim URI largo;
  - `soloPendientes` vale `true` por defecto;
  - las fechas de hábitos vienen en UTC con `Z` (desde HabitTracker v2; `parsearFechaUtc` acepta también fechas sin zona);
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

La identidad es «la hoja del día» (ver `src/index.css`): tinta verde abeto sobre papel, un subrayador amarillo-verde (`marcador`) solo para lo hecho, esquinas casi rectas (radios 3–9 px), listas regladas con la hora al margen en lugar de tarjetas, sin degradados ni sombras. Bricolage Grotesque en títulos y cifras y Atkinson Hyperlegible Next en el cuerpo. La especificación `docs/mejoras/diseno-ui-web-movil.md` sigue mandando en estructura y componentes; solo cambian los tokens de color, tipografía y radio. Temas Claro,
Oscuro y Sistema sobre tokens semánticos (`src/index.css`); la equivalencia con los tokens de la
especificación y las desviaciones por accesibilidad están en `docs/PLAN-FRONTEND-NEXUS.md`.

## Pantallas de producto (visión de producción)

Construidas según `docs/mejoras/diseno-ui-web-movil.md` y documentadas en `docs/PLAN-FRONTEND-NEXUS.md`:

- Públicas: landing `/`, `/metas`, `/calculadoras` y `/legal/:documento`.
- Alta: `/registro` con consentimientos y `/bienvenida` (onboarding).
- Estándar: `/hoy` (inicio) y `/progreso`.
- Configuración (`/ajustes`, ambos roles; `app/ajustes/AjustesLayout`): Cuenta, Contraseña y seguridad, Inicio de sesión, Privacidad y datos (solo Estándar) y Apariencia. `/cuenta` redirige a `/ajustes/cuenta`. Las secciones salen de `SECCIONES_AJUSTES` (`app/navigation.ts`).
- Administrador: `/admin` (inicio) y `/admin/auditoria`.

Cada pantalla pone su título de pestaña y el foco en su encabezado (`app/layout/RaizApp.tsx`). Las rutas nuevas llevan `handle: { titulo }` en `routes.tsx`.

## Backend y contratos propuestos

El backend ya no está congelado (las prácticas terminaron) y se conecta módulo por módulo. **Control de
Acceso está conectado** (ver abajo). Las pantallas que necesitan endpoints inexistentes siguen usando
contratos propuestos simulados en `src/mocks/handlers/propuestas/` (registro `CONTRATOS_PROPUESTOS`,
verificado por `mocks/propuestas.test.ts`). Validaciones: idénticas al backend donde ya existen; si no,
límites marcados como "Límite propuesto para el backend (mejora futura)". Cada necesidad se anota en
`docs/PLAN-FRONTEND-NEXUS.md` › "Pendientes para el backend". Cuando un módulo se conecta, sus contratos
salen de `CONTRATOS_PROPUESTOS` y los mocks pasan a replicar los controllers reales.

## Conectar con el backend real

1. CORS ya está habilitado en `Host/WebApi` (`Cors:OrigenesPermitidos`, con credenciales). Nunca comodines.
2. Crear `frontend/.env.local` con `VITE_USE_MOCKS=false` y `VITE_API_URL=http://localhost:5246`.
3. Ejecutar `frontend-api-contract` para confirmar que no hay diferencias.

### Contrato de Control de Acceso (ya conectado)

- **Sesión:** el token de acceso (15 min) solo en memoria (`shared/auth/tokenStorage.ts`); el refresh token
  en una cookie `HttpOnly` del backend. `shared/api/http.ts` envía `credentials: 'include'` y, ante un 401
  con token, renueva UNA vez (`shared/api/sesion.ts`, una sola renovación en curso: dos con el mismo refresh
  token cierran la sesión) y repite la petición. `AuthProvider` restaura la sesión al cargar (si hubo una
  antes en el navegador) y renueva el token un minuto antes de que venza.
- **Registro:** `POST /api/auth/register` exige `consentimientos {terminosVersion, privacidadVersion,
marketing}` con las versiones vigentes; sin ellos no se crea la cuenta. **Activación:** `POST
/api/auth/activate` con `{correo, codigo}` (6 dígitos); el enlace `/activar?correo=...&codigo=...` rellena
  el formulario pero NO activa solo. **Contraseña:** 12 a 1024 caracteres con letras y números.
- **Errores:** los rechazos de Control de Acceso traen `codigo` en el ProblemDetails (`cuenta_no_activa`,
  `cuenta_bloqueada`, `clave_actual_incorrecta`, `sesion_invalida`...); ver README raíz › "Control de
  Acceso: errores, límites y CORS". Una contraseña actual incorrecta es un 400, no un 401.
- **Inicio con Google (conectado):** `features/auth/google/`. El ID token lo entrega Google Identity Services
  (`BotonGoogleReal`, script de `accounts.google.com/gsi/client` cargado una vez en `gis.ts`) y la API lo valida.
  `useEntradaGoogle` orquesta `POST /api/auth/google`: 200/201 abre sesión, `400 consentimiento_requerido`
  pide los documentos y repite, `409 vinculacion_requerida` pide la contraseña (`/google/vincular`). «Cuentas
  vinculadas» (`GET /api/auth/vinculos`, `DELETE /api/auth/vinculos/google`) vive en Configuración › Inicio de sesión; una cuenta sin
  contraseña no puede desvincular Google y ve un enlace para crearla con la recuperación. `/google/vincular` está
  en `RUTAS_SIN_RENOVACION`: su 401 es «contraseña incorrecta», no «token vencido». Con mocks, el botón es una
  demostración (`window.prompt`) y el «ID token» es `mock-google|sub|correo|nombre|1` (`tokenDemo.ts`): nunca
  llega al backend real.
- **Planes (conectado, HabitTracker v2 Fase 3):** `features/planes/`. El catálogo de 13 planes viene de la API: `GET /api/plans` y `/{slug}` (anónimos, caché de 5 min) y `GET /api/plans/recommended` (Estándar). Pantallas: catálogo en la landing y en `/metas` (`CatalogoPlanes`, filtros por nivel y área), detalle en `/metas/:slug` y `/planes/:slug` (aviso de seguridad, tareas con su versión mínima y guía semanal) y **Planes** `/planes` (recomendados con sus motivos y «No recomendado para ti» con su razón). La landing recibe el catálogo por una ranura (`LandingPage catalogo={…}`) porque una feature no importa componentes de otra. Un slug que llega de la URL o de `localStorage` se valida por formato (`esSlugPlan`) y, para mostrar su nombre, contra la lista del servidor (`usePlanResumen`). «Empezar este plan» abre «¿Cuándo lo harás?» (ancla y hora) y llama a `POST /api/my-plans` (`planes/components/EmpezarPlanDialogo.tsx`). El slug que la persona eligió en la landing se recuerda hasta el onboarding, que termina en la ficha de ese plan. Los hooks van siempre antes de un `return` anticipado.
- **Mis planes (conectado, HabitTracker v2 Fase 4):** `features/mis-planes/` (`api.ts`, `types.ts`, `schemas.ts`). `GET /api/today` alimenta Hoy (`habits/components/TareaDePlanFila.tsx` con «Hecho» y «Hoy solo lo mínimo», que registra `cantidad: 1` con `esVersionMinima`; `AvisosDeHoy.tsx` con la celebración semanal y la sugerencia de ajuste, que nunca se aplica sola). Los hábitos sueltos quedan en «Otros hábitos». «Mis planes» vive arriba de `/planes` y su detalle en `/mis-planes/:id` (pausar con motivo, reanudar, ajustar, recordatorio, completar y dejar el plan con confirmación). Progreso lee `GET /api/progress/summary` (logros y «tu mes en números»). El perfil ya no tiene `metaSlug` ni `inicioPlan`. En las pruebas el plan de ejemplo no se siembra: usa `sembrarPlanDemo()` de `mocks/db.ts`. Sin rojo ni textos de culpa (P3/P4).
- **Sesiones activas (conectado):** Configuración › Inicio de sesión › «Sesiones activas» (`features/auth/components/SesionesActivas.tsx`):
  `GET /api/auth/sesiones`, `DELETE /api/auth/sesiones/{id}` (404 si no es tuya) y `POST /api/auth/logout-todas`.
  «Cerrar las demás» cierra una por una; «Cerrar en todos los dispositivos» corta también este navegador.
- **Centro de privacidad (conectado):** `/api/privacy/*`. Consentimientos (`GET/PUT /consents`, para cualquier
  usuario autenticado); baja (`POST /deletion-request {modo: desactivar|eliminar, password | idTokenGoogle}` → 202
  `{modo, borradoEn}`, `GET`/`DELETE` para consultar o cancelar la programada, 30 días de gracia); descarga
  (`GET /export`, el servidor la reúne y la audita). Solo el rol Estándar (el Administrador recibe 403). Una
  confirmación incorrecta es SIEMPRE un 400 (un 401 cerraría la sesión) y cuenta para el bloqueo; una cuenta sin
  contraseña confirma con Google. `AvisosDeCuenta` (en `AppLayout`) muestra `requiereAceptarDocumentos` y la baja
  programada con «Cancelar eliminación». Las cuentas desactivadas por su dueño se reactivan al iniciar sesión.
- **Pendiente de conectar:** perfil físico, peso y medidas, métricas de administración y el resto de módulos
  (siguen simulados en `mocks/handlers/propuestas/`).
