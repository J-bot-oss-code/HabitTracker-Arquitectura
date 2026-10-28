# Plan de acción — Prueba de endpoints desde Swagger (Práctica 3)

## Objetivo
Validar manualmente que todos los endpoints de Control de Acceso, Gestión de Permisos y Habit Tracker funcionan y cumplen la lógica de negocio.

## Precondiciones
- .NET 10 SDK instalado (`dotnet --version`).
- SQL Server local disponible.
- Tag `practica-3`.
- User Secrets configurados para `JwtSettings:Clave` (y SMTP solo si se quieren correos reales).
- Base de datos de prueba (no la compartida). Se puede apuntar a otra base sin tocar el código:
  `ConnectionStrings__AccessControl=Server=.\SQLEXPRESS;Database=HabitTracker_Prueba;Trusted_Connection=True;TrustServerCertificate=True`
- Para no enviar correos reales, arrancar con la variable `SMTP_HOST` vacía: los correos quedan en `CorreosEnCola`.
- Usar correos de prueba de un dominio reservado (`@example.com`).

## Arranque
1. Aplicar migraciones (con la misma variable de conexión):
   - `dotnet ef database update --project Modules/AccessControl/AccessControl.Infrastructure --startup-project Host/WebApi --context ApplicationDbContext`
   - `dotnet ef database update --project Modules/HabitTracker/HabitTracker.Infrastructure --startup-project Host/WebApi --context HabitTrackerDbContext`
   - `dotnet ef database update --project Modules/Permissions/Permissions.Infrastructure --startup-project Host/WebApi --context PermissionsDbContext`
2. Insertar datos de prueba de Meta y guardar el Id como `META_ID`:
   ```sql
   DECLARE @c uniqueidentifier = NEWID(), @m uniqueidentifier = NEWID();
   INSERT Categorias(Id, Nombre, Descripcion) VALUES (@c, N'Salud', N'Prueba');
   INSERT Metas(Id, Titulo, EstadoActual, FechaLimite, CategoriaId) VALUES (@m, N'Meta de prueba', N'Activa', '2026-12-31', @c);
   SELECT @m AS META_ID;
   ```
3. Ejecutar la API: `dotnet run --project Host/WebApi/WebApi.csproj`
4. Abrir Swagger: `http://localhost:5246/swagger`

## Usuarios de prueba
- **E1** y **E2**: Estándar.
- **Admin**: registrar, activar y promover solo en la base de prueba con `UPDATE usuarios SET Rol = 2 WHERE Correo = N'admin@example.com'`; después volver a iniciar sesión.
- **Bloqueo**: usuario desechable para la prueba de fuerza bruta (queda bloqueado 15 min).
- Token de activación: `SELECT TokenAcceso FROM usuarios WHERE Correo = N'...'`.
- Código de recuperación: cuerpo del último correo en `CorreosEnCola` para ese destinatario.
- `logout` y `reset-password` invalidan el JWT: volver a iniciar sesión antes de seguir.
- Los enums se envían como texto (`"Diaria"`, `"Activo"`) o como número.

## Orden de prueba (status esperado)

### 1. Control de Acceso
- `POST /api/auth/register` con clave débil → 400.
- `POST /api/auth/register` E1 → 201 (inactivo). Login antes de activar → 400.
- `POST /api/auth/activate` → 200. Reutilizar el mismo token → 400.
- `POST /api/auth/login` → 200 + JWT. `GET /api/auth/me` → 200 con id, correo y rol.
- `POST /api/auth/logout` → 200; repetir `GET /api/auth/me` con el mismo JWT → 401.
- `POST /api/auth/forgot-password` con correo inexistente y con uno registrado → 200 con el mismo mensaje.
- `POST /api/auth/reset-password` con el código → 200. Reutilizar el código → 400. JWT anterior → 401. Login con la clave anterior → 401; con la nueva → 200.
- E1 en `GET /api/admin/usuarios` → 403. Admin → 200.

