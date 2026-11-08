# Inventario generado

> Generado por `scripts/contexto/generar-inventario.sh`. No editar a mano: regenerar tras cambiar
> controllers, migraciones, pruebas, features o contenido. El CI (`verificar-contexto.sh`) falla si quedó viejo.

## Endpoints (Host/WebApi/Controllers)

| Controller | Verbo | Ruta | Acceso |
|---|---|---|---|
| AdminUsuariosController | GET | `/api/admin/usuarios` | rol Administrador |
| AdminUsuariosController | PATCH | `/api/admin/usuarios/{id:guid}/rol` | rol Administrador |
| AdminUsuariosController | PATCH | `/api/admin/usuarios/{id:guid}/estado` | rol Administrador |
| AdminUsuariosController | POST | `/api/admin/usuarios/{id:guid}/forzar-restablecimiento` | rol Administrador |
| AuthController | POST | `/api/auth/register` | anónimo |
| AuthController | POST | `/api/auth/activate` | anónimo |
| AuthController | GET | `/api/auth/activate` | anónimo |
| AuthController | POST | `/api/auth/resend-activation` | anónimo |
| AuthController | POST | `/api/auth/login` | anónimo |
| AuthController | POST | `/api/auth/google` | anónimo |
| AuthController | POST | `/api/auth/google/vincular` | anónimo |
| AuthController | GET | `/api/auth/vinculos` | autenticado |
| AuthController | DELETE | `/api/auth/vinculos/{proveedor}` | autenticado |
| AuthController | POST | `/api/auth/refresh` | anónimo |
| AuthController | GET | `/api/auth/me` | autenticado |
| AuthController | POST | `/api/auth/logout` | autenticado |
| AuthController | POST | `/api/auth/logout-todas` | autenticado |
| AuthController | GET | `/api/auth/sesiones` | autenticado |
| AuthController | DELETE | `/api/auth/sesiones/{id:guid}` | autenticado |
| AuthController | POST | `/api/auth/recuperar-password` | anónimo |
| AuthController | POST | `/api/auth/forgot-password` | anónimo |
| AuthController | POST | `/api/auth/restablecer-password` | anónimo |
| AuthController | POST | `/api/auth/reset-password` | anónimo |
| AuthController | POST | `/api/auth/cambiar-password` | autenticado |
| DocumentsController | POST | `/api/documents` | autenticado |
| DocumentsController | GET | `/api/documents` | autenticado |
| DocumentsController | GET | `/api/documents/{id:guid}` | autenticado |
| DocumentsController | GET | `/api/documents/{id:guid}/download` | autenticado |
| DocumentsController | DELETE | `/api/documents/{id:guid}` | autenticado |
| HabitsController | POST | `/api/habits` | rol Estandar |
| HabitsController | GET | `/api/habits` | rol Estandar |
| HabitsController | GET | `/api/habits/{id:guid}` | rol Estandar |
| HabitsController | POST | `/api/habits/{id:guid}/transition` | rol Estandar |
| HabitsController | POST | `/api/habits/{id:guid}/progress` | rol Estandar |
| HabitsController | POST | `/api/habits/{id:guid}/advanced-reconfig` | rol Estandar |
| PermissionsController | POST | `/api/permissions` | autenticado |
| PermissionsController | GET | `/api/permissions` | autenticado |
| PermissionsController | GET | `/api/permissions/effective/{permiso}` | autenticado |
| PermissionsController | GET | `/api/permissions/actions/{permiso}/execute` | autenticado |
| PermissionsController | GET | `/api/permissions/{id:guid}` | autenticado |
| PermissionsController | POST | `/api/permissions/{id:guid}/approve` | rol Administrador |
| PermissionsController | POST | `/api/permissions/{id:guid}/reject` | rol Administrador |
| PlanDelDiaController | GET | `/api/habits/plan-del-dia` | rol Estandar |
| PrivacidadController | GET | `/api/privacy/consents` | autenticado |
| PrivacidadController | PUT | `/api/privacy/consents` | autenticado |
| PrivacidadController | POST | `/api/privacy/deletion-request` | rol Estandar |
| PrivacidadController | GET | `/api/privacy/deletion-request` | rol Estandar |
| PrivacidadController | DELETE | `/api/privacy/deletion-request` | rol Estandar |
| PrivacidadController | GET | `/api/privacy/export` | rol Estandar |

