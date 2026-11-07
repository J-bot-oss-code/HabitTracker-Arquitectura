import { useEffect, useRef } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router'
import { useAuth } from '@/shared/auth/AuthContext'
import { cn } from '@/shared/lib/cn'
import { Icono } from '@/shared/ui/Icono'
import { useHandleRuta } from '../layout/RaizApp'
import { seccionesAjustesPara } from '../navigation'

// Configuración: un solo lugar para la cuenta, la apariencia, el inicio de sesión, la seguridad y
// la privacidad. Menú de secciones a la izquierda desde lg y en pestañas desplazables en móvil.
// El h1 es el nombre de la sección (RaizApp le pasa el foco al navegar entre secciones).
export function AjustesLayout() {
  const { usuario } = useAuth()
  const handle = useHandleRuta()
  const { pathname } = useLocation()
  const nav = useRef<HTMLElement>(null)

  // En móvil las secciones son una tira desplazable: la activa se mantiene a la vista.
  useEffect(() => {
    nav.current
      ?.querySelector<HTMLElement>('[aria-current="page"]')
      ?.scrollIntoView?.({ inline: 'center', block: 'nearest' })
  }, [pathname])
  if (!usuario) return null
  const secciones = seccionesAjustesPara(usuario.rol)

  return (
    <div className="mx-auto max-w-5xl">
      <header className="mb-6">
        <p className="rotulo">Configuración</p>
        <h1 className="mt-1 text-3xl font-extrabold sm:text-[2rem]">{handle?.titulo}</h1>
        {handle?.descripcion && <p className="mt-1.5 text-atenuado">{handle.descripcion}</p>}
      </header>

      <div className="grid grid-cols-[minmax(0,1fr)] items-start gap-6 lg:grid-cols-[15rem_minmax(0,1fr)] lg:gap-8">
        <nav
          ref={nav}
          aria-label="Secciones de configuración"
          data-no-imprimir
          className="min-w-0 lg:sticky lg:top-24"
        >
          <ul className="-mx-4 flex gap-1 overflow-x-auto px-4 pb-1 lg:mx-0 lg:flex-col lg:overflow-visible lg:px-0 lg:pb-0">
            {secciones.map((s) => (
              <li key={s.a} className="shrink-0">
                <NavLink
                  to={s.a}
                  className={({ isActive }) =>
                    cn(
                      'flex min-h-11 items-center gap-2.5 whitespace-nowrap rounded-xl px-3 text-sm transition-colors',
                      isActive
                        ? 'bg-acento-suave font-bold text-acento-fuerte'
                        : 'font-medium text-texto-2 hover:bg-hover hover:text-texto',
                    )
                  }
                >
                  <Icono nombre={s.icono} className="size-5 shrink-0" />
                  {s.etiqueta}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
        <div className="min-w-0">
          <Outlet />
        </div>
      </div>
    </div>
  )
}