### 2. Gestión de Permisos
Usar el permiso `Consultar mi historial de permisos` (no `Habits.AdvancedReconfig`, que se prueba en la sección 3).
- E1 `POST /api/permissions` → 201 (S1). `GET /api/permissions/{S1}` → `Pendiente`.
- E2 `GET /api/permissions?soloPendientes=false` → no ve S1. E2 `GET /api/permissions/{S1}` → 403.
- E1 `GET /api/permissions/actions/Consultar%20mi%20historial%20de%20permisos/execute` → 403.
- E1 `POST /api/permissions/{S1}/approve` → 403. Admin → 200 (`Aplicada`, con aprobador y fecha).
- Admin approve otra vez sobre S1 → 400. E1 execute → 200.
- E1 crea S2. Admin `reject` con motivo vacío → 400 (sigue `Pendiente`); con motivo → 200 (`Rechazada`, motivo guardado). Approve sobre S2 → 400.
- Admin crea S3 y la aprueba o rechaza él mismo → 400 (RF-GP-07).
- Solicitud inexistente → 404.

### 3. Habit Tracker
Hábito H1: `{ "nombre":"Leer 20 paginas","periodicidad":"Diaria","cantidadObjetivo":20,"unidad":"paginas","metaId":"META_ID" }`
- Sin JWT → 401. Con JWT de Admin → 403 (las rutas requieren rol Estándar). `metaId` inexistente → 400. `cantidadObjetivo` 0 → 400.
- E1 crea H1 → 201, estado `Planificado`. E1 lo ve en `GET /api/habits`; E2 no.
- **Recién creado:** transición a `Completado` → 400. Avance → 400. E2 lo activa → 403.
- E1 `Activo` → 204. Avance con cantidad 0 → 400. `Completado` sin meta → 400.
- `Pausado` → 204. **Estando pausado:** avance → 400; `Completado` → 400. Volver a `Activo` → 204.
- Avance con la **fecha UTC actual** (`fechaUtc` de hoy con `Z`) de 10 → 204. Completar → 400 (10/20). Otro avance de 10 hoy → 204. E2 registra avance o completa → 403.
- E1 `Completado` → 204. Cualquier transición desde `Completado` → 400. `advanced-reconfig` sobre H1 → 400.
- Hábito H2 (otro hábito, no completado): crear, `Activo`, registrar un avance.
- `POST /api/habits/{H2}/advanced-reconfig` con `{ "periodicidad":"Semanal","cantidadObjetivo":30,"unidad":"minutos" }` sin solicitud → 403.
- E1 solicita `Habits.AdvancedReconfig` → 201. Reconfigurar con la solicitud `Pendiente` → 403.
- Admin aprueba → 200. E2 reconfigura H2 → 403. Reconfigurar con objetivo 0 → 400. E1 reconfigura → 204.
- `GET /api/habits`: H2 con `Semanal`/30/`minutos`, mismo estado e historial de avances conservado.

Nota: un avance con una fecha anterior al inicio del ciclo o de otro día (por ejemplo `2026-10-26T10:00:00Z`) se guarda, pero no cuenta para la meta del período vigente.

### 4. Persistencia
- Detener la API (`Ctrl+C`) sin borrar la base y volver a iniciarla.
- `GET /api/habits` (E1): H1 `Completado` con sus avances y H2 reconfigurado.
- `GET /api/permissions/{S1}` (Admin): `Aplicada` con aprobador y fecha.

### 5. Bloqueo
- Cinco logins fallidos con el usuario Bloqueo; después, login con la clave correcta → rechazado (400).

## Criterio de éxito
- Todos los endpoints responden con el status esperado.
- Las reglas de negocio se cumplen (estados, propietario, permiso, persistencia).
- No se usan datos de producción compartidos.

## Última ejecución
28-10-2026, base aislada `HabitTracker_PruebaP3`, SMTP desactivado: 83/83 comprobaciones correctas y persistencia verificada tras reiniciar. Esta ejecución detectó y permitió corregir dos defectos: avances que no se guardaban (500 por `DbUpdateConcurrencyException`) y enums que no se aceptaban como texto.
