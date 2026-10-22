# Bitácora de Sesión con el Agente — Asignación 2 (Gestión de Permisos)

## 1. Contexto de la Sesión
- **Fecha**: 2026-10-15
- **Módulo**: Gestión de Permisos (Core - Pieza 2)
- **Objetivo**: Implementación de solicitudes de permisos, máquina de estados y suite de pruebas unitarias.

## 2. Tareas Delegadas al Agente

### Tarea 1: Inicializar rama y bitácora de Asignación 2
- **Lo que se le pidió**: Verificar que `main` estuviera limpia, crear `feature/gestion-permisos`, validar la compilación y las pruebas existentes, y preparar esta bitácora.
- **Lo que devolvió**: `main` estaba limpia; la rama de trabajo fue creada. La solución no tiene proyectos de pruebas. La compilación inicial no pudo restaurar dependencias porque el proceso no tenía permiso para leer el `NuGet.Config` del perfil de usuario.
- **Error o imprecisión detectada en la IA**: El primer intento de crear la rama falló al escribir la referencia en `.git` por permisos del entorno.
- **Cómo se detectó y cómo se corrigió**: Git informó `Permission denied` al crear la referencia. Se reintentó la misma operación con permisos elevados y Git confirmó la creación de la rama.

### Tarea 2: Modelar SolicitudPermiso y centralizar sus transiciones
- **Lo que se le pidió**: Crear en `Permissions.Domain` los estados y la entidad `SolicitudPermiso`; permitir aprobación con aplicación automática, rechazo con motivo obligatorio y bloquear la auto-resolución y cualquier transición inválida; compilar y registrar un commit atómico con los requisitos RF-GP y RD-04.
- **Lo que devolvió**: Se añadieron `EstadoSolicitudPermiso`, `SolicitudPermiso`, `TransicionesSolicitudPermiso`, `DomainResult` y una suite unitaria para creación, aprobación, aplicación automática, rechazo y transiciones inválidas. Las transiciones se validan antes de cambiar el estado; la resolución requiere fecha UTC. `dotnet test` pasó 13 pruebas y `dotnet build` terminó con cero advertencias y cero errores.
- **Error o imprecisión detectada en la IA**: La primera ejecución de compilación no pudo leer el `NuGet.Config` del perfil de usuario por permisos del entorno. Además, el proyecto `Permissions.Domain.Tests` se ubicó inicialmente dentro de `Modules/Permissions` en vez de la carpeta raíz dedicada `tests/`.
- **Cómo se detectó y cómo se corrigió**: El error de acceso apareció durante la restauración de NuGet. Se repitió el build con permisos elevados y terminó correctamente. La revisión RD señaló que la pieza aún carecía de pruebas aisladas; se incorporó el proyecto de pruebas de dominio y cobertura de las reglas de esta fase. Al revisar la estructura, se detectó la ubicación incorrecta y, siguiendo la corrección solicitada, el proyecto se movió a `tests/Permissions.Domain.Tests`, se ajustó su referencia a `Permissions.Domain` y se actualizó la solución con `dotnet sln remove` y `dotnet sln add`.

