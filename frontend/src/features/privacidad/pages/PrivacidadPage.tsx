import { Link } from 'react-router'
import { Icono } from '@/shared/ui/Icono'
import { PanelBaja } from '../components/PanelBaja'
import { PanelConsentimientos } from '../components/PanelConsentimientos'
import { PanelDescarga } from '../components/PanelDescarga'

const MENU = [
  { a: '/cuenta', etiqueta: 'Seguridad e inicio de sesión' },
  { a: '/ajustes/privacidad', etiqueta: 'Privacidad y datos' },
] as const

// Centro de privacidad (especificación §5.5): descargar, decidir y retirarse. Es de primera
// clase en el menú porque los datos de salud son lo más sensible que guarda la aplicación.
export function PrivacidadPage() {
  return (
    <>
      <Link
        to="/hoy"
        className="inline-flex min-h-11 items-center gap-1.5 text-sm text-atenuado hover:text-texto"
      >
        <Icono nombre="volver" className="size-4" />
        Volver a Hoy
      </Link>
      <h1 className="mb-6 mt-1 text-3xl font-extrabold sm:text-[2rem]">Privacidad y datos</h1>

      <div className="grid items-start gap-6 lg:grid-cols-[14rem_minmax(0,1fr)]">
        <nav aria-label="Ajustes" data-no-imprimir>
          <ul className="flex flex-wrap gap-1 lg:flex-col">
            {MENU.map((m) => (
              <li key={m.a}>
                <Link
                  to={m.a}
                  aria-current={m.a === '/ajustes/privacidad' ? 'page' : undefined}
                  className={
                    m.a === '/ajustes/privacidad'
                      ? 'flex min-h-11 items-center rounded-lg bg-acento-suave px-3 text-sm font-bold text-acento-fuerte'
                      : 'flex min-h-11 items-center rounded-lg px-3 text-sm font-medium text-texto-2 hover:bg-hover hover:text-texto'
                  }
                >
                  {m.etiqueta}
                </Link>
              </li>
            ))}
          </ul>
        </nav>

        <div className="space-y-6">
          <PanelDescarga />
          <PanelConsentimientos />
          <PanelBaja />
        </div>
      </div>
    </>
  )
}
