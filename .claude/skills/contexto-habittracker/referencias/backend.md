# Backend (.NET 10)

> Verificado contra el código el 12-11-2026. Endpoints, migraciones y número de pruebas: `inventario.generado.md`.

## Módulos

| Módulo | Estado | Contexto EF | Cableado |
|---|---|---|---|
| AccessControl | Completo (registro, activación por código, JWT 15 min + refresh en cookie, Google, bloqueo, consentimientos, baja y exportación, administración, cola de correos) | `ApplicationDbContext` | `Host/WebApi/Extensions/ServiceExtensions.cs` |
| Permissions | Completo (`Pendiente → Aprobada → Aplicada`, `Pendiente → Rechazada`) | `PermissionsDbContext` | `Modules/Permissions/Permissions.Infrastructure/DependencyInjection/` |
| HabitTracker (negocio) | Hábitos (meta opcional, errores con código), perfil en dos niveles, cuestionario de salud **cifrado**, peso y medidas, catálogo de planes, **Mis planes** (pausas, ajustes, logros, mensajes) y `plan-del-dia` (clima) | `HabitTrackerDbContext` | `Host/WebApi/Extensions/HabitTrackerModuleExtensions.cs` |
| DocumentManager | Completo (RF-DOC-01..05) | `DocumentManagerDbContext` | su `DependencyInjection/` en Infrastructure |
| Auditing, Notifications, Reports | Solo `.csproj` vacíos | — | — |

- Los cuatro contextos usan la misma cadena `ConnectionStrings:AccessControl` (SQL Server) y la tabla `__EFMigrationsHistory` por defecto.
- `Host/WebApi/Program.cs` registra los adaptadores entre módulos, `JsonStringEnumConverter` (los enums viajan como texto) y llama a `InicializarBaseDeDatosAsync`.
- `Host/WebApi/Extensions/InicializacionBaseDeDatos.cs`: con `BaseDeDatos:MigrarAlIniciar=true` (Docker) migra los contextos, siembra el Administrador (`ADMIN_CORREO`/`ADMIN_CLAVE`) y la Meta de demostración `5f1c0a00-0000-4000-8000-000000000002`.

## HabitTracker por dentro

