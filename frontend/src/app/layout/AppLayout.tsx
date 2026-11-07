import { useEffect, useId, useRef, useState } from 'react'
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
  const botonMenu = useRef<HTMLButtonElement>(null)
  const navId = useId()
  // Desde lg el menú vive en un panel lateral que abre el botón de tres líneas; en móvil es la
  // barra inferior y este estado no la afecta (todas sus clases llevan el prefijo lg:).
  const [menuAbierto, setMenuAbierto] = useState(false)
  const [rutaDelMenu, setRutaDelMenu] = useState(pathname)

  // Al navegar se cierra el panel (ajuste de estado durante el render, sin efecto en cascada).
  if (rutaDelMenu !== pathname) {
    setRutaDelMenu(pathname)
    setMenuAbierto(false)
  }

  // En móvil la barra inferior se desplaza: el ítem activo se mantiene a la vista.
  useEffect(() => {
    nav.current
      ?.querySelector<HTMLElement>('[aria-current="page"]')
      ?.scrollIntoView?.({ inline: 'center', block: 'nearest' })
  }, [pathname])

  const cerrarMenu = () => {
    setMenuAbierto(false)
    botonMenu.current?.focus()
  }

  // Panel abierto: el foco entra en la página actual del menú; Escape lo cierra y devuelve el
  // foco al botón. Si el foco sale del panel (Tab o clic fuera), se cierra. Mientras está abierto
  // la página de fondo no se desplaza y queda inerte (ver <main inert>), como bajo cualquier velo.
  useEffect(() => {
    if (!menuAbierto) return
    const desbordeAnterior = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    const panel = nav.current
    const actual = panel?.querySelector<HTMLElement>('[aria-current="page"]')
    ;(actual ?? panel?.querySelector<HTMLElement>('a'))?.focus()
    const alPresionar = (e: KeyboardEvent) => {
      if (e.key !== 'Escape') return
      setMenuAbierto(false)
      botonMenu.current?.focus()
    }
    const alSalirElFoco = (e: FocusEvent) => {
      const destino = e.relatedTarget as Node | null
      if (destino && !panel?.contains(destino) && destino !== botonMenu.current) {
        setMenuAbierto(false)
      }
    }
    document.addEventListener('keydown', alPresionar)
    panel?.addEventListener('focusout', alSalirElFoco)
    return () => {
      document.body.style.overflow = desbordeAnterior
      document.removeEventListener('keydown', alPresionar)
      panel?.removeEventListener('focusout', alSalirElFoco)
    }
  }, [menuAbierto])

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
        <div className="mx-auto flex h-16 max-w-[88rem] items-center 2xl:max-w-[min(112rem,92vw)] justify-between gap-2 px-4 sm:gap-3 sm:px-6 lg:px-8">
          <div className="flex min-w-0 items-center gap-2.5">
            <button
              ref={botonMenu}
              type="button"
              onClick={() => setMenuAbierto((v) => !v)}
              aria-expanded={menuAbierto}
              aria-controls={navId}
              className="hidden min-h-11 shrink-0 items-center gap-2 rounded-lg border border-borde-fuerte px-3 text-sm font-semibold text-texto-2 hover:border-acento hover:text-texto lg:inline-flex"
            >
              <Icono nombre="menu" />
              Menú
            </button>
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
              to="/ajustes/cuenta"
              title="Configuración de la cuenta"
              className="flex min-h-11 min-w-11 items-center justify-center gap-2.5 rounded-lg border border-borde-fuerte bg-superficie px-2.5 hover:border-acento md:px-3"
            >
              <Icono nombre="cuenta" />
              {/* En móvil solo se ve el icono; desde md, el correo y el rol nombran el enlace (WCAG 2.5.3). */}
              <span className="sr-only md:hidden">Configuración de la cuenta</span>
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

      {/* Velo del panel lateral (solo desde lg): un clic fuera lo cierra. */}
      {menuAbierto && (
        <div
          aria-hidden="true"
          onClick={cerrarMenu}
          className="anim-aparecer fixed inset-0 z-50 hidden bg-velo lg:block"
        />
      )}

      <div className="mx-auto w-full max-w-[88rem] flex-1 px-4 2xl:max-w-[min(112rem,92vw)] pb-24 pt-6 sm:px-6 lg:px-8 lg:pb-10 lg:pt-8">
        {/* Un solo <nav>: barra inferior desplazable en móvil y panel lateral desde lg. */}
        <nav
          ref={nav}
          id={navId}
          data-no-imprimir
          aria-label="Principal"
          className={cn(
            'fixed inset-x-0 bottom-0 z-40 flex gap-1 overflow-x-auto border-t border-borde-fuerte bg-superficie px-2 py-1.5',
            'lg:inset-x-auto lg:bottom-0 lg:left-0 lg:top-0 lg:z-50 lg:w-72 lg:flex-col lg:gap-1 lg:overflow-y-auto lg:border-r lg:border-t-0 lg:px-3 lg:pb-4 lg:pt-0 lg:shadow-flotante',
            menuAbierto ? 'lg:anim-panel lg:flex' : 'lg:hidden',
          )}
        >
          {/* Cabecera del panel (solo desde lg): la marca y el botón para cerrarlo. */}
          <div className="mb-2 hidden h-16 shrink-0 items-center justify-between border-b border-borde px-1 lg:flex">
            <Marca forzarTexto />
            <button
              type="button"
              onClick={cerrarMenu}
              aria-label="Cerrar menú"
              className="grid size-11 place-items-center rounded-lg text-atenuado hover:bg-hover hover:text-texto"
            >
              <Icono nombre="cerrar" />
            </button>
          </div>
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

        <main
          id="contenido"
          tabIndex={-1}
          inert={menuAbierto}
          className="min-w-0 flex-1 outline-none"
        >
          <AvisosDeCuenta />
          <Outlet />
        </main>
      </div>
    </div>
  )
}
