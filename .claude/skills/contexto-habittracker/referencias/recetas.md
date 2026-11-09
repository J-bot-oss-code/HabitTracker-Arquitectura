# Recetas para tareas repetidas

> Pasos mínimos. Cada receta cita el archivo que sirve de modelo: ábrelo en lugar de buscar ejemplos por el repo.

## Endpoint nuevo

1. **Caso de uso:** en el `Application` del módulo, una interfaz (`I<Algo>Service`) y su implementación. La validación de la entrada va antes de tocar la BD (RD-07). La lógica de negocio nunca va en el controller (RD-02).
2. **Errores:** una excepción con `codigo` estable (modelo: `Modules/AccessControl/AccessControl.Domain/Excepciones/ControlAccesoException.cs`), con su `catch` en `Host/WebApi/Middlewares/GlobalExceptionMiddleware.cs` **antes** de los genéricos. Un 404 real siempre lleva `ProblemDetails`.
3. **Controller** en `Host/WebApi/Controllers/`:
   - `[Authorize(Roles = "...")]` (RD-06, RF-NEG-06) y la identidad desde los claims: `ClaimTypes.NameIdentifier` y `ClaimTypes.Email`;
   - DTO con anotaciones de rango y longitud;
   - `[EnableRateLimiting]` si es anónimo o sensible (`Host/WebApi/Extensions/LimitacionDePeticionesExtensions.cs`).
   - Modelo: `Host/WebApi/Controllers/PrivacidadController.cs`.
4. **Cableado:** en el `*ModuleExtensions` o `DependencyInjection/` del módulo; en HabitTracker, `Host/WebApi/Extensions/HabitTrackerModuleExtensions.cs`.
5. **Pruebas:** servicio con dobles a mano y controller (`tests/<Módulo>.Api.Tests`). Nombres `RF_XX_NN_Sujeto_Escenario_Resultado` + `[Trait("Requirement", "RF-XX-NN")]`.
6. **Contrato:** README del módulo y frontend (`frontend-api-contract`). Si el contrato estaba en `CONTRATOS_PROPUESTOS`, sale de ahí.
7. Regenera el inventario: `bash scripts/contexto/generar-inventario.sh`.

## Entidad nueva y migración

1. Entidad en `Domain` con setters privados, constructor que valida y `Id = Guid.NewGuid()`. Sin EF ni referencias.
2. Configuración en Infrastructure con `IEntityTypeConfiguration<T>`:
   - `HasMaxLength` en los textos y `HasPrecision` en los decimales;
   - enums con `HasConversion<string>()`;
   - fechas `datetime2` que vuelven como UTC;
   - índices por `UsuarioId` y por los filtros;
   - **si es hija añadida por una colección de navegación**, `Property(x => x.Id).ValueGeneratedNever()`.
3. `dotnet ef migrations add <Nombre> --project Modules/<M>/<M>.Infrastructure --startup-project Host/WebApi --context <Ctx>`. Revisa el `Up`/`Down` generado.
4. **Datos por usuario:** inclúyelos en la exportación y en el borrado (`Host/WebApi/Adapters/ParticipanteHabitos.cs` y su servicio). Prueba de aislamiento entre dos personas con SQLite.
5. **Prueba de persistencia** con SQLite en memoria: modelo `tests/HabitTracker.Integration.Tests/HabitoPersistenciaTests.cs`.

## Dato sensible (de salud o personal)

1. **Consentimiento:** guardarlo exige el consentimiento `datosSalud` con `Guardas.ExigirConsentimientoAsync` (`Modules/HabitTracker/HabitTracker.Application/Services/PerfilSaludServices.cs`). Si falta, la respuesta es `409 consentimiento_salud_requerido`.
2. **Cifrado en reposo:** con `ICifradorDatos` y un propósito propio (`Tipo.Bloque.vN`). El repositorio guarda una fila de persistencia con el texto cifrado (modelo: `Modules/HabitTracker/HabitTracker.Infrastructure/Repositories/PerfilSaludRepositories.cs`). El dominio no sabe nada del cifrado y nunca se filtra por esa columna en SQL.
3. **Descarga y baja:** el dato entra en la descarga (descifrado para su dueño) y en la baja, a través de `IDatosPerfilSaludService` o de su propio participante.
4. **Pruebas:** la columna cruda de SQLite no contiene el texto en claro, la ida y vuelta funciona, y otras claves u otro propósito no descifran (modelo: `tests/HabitTracker.Integration.Tests/PerfilSaludPersistenciaTests.cs`).