- **Dominio** (`Modules/HabitTracker/HabitTracker.Domain/`):
  - `Entities/Habito.cs`: agregado con setters privados. `Activar`, `Pausar`, `Completar`, `Reconfigurar` y `RegistrarAvance`; `CumpleMetaFinal` suma el **día UTC** (Diaria) o la semana ISO UTC (Semanal) desde `FechaInicioCiclo`.
  - `Entities/RegistroDiario.cs`: el avance (`Cantidad > 0`, `Fecha` UTC, `Unidad`). Se crea solo desde `Habito.RegistrarAvance`.
  - `Entities/TransicionesHabito.cs`: único punto de la máquina. Prohibidas explícitas: Planificado→Completado, Pausado→Completado, salir de Completado.
  - `Meta`, `Categoria`, `Recompensa` y `Etiqueta`: clases de datos con setters públicos (cumplen RF-NEG-01). `Meta` es un catálogo compartido y no tiene endpoint.
  - `Servicios/RecomendadorClima.cs`: puro.
  - `Excepciones/HabitTrackerException.cs`: base con `Codigo`; `RecursoNoEncontradoException` (404, `habito_no_encontrado`) y `ReglaNegocioException` (400, `fecha_futura`, `meta_no_encontrada`). Las transiciones prohibidas siguen siendo `InvalidOperationException` (400 genérico) para no tocar las suites de la P3.
  - `Habito.MetaId` es opcional (`Guid?`).
  - `Perfil/PerfilUsuario.cs` (nivel 1: jornada, zona IANA y aviso; el resto, opcional), `Salud/CuestionarioSalud.cs` (3 bloques) y `Salud/EvaluadorSalud.cs`, **la única definición de las reglas de salud**: restricciones y `BloquesFaltantesPara`. `Progreso/ProgresoCorporal.cs` contiene peso y medidas, uno por día UTC.
  - `ConsentimientoSaludRequeridoException` → 409 `consentimiento_salud_requerido`. Control de Acceso tiene otra `ConsentimientoRequeridoException`: no hay que confundirlas.
  - `Planes/` (Fase 3):
    - `PlanPlantilla.cs`: plan, `TareaPlantilla`, `ContenidoPlan` y enums `AreaPlan`, `NivelPlan`, `IntensidadPlan`, `SexoPlan` y `Ancla`. Las invariantes de seguridad del contenido viven aquí (`NecesitaCuestionario`, topes de agua y semanas por nivel).
    - `ReglasSeguridadPlanes.cs`: `SituacionPersona.Crear` (sin consentimiento no usa el cuestionario) y **la única definición de "no apto"** (`MotivoNoApto`). La reutiliza la inscripción de la Fase 4 (400 `plan_no_apto`).
    - `RecomendadorPlanes.cs`: puntaje, motivos y advertencias, más `VariantesMiDia` (perfil laboral → slug de "Mi día").
    - `PlanUsuario.cs` (Fase 4): el plan de una persona, `PeriodoPausa` y `TransicionesPlanUsuario`, **el único punto de la máquina del plan**. `Reanudar(ahora, zona)` agrega al final los días locales de la pausa (`DiasDePausa`). `ResolvedorAncla.cs`: ancla + jornada → hora local.
  - `Adherencia/` (Fase 4): `CalculadoraProgreso` es **la única definición de racha y cumplimiento** (día local; un plan solo semanal se mide en semanas, `UnidadSeguimiento`), `SugeridorAjuste` (nunca se aplica solo) y `CalculadoraLogros` (no recibe peso ni medidas: lo comprueba una prueba por reflexión). `Motivacion/SelectorMensajesMotivacion.cs`: determinista (FNV-1a).
  - `Habito` gana `PlanUsuarioId`, `Descripcion`, `VersionMinima`, `Ancla` y `PausadoPorPlan` (`PausarPorPlan`/`ReanudarPorPlan`/`DesdeTareaDePlan`); `RegistroDiario`, `EsVersionMinima`.
- **Application** (`Modules/HabitTracker/HabitTracker.Application/`):
  - `Services/HabitoService.cs` implementa `Interfaces/IHabitoService.cs`. Recibe un `TimeProvider` opcional (rechazo de fechas futuras con `ToleranciaReloj` de 5 min, en el servicio y no en el dominio: las pruebas de la P3 registran fechas futuras a propósito para probar los límites del cálculo).
  - `Services/PlanDelDiaService.cs` (`IPlanDelDiaService`) con el puerto `Interfaces/IServicioClima.cs`.
  - Puertos: `IHabitoRepository`, `IMetaRepository` (solo `ExistsAsync`), `IUnitOfWork`, `IPermisoService` (→ Permissions), `IConsentimientoSalud` (→ consentimientos de Control de Acceso) e `ICifradorDatos` (`Interfaces/IPerfilSaludPuertos.cs`).
  - `Services/PerfilSaludServices.cs`: `IPerfilService`, `ICuestionarioSaludService`, `IProgresoCorporalService` e `IDatosPerfilSaludService` (exportar y borrar).
  - `Services/PlanesService.cs` (`IPlanesService`: listar, detalle y recomendados) con el puerto `Interfaces/IPlanPlantillaRepository.cs` (solo lectura) y `DTOs/PlanesDtos.cs`.
  - `Services/PlanUsuarioService.cs` (`IPlanUsuarioService`: inscribirse, listar, detalle, transición, ajuste y recordatorio) y `Services/SeguimientoDiarioServices.cs` (`ILogrosService`, `IHoyService`, `IResumenProgresoService`, `IDatosPlanesService`), con `DTOs/PlanUsuarioDtos.cs` y los puertos de `Interfaces/IPlanUsuarioPuertos.cs`.
  - `Exceptions/ClimaExceptions.cs`: excepciones de clima (503 y 404). `Exceptions/ConflictoDeUnicidadException.cs`: la violación de un índice único, traducida por el `IUnitOfWork` para que Application no conozca a EF.
