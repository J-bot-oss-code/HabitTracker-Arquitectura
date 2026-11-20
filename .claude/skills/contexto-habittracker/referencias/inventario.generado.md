# Inventario generado

> Generado por `scripts/contexto/generar-inventario.sh`. No editar a mano: regenerar tras cambiar
> controllers, migraciones, pruebas, features o contenido. El CI (`verificar-contexto.sh`) falla si quedó viejo.

## Endpoints (Host/WebApi/Controllers)

| Controller | Verbo | Ruta | Acceso |
|---|---|---|---|
| AdjuntosPlanController | GET | `/api/my-plans/attachments` | rol Estandar |
| AdjuntosPlanController | GET | `/api/my-plans/{id:guid}/attachments` | rol Estandar |
| AdjuntosPlanController | POST | `/api/my-plans/{id:guid}/attachments` | rol Estandar |
| AdjuntosPlanController | DELETE | `/api/my-plans/{id:guid}/attachments/{documentoId:guid}` | rol Estandar |
| AdminColaCorreosController | GET | `/api/admin/email-queue` | rol Administrador |
| AdminColaCorreosController | POST | `/api/admin/email-queue/process` | rol Administrador |
| AdminUsuariosController | GET | `/api/admin/usuarios` | rol Administrador |
| AdminUsuariosController | PATCH | `/api/admin/usuarios/{id:guid}/rol` | rol Administrador |
| AdminUsuariosController | PATCH | `/api/admin/usuarios/{id:guid}/estado` | rol Administrador |
| AdminUsuariosController | POST | `/api/admin/usuarios/{id:guid}/forzar-restablecimiento` | rol Administrador |
| AuditController | GET | `/api/audit` | rol Administrador |
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
| BajaRecordatoriosController | GET | `/api/reminders/unsubscribe` | anónimo |
| BajaRecordatoriosController | POST | `/api/reminders/unsubscribe` | anónimo |
| CheckinCorreoController | GET | `/api/reminders/checkin` | anónimo |
| CheckinCorreoController | POST | `/api/reminders/checkin` | anónimo |
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
| MiActividadController | GET | `/api/me/activity` | autenticado |
| MyPlansController | POST | `/api/my-plans` | rol Estandar |
| MyPlansController | GET | `/api/my-plans` | rol Estandar |
| MyPlansController | GET | `/api/my-plans/{id:guid}` | rol Estandar |
| MyPlansController | POST | `/api/my-plans/{id:guid}/transition` | rol Estandar |
| MyPlansController | POST | `/api/my-plans/{id:guid}/adjust` | rol Estandar |
| MyPlansController | PUT | `/api/my-plans/{id:guid}/reminder` | rol Estandar |
| NotificationsController | GET | `/api/notifications` | autenticado |
| NotificationsController | POST | `/api/notifications/{id:guid}/read` | autenticado |
| NotificationsController | GET | `/api/notifications/preferences` | autenticado |
| NotificationsController | PUT | `/api/notifications/preferences` | autenticado |
| PermissionsController | POST | `/api/permissions` | autenticado |
| PermissionsController | GET | `/api/permissions` | autenticado |
| PermissionsController | GET | `/api/permissions/effective/{permiso}` | autenticado |
| PermissionsController | GET | `/api/permissions/actions/{permiso}/execute` | autenticado |
| PermissionsController | GET | `/api/permissions/{id:guid}` | autenticado |
| PermissionsController | POST | `/api/permissions/{id:guid}/approve` | rol Administrador |
| PermissionsController | POST | `/api/permissions/{id:guid}/reject` | rol Administrador |
| PlanDelDiaController | GET | `/api/habits/plan-del-dia` | rol Estandar |
| PlansController | GET | `/api/plans` | anónimo |
| PlansController | GET | `/api/plans/recommended` | rol Estandar |
| PlansController | GET | `/api/plans/{slug}` | anónimo |
| PrivacidadController | GET | `/api/privacy/consents` | autenticado |
| PrivacidadController | PUT | `/api/privacy/consents` | autenticado |
| PrivacidadController | POST | `/api/privacy/deletion-request` | rol Estandar |
| PrivacidadController | GET | `/api/privacy/deletion-request` | rol Estandar |
| PrivacidadController | DELETE | `/api/privacy/deletion-request` | rol Estandar |
| PrivacidadController | GET | `/api/privacy/export` | rol Estandar |
| ProfileController | GET | `/api/profile` | rol Estandar |
| ProfileController | PUT | `/api/profile` | rol Estandar |
| ProfileController | GET | `/api/profile/health` | rol Estandar |
| ProfileController | PUT | `/api/profile/health/objetivos` | rol Estandar |
| ProfileController | PUT | `/api/profile/health/seguridad` | rol Estandar |
| ProfileController | PUT | `/api/profile/health/condiciones` | rol Estandar |
| ProgressController | GET | `/api/progress/weights` | rol Estandar |
| ProgressController | POST | `/api/progress/weights` | rol Estandar |
| ProgressController | GET | `/api/progress/measurements` | rol Estandar |
| ProgressController | POST | `/api/progress/measurements` | rol Estandar |
| ReportsController | GET | `/api/reports/permissions` | autenticado |
| ReportsController | GET | `/api/reports/adherence` | autenticado |
| TodayController | GET | `//api/today` | rol Estandar |
| TodayController | GET | `//api/progress/summary` | rol Estandar |
| TodayController | POST | `//api/progress/achievements/seen` | rol Estandar |