### Tarea 3: Persistir solicitudes de permisos con EF Core
- **Lo que se le pidió**: Configurar el mapeo EF Core, registrar el `DbContext`, crear persistencia para agregar, consultar por Id y listar pendientes, generar y aplicar una migración SQL Server, y verificar build y pruebas.
- **Lo que devolvió**: Se añadieron `SolicitudPermisoConfiguration`, `PermissionsDbContext`, su factory de diseño, el contrato de repositorio en Application, la implementación EF Core en Infrastructure y el registro de dependencias en WebApi. Se generó y aplicó `AgregarTablaSolicitudesPermisos`; EF Core confirmó que quedó aplicada. `dotnet build` terminó con 0 advertencias y 0 errores; `dotnet test` pasó 13 pruebas.
- **Error o imprecisión detectada en la IA**: La variable de entorno disponible se llama `ConnectionStrings__Default`, mientras que el proyecto existente obtiene `ConnectionStrings:AccessControl`. La variable presente tampoco identifica `.\SQLEXPRESS` como servidor; la cadena `AccessControl` configurada en appsettings sí lo hace.
- **Cómo se detectó y cómo se corrigió**: Se inspeccionaron únicamente los nombres y metadatos de las cadenas, sin imprimir sus valores. Para cumplir el destino explícito `.\SQLEXPRESS` y mantener la configuración compartida del proyecto, la factory y el registro de Permissions leen `ConnectionStrings:AccessControl`, que admite overrides de entorno con esa misma clave. Se añadieron los paquetes de configuración faltantes y se fijó `System.Security.Cryptography.Pkcs` en la versión parcheada 9.0.21. Luego se aplicó la migración con EF Core y se verificó que aparezca como aplicada.

### Tarea 4: Casos de uso de aplicación (crear, consultar, aprobar, rechazar)
- **Lo que se le pidió**: Implementar en `Permissions.Application` los comandos/consultas y el servicio que conecta con el dominio y el repositorio: crear (RF-GP-01), consultar con filtro por rol (RF-GP-02), aprobar con aplicación automática (RF-GP-03, RF-GP-05, RF-GP-07) y rechazar con motivo obligatorio (RF-GP-04, RF-GP-07); verificar build y pruebas.
- **Lo que devolvió**: Se añadieron los DTOs de comandos/consulta (`CrearSolicitudPermisoCommand`, `ObtenerSolicitudesQuery`, `AprobarSolicitudCommand`, `RechazarSolicitudCommand`, `SolicitudPermisoDto`), el contrato `ISolicitudPermisoService` y su implementación `SolicitudPermisoService`. El servicio valida entradas, delega las reglas al dominio (`Aprobar`/`Rechazar`), solo persiste cuando el dominio acepta y traduce los fallos de dominio a `InvalidOperationException` con mensaje controlado. El Administrador ve pendientes o todas; el Estándar solo ve las propias y el detalle ajeno se rechaza. Se extendió el repositorio con `GetAllAsync` y se registró el servicio en el DI del módulo. `dotnet build` terminó con 0 advertencias y 0 errores; `dotnet test` pasó 17 pruebas.
- **Error o imprecisión detectada en la IA**: El primer `using` del servicio apuntó por error a `Permissions.Domain.Interfaces` (inexistente); el puerto vive en `Permissions.Application.Interfaces`. El build lo señaló (CS0234) y se corrigió el using.
- **Cómo se detectó y cómo se corrigió**: El error CS0234 indicó el namespace incorrecto. Se revisó dónde está declarado `ISolicitudPermisoRepository`, se corrigió el `using` y la compilación quedó limpia.

### Tarea 5: Controladores WebAPI con autorización JWT y respuestas RFC 7807
- **Lo que se le pidió**: Exponer `PermissionsController` (`api/permissions`) con `[Authorize]` global: crear (201), listar con filtro por rol, detalle, aprobar/rechazar solo Administrador, 404 controlado para IDs inexistentes y errores en `ProblemDetails` sin trazas; verificar build y pruebas.
- **Lo que devolvió**: Se creó el controlador con los 5 endpoints; el email y rol del JWT alimentan el servicio; el `404` se mapea en el controlador y el resto de errores burbujea al middleware global. Smoke test en vivo: crear 201, lista propia/admin, aprobar 200, re-aprobar 400, inexistente 404, rechazo sin motivo 400, con motivo 200, detalle ajeno 401, sin token 401, aprobar como Estándar 403 y auto-resolución rechazada 400. `dotnet build` con 0 advertencias y 0 errores; `dotnet test` pasó 17 pruebas.
- **Error o imprecisión detectada en la IA**: Los DTOs record usaban `[property: Required]` en parámetros del constructor primario y un `using` apuntó al namespace de interfaces equivocado; además el smoke test inicial falló porque la API en ejecución era anterior al fix. El log del servidor mostró `ThrowIfRecordTypeHasValidationOnProperties` y se corrigieron los atributos a `[param:]`.
- **Cómo se detectó y cómo se corrigió**: Se reprodujo el error contra la API en ejecución y se leyó la excepción real en el log (model binding de records). Se cambiaron los atributos a `[param:]` en `RegistroRequestDto`, `LoginRequestDto` y los records de los controladores, se reconstruyó y el smoke test pasó completo. Los datos de prueba se eliminaron de la BD.