- **Infrastructure** (`Modules/HabitTracker/HabitTracker.Infrastructure/`):
  - `Persistence/HabitTrackerDbContext.cs`: `ApplyConfigurationsFromAssembly` con una clase `IEntityTypeConfiguration` por entidad en `Persistence/Configuraciones/`, y `ConfigureConventions` que guarda todo `DateTime` como `datetime2` y lo lee con `Kind=Utc` (la API devuelve `Z`). Hábito→Registros en cascada, **Hábito→Meta en `SetNull`**, textos con `HasMaxLength` y decimales `HasPrecision(18, 2)`.
  - `RegistroDiario.Id` tiene `ValueGeneratedNever()`: **toda entidad hija nueva que se añada por una colección de navegación con Id asignado en el constructor lo necesita**, o EF emite UPDATE en vez de INSERT.
  - `Clima/OpenWeatherServicioClima.cs` y `DependencyInjection/ClimaExtensions.cs`.
  - Tiene `FrameworkReference Microsoft.AspNetCore.App` (Data Protection; también trae `Hosting.Abstractions` y `Configuration`, así que no hay que añadirlos como paquete: NU1510).
  - `Persistence/Configuraciones/PlanUsuarioConfiguracion.cs`: `PlanesUsuario` (índice único filtrado por persona y plantilla mientras el plan está Activo o Pausado), `PeriodosPausa` (`OwnsMany`, `ValueGeneratedNever`) y `LogrosUsuario` (único por persona y clave). `HabitTrackerUnitOfWork` traduce la violación de unicidad (`ViolacionesDeUnicidad`). `LogroUsuarioRepository.AgregarSiNoExisteAsync` inserta con `WHERE NOT EXISTS`. `Contenido/mensajes-motivacion.json` (33 mensajes) y `CatalogoMensajes`.
  - `Seguridad/CifradorDataProtection.cs` y `Repositories/PerfilSaludRepositories.cs`: el cuestionario se guarda en `CuestionarioSaludFila`, con cada bloque como JSON cifrado y un propósito por bloque.
  - `Contenido/`: `catalogo-planes.json` (recurso embebido; un plan nuevo es solo JSON), `LectorCatalogoPlanes` (estricto: campo desconocido, enum inválido o slug repetido → excepción) y `SembradorCatalogoPlanes` (upsert por slug y `versionContenido`, nunca borra). Lo llama `InicializacionBaseDeDatos.SembrarCatalogoPlanesAsync` **en cada arranque**, no solo con `MigrarAlIniciar`.
  - `Persistence/Configuraciones/PlanPlantillaConfiguracion.cs`: listas como JSON (`PrimitiveCollection` con enums como texto y `OwnsMany(...).ToJson()` para la guía) y las tareas en `TareasPlantilla` con `Guid` propio. La base no garantiza el orden de las tareas: `PlanPlantilla.Tareas` las ordena por `Orden`.

## Errores → HTTP (`Host/WebApi/Middlewares/GlobalExceptionMiddleware.cs`)

El orden de los `catch` importa: una excepción nueva que herede de `InvalidOperationException` o de `ArgumentException` debe capturarse **antes** que ellas.

| Excepción | HTTP |
|---|---|
| `ServicioClimaNoDisponibleException` / `CiudadNoEncontradaException` | 503 / 404 |
| `DocumentoNoEncontradoException` / `ArchivoInvalidoException` | 404 / 400 |
| `BadHttpRequestException` | 413 o 400 |
| `CredencialesInvalidasException` | 401 |
| `ProveedorExternoNoDisponibleException`, `TokenExternoInvalidoException`, `VinculacionRequeridaException`, `SesionInvalidaException`, `OperacionNoPermitidaException` | 503, 401, 409, 401 y 403, todas con `codigo` |
| `ControlAccesoException` (base, hereda de `InvalidOperationException`) | 400 + `codigo` |
| `ConsentimientoSaludRequeridoException` / `RecursoNoEncontradoException` (`habito_no_encontrado`, `perfil_no_encontrado`, `plan_no_encontrado`, `plan_usuario_no_encontrado`) / `HabitTrackerException` (HabitTracker) | 409 / 404 / 400, todas con `codigo` |
| `ArgumentException` | 400 genérico |
| `UnauthorizedAccessException` | 403 con sesión, 401 sin ella |
| `InvalidOperationException` | 400 genérico |
| `Exception` | 500 |

