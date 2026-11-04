import { useEffect, useId, useRef, useState } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router'
import { useAuth } from '@/shared/auth/AuthContext'
import { cn } from '@/shared/lib/cn'
import { SelectorTemaMenu } from '@/shared/theme/SelectorTema'
import { claseBoton } from '@/shared/ui/claseBoton'
import { Icono } from '@/shared/ui/Icono'
import { rutaInicial } from '../navigation'
import { AvisoModoDemo } from './AvisoModoDemo'
import { Marca } from './Marca'

const enlaces = [
  { a: '/metas', etiqueta: 'Metas' },
  { a: '/calculadoras', etiqueta: 'Calculadoras' },
  { a: '/#como-funciona', etiqueta: 'Cómo funciona' },
]

const legales = [
  { a: '/legal/terminos', etiqueta: 'Términos de uso' },
  { a: '/legal/privacidad', etiqueta: 'Política de privacidad' },
  { a: '/legal/cookies', etiqueta: 'Cookies' },
  { a: '/legal/salud', etiqueta: 'Aviso de salud' },
]

const claseEnlaceNav = ({ isActive }: { isActive: boolean }) =>
  cn(
    'flex min-h-11 items-center rounded-lg px-3 text-sm font-medium transition-colors',
    isActive
      ? 'bg-acento-suave font-bold text-acento-fuerte shadow-[inset_0_-3px_0_var(--c-acento)]'
      : 'text-texto-2 hover:bg-hover hover:text-texto',
  )

// Un ancla de la landing (/#como-funciona) no es una página: nunca se marca como actual.
function EnlaceNav({ a, etiqueta }: { a: string; etiqueta: string }) {
  if (a.includes('#')) {
    return (
      <Link to={a} className={claseEnlaceNav({ isActive: false })}>
        {etiqueta}
      </Link>
    )
  }
  return (
    <NavLink to={a} end className={claseEnlaceNav}>
      {etiqueta}
    </NavLink>
  )
}

// Páginas públicas (§5.1): se explora sin cuenta. Con sesión abierta, la cabecera lleva al
// panel del rol en lugar de ofrecer el registro.
export function PublicLayout() {
  const { usuario } = useAuth()
  const { key } = useLocation()
  // El menú móvil queda abierto solo en la navegación en que se abrió: cualquier cambio de
  // página (enlace, logo, botón Atrás) lo cierra sin un efecto que reajuste el estado.
  const [abiertoEn, setAbiertoEn] = useState<string | null>(null)
  const menuAbierto = abiertoEn === key
  const botonMenu = useRef<HTMLButtonElement>(null)
  const idMenu = useId()

  // Escape cierra el menú y devuelve el foco al botón que lo abrió.
  useEffect(() => {
    if (!menuAbierto) return
    const tecla = (e: KeyboardEvent) => {
      if (e.key !== 'Escape') return
      setAbiertoEn(null)
      botonMenu.current?.focus()
    }
    document.addEventListener('keydown', tecla)
    return () => document.removeEventListener('keydown', tecla)
  }, [menuAbierto])

  const acciones = usuario ? (
    <Link to={rutaInicial(usuario.rol)} className={claseBoton({ tamano: 'sm' })}>
      Ir a mi panel
    </Link>
  ) : (
    <>
      <Link
        to="/login"
        className="hidden min-h-11 items-center rounded-lg px-3 text-sm font-semibold text-texto-2 hover:text-texto sm:inline-flex"
      >
        Iniciar sesión
      </Link>
      <Link to="/registro" className={claseBoton({ tamano: 'sm' })}>
        Crear cuenta
      </Link>
    </>
  )

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
        <div className="mx-auto flex h-16 max-w-[1200px] items-center justify-between gap-2 px-4 sm:px-6">
          <Link
            to="/"
            aria-label="HabitTracker Nexus, inicio"
            className="inline-flex min-h-11 min-w-11 items-center rounded-lg"
          >
            <Marca />
          </Link>
          <nav aria-label="Principal" className="hidden items-center gap-1 md:flex">
            {enlaces.map((e) => (
              <EnlaceNav key={e.a} {...e} />
            ))}
          </nav>
          <div className="flex shrink-0 items-center gap-2">
            <SelectorTemaMenu />
            {acciones}
            <button
              ref={botonMenu}
              type="button"
              aria-expanded={menuAbierto}
              aria-controls={menuAbierto ? idMenu : undefined}
              aria-label="Menú"
              onClick={() => setAbiertoEn(menuAbierto ? null : key)}
              className="grid size-11 place-items-center rounded-lg border border-borde-fuerte bg-superficie md:hidden"
            >
              <Icono nombre={menuAbierto ? 'cerrar' : 'menu'} />
            </button>
          </div>
        </div>
        {menuAbierto && (
          <nav
            id={idMenu}
            aria-label="Principal (móvil)"
            className="border-t border-borde px-4 py-2 md:hidden"
          >
            <ul className="flex flex-col">
              {enlaces.map((e) => (
                <li key={e.a}>
                  <EnlaceNav {...e} />
                </li>
              ))}
              {!usuario && (
                <li>
                  <EnlaceNav a="/login" etiqueta="Iniciar sesión" />
                </li>
              )}
            </ul>
          </nav>
        )}
      </header>

      <main
        id="contenido"
        tabIndex={-1}
        className="mx-auto w-full max-w-[1200px] flex-1 px-4 outline-none sm:px-6"
      >
        <Outlet />
      </main>

      <footer data-no-imprimir className="border-t border-borde bg-superficie">
        <div className="mx-auto flex max-w-[1200px] flex-col gap-3 px-4 py-6 text-sm text-atenuado sm:flex-row sm:items-center sm:justify-between sm:px-6">
          <p>© 2026 HabitTracker Nexus</p>
          <nav aria-label="Documentos legales">
            <ul className="flex flex-wrap gap-x-1 gap-y-1">
              {legales.map((l) => (
                <li key={l.a}>
                  <Link
                    to={l.a}
                    className="inline-flex min-h-11 items-center rounded-lg px-2 hover:text-texto hover:underline"
                  >
                    {l.etiqueta}
                  </Link>
                </li>
              ))}
            </ul>
          </nav>
        </div>
      </footer>
    </div>
  )
}
