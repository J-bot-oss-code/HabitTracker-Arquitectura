# Plan del frontend — HabitTracker Nexus (visión de producción)

> Rama: `feature/frontend-nexus` (desde `feature/frontend-base`). Inicio: 02-11-2026.
> Fuentes: [`docs/mejoras/diseno-ui-web-movil.md`](../../docs/mejoras/diseno-ui-web-movil.md) (pantallas, tokens, componentes) y [`docs/mejoras/mejoras-y-funcionalidades.md`](../../docs/mejoras/mejoras-y-funcionalidades.md) (acceso, privacidad, prioridades).

## Alcance

- **El backend no se modifica** hasta terminar las prácticas del maestro. El frontend muestra cómo debe verse la aplicación en producción; después, el backend se adaptará a él.
- **Validaciones:** si el backend ya valida un campo, el frontend usa exactamente sus límites. Si el campo aún no existe en el backend, el límite se define una vez en el `schemas.ts` de la feature y se marca como `// Límite propuesto para el backend (mejora futura)`.
- **Contratos propuestos:** las pantallas sin backend usan la API simulada (MSW) en `src/mocks/handlers/propuestas/`, registrada en `CONTRATOS_PROPUESTOS`. Con `VITE_USE_MOCKS=false`, esas pantallas muestran "Disponible cuando el backend entregue…" en lugar de fallar (`esNoDisponible`).
- Lo que no se pueda implementar del Core en el módulo de negocio se documentará en un ORD y en el README **después** de las prácticas. La sección "Pendientes para el backend" de este archivo es la fuente para ese documento.

## Checklist

- [x] **Paso 0** — Rama, línea base (backend 106/106, frontend 222/222) y este plan.
- [x] **Paso 1** — Identidad de la especificación (tokens, Plus Jakarta Sans, temas Claro, Oscuro y Sistema; sin halos ni degradados), componentes base reestilizados (`Estadistica` con barra, `BarraProgreso`, insignia Admin), cliente con `put`/`delete`, `esNoDisponible` y registro de contratos propuestos con su prueba. Cada ruta nueva se registra en el paso que construye su pantalla, para que ningún commit deje rutas vacías.
- [x] **Paso 2** — Landing pública en `/` con `PublicLayout` (cabecera con menú móvil y pie legal), catálogo de metas con filtro por área (`/metas`), calculadora de calorías Mifflin-St Jeor sin guardar nada (`/calculadoras`) y documentos legales versionados (`/legal/:documento`: términos v2.1, privacidad v1.4, cookies, salud). Con sesión, la cabecera lleva al panel del rol.
- [ ] **Paso 3** — Registro con consentimiento legal y onboarding de 4 pasos. _(Crítica de diseño, ronda 1.)_
- [ ] **Paso 4** — Panel "Hoy".
- [ ] **Paso 5** — Progreso y estadísticas: peso, medidas, constancia, récords. _(Crítica de diseño, ronda 2.)_
- [ ] **Paso 6** — Centro de privacidad: descarga de datos, consentimientos y baja.
- [ ] **Paso 7** — Panel de administración: métricas anónimas y auditoría. _(Crítica de diseño, ronda 3 + WCAG 2.2 AA.)_
- [ ] **Cierre** — `frontend/AGENTS.md`, `frontend/README.md`, `docs/ESTADO-DEL-PROYECTO.md` §10 y recorrido manual a 360 px y 1280 px.

Cada paso cierra con `typecheck`, `lint`, `format:check`, `test` y `build` del frontend, `dotnet test` (106 en verde), `git diff --stat -- Host Modules tests` vacío, revisión RD-01..RD-12 y un commit en español con sus RF/RD.

## Tokens: especificación → tokens semánticos

Los componentes usan nombres semánticos (`bg-superficie`, `text-atenuado`…) definidos en `src/index.css`. Así se conserva la estructura del código y el tema Oscuro redefine los mismos nombres.