- `WriteProblemAsync` añade `Instance = TraceIdentifier` y `extensions.codigo` solo si se pasa un código. Patrón a copiar: `Modules/AccessControl/AccessControl.Domain/Excepciones/ControlAccesoException.cs`.
- HabitTracker: `RecursoNoEncontradoException` (404) y `HabitTrackerException` (400) van justo después de `ControlAccesoException`, antes de `ArgumentException` e `InvalidOperationException`. `PermissionsController` aún decide su 404 buscando `"no existe"` en el mensaje (`EsNoEncontrado`, deuda).

## Puertos entre módulos (adaptadores en `Host/WebApi/Adapters/`)

- **Puerto del negocio implementado en el Host:** `IPermisoService` (HabitTracker.Application) → `PermisoServiceAdapter` → `ISolicitudPermisoService` (Permissions).
- **Consentimiento de salud:** `IConsentimientoSalud` (HabitTracker) → `Host/WebApi/Adapters/ConsentimientoSaludAdapter.cs` → `IConsentimientoService`. Una cuenta sin consentimientos equivale a no consentir.
- **Puerto del Core implementado en el Host:** `IParticipanteDatosUsuario` (`Modules/AccessControl/AccessControl.Application/Interfaces/IParticipanteDatosUsuario.cs`, con `Seccion`, `ExportarAsync` y `EliminarAsync` idempotente). Lo implementan `ParticipanteHabitos`, `ParticipantePerfilSalud` (sección `perfilYSalud`), `ParticipantePlanes` (sección `planes`: planes, pausas y logros), `ParticipantePermisos` y `ParticipanteDocumentos`, y lo usan la descarga de datos y la baja de cuenta. **Todo dato nuevo por usuario debe entrar aquí** (exportar y borrar).
- **Consentimiento:** `IConsentimientoService` (AccessControl.Application), con `DatosSalud`, `Marketing`, `CompartirCumplimiento` y `EstadisticasAnonimas`. Los mocks del frontend esperan **409** si falta `datosSalud`.
- **Usuario** (correo, nombre, activo, baja programada): `IUsuarioRepository.GetByIdAsync` (AccessControl).

## Correo y workers

- **Cola:** `CorreoEnCola` (tabla `CorreosEnCola`, estados Pendiente, Procesando, Enviado y Fallido, `MaxIntentos = 3`, espera de 2^n min entre reintentos).
  - `IEmailQueue` solo tiene `EncolarCorreoRecuperacionAsync` y `EncolarCorreoActivacionAsync`: **no hay un método genérico**.
  - `DatabaseEmailQueue.PlantillaHtml` siempre dibuja la caja del código.
  - Solo `UsuarioService` encola.
- **`EmailSenderWorker`:** cada 15 s marca `Procesando` antes de enviar (RF-NOT-12). Un correo que quedó en `Procesando` tras una caída no se reintenta. SMTP con `SMTP_HOST/PORT/USER/PASS`; con `CORREO_MODO=consola` y `SMTP_HOST` vacío escribe `[CORREO EN CONSOLA]` en el log.
- **`BajaCuentasWorker`:** el patrón a copiar. Abre un scope por pasada y solo llama a `IServicioBajaCuentas.EjecutarAsync(ahora)`. Las opciones (`OpcionesPrivacidad`, con `Seccion` + `Validar()`) se enlazan y registran como singleton.
- Los dos workers viven en `Modules/AccessControl/AccessControl.Infrastructure/Workers/`.

## Configuración

