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
- [x] **Paso 3** — Registro con consentimiento legal y onboarding (3 pantallas: pasos 3 a 5 del alta). _(Crítica de diseño, ronda 1.)_
- [x] **Paso 4** — Panel "Hoy" en `/hoy` (destino tras iniciar sesión y tras el onboarding del Estándar): fecha y saludo, 4 estadísticas, tareas del día con "Marcar hecha" y "Registrar cantidad", próxima tarea y tira de la semana. Usa los hábitos reales; el perfil propuesto solo aporta el nombre y el plan.
- [x] **Paso 5** — Progreso y estadísticas en `/progreso`: peso con gráfica Real frente al Plan y selector de periodo, medidas Inicio/Hoy, fotos (próximamente), mapa de constancia de 10 semanas, récords y logros. Peso y medidas son contratos propuestos; la constancia, los récords y los logros salen de los hábitos reales. _(Crítica de diseño, ronda 2.)_
- [x] **Paso 6** — Centro de privacidad en `/ajustes/privacidad`: descarga de datos (JSON armado en el navegador), consentimientos con interruptores (retirar los datos de salud pide confirmación), y baja con contraseña (desactivar o eliminar con 30 días de gracia). Contratos propuestos de consentimientos, exportación y baja; la auditoría registra descarga y baja.
- [x] **Paso 7** — Administración: `/admin` (inicio del Administrador) con indicadores agregados y anónimos, planes más iniciados y auditoría reciente; `/admin/auditoria` con filtros por usuario, acción y fechas, y exportación a CSV. Contratos propuestos de métricas y auditoría.
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

**Desviaciones y notas del Paso 7:**

- Resumen y Auditoría viven en `features/auditoria` (una sola feature de administración) en lugar de dos, para que la tabla de eventos se comparta sin importar entre features. Las piezas de `piezas.ts` (RF-AUD) se conservan como requisitos pendientes del backend.
- Las métricas son solo conteos y porcentajes: ninguna cifra identifica a una persona. "% con Google" no se muestra (no hay Google). La retención se calcula con los inicios de sesión auditados.
- La auditoría es de solo lectura: el contrato propuesto no tiene rutas para modificar ni borrar (RF-AUD-06). Registra registro, inicio de sesión, cambios de rol y estado, resolución de permisos, descarga y baja con valor anterior y nuevo (RF-AUD-01).
- El CSV neutraliza las fórmulas (=, +, -, @) y lleva BOM para Excel. Ninguna de las dos exportaciones incluye contraseñas.
- No se muestra "MFA activo" en la cabecera del administrador: el backend no lo tiene. El menú agrupa por orden, sin las secciones "Contenido" y "Operación" de la especificación, que hoy tendrían casi todo en "Pronto".
- Backend: `GET /api/admin/metrics` y `GET /api/audit` (filtros `actor, accion, desde, hasta, limite`; el servidor valida cada uno). Todas las acciones sensibles deben auditarse en el servidor, no solo las que hoy dispara este mock.
- Contrato de la auditoría para el backend: la lista llega con un máximo de 200 eventos (la pantalla avisa y el CSV incluye solo esos; un CSV completo debe generarlo el servidor); consultar y exportar la auditoría también debe quedar auditado; `valorAnterior` y `valorNuevo` no deben contener datos de salud ni contraseñas; el filtro por usuario viaja en la URL, así que esos registros de acceso no deben conservar la consulta (o usar un POST de búsqueda).
- Métricas anónimas: con pocos usuarios, un conteo de 1 identifica a una persona (la meta elegida es un dato de salud). El servidor debe agrupar en "Otros" lo que no llegue a un umbral mínimo (por ejemplo, 5).
- El Administrador no tiene Centro de privacidad en este frontend: sus datos personales (derecho de acceso y de baja) quedan pendientes de definir con el backend.
- Móvil: la tabla de auditoría se desplaza horizontalmente (región enfocable) con la acción y el usuario primero; una vista de tarjetas por evento queda como mejora.

