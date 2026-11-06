import { useEffect, useRef } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router'
import { useCerrarSesion } from '@/features/auth/api'
import { useAuth } from '@/shared/auth/AuthContext'
import { etiquetaRol } from '@/shared/auth/roles'
import { cn } from '@/shared/lib/cn'
import { SelectorTemaMenu } from '@/shared/theme/SelectorTema'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { AvisosDeCuenta } from '@/features/privacidad/components/AvisosDeCuenta'
import { Icono } from '@/shared/ui/Icono'
import { navegacionPara, rutaInicial } from '../navigation'
import { AvisoModoDemo } from './AvisoModoDemo'
import { Marca } from './Marca'

export function AppLayout() {
  const { usuario } = useAuth()
  const { salir, saliendo } = useCerrarSesion()
  const { pathname } = useLocation()
  const nav = useRef<HTMLElement>(null)

  // En móvil la barra inferior se desplaza: el ítem activo se mantiene a la vista.
  useEffect(() => {
    nav.current
      ?.querySelector<HTMLElement>('[aria-current="page"]')
      ?.scrollIntoView?.({ inline: 'center', block: 'nearest' })
  }, [pathname])

  if (!usuario) return null
  const items = navegacionPara(usuario.rol)

  return (
    <div className="flex min-h-screen flex-col">
      <a
        href="#contenido"
        className="sr-only z-[70] rounded-lg bg-acento px-4 py-2 font-semibold text-acento-texto focus:not-sr-only focus:fixed focus:left-4 focus:top-4"
      >
        Saltar al contenido
      </a>
      <AvisoModoDemo />

      <header data-no-imprimir className="sticky top-0 z-40 border-b border-borde bg-superficie">
        <div className="mx-auto flex h-16 max-w-7xl items-center justify-between gap-2 px-4 sm:gap-3 sm:px-6">
          <div className="flex min-w-0 items-center gap-2.5">
            <Link
              to={rutaInicial(usuario.rol)}
              aria-label="Inicio"
              className="inline-flex min-h-11 min-w-11 items-center rounded-lg"
            >
              <Marca />
            </Link>
            {/* Cabecera de administración (§5.6): insignia visible del rol. */}
            {usuario.rol === 'Administrador' && <Badge tono="marca">Admin</Badge>}
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <SelectorTemaMenu />
            <Link
              to="/cuenta"
              aria-label="Mi cuenta"
              className="flex min-h-11 min-w-11 items-center justify-center gap-2.5 rounded-lg border border-borde-fuerte bg-superficie px-2.5 hover:border-acento md:px-3"
            >
              <Icono nombre="cuenta" />
              <span className="hidden text-left leading-tight md:block">
                <span className="block text-sm font-semibold">{usuario.correo}</span>
                <span className="rotulo block">{etiquetaRol[usuario.rol]}</span>
              </span>
            </Link>
            <Button
              variante="secundario"
              tamano="sm"
              cargando={saliendo}
              onClick={salir}
              aria-label="Cerrar sesión"
              className="px-2.5 sm:px-3.5"
            >
              <Icono nombre="salir" className="size-4" />
              <span className="hidden sm:inline" aria-hidden="true">
                Cerrar sesión
              </span>
            </Button>
          </div>
        </div>
      </header>

      <div className="mx-auto flex w-full max-w-7xl flex-1 gap-8 px-4 pb-24 pt-6 sm:px-6 lg:pb-8">
        {/* Un solo <nav>: barra inferior desplazable en móvil y barra lateral desde lg. */}
        <nav
          ref={nav}
          data-no-imprimir
          aria-label="Principal"
          className={cn(
            'fixed inset-x-0 bottom-0 z-40 flex gap-1 overflow-x-auto border-t border-borde-fuerte bg-superficie px-2 py-1.5',
            'lg:sticky lg:top-24 lg:bottom-auto lg:z-auto lg:h-fit lg:w-60 lg:shrink-0 lg:flex-col lg:overflow-visible lg:rounded-2xl lg:border lg:border-borde lg:bg-superficie lg:p-2',
          )}
        >
          {items.map((item) => (
            <NavLink
              key={item.a}
              to={item.a}
              end={item.fin}
              className={({ isActive }) =>
                cn(
                  'relative flex min-h-14 min-w-[5rem] shrink-0 flex-col items-center justify-center gap-0.5 rounded-xl px-2 py-1 text-xs font-semibold leading-tight transition-colors',
                  'lg:min-h-11 lg:min-w-0 lg:flex-row lg:justify-start lg:gap-3 lg:px-3 lg:text-sm',
                  // La página actual no depende solo del color (WCAG 1.4.1): negrita y una marca
                  // de acento (arriba en la barra inferior, a la izquierda en el lateral).
                  isActive
                    ? 'bg-acento-suave font-bold text-acento-fuerte shadow-[inset_0_3px_0_var(--c-acento)] lg:shadow-[inset_3px_0_0_var(--c-acento)]'
                    : 'text-texto-2 hover:bg-hover hover:text-texto',
                )
              }
            >
              <Icono nombre={item.icono} className="size-5 shrink-0" />
              <span className="text-center lg:flex-1 lg:text-left">{item.etiqueta}</span>
              {item.proximamente && (
                <>
                  {/* Móvil: punto de color; escritorio: etiqueta legible. */}
                  <span
                    aria-hidden="true"
                    className="absolute right-2 top-1.5 size-2 rounded-full bg-aviso lg:hidden"
                  />
                  <span className="hidden rounded-full bg-superficie-2 px-2 text-xs font-medium text-atenuado lg:inline">
                    Pronto
                  </span>
                  <span className="sr-only">(próximamente)</span>
                </>
              )}
            </NavLink>
          ))}
        </nav>

        <main id="contenido" tabIndex={-1} className="min-w-0 flex-1 outline-none">
          <AvisosDeCuenta />
          <Outlet />
        </main>
      </div>
    </div>
  )
}
