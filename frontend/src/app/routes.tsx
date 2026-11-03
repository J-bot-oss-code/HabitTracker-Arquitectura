import type { ComponentType } from 'react'
import { Link, type RouteObject } from 'react-router'
import { ActivatePage } from '@/features/auth/pages/ActivatePage'
import { LoginPage } from '@/features/auth/pages/LoginPage'
import { RegisterPage } from '@/features/auth/pages/RegisterPage'
import { ResetPasswordPage } from '@/features/auth/pages/ResetPasswordPage'
import { ForgotPasswordPage, ResendActivationPage } from '@/features/auth/pages/SoloCorreoPage'
import { ComingSoonPage } from '@/features/coming-soon/ComingSoonPage'
import { PIEZAS_PENDIENTES } from '@/features/coming-soon/piezas'
import { LandingPage } from '@/features/publico/pages/LandingPage'
import { useAuth } from '@/shared/auth/AuthContext'
import { EmptyState } from '@/shared/ui/Card'
import { ErrorRuta } from './ErrorRuta'
import { RequireAuth, SoloInvitados } from './guards'
import { AppLayout } from './layout/AppLayout'
import { AuthLayout } from './layout/AuthLayout'
import { PublicLayout } from './layout/PublicLayout'
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

// Las pantallas se cargan bajo demanda, salvo la landing, el login y el registro (puertas de
// entrada): así cada visita descarga solo lo que usa.
function perezosa<M>(cargar: () => Promise<M>, nombre: keyof M): Pick<RouteObject, 'lazy'> {
  return {
    lazy: async () => ({ Component: (await cargar())[nombre] as ComponentType }),
  }
}

const paginas = {
  metas: perezosa(() => import('@/features/publico/pages/MetasPage'), 'MetasPage'),
  calculadoras: perezosa(
    () => import('@/features/publico/pages/CalculadorasPage'),
    'CalculadorasPage',
  ),
  legal: perezosa(() => import('@/features/publico/pages/LegalPage'), 'LegalPage'),
  cuenta: perezosa(() => import('@/features/auth/pages/AccountPage'), 'AccountPage'),
  habitos: perezosa(() => import('@/features/habits/pages/HabitsPage'), 'HabitsPage'),
  habito: perezosa(() => import('@/features/habits/pages/HabitDetailPage'), 'HabitDetailPage'),
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
      { index: true, element: <LandingPage /> },
      { path: 'metas', ...paginas.metas },
      { path: 'calculadoras', ...paginas.calculadoras },
      { path: 'legal/:documento', ...paginas.legal },
      // Cualquier dirección desconocida, con o sin sesión. Las rutas protegidas son explícitas,
      // así que una de ellas sin sesión sigue llevando al login.
      { path: '*', element: <NoEncontrada /> },
    ],
  },
  {
    element: <AuthLayout />,
    children: [
      {
        element: <SoloInvitados />,
        children: [
          { path: 'login', element: <LoginPage /> },
          { path: 'registro', element: <RegisterPage /> },
          { path: 'recuperar', element: <ForgotPasswordPage /> },
          { path: 'reenviar-activacion', element: <ResendActivationPage /> },
        ],
      },
      // Accesibles también con sesión: vienen de enlaces de correo.
      { path: 'activar', element: <ActivatePage /> },
      { path: 'restablecer', element: <ResetPasswordPage /> },
    ],
  },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <AppLayout />,
        children: [
          { path: 'cuenta', ...paginas.cuenta },
          { path: 'permisos/:id', ...paginas.permiso },
          {
            element: <RequireAuth roles={['Estandar']} />,
            children: [
              { path: 'habitos', ...paginas.habitos },
              { path: 'habitos/:id', ...paginas.habito },
              { path: 'permisos', ...paginas.misPermisos },
            ],
          },
          {
            element: <RequireAuth roles={['Administrador']} />,
            children: [
              { path: 'admin/permisos', ...paginas.bandejaPermisos },
              { path: 'admin/usuarios', ...paginas.usuarios },
              {
                path: 'admin/cola-correos',
                element: <ComingSoonPage pieza={PIEZAS_PENDIENTES.colaCorreos} />,
              },
              {
                path: 'admin/auditoria',
                element: <ComingSoonPage pieza={PIEZAS_PENDIENTES.auditoria} />,
              },
            ],
          },
          { path: 'documentos', element: <ComingSoonPage pieza={PIEZAS_PENDIENTES.documentos} /> },
          {
            path: 'notificaciones',
            element: <ComingSoonPage pieza={PIEZAS_PENDIENTES.notificaciones} />,
          },
          { path: 'reportes', element: <ComingSoonPage pieza={PIEZAS_PENDIENTES.reportes} /> },
        ],
      },
    ],
  },
]

// Raíz del router (App y pruebas): todas las rutas con un límite de errores genérico.
export const rutaRaiz: RouteObject = { path: '/', errorElement: <ErrorRuta />, children: rutas }