| Clave (sección `:` o `__` en variables) | Uso |
|---|---|
| `ConnectionStrings:AccessControl` | SQL Server; en local, `.\SQLEXPRESS`, base `HabitTracker` |
| `JwtSettings:Clave/Emisor/Audiencia/MinutosExpiracion` | JWT (la clave va en User Secrets o en el entorno) |
| `Sesion:DiasRefresh/CookieSegura/CookieSameSite` | refresh token en cookie |
| `Privacidad:DiasGraciaBaja/IntervaloEjecucionMinutos` | baja de cuenta |
| `Legal:VersionTerminos/VersionPrivacidad` | versiones vigentes (`v2.1` y `v1.4`) |
| `Google:ClientId`, `Cors:OrigenesPermitidos`, `App:BaseUrl` | Google, CORS y enlaces |
| `Seguridad:ContrasenasFiltradas:Habilitado/UrlBase` | Pwned Passwords |
| `Documentos:RutaAlmacenamiento`, `Swagger:Habilitado`, `Https:RedireccionDesactivada`, `BaseDeDatos:MigrarAlIniciar` | varios |
| `SMTP_HOST/PORT/USER/PASS`, `CORREO_MODO`, `OPENWEATHER_API_KEY` | correo y clima |
| `DATAPROTECTION_KEYS_PATH` | claves de cifrado de los datos de salud; obligatoria fuera de Development (`Host/WebApi/Extensions/ProteccionDeDatosExtensions.cs`), volumen `dataprotection-claves` en Docker |
| `RateLimit:*` | políticas `login`, `registro`, `recuperacion`, `activacion`, `refresh`, `google`, `privacidad` y `catalogo` (`Host/WebApi/Extensions/LimitacionDePeticionesExtensions.cs`) |

## Pruebas

- **Proyectos:** `tests/<Módulo>.<Capa>.Tests`. HabitTracker tiene `Domain`, `Application` (dobles a mano, **sin Moq**) e `Integration` (EF Core sobre SQLite).
  - `tests/HabitTracker.Api.Tests` prueba los controllers (`HabitsControllerTests`, `PlanDelDiaTests`, `PerfilSaludControllersTests` y `PlansControllerTests`) y el middleware sobre un `DefaultHttpContext`.
  - **Prueba de contenido:** `tests/HabitTracker.Integration.Tests/CatalogoPlanesTests.cs` valida `catalogo-planes.json` (13 slugs, cuestionario cuando hay datos de nivel 2, topes y aviso del agua, palabras prohibidas) y el sembrador sobre SQLite.
  - **Mis planes:** `MisPlanesPersistenciaTests` (SQLite con los repositorios y el catálogo reales: inscripción de los 13 planes, índice único, logros idempotentes, baja de cuenta), `MensajesMotivacionTests` (contenido de los mensajes) y `MisPlanesControllersTests`.
- **Nombres:** `RF_NEG_02_CrearHabito_NombreVacio_Rechaza` con `[Trait("Requirement", "RF-NEG-02")]`, comentarios `// Arrange // Act // Assert`, `Record.ExceptionAsync` + `Assert.IsType<>` y `Assert.Equal(0, unitOfWork.SaveChangesCallCount)`.
- **Dobles:** clases privadas anidadas (`InMemoryHabitoRepository`, `InMemoryUnitOfWork`, `FakePermisoService`). Un estado se fuerza por reflexión (`HabitoTests.cs`).
- **SQLite:** `PrepararBaseAsync` en `tests/HabitTracker.Integration.Tests/HabitoPersistenciaTests.cs` abre `Data Source=:memory:` y llama a `EnsureCreatedAsync()` (no a las migraciones). Cada operación usa un `HabitTrackerDbContext` nuevo.
- **Rutas largas en Windows:** con más de 260 caracteres SQLite no carga `e_sqlite3`; hay que clonar en una ruta corta.

## Migraciones

```bash
dotnet ef migrations add <Nombre> --project Modules/<M>/<M>.Infrastructure --startup-project Host/WebApi --context <Ctx>
dotnet ef database update --project Modules/<M>/<M>.Infrastructure --startup-project Host/WebApi --context <Ctx>
dotnet ef migrations list --project Modules/<M>/<M>.Infrastructure --startup-project Host/WebApi --context <Ctx>
```

Contextos: `ApplicationDbContext`, `PermissionsDbContext`, `HabitTrackerDbContext` y `DocumentManagerDbContext`. Tras traer cambios, aplica las migraciones pendientes: sin ellas, la API responde 500.
