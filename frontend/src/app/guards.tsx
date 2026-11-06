import { Link, Navigate, Outlet, useLocation } from 'react-router'
import { rutaInicial, rutaPermitida } from '@/app/navigation'
import { esPerfilInexistente, usePerfil } from '@/features/perfil/api'
import { useAuth } from '@/shared/auth/AuthContext'
import type { Rol } from '@/shared/auth/roles'
import { EmptyState } from '@/shared/ui/Card'
import { CargandoPagina } from '@/shared/ui/Spinner'

// Exige sesión y, opcionalmente, un rol. Es una ayuda de navegación: la autorización
// real la decide el servidor en cada petición (RD-06).
export function RequireAuth({ roles }: { roles?: Rol[] }) {
  const { usuario, iniciando, cierreVoluntario } = useAuth()
  const location = useLocation()

  // Se está restaurando la sesión con la cookie: aún no se sabe si hay que ir al login.
  if (iniciando) return <CargandoPagina />
  if (!usuario) {
    // Tras un "Cerrar sesión" no se recuerda la ruta: quien entre después empieza de cero.
    const estado = cierreVoluntario ? undefined : { desde: location.pathname + location.search }
    return <Navigate to="/login" replace state={estado} />
  }
  if (roles && !roles.includes(usuario.rol)) {
    return (
      <div className="mx-auto max-w-lg px-4 py-12">
        <EmptyState
          nivelTitulo={1}
          titulo="No tienes acceso a esta sección"
          descripcion="Tu rol no permite usar esta parte de la aplicación."
          accion={
            <Link
              to={rutaInicial(usuario.rol)}
              className="inline-flex min-h-11 items-center rounded-lg px-3 font-semibold text-acento underline"
            >
              Ir a mi panel
            </Link>
          }
        />
      </div>
    )
  }
  return <Outlet />
}

// Las pantallas de login/registro no tienen sentido con una sesión abierta: al iniciar
// sesión, este guard lleva a la ruta que se pidió antes o a la inicial del rol.
export function SoloInvitados() {
  const { usuario, iniciando } = useAuth()
  const location = useLocation()
  if (iniciando) return <CargandoPagina />
  if (usuario) {
    const desde = (location.state as { desde?: unknown } | null)?.desde
    // Solo rutas internas (evita redirecciones abiertas) y accesibles para el rol que entra.
    const destino =
      typeof desde === 'string' &&
      desde.startsWith('/') &&
      !/^\/[\\/]/.test(desde) &&
      rutaPermitida(usuario.rol, desde)
        ? desde
        : rutaInicial(usuario.rol)
    return <Navigate to={destino} replace />
  }
  return <Outlet />
}

// Una cuenta Estándar sin perfil (recién registrada) completa primero el onboarding. Si la
// pieza no existe en el servidor (backend real sin el contrato propuesto) o falla por otra
// causa, no bloquea: cada pantalla maneja sus propios errores.
export function RequierePerfil() {
  const perfil = usePerfil()
  const location = useLocation()
  if (perfil.isPending) return <CargandoPagina />
  if (perfil.isError && esPerfilInexistente(perfil.error)) {
    // Al terminar, el onboarding vuelve a la página que se pidió (p. ej. un enlace profundo).
    return (
      <Navigate to="/bienvenida" replace state={{ desde: location.pathname + location.search }} />
    )
  }
  return <Outlet />
}
