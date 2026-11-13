import type { ComponentType } from 'react'
import { Link, Navigate, useLocation, type RouteObject } from 'react-router'
import { ActivatePage } from '@/features/auth/pages/ActivatePage'
import { LoginPage } from '@/features/auth/pages/LoginPage'
import { RegisterPage } from '@/features/auth/pages/RegisterPage'
import { ResetPasswordPage } from '@/features/auth/pages/ResetPasswordPage'
import { ForgotPasswordPage, ResendActivationPage } from '@/features/auth/pages/SoloCorreoPage'
import { ComingSoonPage } from '@/features/coming-soon/ComingSoonPage'
import { PIEZAS_PENDIENTES } from '@/features/coming-soon/piezas'
import { CatalogoPlanes } from '@/features/planes/components/CatalogoPlanes'
import { LandingPage } from '@/features/publico/pages/LandingPage'
import { useAuth } from '@/shared/auth/AuthContext'
import { EmptyState } from '@/shared/ui/Card'
import { ErrorRuta } from './ErrorRuta'
import { RequierePerfil, RequireAuth, SoloInvitados } from './guards'
import { AjustesLayout } from './ajustes/AjustesLayout'
import { AppLayout } from './layout/AppLayout'
import { AuthLayout } from './layout/AuthLayout'
import { PublicLayout } from './layout/PublicLayout'
import { RaizApp } from './layout/RaizApp'
import { rutaInicial } from './navigation'

// Página 404 pública: con sesión, "Ir al inicio" vuelve al panel del rol.
function NoEncontrada() {
  const { usuario } = useAuth()
  return (
    <div className="py-12">
      <EmptyState
        nivelTitulo={1}
        icono="alerta"
        titulo="Página no encontrada"
        descripcion="La dirección no existe."
        accion={
          <Link
            to={usuario ? rutaInicial(usuario.rol) : '/'}
            className="inline-flex min-h-11 items-center rounded-lg px-3 font-semibold text-acento underline"
          >
            {usuario ? 'Ir a mi panel' : 'Ir al inicio'}
          </Link>
        }
      />
    </div>
  )
}

// «Mis permisos» ya no es una sección del menú: se pide desde cada acción y el historial vive en Configuración. El enlace
// viejo (/permisos?solicitar=…) sigue funcionando.
function RedirigirASolicitudes() {
  const { search } = useLocation()
  return <Navigate to={{ pathname: '/ajustes/solicitudes', search }} replace />
}

// Las pantallas se cargan bajo demanda, salvo la landing, el login y el registro (puertas de
// entrada): así cada visita descarga solo lo que usa.
function perezosa<M>(cargar: () => Promise<M>, nombre: keyof M): Pick<RouteObject, 'lazy'> {
  return {
    lazy: async () => ({ Component: (await cargar())[nombre] as ComponentType }),
  }
}

const paginas = {
  metas: perezosa(() => import('@/features/planes/pages/MetasPage'), 'MetasPage'),
  planDetalle: perezosa(() => import('@/features/planes/pages/PlanDetallePage'), 'PlanDetallePage'),
  planes: perezosa(() => import('@/features/planes/pages/PlanesPage'), 'PlanesPage'),
  miPlan: perezosa(() => import('@/features/mis-planes/pages/MiPlanPage'), 'MiPlanPage'),
  calculadoras: perezosa(
    () => import('@/features/publico/pages/CalculadorasPage'),
    'CalculadorasPage',
  ),
  legal: perezosa(() => import('@/features/publico/pages/LegalPage'), 'LegalPage'),
  bienvenida: perezosa(() => import('@/features/perfil/pages/OnboardingPage'), 'OnboardingPage'),
  ajustesCuenta: perezosa(() => import('./ajustes/CuentaAjustesPage'), 'CuentaAjustesPage'),
  ajustesSeguridad: perezosa(
    () => import('@/features/auth/pages/AjustesCuentaPages'),
    'AjustesSeguridadPage',
  ),
  ajustesInicioSesion: perezosa(
    () => import('@/features/auth/pages/AjustesCuentaPages'),
    'AjustesInicioSesionPage',
  ),
  apariencia: perezosa(() => import('./ajustes/AparienciaPage'), 'AparienciaPage'),
  perfilSalud: perezosa(() => import('@/features/perfil/pages/PerfilSaludPage'), 'PerfilSaludPage'),
  privacidad: perezosa(
    () => import('@/features/privacidad/pages/PrivacidadPage'),
    'PrivacidadPage',
  ),
  progreso: perezosa(() => import('@/features/progreso/pages/ProgresoPage'), 'ProgresoPage'),
  resumenAdmin: perezosa(
    () => import('@/features/auditoria/pages/ResumenAdminPage'),
    'ResumenAdminPage',
  ),
  auditoria: perezosa(() => import('@/features/auditoria/pages/AuditoriaPage'), 'AuditoriaPage'),
  hoy: perezosa(() => import('@/features/habits/pages/HoyPage'), 'HoyPage'),
  habitos: perezosa(() => import('@/features/habits/pages/HabitsPage'), 'HabitsPage'),
  habito: perezosa(() => import('@/features/habits/pages/HabitDetailPage'), 'HabitDetailPage'),
  documentos: perezosa(
    () => import('@/features/documentos/pages/DocumentosPage'),
    'DocumentosPage',
  ),
  misPermisos: perezosa(
    () => import('@/features/permissions/pages/MyPermissionsPage'),
    'MyPermissionsPage',
  ),
  permiso: perezosa(
    () => import('@/features/permissions/pages/PermissionDetailPage'),
    'PermissionDetailPage',
  ),
  bandejaPermisos: perezosa(
    () => import('@/features/permissions/pages/PermissionsInboxPage'),
    'PermissionsInboxPage',
  ),
  usuarios: perezosa(() => import('@/features/admin-users/pages/UsersPage'), 'UsersPage'),
}

