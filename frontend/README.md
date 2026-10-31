# HabitTracker Nexus — Frontend

Interfaz web de HabitTracker Nexus. Cubre las piezas que el backend ya implementa:

- **Control de acceso:** registro, activación, inicio y cierre de sesión, recuperación y cambio
  de contraseña.
- **Hábitos:** crear, máquina de estados, registrar avance y reconfiguración avanzada.
- **Gestión de permisos:** solicitar, aprobar y rechazar.
- **Administración de usuarios.**

Las piezas que el backend aún no entrega aparecen como pantallas **Próximamente**.

Mientras el frontend no se conecta al backend, usa una **API simulada (MSW)** con el mismo
contrato que `Host/WebApi`. Al conectarlo solo cambia una variable de entorno.

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

Diseño deportivo y futurista: cifras tipo marcador, anillos de progreso, rachas y barras de los últimos 7 días. Cinco temas — **Sistema** (por defecto), **Claro**, **Oscuro**, **Cálido** y **Estadio** (alto contraste, neón) — que se eligen desde el icono de la cabecera o en **Mi cuenta → Apariencia**. La elección se guarda en el navegador y se aplica antes del primer pintado.

Los colores viven en tokens `--c-*` de `src/index.css` y cada tema cumple WCAG 2.2 AA (lo verifica `npm test`). Las fuentes (Barlow Condensed e Inter) se empaquetan con la app: no hay peticiones a CDN externas.

## Configuración

Copia `.env.example` a `.env.local` (no se versiona):

| Variable                   | Por defecto                     | Uso                                          |
| -------------------------- | ------------------------------- | -------------------------------------------- |
| `VITE_USE_MOCKS`           | `true` en dev, `false` en build | `false` para llamar al backend real          |
| `VITE_API_URL`             | `http://localhost:5246`         | URL de `Host/WebApi`                         |
| `VITE_META_ID_POR_DEFECTO` | vacío                           | Id de meta que se propone al crear un hábito |

Todo lo que empieza por `VITE_` termina en el bundle público: nunca pongas credenciales ahí.

Para usar el backend real hay que habilitar antes CORS en `Host/WebApi` (ver `AGENTS.md`).

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