**Desviaciones y notas del Paso 6:**

- "Perfil y datos físicos" (rectificación) no tiene pantalla todavía: el menú de ajustes solo enlaza Seguridad (`/cuenta`) y Privacidad. El perfil se puede completar en el onboarding pero no editar después; queda como pendiente.
- "Cuentas vinculadas y sesiones" se omite: no hay Google ni sesiones múltiples en el backend.
- La descarga es un JSON (no un ZIP con aviso por correo): se arma con lo que la API ya entrega al usuario y solo se avisa al servidor para la auditoría. Una pieza ausente o inexistente sale como `null`; cualquier otro fallo la interrumpe para no entregar un archivo incompleto.
- Una contraseña errónea al pedir la baja es un 400 con motivo: un 401 cerraría la sesión en el cliente.
- El Centro de privacidad no exige perfil: descargar, decidir y retirarse no deben condicionarse a dar más datos. Cambiar un interruptor conserva las versiones de documentos que la persona ya aceptó (no acepta en silencio versiones nuevas).
- El servidor debe aplicar a la baja el mismo bloqueo por intentos fallidos que el inicio de sesión y revocar las sesiones al aceptarla.
- Solo el consentimiento de datos de salud pide confirmación al retirarse; los demás se guardan al instante. Reaceptar versiones nuevas de los documentos queda como pendiente.

**Desviaciones y notas del Paso 5:**

- Los récords de la especificación ("5 km más rápido", "Sentadilla") dependen de entrenamientos que no existen; se reemplazan por racha más larga, avances y hábitos completados. Las insignias premian constancia, nunca peso (mejoras §4.6).
- La línea del Plan va del **primer peso registrado** al **peso objetivo** en la fecha en que termina el plan de la meta; el selector de periodo solo recorta el eje, nunca cambia su pendiente. Sin objetivo o sin plan activo no se dibuja. El objetivo se escribe al registrar un peso.
- Mapa de constancia: el nivel 0 se dibuja con borde (sin relleno), los días futuros con borde punteado y los niveles 1 a 3 con una rampa que cumple 3:1 contra la superficie y saltos de al menos 2:1 (`contraste.test.ts`). Se aparta de la paleta de la especificación (`#C7D2FE` no se distinguía del vacío).
- La gráfica se dibuja en píxeles reales (texto de 12 px también en móvil) y rotula el último valor. Los récords usan una variante compacta en lugar de tarjetas anidadas.
- "Exportar PDF" usa la impresión del navegador (`@media print`, siempre con colores claros); no hay generación de PDF en el servidor.
- Las fotos esperan al Manejador de documentos (RF-DOC-01). Cada celda del mapa de constancia lleva su descripción en texto porque los niveles `calor-1` y `calor-2` no se distinguen entre sí por color.
- Los récords y logros de racha usan todo el historial de avances (reconfigurar un hábito no los borra); el mapa de constancia respeta el ciclo vigente de cada hábito activo. Un hábito Completado o Pausado suma sus días cumplidos pero no se exige.
- El peso de la serie de Progreso y el `pesoKg` del perfil del onboarding son datos separados y pueden divergir; cuando exista el backend conviene que el primero alimente al segundo.
- El peso objetivo se puede cambiar pero no quitar desde la interfaz (el contrato admite `null`). Pendiente del backend: qué hace con peso y medidas al revocar el consentimiento de salud (conservar, ocultar o borrar) y que la exportación y el borrado de cuenta del Paso 6 los incluyan.
- Los decimales siguen la localidad `es-DO` (punto decimal), no la coma de los ejemplos de la especificación.

**Desviaciones y notas del Paso 4:**

- No se incluyen "Iniciar entrenamiento", "Mi plan", "Retos" ni el aviso de revisión semanal: no hay backend de entrenamientos, retos ni reportes semanales.
- La racha, el cumplimiento y la tira cuentan solo los hábitos **diarios** activos; los semanales aparecen en las tareas pero no en esas métricas.
- El día es el día UTC (RD-11), igual que los avances. El saludo usa la hora local de quien mira; en UTC-4, de 20:00 a 24:00 el encabezado dice "Buenas noches" con la fecha del día siguiente.
- Un avance no se deshace desde la interfaz: la API no tiene borrado de avances.

