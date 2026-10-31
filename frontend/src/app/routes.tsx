import type { ComponentType } from 'react'
import { Link, type RouteObject } from 'react-router'
import { ActivatePage } from '@/features/auth/pages/ActivatePage'
import { LoginPage } from '@/features/auth/pages/LoginPage'
import { RegisterPage } from '@/features/auth/pages/RegisterPage'
import { ResetPasswordPage } from '@/features/auth/pages/ResetPasswordPage'
import { ForgotPasswordPage, ResendActivationPage } from '@/features/auth/pages/SoloCorreoPage'
import { ComingSoonPage } from '@/features/coming-soon/ComingSoonPage'
import { PIEZAS_PENDIENTES } from '@/features/coming-soon/piezas'
import { EmptyState } from '@/shared/ui/Card'
import { RedirigirInicio, RequireAuth, SoloInvitados } from './guards'
import { AppLayout } from './layout/AppLayout'
import { AuthLayout } from './layout/AuthLayout'

function NoEncontrada() {
  return (
    <EmptyState
      icono="alerta"
      titulo="Página no encontrada"
      descripcion="La dirección no existe."
      accion={
        <Link to="/" className="font-semibold text-acento underline">
          Ir al inicio
        </Link>
      }
    />
  )
}

// Las pantallas autenticadas se cargan bajo demanda: el login no descarga cada feature.
function perezosa<M>(cargar: () => Promise<M>, nombre: keyof M): Pick<RouteObject, 'lazy'> {
  return {
    lazy: async () => ({ Component: (await cargar())[nombre] as ComponentType }),
  }
}

const paginas = {
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
  { index: true, element: <RedirigirInicio /> },
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
          { path: '*', element: <NoEncontrada /> },
        ],
      },
    ],
  },
]
