import { Link } from 'react-router'
import { AjustesCuentaPage } from '@/features/auth/pages/AjustesCuentaPages'
import { useAuth } from '@/shared/auth/AuthContext'
import { Icono } from '@/shared/ui/Icono'
import { seccionesAjustesPara } from '../navigation'

// Configuración › Cuenta: los datos de la cuenta y, en móvil, una tarjeta por sección (la tira de
// pestañas no deja ver todas). En escritorio el menú lateral ya las muestra, así que se ocultan.
// Las tarjetas salen de SECCIONES_AJUSTES: la misma lista y los mismos roles que el menú.
export function CuentaAjustesPage() {
  const { usuario } = useAuth()
  const secciones = usuario
    ? seccionesAjustesPara(usuario.rol).filter((s) => s.a !== '/ajustes/cuenta')
    : []
  return (
    <div className="grid gap-6">
      <AjustesCuentaPage />
      <ul className="grid gap-3 lg:hidden" aria-label="Más ajustes">
        {secciones.map((s) => (
          <li key={s.a}>
            <Link
              to={s.a}
              className="group flex items-start gap-3 rounded-2xl border border-borde bg-superficie p-4 transition-colors hover:border-acento"
            >
              <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-acento-suave text-acento-fuerte">
                <Icono nombre={s.icono} />
              </span>
              <span className="min-w-0 flex-1">
                <span className="block font-semibold">{s.etiqueta}</span>
                <span className="mt-0.5 block text-sm text-atenuado">{s.descripcion}</span>
              </span>
              <Icono
                nombre="flecha"
                className="mt-2 size-4 shrink-0 text-atenuado group-hover:text-acento"
              />
            </Link>
          </li>
        ))}
      </ul>
    </div>
  )
}
