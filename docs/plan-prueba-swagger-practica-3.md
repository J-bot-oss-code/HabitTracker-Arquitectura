# Plan de acción — Prueba de endpoints desde Swagger (Práctica 3)

## Objetivo
Validar manualmente que todos los endpoints de Control de Acceso, Gestión de Permisos y Habit Tracker funcionan y cumplen la lógica de negocio.

## Precondiciones
- .NET 10 SDK instalado (`dotnet --version`).
- SQL Server local disponible.
- Rama `feature/practica-3`, tag `practica-3`.
- User Secrets configurados para `ConnectionStrings:AccessControl`, `JwtSettings:Clave`, `JwtSettings:Emisor`, `JwtSettings:Audiencia`.
- Base de datos de prueba (no la compartida).

## Arranque
1. Aplicar migraciones:
   - `dotnet ef database update --project Modules/AccessControl/AccessControl.Infrastructure --startup-project Host/WebApi --context ApplicationDbContext`
   - `dotnet ef database update --project Modules/HabitTracker/HabitTracker.Infrastructure --startup-project Host/WebApi --context HabitTrackerDbContext`
   - `dotnet ef database update --project Modules/Permissions/Permissions.Infrastructure --startup-project Host/WebApi --context PermissionsDbContext`
2. Insertar datos de prueba de Meta:
   - Insertar una `Categoria` y una `Meta`; guardar `Metas.Id` como `META_ID`.
3. Ejecutar API:
   - `dotnet run --project Host/WebApi/WebApi.csproj`
4. Abrir Swagger:
   - `http://localhost:5246/swagger`

## Autenticación en Swagger
1. Registrar/activar usuarios de prueba.
2. Hacer login y copiar el JWT.
3. Pulsar **Authorize** y pegar `Bearer <JWT>`.

## Orden sugerido de prueba

### 1. Control de Acceso
- `POST /api/auth/register` → 201, usuario inactivo.
- `POST /api/auth/activate` → 200.
- `POST /api/auth/login` → 200 + JWT.
- `GET /api/auth/me` → 200 con id, correo y rol.
- `POST /api/auth/logout` → 200; repetir `GET /api/auth/me` → 401.
- `POST /api/auth/forgot-password` → 200 genérico.
- `POST /api/auth/reset-password` → 200.
- Login 5 veces con clave incorrecta → bloqueo.
- Usuario Estándar en `GET /api/admin/usuarios` → 403.
- Administrador en `GET /api/admin/usuarios` → 200.

### 2. Gestión de Permisos
- `POST /api/permissions` con `{ "permisoSolicitado": "Habits.AdvancedReconfig" }` → 201.
- `GET /api/permissions/{id}` → estado `Pendiente`.
- `GET /api/permissions?soloPendientes=false` → solo propias para Estándar.
- Administrador `POST /api/permissions/{id}/approve` → 200, estado `Aplicada`.
- Repetir approve sobre aplicada → 400.
- `POST /api/permissions/{id}/reject` con motivo vacío → 400.
- `POST /api/permissions/{id}/reject` con motivo → 200, estado `Rechazada`.
- `GET /api/permissions/actions/{permiso}/execute` sin permiso → 403; con permiso aplicado → 200.

### 3. Habit Tracker
- `POST /api/habits` con:
  `{ "nombre":"Leer 20 paginas","periodicidad":"Diaria","cantidadObjetivo":20,"unidad":"paginas","metaId":"META_ID" }`
  → 201, estado `Planificado`.
- `GET /api/habits` → solo propios.
- `POST /api/habits/{id}/transition` con `{ "nuevoEstado":"Activo" }` → 204.
- `Planificado -> Completado` → 400.
- `Activo -> Pausado` → 204.
- `Pausado -> Activo` → 204.
- `POST /api/habits/{id}/progress` con `{ "cantidad":5,"fechaUtc":"2026-10-26T10:00:00Z" }` → 204.
- Cantidad 0 → 400.
- Avance con hábito pausado → 400.
- `POST /api/habits/{id}/transition` a `Completado` sin meta cumplida → 400.
- Registrar avances hasta cumplir meta y completar → 204.
- Cualquier transición desde `Completado` → 400.
- `POST /api/habits/{id}/advanced-reconfig` antes de aprobar permiso → 403.
- Aprobar `Habits.AdvancedReconfig` y reintentar → 204.
- Verificar que el historial se conserva.
- Otro usuario sobre hábito ajeno → 403.

## Criterio de éxito
- Todos los endpoints responden con el status esperado.
- Las reglas de negocio se cumplen (estados, propietario, permiso, persistencia).
- No se usan datos de producción compartidos.