### Tarea 6: Consolidación de la suite de pruebas unitarias (rúbrica)
- **Lo que se le pidió**: Revisar `tests/Permissions.Domain.Tests` para garantizar cobertura AAA de transición válida, transición prohibida con estado intacto explícito, auto-resolución y rechazo sin motivo; ejecutar `dotnet test`.
- **Lo que devolvió**: La suite ya cubría los 4 escenarios (aprobación hasta `Aplicada`, rechazo con motivo, re-aprobar/rechazar sobre `Aplicada`/`Rechazada` con `Assert.Equal` de estado intacto, auto-aprobación/auto-rechazo con `Pendiente` intacto y rechazo sin motivo con Theory nulo/vacío/espacios). No se requirió código nuevo. `dotnet test` pasó 17/17 (16 dominio + 1 infraestructura), rápido e independiente.
- **Error o imprecisión detectada en la IA**: Ninguna; la cobertura ya estaba consolidada.
- **Cómo se detectó y cómo se corrigió**: Se mapeó cada escenario de la rúbrica contra los métodos existentes y se ejecutó la suite completa en verde.

### Tarea 7: Alinear pruebas de Gestión de Permisos y preparar la Práctica 2
- **Lo que se hizo**: Se consolidó la suite para RF-GP-01 a RF-GP-07, se corrigió el filtrado de solicitudes estándar en el repositorio y se documentaron los seis defectos de la Variante A en el README. La base correcta quedó en el tag `practica-2`; cada tag de defecto se creó desde un commit hijo directo de esa base, cambiando solo código de producción.
- **Resultado verificado**: En la base, `dotnet test` pasó 25/25. En cada tag `practica-2-defecto-1` a `practica-2-defecto-6`, falló la prueba asignada. La rama `main` y los siete tags se publicaron en el remoto.
- **Errores o imprecisiones detectadas y corregidas**: El conteo inicial de la suite se documentó como 17, aunque la solución ya contenía 25 pruebas; además se corrigió el nombre de la prueba que detecta el defecto del filtro de consulta para reflejar el método real.

### Tarea 8: Completar pruebas y guía de Control de Acceso
- **Lo que se pidió**: Añadir pruebas unitarias independientes para registro/activación, emisión y claims de JWT, autorización por rol, y recuperación/restablecimiento; documentar la recuperación paso a paso; actualizar el conteo y registrar los hitos de Práctica 2.
- **Lo que se implementó**: Se añadieron cuatro pruebas unitarias al proyecto `tests/AccessControl.Application.Tests`, usando mocks para repositorio/correo y `JwtProvider` real con configuración de prueba en memoria. Se añadieron los alias HTTP `/api/auth/forgot-password` y `/api/auth/reset-password`, preservando las rutas existentes en español. El README ahora explica cómo obtener el código desde el correo o la cola local `CorreosEnCola` y cómo usarlo una sola vez.
- **Errores o imprecisiones detectadas y corregidas**: El texto anterior no explicaba cómo obtener el código de recuperación y el conteo de pruebas estaba desactualizado. Se documentaron ambas rutas de recuperación realmente expuestas y el total actualizado de la suite.