**Desviaciones de la especificación en el Paso 2:**

- La cabecera pública no incluye "Ejercicios": la biblioteca de ejercicios no existe todavía (mejoras §4.3).
- "Ver plan" pasa a "Empezar este plan", porque lleva al registro con la meta y no a un detalle del plan.

## Pendientes para el backend (mejoras futuras)

Se completa en cada paso. Cada fila es una necesidad del frontend que hoy resuelve la API simulada o que se omite.

| Pieza                              | Qué falta en el backend                                                                                                                                                                                                                                                                                                   | Cómo lo resuelve hoy el frontend                                       | Origen                       |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------- | ---------------------------- |
| CORS                               | `Program.cs` no permite el origen del frontend                                                                                                                                                                                                                                                                            | Solo funciona con la API simulada                                      | `frontend/AGENTS.md`         |
| Catálogo de metas y plantillas     | `PlanTemplate` y un `GET` público del catálogo; el `slug` como identificador estable                                                                                                                                                                                                                                      | Catálogo estático en `features/publico/catalogo.ts`                    | mejoras §4.1                 |
| Documentos legales versionados     | Textos y versiones servidos por el Manejador de documentos                                                                                                                                                                                                                                                                | Textos de demostración en `features/publico/legal.ts`                  | mejoras §3, §6.2             |
| Peso y medidas corporales          | `GET/POST /api/progress/weights` (con `objetivoKg`) y `GET/POST /api/progress/measurements`; datos de salud: exigen consentimiento (409) y solo los ve su dueño                                                                                                                                                           | Contrato propuesto (Paso 5)                                            | mejoras §4.5                 |
| Métricas y auditoría               | `GET /api/admin/metrics` (agregadas y anónimas) y `GET /api/audit` (RF-AUD-01, 06 y 07): usuario, acción, entidad, valor anterior y nuevo, solo inserción, solo Administrador                                                                                                                                             | Contrato propuesto (Paso 7)                                            | mejoras §6.1 y §6.4          |
| Perfil físico y plan               | `GET/PUT /api/profile` con los límites de `LIMITES_CORPORALES` y `LIMITES_PLAN`                                                                                                                                                                                                                                           | Contrato propuesto (Paso 3)                                            | mejoras §3                   |
| Consentimientos versionados        | `PUT /api/privacy/consents` que solo acepte las versiones vigentes y fije `aceptadoEn` en el servidor                                                                                                                                                                                                                     | Contrato propuesto (Paso 3)                                            | mejoras §3 (`ConsentRecord`) |
| Consentimiento en el registro      | `POST /api/auth/register` debe recibir y guardar las versiones aceptadas y el consentimiento de correos, con `aceptadoEn` = momento del registro                                                                                                                                                                          | Se recuerdan en el navegador (máximo 7 días) y el onboarding los envía | mejoras §3                   |
| Inicio con Google                  | OAuth 2.0 / OIDC con PKCE y `ExternalLogin`                                                                                                                                                                                                                                                                               | Botón deshabilitado con "Muy pronto"                                   | mejoras §5.1                 |
| Política de contraseña             | La especificación pide 12 caracteres y revisión contra filtraciones; el backend exige 8 con letras y números                                                                                                                                                                                                              | Se aplica la del backend                                               | mejoras §5.1                 |
| Descarga de datos y baja de cuenta | `GET /api/privacy/consents`, `POST /api/privacy/export-request` (auditoría) y `POST /api/privacy/deletion-request` (contraseña, desactivar o eliminar con periodo de gracia y borrado definitivo de cuenta, hábitos, peso, medidas, consentimientos y archivos); la exportación y el borrado deben incluir peso y medidas | Contrato propuesto (Paso 6)                                            | mejoras §7.1                 |
