import { Outlet } from 'react-router'
import { SelectorTemaMenu } from '@/shared/theme/SelectorTema'
import { Icono } from '@/shared/ui/Icono'
import { AvisoModoDemo } from './AvisoModoDemo'
import { Marca } from './Marca'

const puntos = [
  { icono: 'llama', texto: 'Rachas que te mantienen en movimiento' },
  { icono: 'trofeo', texto: 'Metas con cierre claro: cumples o sigues' },
  { icono: 'permisos', texto: 'Acciones avanzadas con permiso del administrador' },
] as const

// Pantalla dividida: panel deportivo a la izquierda (desde lg) y formulario a la derecha.
export function AuthLayout() {
  return (
    <div className="flex min-h-screen flex-col">
      <AvisoModoDemo />
      <div className="grid flex-1 lg:grid-cols-[1.05fr_1fr]">
        <aside className="relative hidden flex-col justify-between overflow-hidden border-r border-borde bg-superficie p-10 lg:flex">
          <div
            aria-hidden="true"
            className="pointer-events-none absolute -right-24 -top-24 size-96 rounded-full bg-acento/20 blur-3xl"
          />
          <div
            aria-hidden="true"
            className="pointer-events-none absolute -bottom-32 -left-16 size-80 rounded-full bg-acento-2/20 blur-3xl"
          />
          <Marca className="relative" forzarTexto />
          <div className="relative space-y-8">
            <p className="font-display text-6xl font-bold uppercase leading-[0.95] tracking-wide xl:text-7xl">
              Entrena la
              <br />
              <span className="text-acento">constancia.</span>
            </p>
            <ul className="space-y-4">
              {puntos.map((p) => (
                <li key={p.texto} className="flex items-center gap-3 text-atenuado">
                  <span className="grid size-10 shrink-0 place-items-center rounded-xl border border-borde-fuerte bg-superficie-2 text-acento-2">
                    <Icono nombre={p.icono} />
                  </span>
                  <span className="text-texto">{p.texto}</span>
                </li>
              ))}
            </ul>
          </div>
          <p className="relative text-xs text-atenuado">Programación III · ITLA · 2026</p>
        </aside>

        <main className="relative flex flex-col items-center justify-center gap-8 px-4 py-10">
          <div className="absolute right-4 top-4">
            <SelectorTemaMenu />
          </div>
          <Marca className="lg:hidden" forzarTexto />
          <Outlet />
        </main>
      </div>
    </div>
  )
}