## Puerto entre módulos

1. La interfaz vive en el `Application` del módulo que la **necesita** (por ejemplo `HabitTracker.Application/Interfaces/IPermisoService.cs`).
2. El adaptador va en `Host/WebApi/Adapters/` y llama a la interfaz de `Application` del otro módulo (modelo: `Host/WebApi/Adapters/PermisoServiceAdapter.cs`). Se registra en `Host/WebApi/Program.cs`.
3. Prohibido: referenciar el `Infrastructure` de otro módulo, leer sus tablas o que el Core use `using HabitTracker` (el CI falla).
4. Pruebas: el servicio, con un doble del puerto; el adaptador, con Moq (modelo: `tests/AccessControl.Application.Tests/ParticipantesDeDatosTests.cs`).

## Worker en segundo plano

1. La lógica va en un servicio de `Application` con `EjecutarAsync(DateTime ahoraUtc)`, que se prueba sin el worker.
2. El worker (`BackgroundService` en Infrastructure) abre un scope por pasada, llama al servicio y captura las excepciones. Modelo: `Modules/AccessControl/AccessControl.Infrastructure/Workers/BajaCuentasWorker.cs`.
3. Las opciones van en una clase con `Seccion` y `Validar()`; se enlazan y registran como singleton. Una configuración inválida detiene el arranque. Modelo: `Modules/AccessControl/AccessControl.Application/Service/OpcionesPrivacidad.cs`.
4. Si el trabajo puede correr en dos réplicas, reclama cada fila con un `UPDATE … WHERE` atómico (modelo: `UsuarioRepository.ReservarIntentoActivacionAsync`).

## Feature del frontend conectada

1. Diseño: `frontend-architect`. Tipos y mock: `frontend-api-contract`. Las reglas del mock deben ser las del backend.
2. Pantalla: `frontend-ui-builder` ⇄ `frontend-design-critic`. Pruebas: `frontend-test-writer`. Antes del commit: `frontend-reviewer`.
3. zod con los mismos límites que el backend y errores con `mensajeDeError`. Nada sensible en `VITE_*`.

## Verificación local antes de la PR

```bash
dotnet build HabitTrackerSystem.slnx && dotnet test
cd frontend && npm test && npm run lint && npm run typecheck && npm run build && cd ..
bash scripts/ci/verificar-reglas-rd.sh
bash scripts/ci/verificar-commits.sh origin/develop HEAD
bash scripts/ci/verificar-contexto.sh
```

- **Prueba manual** con la API real: `dotnet run --project Host/WebApi/WebApi.csproj` + `VITE_API_URL=http://localhost:5246 npx vite --port 5174`.
  - Para no enviar correos: `SMTP_HOST=` vacío y revisar `CorreosEnCola`.
  - Para promover a Administrador: `UPDATE usuarios SET Rol = 2 WHERE Correo = ...`.
- **Mutación:** rompe a propósito la regla clave del cambio y confirma que al menos una prueba falla.

## PR

1. Rama de trabajo, nunca push a `develop` ni a `main`. Si los commits se hicieron en `develop` local: `git push origin develop:feature/<nombre>` (sin `-u`).
2. Skill `flujo-pr`: cuerpo en 4 secciones (Qué cambia, Por qué, Cómo probarlo, Qué NO incluye). La PR va a `develop`. La PR `develop → main` se abre solo cuando el plan esté listo y funcional en `develop`.
3. Al cerrar: inventario, referencias de esta skill y ESTADO actualizados.
