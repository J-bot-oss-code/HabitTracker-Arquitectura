# Estado del proyecto — contexto para nuevas sesiones

> Última actualización: 05-11-2026, al activar la protección de ramas y de etiquetas del CI (§12). Antes, el 04-11-2026, al montar la integración continua. Ese mismo día, al cerrar la Práctica 4 (PR #40 y #41 → `develop`, PR #42 → `main`, etiqueta `practica-4`). Antes: 28-10-2026, Práctica 3 integrada en `main` (PR #32 → `develop`, PR #33 → `main`).
> Lee este archivo al iniciar una sesión. Las reglas de trabajo están en `AGENTS.md` y la guía de uso y evaluación en `README.md`.

## 1. Resumen

HabitTracker Nexus es un monolito modular en .NET 10 con Clean Architecture: un solo host (`Host/WebApi`) y 7 módulos en `Modules/`. Cada módulo tiene `Domain ← Application ← Infrastructure` y su propio `DbContext`. Los tres contextos implementados comparten la cadena `ConnectionStrings:AccessControl` (SQL Server).

| Módulo | Estado | Entrega |
|---|---|---|
| AccessControl (Control de Acceso) | Completo: registro + activación por correo, JWT, logout con blacklist, bloqueo 15 min, roles `Estandar`/`Administrador`, recuperación de contraseña, cola de correos en BD, administración de usuarios | Práctica 1 |
| Permissions (Gestión de Permisos) | Completo: `Pendiente → Aprobada → Aplicada`, `Pendiente → Rechazada`, autoresolución prohibida, acción protegida por permiso aplicado | Asignación 2 / Práctica 2 |
| HabitTracker (módulo de negocio) | Completo para la P3: `Planificado → Activo ⇄ Pausado → Completado` (terminal), 5 endpoints en `api/habits`, reconfiguración con permiso `Habits.AdvancedReconfig` | Práctica 3 |
| DocumentManager (Manejador de documentos) | Completo para la P4: subir, listar, consultar, descargar y eliminar de forma lógica (RF-DOC-01 a 05), volumen con nombre, 5 endpoints en `api/documents` | Práctica 4 |
| Auditing, Notifications, Reports | Solo esqueleto (proyectos vacíos) | Pendiente |

## 2. Tags de entrega (no mover ni reescribir)

| Tag | Commit | Notas |
|---|---|---|
| `asignacion-1` | `0056600` | |
| `practica-1` | `171d0fd` | |
| `asignacion-2` | `6433209` | |
| `practica-2` | `86f7e0a` | 39 pruebas. Defectos `practica-2-defecto-1..6`, hijos directos. |
| `practica-3` | `318daab` | 106 pruebas. Defectos `practica-3-defecto-1..4` (`485a0a1`, `dcd2bea`, `f1d3335`, `f802c65`), hijos directos. |
| `practica-4` | `0403df5` | 347 pruebas. Contenedor, documentos, API externa y activación con código de 6 dígitos (ver §11). Creado el 04-11-2026 sobre `main` tras verificarlo desde un clon limpio (92 + 4 comprobaciones). |

- Los tags de la Práctica 3 se rehicieron el 28-10 (antes de entregar) para incluir dos correcciones (ver §5). Quien tenga clones antiguos debe ejecutar `git fetch --tags --force`.
- Cada commit de defecto cambia un solo archivo de producción y su mensaje indica archivo y línea. La tabla con la prueba que detecta cada defecto está en `README.md` › `## Defectos — Práctica 2/3`.
- Para la entrega en Moodle: URL del repositorio + nombre del tag base.

## 3. Pruebas

`dotnet test` ejecuta 106 pruebas sin levantar la API ni usar una BD externa:

| Proyecto | Pruebas |
|---|---|
| HabitTracker.Domain.Tests | 43 |
| HabitTracker.Application.Tests | 21 (con dobles en memoria) |
| HabitTracker.Integration.Tests | 3: 1 de integración real con Permisos + 2 de persistencia de avances, con EF Core/SQLite |
| Permissions.Domain / Application / Infrastructure | 18 / 6 / 3 |
| AccessControl.Application.Tests | 12 |

`tests/Permissions.Api.Tests` es una carpeta sin proyecto (solo `bin/obj`, no versionada).

## 4. Entorno local

- SQL Express `.\SQLEXPRESS`, base `HabitTracker` (desarrollo) y base aislada `HabitTracker_PruebaP3` (prueba E2E; se puede borrar).
- Secrets (User Secrets de `Host/WebApi`): `JwtSettings:Clave` y `SMTP_HOST/PORT/USER/PASS`, que son **reales**. La cadena de conexión está en `appsettings.json` (Windows Auth, sin contraseña).
- Para probar sin tocar la base de desarrollo ni enviar correos:
  `ConnectionStrings__AccessControl=Server=.\SQLEXPRESS;Database=<BD_PRUEBA>;Trusted_Connection=True;TrustServerCertificate=True` y `SMTP_HOST=` vacío. Los correos quedan en `CorreosEnCola`.
- La API corre en `http://localhost:5246` y Swagger en `/swagger`. Para promover a Administrador: `UPDATE usuarios SET Rol = 2 WHERE Correo = ...` y volver a iniciar sesión.
- Plan de prueba manual verificado (83/83): `docs/plan-prueba-swagger-practica-3.md`.

## 5. Decisiones y correcciones relevantes

- **`RegistroDiario.Id` con `ValueGeneratedNever`** (migración vacía `RegistroDiarioIdAsignadoPorDominio`). El dominio asigna el Id. Sin esto, EF trataba el avance nuevo como existente (`UPDATE` en vez de `INSERT`) y la API respondía 500. Aplica a cualquier entidad hija nueva con Id asignado en el constructor.
- **`JsonStringEnumConverter`** en `Program.cs`: los enums viajan como texto (`"Diaria"`, `"Activo"`) y también se aceptan números.
- **Middleware de excepciones:** `UnauthorizedAccessException` → 403 si el usuario está autenticado y 401 si no. `InvalidOperationException` → 400. `ArgumentException` → 400. Los mensajes al cliente son genéricos (RD-08).
- **Transiciones** en un solo punto: `TransicionesHabito.EsTransicionValida` y `TransicionesSolicitudPermiso`. `Habito.Completar` no duplica la validación de la meta.
- **Reconfigurar** inicia un ciclo nuevo (`FechaInicioCiclo`) y conserva el historial; se rechaza si el hábito está `Completado`.

## 6. Deuda técnica conocida

1. `HabitsController` y `PermissionsController` deciden el 404 buscando `"no existe"` en el mensaje (`EsNoEncontrado`). Es frágil: conviene una excepción de dominio específica.
2. `HabitsController` depende de la clase concreta `HabitoService` y no de una interfaz (RD-01).
3. `RegistrarAvance` acepta fechas futuras.
4. Las fechas leídas de SQL vuelven con `DateTimeKind.Unspecified`, y la API devuelve `fechaInicioCiclo` y `fecha` sin `Z`.
5. `.github/PULL_REQUEST_TEMPLATE.md` contiene una PR ya rellenada en lugar de una plantilla vacía con las 4 secciones.
6. El README mezcla secciones de las prácticas 1–3: se podría reorganizar por pieza.

## 7. Flujo de trabajo

- Commits en español con los RF/RD (`feat: ... [RF-NEG-02]`) y revisión RD-01..RD-12 antes de cada commit (ver `AGENTS.md`).
- Ramas: trabajo en `feature/...`, `fix/...` o `docs/...`; PR hacia `develop`; luego PR `develop → main` (`Promover a main — ...`). Cuerpo de PR en 4 secciones: Qué cambia, Por qué, Cómo probarlo, Qué NO incluye. Nunca push directo a `main` ni a `develop`. Las PRs las fusiona el usuario.
- El usuario tiene la skill `flujo-pr` (a nivel de usuario, en `~/.claude/skills/flujo-pr`) con este flujo.
- La bitácora de cada práctica (`docs/bitacora-*.md`) registra las solicitudes reales al agente, sus errores y las correcciones.

## 8. Problemas de entorno conocidos

- **Rutas largas en Windows:** con más de 260 caracteres, SQLite no carga `e_sqlite3` y las pruebas de persistencia fallan. Hay que clonar o copiar el repo en una ruta corta.
- **`sqlcmd`:** no admite `-W` junto con `-y`, ni `-h` con `-y 0`. Con `-W`, las columnas largas (p. ej. `CorreosEnCola.Cuerpo`) se truncan; extraer el fragmento necesario con `SUBSTRING(... CHARINDEX(...))`.
- **Git Bash + curl:** los acentos dentro de JSON pueden llegar mal codificados; usar ASCII en los datos de prueba.

## 9. Próximos pasos posibles

- Módulo de negocio: RF-NEG-07, RF-NEG-08 y RF-NEG-10 (fuera del alcance de la P3).
- Piezas del Core pendientes: Notificaciones, Auditoría, Reportes y Manejador de Documentos (ver `docs/WORKFLOW_STATUS.md`).
- Atender la deuda técnica de §6.

## 10. Frontend (monorepo)

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

### 10.1 Frontend de producción (rama `feature/frontend-nexus`)

> Añadido el 03-11-2026. Sigue sin modificar el backend (`Host/`, `Modules/`, `tests/` intactos): el backend no se toca hasta terminar las prácticas del maestro, y después se adaptará a este frontend.

- **Plan y bitácora:** `frontend/docs/PLAN-FRONTEND-NEXUS.md` (checklist por paso, equivalencia de tokens, desviaciones de la especificación y la tabla **"Pendientes para el backend"**, que es la base del documento ORD y del README que se redactarán al terminar las prácticas). La especificación es `docs/mejoras/diseno-ui-web-movil.md`; el banco de propuestas, `docs/mejoras/mejoras-y-funcionalidades.md`.
- **Identidad:** sustituye al rediseño deportivo-futurista de §10 por la especificación sobria (acento índigo, Plus Jakarta Sans, sin degradados ni brillos) y reduce los temas a **Sistema, Claro y Oscuro**. El contraste WCAG 2.2 AA de cada tema y de los niveles del mapa de constancia lo vigila `shared/theme/contraste.test.ts`.
- **Pantallas nuevas:** landing pública con catálogo de metas y calculadora de calorías; registro con consentimiento legal; onboarding; panel **Hoy** (inicio del Estándar); **Progreso** (peso, medidas, constancia, récords y logros); **Centro de privacidad** (descarga de datos, consentimientos y baja); **Resumen** y **Auditoría** del Administrador.
- **Contratos propuestos (sin backend):** perfil, consentimientos, peso, medidas, exportación y baja, métricas y auditoría. Están simulados en `src/mocks/handlers/propuestas/` y registrados en `CONTRATOS_PROPUESTOS` (una prueba exige que coincidan). Con el backend real (404 sin cuerpo) esas pantallas muestran "llegará pronto" en lugar de fallar (`esNoDisponible`). Todo lo demás (hábitos, permisos, usuarios, login) usa los endpoints reales.
- **Pruebas:** `cd frontend && npm test` ejecuta 452 pruebas sin backend (Vitest limitado a 4 trabajadores para que la suite sea estable). El backend sigue en 106.
- **Revisión:** cada paso pasó por `frontend-reviewer` y, en rondas, por `frontend-design-critic`; la última ronda midió en Edge real que no hay desborde horizontal a 360 px.
- **Commits del trabajo:** `9101919` (docs), `c2b6c4c` (identidad), `fbba9bd` (pública), `aa80c85` (alta), `9eec324` (Hoy), `289f08f` (Progreso), `b2562e5` (privacidad) y `62f411a` (administración).
- **Pendiente:** el recorrido manual a 360 px y 1280 px con `npm run dev`; la auditoría de seguridad con `ciberseguridad-pentester` (sigue reservada para antes de integrar con el backend); la edición del perfil (rectificación), reaceptar versiones nuevas de los documentos y una vista de tarjetas de la auditoría en móvil.

## 11. Práctica 4 — contenedor, documentos y API externa

> Añadido el 04-11-2026. Detalle de la revisión: `docs/revision-practica-4.md`; guion de prueba: `docs/plan-prueba-practica-4.md`; bitácora: `docs/bitacora-practica-4.md`.

- **Arranque:** `copy .env.example .env` + `docker compose up --build` → `http://localhost:8080` (Swagger en `/swagger`). Servicios `db` (SQL Server 2022, sin puerto publicado) y `webapi`. Volúmenes con nombre `documentos-archivos` (archivos) y `sqlserver-datos` (base). Con `BaseDeDatos__MigrarAlIniciar=true` la API migra los cuatro contextos al arrancar (`Host/WebApi/Extensions/InicializacionBaseDeDatos.cs`) y siembra el Administrador (`ADMIN_CORREO`/`ADMIN_CLAVE`, mínimo 12 caracteres) y la Meta de demostración `5f1c0a00-0000-4000-8000-000000000002`.
- **Documentos:** `Modules/DocumentManager/*` (tabla `Documentos`, migración `AgregarTablaDocumentos`). El tamaño y el tipo MIME los calcula el servidor (tope de 10 MB, lista blanca de extensiones); el archivo se guarda con un GUID. Las excepciones del módulo (`DocumentoNoEncontradoException` → 404, `ArchivoInvalidoException` → 400) y las del clima (503/404) se traducen en `GlobalExceptionMiddleware`.
- **API externa:** OpenWeatherMap (`OPENWEATHER_API_KEY`) alimenta `GET /api/habits/plan-del-dia` (rol Estándar); la lógica está en `RecomendadorClima` y `PlanDelDiaService`. Sin clave o con la API caída → 503 controlado.
- **Correo en el contenedor:** `CORREO_MODO=consola` con `SMTP_HOST` vacío escribe una línea `[CORREO EN CONSOLA]` con el enlace/código en el log (`docker compose logs webapi`).
- **Pruebas:** 347 (nuevos proyectos `DocumentManager.Domain/Application/Infrastructure/Api.Tests`). Script de revisión: `scripts/revision-practica-4.sh` (95 comprobaciones + 4 de persistencia contra el contenedor).
- **Activación de cuenta con código de 6 dígitos:** `POST /api/auth/activate` recibe `{correo, codigo}` (también `GET /api/auth/activate?correo=&codigo=`). El código sale de `RandomNumberGenerator`, vence en 24 h y se invalida a los 5 intentos; el intento se reserva con `UsuarioRepository.ReservarIntentoActivacionAsync` (`UPDATE ... OUTPUT`, atómico) y el reenvío espera 60 s. Columna `usuarios.IntentosActivacion` (nullable; migración `AgregarIntentosActivacion`). Limitador por IP: política `activacion`, 30/min (`RateLimit:ActivacionPorMinuto`). Los tokens de 32 caracteres anteriores ya no activan.
- **Deuda nueva:** el limitador por IP está en memoria (una réplica) y, **dentro de Docker, `RemoteIpAddress` es la IP de la pasarela**, así que todos los clientes comparten un único cubo de 30/min (hasta configurar `ForwardedHeaders` con `KnownProxies` explícitos detrás de un proxy real); el registro del limitador solo se verifica en ejecución (script de revisión), no con una prueba de unidad; el enlace GET de activación tiene efecto de estado (un escáner de correos podría consumir un intento); `Usuario.Correo` sin índice único (siembra no apta para varias réplicas); sin caché del clima (cuota gratuita compartida); `webapi` sin healthcheck; la imagen se compila con `COPY . .` (sin caché de restore por `.csproj`); `PlanDelDiaTests` vive en `DocumentManager.Api.Tests` por comodidad.
- **Entorno de Docker:** la primera construcción tarda varios minutos (SQL Server ~2.3 GB). En Git Bash anteponer `MSYS_NO_PATHCONV=1` a `docker exec` con rutas absolutas.

## 12. Integración continua (CI)

> Añadido el 04-11-2026 en la rama `feature/ci-integracion-continua`. Tras la Práctica 4 se levantó la congelación del backend: se puede modificar siempre que no se muevan los tags de entrega ni se rompan los RF/RD. Descripción para el lector en `README.md` › `## Integración continua (CI)`.

- **Workflows:** `.github/workflows/ci.yml` (backend, frontend, reglas del repositorio, agrupados en `ci-ok`) y `.github/workflows/seguridad.yml` (CodeQL de C#, JS/TS y Actions, gitleaks, revisión de dependencias, Trivy sobre el `Dockerfile` y la imagen, actionlint y zizmor, agrupados en `seguridad-ok`). Se ejecutan en PR y push a `develop` y `main`; `seguridad.yml` también cada lunes. Todas las acciones van fijadas por SHA, con permisos mínimos y `persist-credentials: false`. gitleaks, Trivy y actionlint se instalan con el binario oficial y su SHA-256 (`scripts/ci/instalar-herramienta.sh`), sin acciones de terceros.
- **Umbral:** bloquean las severidades **alta y crítica**. Media y baja solo se reportan. El formato de commits (`tipo: descripción en español [RF-XX-NN, RD-NN]`) es bloqueante en las PR y omite los merges y los commits de Dependabot.
- **Compilación estricta:** `Directory.Build.props` activa NuGetAudit (NU1903 y NU1904 rompen el build) y `analizadores-seguridad.globalconfig` convierte en error las 94 reglas de seguridad de los analizadores de .NET (CA2xxx, CA3xxx, CA5xxx), salvo en los proyectos `.Tests`. Las reglas se enumeran por ID porque la severidad por categoría **no** activa las que vienen apagadas (se comprobó con `MD5.Create()`). Al subir de SDK hay que regenerar la lista desde `analysislevelsecurity_<versión>_all.globalconfig` del SDK.
- **Reglas RD automáticas** (`scripts/ci/verificar-reglas-rd.sh`): RD-03 (capas y Core→negocio), RD-05 (MD5/SHA1), RD-08 (`StackTrace`, `ex.ToString()`), RD-10 (archivos de secretos, `JwtSettings:Clave` y contraseñas en `appsettings`) y RD-11 (`DateTime.Now`/`Today`, sin mirar `tests/`, donde se usa a propósito para probar el rechazo de fechas locales). Las pruebas del validador de commits (`pruebas-verificar-commits.sh`, 20 casos) cubren RD-12.
- **Línea base medida antes de añadir el CI:** backend 347 pruebas en verde, 0 avisos y 0 errores de compilación; frontend 452 pruebas, lint, tipos, build y `npm audit signatures` en verde, y `npm audit` con 7 avisos **moderados** (herramientas de lint, no bloquean). `npm ci --ignore-scripts` basta para compilar y probar, por eso el CI lo usa.
- **Triaje de hallazgos:**
  - Analizadores de seguridad de .NET: 0 hallazgos en el código actual.
  - Trivy sobre el `Dockerfile`: 0 configuraciones inseguras altas o críticas. Sobre la imagen (`aspnet:10.0`, Ubuntu 24.04, runtime 10.0.14): 0 vulnerabilidades.
  - gitleaks sobre los 210 commits de todas las ramas: 7 coincidencias, todas la misma contraseña de demostración `ClaveSegura123` de los ejemplos de `curl` del README (cuenta ficticia `persona@ejemplo.com`). Falso positivo; se acota en `.gitleaks.toml` por valor y archivo exactos, no por carpeta.
  - CodeQL: pendiente de la primera ejecución en GitHub; no se puede probar en local.
- **Windows y saltos de línea:** el índice de Git guarda LF, pero con `core.autocrlf=true` el árbol de trabajo queda en CRLF y `npm run format:check` falla en local (205 archivos); en el CI pasa. `.gitattributes` fuerza LF en los `.sh`, o `bash` falla con `$'\r'`.
- **Ajustes de GitHub (activos desde el 05-11-2026):** Default setup de CodeQL sin configurar; Dependency graph (necesario para la revisión de dependencias), Secret scanning y Push protection activados; **Dependabot security updates desactivado** (abriría PR directo a `main` y saltaría el flujo `develop → main`). Dos rulesets, sin actores con bypass:
  - **"Ramas protegidas (main y develop)":** sin borrado ni force-push, PR obligatoria (0 aprobaciones: un solo desarrollador), checks `ci-ok` y `seguridad-ok` con la rama al día, y *code scanning* de CodeQL en "High or higher". Ya no se puede fusionar con un check en rojo ni hacer push directo.
  - **"Etiquetas de entrega":** `practica-*` y `asignacion-*` (los 16 tags de §2) no se pueden actualizar, borrar ni mover con force-push. Crear tags nuevos sigue permitido.
  - El ruleset antiguo "Proteccion de main" (id 26568693) está desactivado y sin ramas objetivo; quedó sin efecto y se puede borrar.
- **Dependabot (desde el 04-11-2026):** PR semanales hacia `develop`. Las menores y parches van agrupadas; los saltos mayores llegan sueltos. `typescript` 7 se ignora: `typescript-eslint` declara el peer `typescript <6.1.0` y `npm ci` falla con ERESOLVE (retomar cuando lo soporte).
- **Escaneo semanal de Trivy:** puede bloquear por parches de Ubuntu publicados después del último cambio (CVE-2026-109518 de `apt`, 05-11-2026). El `Dockerfile` ejecuta `apt-get upgrade` en la etapa final como solución; por eso la imagen no es reproducible bit a bit.
- **Pendiente:** descripción automática de las PR (Qué cambia / Por qué / Cómo probarlo / Qué NO incluye), el CD (el destino previsto es Vercel, que no hospeda .NET de forma nativa), el Dockerfile del frontend y la conexión frontend↔backend. Deuda anotada: `frontend/AGENTS.md` aún dice "backend congelado" y `.github/PULL_REQUEST_TEMPLATE.md` (§6.5) sigue siendo una PR rellenada.