| Especificación (§2.1)         | Token (`--c-*` / clase Tailwind)            | Claro                                   |
| ----------------------------- | ------------------------------------------- | --------------------------------------- |
| `--color-bg`                  | `fondo`                                     | `#F7F8FB`                               |
| `--color-surface`             | `superficie`                                | `#FFFFFF`                               |
| `--color-surface-muted`       | `superficie-2`                              | `#F8FAFC`                               |
| `--color-border`              | `borde`                                     | `#E2E8F0`                               |
| `--color-border-strong`       | `borde-fuerte`                              | `#CBD5E1`                               |
| — (ver nota)                  | `control`                                   | `#64748B`                               |
| `--color-text`                | `texto`                                     | `#0F172A`                               |
| `--color-text-secondary`      | `texto-2`                                   | `#334155`                               |
| `--color-text-muted`          | `atenuado`                                  | `#475569`                               |
| `--color-accent`              | `acento`                                    | `#4F46E5`                               |
| `--color-accent-strong`       | `acento-fuerte`                             | `#3730A3`                               |
| `--color-accent-soft`         | `acento-suave`                              | `#EEF2FF`                               |
| `--color-accent-tint`         | `acento-tinte`                              | `#E0E7FF`                               |
| `--color-accent-light`        | `acento-claro`                              | `#A5B4FC`                               |
| `--color-ink-dark`            | `tinta` (+ `tinta-texto`, `tinta-atenuado`) | `#1E1B4B`                               |
| `--color-streak-bg/text`      | `racha-fondo` / `racha-texto`               | `#FEF3C7` / `#B45309`                   |
| `--color-info-bg/border/text` | `nota-fondo` / `nota-borde` / `nota-texto`  | `#FFFBEB` / `#FDE68A` / `#78350F`       |
| `--color-success-bg/text`     | `exito-fondo` / `exito`                     | `#D1FAE5` / `#047857`                   |
| `--color-danger` / `-border`  | `peligro` / `peligro-borde`                 | `#B91C1C` / `#FECACA`                   |
| Calendario (4 niveles)        | `calor-0..3`                                | `#E2E8F0` `#C7D2FE` `#818CF8` `#4F46E5` |
| — (añadido)                   | `hover`                                     | `#F1F5F9`                               |
| — (añadido)                   | `tinta-foco` (vía `.panel-tinta`)           | `#A5B4FC`                               |

**Nota de accesibilidad (`control`):** la especificación usa `#CBD5E1` como borde de inputs y `#94A3B8` como borde de casillas vacías. Sobre blanco dan 1,5:1 y 2,6:1, por debajo del 3:1 que exige WCAG 2.2 (1.4.11) para identificar un control. Por eso inputs, casillas e interruptores usan `control` (`#64748B`, 4,8:1). `borde-fuerte` (`#CBD5E1`) se mantiene para separadores y botones secundarios, que se identifican por su texto.

**Otras decisiones de la revisión del Paso 1:**

- `hover` (`#F1F5F9`): `superficie-2` (`#F8FAFC`) da 1,03:1 frente al blanco y el cambio al pasar el cursor no se veía.
- `.panel-tinta`: sobre la tinta, el índigo del foco da 2,5:1, así que el panel redefine el foco con `tinta-foco` y los enlaces usan `acento-claro`.
- La insignia de aviso usa el par sólido `nota-*`. El ámbar con transparencia daba 4,0–4,3:1 y no cubría AA.
- Al imprimir ("Exportar PDF") se aplican siempre los colores claros, y solo se oculta lo marcado con `data-no-imprimir`.
- Para el Paso 5: entre sí, los niveles `calor-1/2` no llegan a 3:1, así que cada celda del mapa de calor necesita una alternativa en texto.

Tipografía: Plus Jakarta Sans 600–800 (títulos y cifras) e Inter (cuerpo), ambas empaquetadas con `@fontsource`, sin CDN. Radios: inputs y botones 12 px, tarjetas 16 px, destacadas 20 px, chips 999 px. La única sombra es `shadow-flotante`, que se usa en la tarjeta del hero y en las capas flotantes (modal, toast, menú).

**Desviaciones de la especificación en el Paso 2:**

- La cabecera pública no incluye "Ejercicios": la biblioteca de ejercicios no existe todavía (mejoras §4.3).
- "Ver plan" pasa a "Empezar este plan", porque lleva al registro con la meta y no a un detalle del plan.

## Pendientes para el backend (mejoras futuras)

Se completa en cada paso. Cada fila es una necesidad del frontend que hoy resuelve la API simulada o que se omite.

| Pieza                              | Qué falta en el backend                                                                                           | Cómo lo resuelve hoy el frontend                      | Origen               |
| ---------------------------------- | ----------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------- | -------------------- |
| CORS                               | `Program.cs` no permite el origen del frontend                                                                    | Solo funciona con la API simulada                     | `frontend/AGENTS.md` |
| Catálogo de metas y plantillas     | `PlanTemplate` y un `GET` público del catálogo; el `slug` como identificador estable                              | Catálogo estático en `features/publico/catalogo.ts`   | mejoras §4.1         |
| Documentos legales versionados     | Textos y versiones servidos por el Manejador de documentos                                                        | Textos de demostración en `features/publico/legal.ts` | mejoras §3, §6.2     |
| Perfil físico y plan               | `GET/PUT /api/profile` con los límites de `LIMITES_CORPORALES` y `LIMITES_PLAN`                                   | Contrato propuesto (Paso 3)                           | mejoras §3           |
| Descarga de datos y baja de cuenta | Exportación completa y `AccountDeletionRequested` con periodo de gracia; la landing y los términos ya lo prometen | Contrato propuesto (Paso 6)                           | mejoras §7.1         |