## Migraciones por contexto

- **AccessControl:** InitialCreate, AgregarBloqueoCuenta, AgregarCodigoRecuperacion, MejorasCalificacion, AgregarIntentosActivacion, AgregarIndiceUnicoCorreo, AgregarConsentimientos, AgregarSesionesUsuario, AgregarLoginsExternos, AgregarBajaCuenta, IndiceCorreosPorEstado, AgregarOrigenDelCodigoDeRecuperacion
- **Auditing:** CrearRegistrosAuditoria
- **DocumentManager:** AgregarTablaDocumentos
- **HabitTracker:** InitialCreate, ActualizarEsquemaHabitos, RegistroDiarioIdAsignadoPorDominio, HabitoMetaOpcionalYLongitudes, AgregarPerfilYSalud, AgregarCatalogoPlanes, AgregarPlanesUsuario, QuitarMetaSlugDelPerfil, AgregarAdjuntosPlan, AgregarFechaCancelacionAlPlan, AgregarOrigenYClaveDeRegistro, AgregarUltimoRecordatorioDelPlan
- **Notifications:** CrearNotificaciones
- **Permissions:** AgregarTablaSolicitudesPermisos

## Proyectos de prueba (backend)

| Proyecto | Archivos | [Fact] | [Theory] |
|---|---|---|---|
| AccessControl.Application.Tests | 31 | 297 | 47 |
| AccessControl.Infrastructure.Tests | 11 | 82 | 9 |
| Auditing.Api.Tests | 4 | 12 | 0 |
| Auditing.Infrastructure.Tests | 1 | 19 | 2 |
| DocumentManager.Api.Tests | 5 | 33 | 7 |
| DocumentManager.Application.Tests | 2 | 39 | 3 |
| DocumentManager.Domain.Tests | 1 | 15 | 2 |
| DocumentManager.Infrastructure.Tests | 3 | 15 | 2 |
| HabitTracker.Api.Tests | 9 | 52 | 14 |
| HabitTracker.Application.Tests | 9 | 143 | 9 |
| HabitTracker.Domain.Tests | 12 | 170 | 31 |
| HabitTracker.Integration.Tests | 13 | 76 | 4 |
| Notifications.Api.Tests | 4 | 24 | 2 |
| Notifications.Application.Tests | 1 | 12 | 0 |
| Notifications.Domain.Tests | 1 | 8 | 1 |
| Notifications.Infrastructure.Tests | 1 | 5 | 0 |
| Permissions.Application.Tests | 3 | 22 | 2 |
| Permissions.Domain.Tests | 1 | 15 | 1 |
| Permissions.Infrastructure.Tests | 3 | 13 | 1 |
| Reports.Api.Tests | 1 | 6 | 0 |
| Reports.Application.Tests | 1 | 8 | 1 |
| **Total** | | **1066** | **138** |

Los `[Theory]` cuentan varios casos cada uno: el total real lo da `dotnet test`.

## Adaptadores del Host (puertos entre módulos)

