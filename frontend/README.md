# HabitTracker Nexus — Frontend

Interfaz web de HabitTracker Nexus. Cubre las piezas que el backend ya implementa:

- **Control de acceso:** registro, activación, inicio y cierre de sesión, recuperación y cambio
  de contraseña.
- **Hábitos:** crear, máquina de estados, registrar avance y reconfiguración avanzada.
- **Gestión de permisos:** solicitar, aprobar y rechazar.
- **Administración de usuarios.**

Las piezas que el backend aún no entrega aparecen como pantallas **Próximamente**.

Por defecto (`npm run dev`) usa una **API simulada (MSW)** con el mismo contrato que `Host/WebApi`. Con
`VITE_USE_MOCKS=false` llama al backend real: **Control de Acceso ya está conectado** (registro con
consentimientos, activación, inicio de sesión, sesión con refresh token y cierre de sesión); el resto de
pantallas sigue con la API simulada hasta que su módulo se conecte.

## Requisitos

- Node.js 20 o superior

## Uso

```bash
cd frontend
npm install
npm run dev        # http://localhost:5173 (API simulada)
npm test           # pruebas sin backend
npm run build      # compilación de producción en dist/
```

Otros scripts: `npm run lint`, `npm run typecheck`, `npm run format`, `npm run test:watch`.

## Modo demostración (API simulada)

Los datos viven en memoria y se reinician al recargar la página.

| Usuario             | Contraseña | Rol                                      |
| ------------------- | ---------- | ---------------------------------------- |
| `estandar@demo.com` | `Demo1234` | Estándar (tiene hábitos de ejemplo)      |
| `admin@demo.com`    | `Demo1234` | Administrador                            |
| `otro@demo.com`     | `Demo1234` | Estándar (tiene una solicitud pendiente) |
| `inactiva@demo.com` | `Demo1234` | Estándar, cuenta sin activar             |

- Código de activación (cuentas nuevas e `inactiva@demo.com`): `DEMO-ACTIVACION`.
- Código de recuperación de contraseña: `DEMO-123456`.
- Id de meta para crear hábitos: `3f2b8c1e-6a4d-4e9b-9c7a-1d2e3f4a5b6c`.

Recorrido sugerido:

1. Entra como `estandar@demo.com`, abre **Leer**, registra un avance y completa el hábito.
2. Intenta la reconfiguración avanzada y solicita el permiso.
3. Entra como `admin@demo.com` y aprueba la solicitud.
4. Vuelve como estándar y reconfigura el hábito.

## Apariencia y temas

Diseño sobrio según la especificación `docs/mejoras/diseno-ui-web-movil.md`: «la hoja del día»: tinta verde abeto sobre papel, un subrayador amarillo-verde solo para lo hecho, esquinas casi rectas, listas regladas con la hora al margen y cifras grandes. Tres temas — **Sistema** (por defecto), **Claro** y **Oscuro** — que se eligen desde el icono de la cabecera o en **Configuración → Apariencia**. La elección se guarda en el navegador y se aplica antes del primer pintado.

Los colores viven en tokens `--c-*` de `src/index.css` y cada tema cumple WCAG 2.2 AA (lo verifica `npm test`). Las fuentes (Bricolage Grotesque y Atkinson Hyperlegible Next) se empaquetan con la app: no hay peticiones a CDN externas.

## Configuración

Copia `.env.example` a `.env.local` (no se versiona):

| Variable                | Por defecto                     | Uso                                                                           |
| ----------------------- | ------------------------------- | ----------------------------------------------------------------------------- |
| `VITE_USE_MOCKS`        | `true` en dev, `false` en build | `false` para llamar al backend real                                           |
| `VITE_API_URL`          | `http://localhost:5246`         | URL de `Host/WebApi`                                                          |
| `VITE_GOOGLE_CLIENT_ID` | vacío                           | ID de cliente de Google (público); el mismo que `Google:ClientId` del backend |

Todo lo que empieza por `VITE_` termina en el bundle público: nunca pongas credenciales ahí.

### Conectar con el backend real

1. Levanta el backend (`dotnet run --project Host/WebApi/WebApi.csproj`, o `docker compose up --build`).
   Su CORS ya permite `http://localhost:5173` y `http://localhost:5174` (en Docker, la variable `FRONTEND_URL`).
2. En `frontend/.env.local`: `VITE_USE_MOCKS=false` y `VITE_API_URL=http://localhost:5246` (o `:8080` en Docker).
3. `npm run dev` y abre `http://localhost:5173`.

**Continuar con Google.** Con `VITE_GOOGLE_CLIENT_ID` el botón de Google Identity Services aparece en
`/login` y `/registro` y en «Cuentas vinculadas» de Configuración › Inicio de sesión. Si el correo de Google es nuevo se piden los
documentos legales y se crea la cuenta; si ya tiene una cuenta con contraseña se pide confirmarla para
vincular; si ya está vinculada entra directo. Sin el ID el botón queda desactivado y con `VITE_USE_MOCKS=true`
es un botón de demostración que pide el correo de una cuenta de Google simulada. Para crear el ID de cliente
(origen autorizado `http://localhost:5173`), ver el README raíz › Inicio de sesión con Google.

**Sesión.** El token de acceso (JWT, 15 minutos) vive solo en memoria. La sesión larga la sostiene el
refresh token, que el navegador guarda en una cookie `HttpOnly` que el JavaScript no puede leer: al recargar
la página se restaura con `POST /api/auth/refresh` y el token se renueva solo antes de vencer. Por eso las
peticiones van con `credentials: 'include'` y el refresh lleva la cabecera `X-HabitTracker-Csrf: 1`.
La contraseña nueva debe tener **12 caracteres o más** (letras y números) y no aparecer en filtraciones
conocidas; el código de activación son 6 dígitos y se envía junto al correo.

## Stack

| Área          | Herramientas                                       |
| ------------- | -------------------------------------------------- |
| Base          | React 19, Vite 8, TypeScript (estricto)            |
| Rutas y datos | React Router 7 (rutas perezosas), TanStack Query 5 |
| Formularios   | react-hook-form + zod                              |
| Estilos       | Tailwind CSS 4                                     |
| API simulada  | MSW 2                                              |
| Pruebas       | Vitest + Testing Library                           |
| Calidad       | ESLint + Prettier                                  |

La estructura, las reglas y el equipo de agentes están en [`AGENTS.md`](AGENTS.md).

## Pantallas y estado del plan

El plan por pasos, las desviaciones de la especificación y los pendientes del backend están en `docs/PLAN-FRONTEND-NEXUS.md`. Las pantallas que aún no tienen endpoint (perfil, peso y medidas, métricas y auditoría) funcionan con la API simulada y se anuncian como "llegará pronto" si el servidor real no las tiene.