## Migraciones por contexto

- **AccessControl:** InitialCreate, AgregarBloqueoCuenta, AgregarCodigoRecuperacion, MejorasCalificacion, AgregarIntentosActivacion, AgregarIndiceUnicoCorreo, AgregarConsentimientos, AgregarSesionesUsuario, AgregarLoginsExternos, AgregarBajaCuenta
- **DocumentManager:** AgregarTablaDocumentos
- **HabitTracker:** InitialCreate, ActualizarEsquemaHabitos, RegistroDiarioIdAsignadoPorDominio, HabitoMetaOpcionalYLongitudes
- **Permissions:** AgregarTablaSolicitudesPermisos

## Proyectos de prueba (backend)

| Proyecto | Archivos | [Fact] | [Theory] |
|---|---|---|---|
| AccessControl.Application.Tests | 27 | 268 | 40 |
| AccessControl.Infrastructure.Tests | 8 | 62 | 7 |
| DocumentManager.Api.Tests | 4 | 31 | 6 |
| DocumentManager.Application.Tests | 1 | 30 | 3 |
| DocumentManager.Domain.Tests | 1 | 15 | 2 |
| DocumentManager.Infrastructure.Tests | 3 | 15 | 2 |
| HabitTracker.Api.Tests | 2 | 12 | 2 |
| HabitTracker.Application.Tests | 2 | 35 | 3 |
| HabitTracker.Domain.Tests | 5 | 44 | 3 |
| HabitTracker.Integration.Tests | 5 | 21 | 4 |
| Permissions.Application.Tests | 1 | 7 | 1 |
| Permissions.Domain.Tests | 1 | 15 | 1 |
| Permissions.Infrastructure.Tests | 2 | 6 | 1 |
| **Total** | | **561** | **75** |

Los `[Theory]` cuentan varios casos cada uno: el total real lo da `dotnet test`.

## Adaptadores del Host (puertos entre módulos)

- `ParticipanteDocumentos.cs`: `class ParticipanteDocumentos(IDocumentoService servicio) : IParticipanteDatosUsuario`
- `ParticipanteHabitos.cs`: `class ParticipanteHabitos(IHabitoService servicio) : IParticipanteDatosUsuario`
- `ParticipantePermisos.cs`: `class ParticipantePermisos(ISolicitudPermisoService servicio) : IParticipanteDatosUsuario`
- `PermisoServiceAdapter.cs`: `class PermisoServiceAdapter(ISolicitudPermisoService solicitudPermisoService) : IPermisoService`

## Workers (BackgroundService)

- `Modules/AccessControl/AccessControl.Infrastructure/Workers/BajaCuentasWorker.cs`
- `Modules/AccessControl/AccessControl.Infrastructure/Workers/EmailSenderWorker.cs`

## Frontend: features

- `admin-users`: 3 archivos
- `auditoria`: 9 archivos
- `auth`: 27 archivos
- `coming-soon`: 2 archivos
- `habits`: 23 archivos
- `perfil`: 7 archivos
- `permissions`: 13 archivos
- `privacidad`: 10 archivos
- `progreso`: 11 archivos
- `publico`: 19 archivos

## Frontend: contratos propuestos (sin backend)

- `GET /api/admin/metrics` (admin.ts)
- `GET /api/audit` (admin.ts)
- `GET /api/profile` (perfil.ts)
- `PUT /api/profile` (perfil.ts)
- `GET /api/progress/weights` (progreso.ts)
- `POST /api/progress/weights` (progreso.ts)
- `GET /api/progress/measurements` (progreso.ts)
- `POST /api/progress/measurements` (progreso.ts)

## Contenido del módulo de negocio

- `catalogo-planes.json`: aún no existe (Fase 3)
- `mensajes-motivacion.json`: aún no existe (Fase 4)