### Tarea 9: Hacer verificable la aplicación efectiva del permiso (RF-GP-05)
- **Lo que se pidió**: Contrastar la pieza con la rúbrica de la Práctica 2 y cerrar la brecha entre marcar una solicitud como `Aplicada` y comprobar que el solicitante puede usar el permiso concedido.
- **Lo que se implementó**: Se añadió una consulta persistida de permiso aplicado por solicitante y nombre de permiso, expuesta mediante `GET /api/permissions/effective/{permiso}`. La operación exige JWT, devuelve `200` únicamente cuando existe una solicitud del usuario en estado `Aplicada`, y responde `403` antes de aprobar o para otro usuario. Se añadieron pruebas de servicio, SQLite y de acción HTTP para confirmar el acceso, el rechazo, la autenticación y el límite de entrada.
- **Ajustes de suite**: Se añadió una prueba de creación en la capa de aplicación para RF-GP-01. Se agregó una prueba directa de la tabla de transiciones para RF-GP-06 (`Rechazada` → `Aplicada`), de modo que el segundo defecto de Variante A pueda plantarse exactamente sin agregar también una transición `Rechazada` → `Aprobada`.
- **Resultado de validación**: `dotnet test` pasó 36/36 y `dotnet build HabitTrackerSystem.slnx` terminó con 0 advertencias y 0 errores.

## 3. Resumen de Commits Realizados
- `docs(bitacora): inicializar bitacora de asignacion 2 y rama de trabajo`
- `feat(permissions): crear entidad SolicitudPermiso y maquina de estados (RF-GP-01, RF-GP-04, RF-GP-05, RF-GP-06, RF-GP-07, RD-04)`
- `feat(permissions): configurar mapeo de SolicitudPermiso en EF Core y aplicar migración (RD-09, RD-10)`
- `feat(permissions): implementar casos de uso de aplicacion para crear, consultar, aprobar y rechazar solicitudes (RF-GP-01, RF-GP-02, RF-GP-03, RF-GP-04, RF-GP-05, RF-GP-07)`
- `feat(permissions): agregar controlador WebAPI con autorizacion JWT y respuestas RFC 7807 (RF-GP-01, RF-GP-02, RF-GP-03, RF-GP-04, RF-GP-05, RF-GP-07, RD-06, RD-07, RD-08)`
- `test(permissions): consolidar suite de pruebas unitarias para transiciones validas, prohibidas y auto-resolucion (RF-GP-01 a RF-GP-07)`
- `test: alinear suite y consulta de permisos para Variante A [RF-GP-01 a RF-GP-07]` (`e49b170`)
- `docs(readme): agregar secciones de Pruebas y tabla de Defectos para Practica 2 [RF-GP-01 a RF-GP-07]` (`4f3d879`)
- `docs(readme): corregir prueba detectora del defecto de consulta [RF-GP-02]` (`26f178c`, tag base `practica-2`)
- `feat(permissions): activar permisos aprobados y ampliar suite [RF-GP-05, RF-GP-06]` (base actualizada de Práctica 2)

Los seis commits de mutación de la entrega se sitúan directamente sobre el commit etiquetado `practica-2` y modifican únicamente código de producción de Gestión de Permisos:

- `practica-2-defecto-1` — re-aprobación de una solicitud aplicada.
- `practica-2-defecto-2` — transición inválida desde Rechazada.
- `practica-2-defecto-3` — auto-resolución por el solicitante Administrador.
- `practica-2-defecto-4` — filtro de consulta invertido para Estándar.
- `practica-2-defecto-5` — transición inválida de Aprobada a Pendiente.
- `practica-2-defecto-6` — omisión del motivo de rechazo.

La actualización de Control de Acceso añade cuatro pruebas a las 25 ya existentes: 29 pruebas en total tras validar esta consolidación.

La consolidación posterior de RF-GP-01, RF-GP-05 y RF-GP-06 suma cuatro pruebas de Permisos y tres de la acción HTTP: el total pasa a 36. El README guía la comprobación funcional del permiso efectivo y los cuatro frentes de Control de Acceso desde el tag de entrega.
