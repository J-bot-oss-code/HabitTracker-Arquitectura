import { Link, Outlet } from 'react-router'
import { SelectorTemaMenu } from '@/shared/theme/SelectorTema'
import { Icono } from '@/shared/ui/Icono'
import { AvisoModoDemo } from './AvisoModoDemo'
import { Marca } from './Marca'

const beneficios = [
  'Un plan diario con pocas tareas concretas',
  'Rachas y progreso visibles cada semana',
  'Descarga o borra tus datos cuando quieras',
] as const

// Pantalla dividida (§5.2): panel de tinta a la izquierda (desde lg) y formulario a la derecha.
export function AuthLayout() {
  return (
    <div className="flex min-h-screen flex-col">
      <AvisoModoDemo />
      <div className="grid flex-1 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.1fr)]">
        <aside className="hidden flex-col justify-between panel-tinta p-10 lg:flex xl:p-14">
          <Link to="/" aria-label="HabitTracker Nexus, inicio" className="self-start rounded-lg">
            <Marca forzarTexto />
          </Link>
          <div className="max-w-md space-y-8">
            <p className="font-display text-4xl font-extrabold leading-[1.1] tracking-tight xl:text-5xl">
              Tu meta, un día a la vez.
            </p>
            <ul className="space-y-4">
              {beneficios.map((texto) => (
                <li key={texto} className="flex items-start gap-3">
                  <Icono nombre="check" className="mt-0.5 size-5 shrink-0 text-acento-claro" />
                  <span>{texto}</span>
                </li>
              ))}
            </ul>
          </div>
          <p className="text-sm text-tinta-atenuado">Programación III · ITLA · 2026</p>
        </aside>

        <main className="relative flex flex-col items-center justify-center gap-8 px-4 py-10">
          <div className="absolute right-4 top-4">
            <SelectorTemaMenu />
          </div>
          <Link to="/" aria-label="HabitTracker Nexus, inicio" className="rounded-lg lg:hidden">
            <Marca forzarTexto />
          </Link>
          <Outlet />
        </main>
      </div>
    </div>
  )
}
