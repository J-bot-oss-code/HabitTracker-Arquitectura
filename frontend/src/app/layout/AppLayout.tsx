import { useState } from 'react'
import { Link, NavLink, Outlet } from 'react-router'
import { authApi } from '@/features/auth/api'
import { useAuth } from '@/shared/auth/AuthContext'
import { etiquetaRol } from '@/shared/auth/roles'
import { cn } from '@/shared/lib/cn'
import { Button } from '@/shared/ui/Button'
import { navegacionPara } from '../navigation'
import { AvisoModoDemo } from './AvisoModoDemo'
import { Marca } from './Marca'

export function AppLayout() {
  const { usuario, cerrarSesion } = useAuth()
  const [menuAbierto, setMenuAbierto] = useState(false)
  const [saliendo, setSaliendo] = useState(false)

  if (!usuario) return null
  const items = navegacionPara(usuario.rol)

  // RF-CA-18: el backend invalida el token (blacklist); localmente se borra siempre.
  const salir = async () => {
    setSaliendo(true)
    await authApi.logout().catch(() => undefined)
    cerrarSesion({ voluntario: true })
  }

  const enlaces = (
    <nav aria-label="Principal" className="flex flex-col gap-1">
      {items.map((item) => (
        <NavLink
          key={item.a}
          to={item.a}
          onClick={() => setMenuAbierto(false)}
          className={({ isActive }) =>
            cn(
              'flex items-center justify-between rounded-lg px-3 py-2 text-sm font-medium transition-colors',
              isActive
                ? 'bg-brand-50 text-brand-700 dark:bg-brand-700/20 dark:text-brand-100'
                : 'text-slate-700 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800',
            )
          }
        >
          {item.etiqueta}
          {item.proximamente && (
            <span className="rounded bg-slate-100 px-1.5 py-0.5 text-[10px] font-semibold uppercase text-slate-500 dark:bg-slate-800">
              Pronto
            </span>
          )}
        </NavLink>
      ))}
    </nav>
  )

  return (
    <div className="flex min-h-screen flex-col">
      <AvisoModoDemo />
      <header className="sticky top-0 z-40 border-b border-slate-200 bg-white/90 backdrop-blur dark:border-slate-800 dark:bg-slate-950/90">
        <div className="mx-auto flex h-16 max-w-7xl items-center justify-between gap-4 px-4 sm:px-6">
          <div className="flex items-center gap-3">
            <button
              type="button"
              className="rounded-md p-2 text-slate-600 hover:bg-slate-100 lg:hidden dark:text-slate-300 dark:hover:bg-slate-800"
              aria-label="Abrir menú"
              aria-expanded={menuAbierto}
              onClick={() => setMenuAbierto((v) => !v)}
            >
              <svg viewBox="0 0 20 20" className="size-5" fill="currentColor" aria-hidden="true">
                <path d="M2 4.75A.75.75 0 0 1 2.75 4h14.5a.75.75 0 0 1 0 1.5H2.75A.75.75 0 0 1 2 4.75Zm0 5.25a.75.75 0 0 1 .75-.75h14.5a.75.75 0 0 1 0 1.5H2.75A.75.75 0 0 1 2 10Zm0 5.25a.75.75 0 0 1 .75-.75h14.5a.75.75 0 0 1 0 1.5H2.75a.75.75 0 0 1-.75-.75Z" />
              </svg>
            </button>
            <Link to="/" aria-label="Inicio">
              <Marca />
            </Link>
          </div>
          <div className="flex items-center gap-3">
            <Link to="/cuenta" className="hidden text-right text-sm sm:block">
              <span className="block font-medium">{usuario.correo}</span>
              <span className="block text-xs text-slate-500">{etiquetaRol[usuario.rol]}</span>
            </Link>
            <Button variante="secundario" tamano="sm" cargando={saliendo} onClick={salir}>
              Cerrar sesión
            </Button>
          </div>
        </div>
      </header>

      <div className="mx-auto flex w-full max-w-7xl flex-1 gap-8 px-4 py-6 sm:px-6">
        <aside className="hidden w-56 shrink-0 lg:block">
          <div className="sticky top-22">{enlaces}</div>
        </aside>
        {menuAbierto && (
          <div className="fixed inset-x-0 top-16 z-30 border-b border-slate-200 bg-white p-4 shadow-lg lg:hidden dark:border-slate-800 dark:bg-slate-950">
            {enlaces}
            <Link
              to="/cuenta"
              onClick={() => setMenuAbierto(false)}
              className="mt-2 block rounded-lg px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100 dark:text-slate-300"
            >
              Mi cuenta
            </Link>
          </div>
        )}
        <main className="min-w-0 flex-1">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