- `AuditorDocumentosAdapter.cs`: `class AuditorDocumentosAdapter(IRegistroAuditoriaService historial, IUsuarioRepository usuarios) : IAuditorDocumentos`
- `AuditorPermisosAdapter.cs`: `class AuditorPermisosAdapter(IRegistroAuditoriaService historial, IUsuarioRepository usuarios) : IAuditorPermisos`
- `AuditoriaControlAccesoAdapter.cs`: `class AuditoriaControlAccesoAdapter(IRegistroAuditoriaService historial, IUsuarioRepository usuarios) : IAuditoriaService`
- `CanalRecordatoriosAdapter.cs`: `class CanalRecordatoriosAdapter(`
- `ColaCorreosAdapter.cs`: `class ColaCorreosAdapter(IEmailQueue cola) : IColaCorreos`
- `ConsentimientoSaludAdapter.cs`: `class ConsentimientoSaludAdapter(IConsentimientoService consentimientos) : IConsentimientoSalud`
- `DirectorioUsuariosAdapter.cs`: `class DirectorioUsuariosAdapter(IUsuarioRepository usuarios) : IDirectorioUsuarios`
- `DocumentosUsuarioAdapter.cs`: `class DocumentosUsuarioAdapter(IDocumentoService documentos) : IDocumentosUsuario`
- `EstadoCuentasAdapter.cs`: `class EstadoCuentasAdapter(IUsuarioRepository usuarios) : IEstadoCuentas`
- `FuenteConstanciaAdapter.cs`: `class FuenteConstanciaAdapter(IReporteConstanciaService constancia) : IFuenteConstancia`
- `FuenteSolicitudesAdapter.cs`: `class FuenteSolicitudesAdapter(ISolicitudPermisoReportes permisos) : IFuenteSolicitudes`
- `GuardaEliminacionDocumentoAdapter.cs`: `class GuardaEliminacionDocumentoAdapter(`
- `NotificadorPermisosAdapter.cs`: `class NotificadorPermisosAdapter(INotificacionService notificaciones, IDirectorioUsuarios directorio) : INotificadorPermisos`
- `NotificadorUsuarioAdapter.cs`: `class NotificadorUsuarioAdapter(INotificacionService notificaciones, IDirectorioUsuarios directorio) : INotificadorUsuario`
- `ParticipanteDocumentos.cs`: `class ParticipanteDocumentos(IDocumentoService servicio) : IParticipanteDatosUsuario`
- `ParticipanteHabitos.cs`: `class ParticipanteHabitos(IHabitoService servicio) : IParticipanteDatosUsuario`
- `ParticipanteNotificaciones.cs`: `class ParticipanteNotificaciones(INotificacionService servicio) : IParticipanteDatosUsuario`
- `ParticipantePerfilSalud.cs`: `class ParticipantePerfilSalud(IDatosPerfilSaludService servicio) : IParticipanteDatosUsuario`
- `ParticipantePermisos.cs`: `class ParticipantePermisos(ISolicitudPermisoService servicio) : IParticipanteDatosUsuario`
- `ParticipantePlanes.cs`: `class ParticipantePlanes(IDatosPlanesService servicio) : IParticipanteDatosUsuario`
- `PermisoServiceAdapter.cs`: `class PermisoServiceAdapter(ISolicitudPermisoService solicitudPermisoService) : IPermisoService`

## Workers (BackgroundService)

- `Modules/AccessControl/AccessControl.Infrastructure/Workers/BajaCuentasWorker.cs`
- `Modules/AccessControl/AccessControl.Infrastructure/Workers/EmailSenderWorker.cs`
- `Modules/HabitTracker/HabitTracker.Infrastructure/Workers/RecordatoriosWorker.cs`

## Frontend: features

- `admin-users`: 3 archivos
- `auditoria`: 14 archivos
- `auth`: 27 archivos
- `cola-correos`: 5 archivos
- `documentos`: 11 archivos
- `habits`: 26 archivos
- `mis-planes`: 10 archivos
- `notificaciones`: 7 archivos
- `perfil`: 11 archivos
- `permissions`: 14 archivos
- `planes`: 11 archivos
- `privacidad`: 10 archivos
- `progreso`: 11 archivos
- `publico`: 18 archivos
- `reportes`: 10 archivos

## Frontend: contratos propuestos (sin backend)

- `GET /api/admin/metrics` (admin.ts)

## Contenido del módulo de negocio

- `Modules/HabitTracker/HabitTracker.Infrastructure/Contenido/catalogo-planes.json`: 13 planes
- `Modules/HabitTracker/HabitTracker.Infrastructure/Contenido/mensajes-motivacion.json`: 33 mensajes