export const rutas: RouteObject[] = [
  // Páginas públicas (§5.1): se ven con y sin sesión.
  {
    element: <PublicLayout />,
    children: [
      {
        index: true,
        handle: { titulo: 'Convierte tu meta en un plan diario' },
        element: (
          <LandingPage
            catalogo={
              <CatalogoPlanes
                base="/metas"
                anclaId="metas"
                titulo="Elige una meta"
                descripcion="Explora los planes libremente. Para empezar uno, crea tu cuenta."
              />
            }
          />
        ),
      },
      { path: 'metas', handle: { titulo: 'Elige una meta' }, ...paginas.metas },
      { path: 'metas/:slug', handle: { titulo: 'Detalle del plan' }, ...paginas.planDetalle },
      {
        path: 'calculadoras',
        handle: { titulo: 'Calculadora de calorías' },
        ...paginas.calculadoras,
      },
      { path: 'legal/:documento', handle: { titulo: 'Documentos legales' }, ...paginas.legal },
      // Cualquier dirección desconocida, con o sin sesión. Las rutas protegidas son explícitas,
      // así que una de ellas sin sesión sigue llevando al login.
      { path: '*', handle: { titulo: 'Página no encontrada' }, element: <NoEncontrada /> },
    ],
  },
  {
    element: <AuthLayout />,
    children: [
      {
        element: <SoloInvitados />,
        children: [
          { path: 'login', handle: { titulo: 'Iniciar sesión' }, element: <LoginPage /> },
          { path: 'registro', handle: { titulo: 'Crea tu cuenta' }, element: <RegisterPage /> },
          {
            path: 'recuperar',
            handle: { titulo: 'Recuperar contraseña' },
            element: <ForgotPasswordPage />,
          },
          {
            path: 'reenviar-activacion',
            handle: { titulo: 'Reenviar activación' },
            element: <ResendActivationPage />,
          },
        ],
      },
      // Accesibles también con sesión: vienen de enlaces de correo.
      { path: 'activar', handle: { titulo: 'Activar cuenta' }, element: <ActivatePage /> },
      {
        path: 'restablecer',
        handle: { titulo: 'Restablecer contraseña' },
        element: <ResetPasswordPage />,
      },
    ],
  },
  // Onboarding (pasos 3 a 5 del alta): con sesión de Estándar y el panel del alta.
  {
    element: <RequireAuth roles={['Estandar']} />,
    children: [
      {
        element: <AuthLayout />,
        children: [
          { path: 'bienvenida', handle: { titulo: 'Prepara tu plan' }, ...paginas.bienvenida },
        ],
      },
    ],
  },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <AppLayout />,
        children: [
          // Antes «Mi cuenta»: el enlace viejo sigue funcionando.
          { path: 'cuenta', element: <Navigate to="/ajustes/cuenta" replace /> },
          {
            path: 'ajustes',
            handle: { seccion: 'Configuración' },
            element: <AjustesLayout />,
            children: [
              { index: true, element: <Navigate to="/ajustes/cuenta" replace /> },
              {
                path: 'cuenta',
                handle: {
                  titulo: 'Cuenta',
                  descripcion: 'Tus datos en HabitTracker.',
                },
                ...paginas.ajustesCuenta,
              },
              {
                path: 'seguridad',
                handle: {
                  titulo: 'Contraseña y seguridad',
                  descripcion: 'Tu contraseña y cómo proteger tu cuenta.',
                },
                ...paginas.ajustesSeguridad,
              },
              {
                path: 'inicio-sesion',
                handle: {
                  titulo: 'Inicio de sesión',
                  descripcion: 'Con qué entras a tu cuenta y en qué dispositivos está abierta.',
                },
                ...paginas.ajustesInicioSesion,
              },
              {
                path: 'apariencia',
                handle: { titulo: 'Apariencia', descripcion: 'Elige cómo se ve la aplicación.' },
                ...paginas.apariencia,
              },
              {
                element: <RequireAuth roles={['Estandar']} />,
                children: [
                  {
                    path: 'perfil-salud',
                    handle: {
                      titulo: 'Perfil y salud',
                      descripcion: 'Tu jornada, tus horarios y, si un plan lo pide, tu salud.',
                    },
                    ...paginas.perfilSalud,
                  },
                  {
                    path: 'solicitudes',
                    handle: {
                      titulo: 'Solicitudes',
                      descripcion: 'Los permisos que pediste y cómo van.',
                    },
                    ...paginas.misPermisos,
                  },
                  // Descargar, decidir y retirarse no deben exigir dar más datos primero.
                  {
                    path: 'privacidad',
                    handle: {
                      titulo: 'Privacidad y datos',
                      descripcion: 'Descarga tus datos, decide qué compartes o retírate.',
                    },
                    ...paginas.privacidad,
                  },
                ],
              },
            ],
          },
          { path: 'permisos/:id', handle: { titulo: 'Solicitud de permiso' }, ...paginas.permiso },
          {
            element: <RequireAuth roles={['Estandar']} />,
            children: [
              {
                element: <RequierePerfil />,
                children: [
                  { path: 'hoy', handle: { titulo: 'Hoy' }, ...paginas.hoy },
                  { path: 'planes', handle: { titulo: 'Planes' }, ...paginas.planes },
                  {
                    path: 'planes/:slug',
                    handle: { titulo: 'Detalle del plan' },
                    ...paginas.planDetalle,
                  },
                  { path: 'mis-planes/:id', handle: { titulo: 'Mi plan' }, ...paginas.miPlan },
                  { path: 'progreso', handle: { titulo: 'Tu progreso' }, ...paginas.progreso },
                  { path: 'habitos', handle: { titulo: 'Mis hábitos' }, ...paginas.habitos },
                  {
                    path: 'habitos/:id',
                    handle: { titulo: 'Detalle del hábito' },
                    ...paginas.habito,
                  },
                  { path: 'permisos', element: <RedirigirASolicitudes /> },
                  { path: 'documentos', handle: { titulo: 'Mis archivos' }, ...paginas.documentos },
                ],
              },
            ],
          },
          {
            element: <RequireAuth roles={['Administrador']} />,
            children: [
              { path: 'admin', handle: { titulo: 'Resumen' }, ...paginas.resumenAdmin },
              {
                path: 'admin/permisos',
                handle: { titulo: 'Solicitudes de permiso' },
                ...paginas.bandejaPermisos,
              },
              { path: 'admin/usuarios', handle: { titulo: 'Usuarios' }, ...paginas.usuarios },
              {
                path: 'admin/cola-correos',
                handle: { titulo: 'Cola de correos' },
                element: <ComingSoonPage pieza={PIEZAS_PENDIENTES.colaCorreos} />,
              },
              { path: 'admin/auditoria', handle: { titulo: 'Auditoría' }, ...paginas.auditoria },
            ],
          },
          {
            path: 'notificaciones',
            handle: { titulo: 'Notificaciones' },
            element: <ComingSoonPage pieza={PIEZAS_PENDIENTES.notificaciones} />,
          },
          {
            path: 'reportes',
            handle: { titulo: 'Reportes' },
            element: <ComingSoonPage pieza={PIEZAS_PENDIENTES.reportes} />,
          },
        ],
      },
    ],
  },
]

// Raíz del router (App y pruebas): todas las rutas con un límite de errores genérico.
export const rutaRaiz: RouteObject = {
  path: '/',
  element: <RaizApp />,
  errorElement: <ErrorRuta />,
  children: rutas,
}
