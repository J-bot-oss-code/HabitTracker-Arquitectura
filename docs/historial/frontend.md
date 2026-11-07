# Historial — Frontend (monorepo)

> Movido desde `docs/ESTADO-DEL-PROYECTO.md` (§10 y §10.1) el 07-11-2026, sin cambios de contenido. El estado actual está en el ESTADO.

## Frontend (monorepo)

> Añadido el 30-10-2026 en la rama `feature/frontend-base`. No modifica el backend: los tags de entrega y los archivos de `Host/`, `Modules/` y `tests/` quedan intactos.

- Ubicación: `frontend/`. Stack: React 19, Vite 8, TypeScript estricto, React Router 7, TanStack Query 5, react-hook-form + zod, Tailwind 4, MSW 2 y Vitest + Testing Library. Guía: `frontend/README.md`. Reglas: `frontend/AGENTS.md`.
- Pantallas funcionales (lo que ya entrega el backend):
  - Control de acceso: registro, activación, login, logout, recuperación, restablecimiento y cambio de contraseña.
  - Hábitos: lista con filtros, creación, transiciones, avance, historial y reconfiguración con permiso.
  - Permisos: solicitud y bandeja del Administrador con aprobación y rechazo.
  - Administración de usuarios.
- Pantallas "Próximamente" con sus RF: Documentos, Notificaciones, Cola de correos, Reportes y Auditoría, que incluyen RF-NEG-07/08/10 (`frontend/src/features/coming-soon/piezas.ts`).
- Datos: API simulada con MSW (`frontend/src/mocks`) que replica el contrato y las reglas de `Host/WebApi`. Para conectar el backend real: `VITE_USE_MOCKS=false` más **CORS en `Program.cs`**, que hoy no está configurado. Este último es un cambio del backend pendiente.
- Pruebas: `cd frontend && npm test` ejecuta 222 pruebas (unitarias, flujos de UI por rol y contrato de la API simulada) sin backend.
- Modo demo sin Service Worker (30-10-2026): en el navegador, la API simulada se resuelve dentro de la página (`frontend/src/mocks/fetchSimulado.ts` envuelve `fetch` con los handlers de MSW). Así, el login demo funciona en modo privado, tras una recarga forzada y en navegadores que bloquean los service workers. Un recorrido E2E en Edge real cubre 26 flujos de los dos roles.
- Agentes del frontend en `.claude/agents/frontend-*.md`: architect, api-contract, ui-builder, test-writer y reviewer. El flujo está en `frontend/AGENTS.md`.
- Contrato observado al construirlo:
  - `GET /api/permissions` usa `soloPendientes=true` por defecto;
  - el login usa `email` y el resto de endpoints `correo`;
  - el rol llega en el claim URI largo;
  - no existe `GET /api/habits/{id}` ni un listado de metas, así que el `metaId` se escribe a mano.
- Rediseño 2026 (30-10-2026): identidad deportiva-futurista con tokens semánticos y cinco temas (Sistema, Claro, Oscuro, Cálido, Estadio) guardados en el navegador. Componentes nuevos en `shared/ui`: anillo de progreso, estadísticas tipo marcador, mini barras, filtros segmentados y diálogo de confirmación. Métricas de presentación (racha, avance por día) en `features/habits/estados.ts`; la regla de completar sigue siendo del servidor.
- Calidad del diseño: `frontend-design-critic` revisa cada cambio (2 rondas sobre el rediseño; sin bloqueantes ni altas al cierre). Las pruebas de contraste (`shared/theme/contraste.test.ts`) exigen WCAG 2.2 AA en todos los temas. Navegación apta para 360 px y acciones de alto impacto con confirmación.
- Seguridad: `ciberseguridad-pentester` y el plugin `claude-security` están reservados para ejecutarse al final, antes de integrar con el backend.
- Deuda del frontend: `npm audit` (completo) marca un aviso moderado de `minimatch` en las herramientas de lint (solo desarrollo, 0 en producción); se resolverá al actualizar ESLint.

### Frontend de producción (rama `feature/frontend-nexus`)

> Añadido el 03-11-2026. Sigue sin modificar el backend (`Host/`, `Modules/`, `tests/` intactos): el backend no se toca hasta terminar las prácticas del maestro, y después se adaptará a este frontend.

- **Plan y bitácora:** `frontend/docs/PLAN-FRONTEND-NEXUS.md` (checklist por paso, equivalencia de tokens, desviaciones de la especificación y la tabla **"Pendientes para el backend"**, que es la base del documento ORD y del README que se redactarán al terminar las prácticas). La especificación es `docs/mejoras/diseno-ui-web-movil.md`; el banco de propuestas, `docs/mejoras/mejoras-y-funcionalidades.md`.
- **Identidad:** sustituye al rediseño deportivo-futurista de §10 por la especificación sobria (acento índigo, Plus Jakarta Sans, sin degradados ni brillos) y reduce los temas a **Sistema, Claro y Oscuro**. El contraste WCAG 2.2 AA de cada tema y de los niveles del mapa de constancia lo vigila `shared/theme/contraste.test.ts`.
- **Pantallas nuevas:** landing pública con catálogo de metas y calculadora de calorías; registro con consentimiento legal; onboarding; panel **Hoy** (inicio del Estándar); **Progreso** (peso, medidas, constancia, récords y logros); **Centro de privacidad** (descarga de datos, consentimientos y baja); **Resumen** y **Auditoría** del Administrador.
- **Contratos propuestos (sin backend):** perfil, consentimientos, peso, medidas, exportación y baja, métricas y auditoría. Están simulados en `src/mocks/handlers/propuestas/` y registrados en `CONTRATOS_PROPUESTOS` (una prueba exige que coincidan). Con el backend real (404 sin cuerpo) esas pantallas muestran "llegará pronto" en lugar de fallar (`esNoDisponible`). Todo lo demás (hábitos, permisos, usuarios, login) usa los endpoints reales.
- **Pruebas:** `cd frontend && npm test` ejecuta 452 pruebas sin backend (Vitest limitado a 4 trabajadores para que la suite sea estable). El backend sigue en 106.
- **Revisión:** cada paso pasó por `frontend-reviewer` y, en rondas, por `frontend-design-critic`; la última ronda midió en Edge real que no hay desborde horizontal a 360 px.
- **Commits del trabajo:** `9101919` (docs), `c2b6c4c` (identidad), `fbba9bd` (pública), `aa80c85` (alta), `9eec324` (Hoy), `289f08f` (Progreso), `b2562e5` (privacidad) y `62f411a` (administración).
- **Pendiente:** el recorrido manual a 360 px y 1280 px con `npm run dev`; la auditoría de seguridad con `ciberseguridad-pentester` (sigue reservada para antes de integrar con el backend); la edición del perfil (rectificación), reaceptar versiones nuevas de los documentos y una vista de tarjetas de la auditoría en móvil.
