import { Navigate, Outlet, useLocation } from 'react-router'
import { rutaInicial } from '@/app/navigation'
import { useAuth } from '@/shared/auth/AuthContext'
import type { Rol } from '@/shared/auth/roles'
import { EmptyState } from '@/shared/ui/Card'

// Exige sesión y, opcionalmente, un rol. Es una ayuda de navegación: la autorización
// real la decide el servidor en cada petición (RD-06).
export function RequireAuth({ roles }: { roles?: Rol[] }) {
  const { usuario, cierreVoluntario } = useAuth()
  const location = useLocation()

  if (!usuario) {
    // Tras un "Cerrar sesión" no se recuerda la ruta: quien entre después empieza de cero.
    const estado = cierreVoluntario ? undefined : { desde: location.pathname + location.search }
    return <Navigate to="/login" replace state={estado} />
  }
  if (roles && !roles.includes(usuario.rol)) {
    return (
      <EmptyState
        titulo="No tienes acceso a esta sección"
        descripcion="Tu rol no permite usar esta parte de la aplicación."
      />
    )
  }
  return <Outlet />
}

// Las pantallas de login/registro no tienen sentido con una sesión abierta: al iniciar
// sesión, este guard lleva a la ruta que se pidió antes o a la inicial del rol.
export function SoloInvitados() {
  const { usuario } = useAuth()
  const location = useLocation()
  if (usuario) {
    const desde = (location.state as { desde?: unknown } | null)?.desde
    // Solo rutas internas: evita redirecciones abiertas.
    const destino =
      typeof desde === 'string' && desde.startsWith('/') && !/^\/[\\/]/.test(desde)
        ? desde
        : rutaInicial(usuario.rol)
    return <Navigate to={destino} replace />
  }
  return <Outlet />
}

export function RedirigirInicio() {
  const { usuario } = useAuth()
  return <Navigate to={usuario ? rutaInicial(usuario.rol) : '/login'} replace />
}
