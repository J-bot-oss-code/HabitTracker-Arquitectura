import { Link, Outlet, useLocation, useSearchParams } from 'react-router'
import { leerRegistroPendiente } from '@/features/perfil/api'
import { usePlanResumen, type PlanResumenDto } from '@/features/planes/api'
import { useAuth } from '@/shared/auth/AuthContext'
import { SelectorTemaMenu } from '@/shared/theme/SelectorTema'
import { Icono } from '@/shared/ui/Icono'
import { AvisoModoDemo } from './AvisoModoDemo'
import { Marca } from './Marca'

const beneficios = [
  'Un plan diario con pocas tareas concretas',
  'Rachas y progreso visibles cada semana',
  'Descarga o borra tus datos cuando quieras',
] as const

// Plan que acompaña el alta: el de ?meta= en el registro o el recordado del registro en el
// onboarding. Siempre validado contra el catálogo del servidor (usePlanResumen).
function useMetaElegida(): PlanResumenDto | undefined {
  const [params] = useSearchParams()
  const { pathname } = useLocation()
  const { usuario } = useAuth()
  const slug =
    pathname === '/registro'
      ? params.get('meta')
      : pathname === '/bienvenida' && usuario
        ? leerRegistroPendiente(usuario.correo)?.metaSlug
        : null
  return usePlanResumen(slug).plan
}

function PanelMeta({ meta }: { meta: PlanResumenDto }) {
  return (
    <div className="max-w-md space-y-6">
      <div>
        <p className="text-sm font-semibold text-tinta-atenuado">Meta elegida</p>
        <p className="mt-2 font-display text-4xl font-extrabold leading-[1.1] tracking-tight xl:text-5xl">
          {meta.nombre}
        </p>
        <p className="mt-3 text-tinta-atenuado">{meta.resumen}</p>
      </div>
      <ul className="space-y-3">
        {meta.beneficios.map((texto) => (
          <li key={texto} className="flex items-start gap-3">
            <Icono nombre="check" className="mt-0.5 size-5 shrink-0 text-acento-claro" />
            <span>{texto}</span>
          </li>
        ))}
      </ul>
      <Link
        to="/metas"
        className="inline-flex min-h-11 items-center gap-1.5 rounded-lg font-semibold text-acento-claro underline-offset-4 hover:underline"
      >
        <Icono nombre="volver" className="size-4" />
        Volver a explorar metas
      </Link>
    </div>
  )
}

function PanelGeneral() {
  return (
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
  )
}

// Pantalla dividida (§5.2): panel de tinta a la izquierda (desde lg) y formulario a la derecha.
export function AuthLayout() {
  const meta = useMetaElegida()
  return (
    <div className="flex min-h-screen flex-col">
      <AvisoModoDemo />
      <div className="grid flex-1 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.1fr)]">
        <aside className="panel-tinta hidden flex-col justify-between p-10 lg:sticky lg:top-0 lg:flex lg:h-screen lg:self-start xl:p-14">
          <Link to="/" aria-label="HabitTracker Nexus, inicio" className="self-start rounded-lg">
            <Marca forzarTexto />
          </Link>
          {meta ? <PanelMeta meta={meta} /> : <PanelGeneral />}
          <p className="text-sm text-tinta-atenuado">Programación III · ITLA · 2026</p>
        </aside>

        <main className="relative flex flex-col items-center justify-start gap-8 px-4 pb-10 pt-20 lg:justify-center lg:pt-10">
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
