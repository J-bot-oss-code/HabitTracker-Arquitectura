# Backend (.NET 10)

> Verificado contra el código el 07-11-2026. Endpoints, migraciones y número de pruebas: `inventario.generado.md`.

## Módulos

| Módulo | Estado | Contexto EF | Cableado |
|---|---|---|---|
| AccessControl | Completo (registro, activación por código, JWT 15 min + refresh en cookie, Google, bloqueo, consentimientos, baja y exportación, administración, cola de correos) | `ApplicationDbContext` | `Host/WebApi/Extensions/ServiceExtensions.cs` |
| Permissions | Completo (`Pendiente → Aprobada → Aplicada`, `Pendiente → Rechazada`) | `PermissionsDbContext` | `Modules/Permissions/Permissions.Infrastructure/DependencyInjection/` |
| HabitTracker (negocio) | Hábitos + `plan-del-dia` (clima) | `HabitTrackerDbContext` | `Host/WebApi/Extensions/HabitTrackerModuleExtensions.cs` |
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
  - No hay excepciones de dominio propias ni value objects.
- **Application** (`Modules/HabitTracker/HabitTracker.Application/`):
  - `Services/HabitoService.cs`: clase concreta **sin interfaz** (deuda). Valida y lanza `ArgumentException`, `InvalidOperationException("El hábito no existe.")` y `UnauthorizedAccessException`.
  - `Services/PlanDelDiaService.cs` (`IPlanDelDiaService`) con el puerto `Interfaces/IServicioClima.cs`.
  - Puertos: `IHabitoRepository`, `IMetaRepository` (solo `ExistsAsync`), `IUnitOfWork` e `IPermisoService` (→ Permissions).
  - `Exceptions/ClimaExceptions.cs`: las únicas excepciones propias del módulo (503 y 404).
- **Infrastructure** (`Modules/HabitTracker/HabitTracker.Infrastructure/`):
  - `Persistence/HabitTrackerDbContext.cs`: configuración en línea, sin `IEntityTypeConfiguration`. Enums como texto, `datetime2`, cascada Hábito→Registros y **Hábito→Meta** (borrar una Meta borra sus hábitos), todo `nvarchar(max)`.
  - `RegistroDiario.Id` tiene `ValueGeneratedNever()`: **toda entidad hija nueva que se añada por una colección de navegación con Id asignado en el constructor lo necesita**, o EF emite UPDATE en vez de INSERT.
  - Las fechas leídas vuelven con `Kind=Unspecified`, y la API las devuelve sin `Z` (deuda).
  - `Clima/OpenWeatherServicioClima.cs` y `DependencyInjection/ClimaExtensions.cs`.
  - No referencia `Microsoft.Extensions.Hosting.Abstractions`: un worker nuevo necesita el paquete.

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
| `ArgumentException` | 400 genérico |
| `UnauthorizedAccessException` | 403 con sesión, 401 sin ella |
| `InvalidOperationException` | 400 genérico |
| `Exception` | 500 |

- `WriteProblemAsync` añade `Instance = TraceIdentifier` y `extensions.codigo` solo si se pasa un código. Patrón a copiar: `Modules/AccessControl/AccessControl.Domain/Excepciones/ControlAccesoException.cs`.
- HabitTracker no tiene códigos todavía. `HabitsController` decide el 404 buscando `"no existe"` en el mensaje (`EsNoEncontrado`, deuda).

## Puertos entre módulos (adaptadores en `Host/WebApi/Adapters/`)

- **Puerto del negocio implementado en el Host:** `IPermisoService` (HabitTracker.Application) → `PermisoServiceAdapter` → `ISolicitudPermisoService` (Permissions).
- **Puerto del Core implementado en el Host:** `IParticipanteDatosUsuario` (`Modules/AccessControl/AccessControl.Application/Interfaces/IParticipanteDatosUsuario.cs`, con `Seccion`, `ExportarAsync` y `EliminarAsync` idempotente). Lo implementan `ParticipanteHabitos`, `ParticipantePermisos` y `ParticipanteDocumentos`, y lo usan la descarga de datos y la baja de cuenta. **Todo dato nuevo por usuario debe entrar aquí** (exportar y borrar).
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
| `RateLimit:*` | políticas `login`, `registro`, `recuperacion`, `activacion`, `refresh`, `google` y `privacidad` (`Host/WebApi/Extensions/LimitacionDePeticionesExtensions.cs`) |

## Pruebas

- **Proyectos:** `tests/<Módulo>.<Capa>.Tests`. HabitTracker tiene `Domain`, `Application` (dobles a mano, **sin Moq**) e `Integration` (EF Core sobre SQLite).
  - No hay pruebas de `HabitsController`.
  - `tests/DocumentManager.Api.Tests/PlanDelDiaTests.cs` prueba el controller del clima.
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
