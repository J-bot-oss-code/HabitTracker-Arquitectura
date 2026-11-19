import { Link } from 'react-router'
import { esMiDia, useRecomendados } from '@/features/planes/api'
import { claseBoton } from '@/shared/ui/claseBoton'
import { Icono } from '@/shared/ui/Icono'

// "Tu día de trabajo saludable" (P2): la acción principal de quien aún no tiene hábitos. La variante de su
// jornada la elige el servidor; empezarla no pide datos de salud. Sin recomendación (cargando, sin perfil o
// sin conexión) no se dibuja nada: el resto del panel funciona igual.
export function TuDiaSaludable() {
  const recomendaciones = useRecomendados()
  const miDia = recomendaciones.data?.recomendados.find((r) => esMiDia(r.plan.slug))
  if (!miDia) return null

  return (
    <section aria-labelledby="tu-dia-titulo" className="panel-tinta mb-6 rounded-2xl p-5 sm:p-6">
      <p className="text-sm font-semibold text-tinta-atenuado">Empieza por aquí</p>
      <h2 id="tu-dia-titulo" className="mt-1 font-display text-2xl font-extrabold">
        Tu día de trabajo saludable
      </h2>
      <p className="mt-2 text-lg font-bold">{miDia.plan.nombre}</p>
      <p className="mt-1 max-w-prose text-tinta-atenuado">{miDia.plan.resumen}</p>
      <Link
        to={`/planes/${encodeURIComponent(miDia.plan.slug)}`}
        className={claseBoton({ tamano: 'lg', className: 'mt-4' })}
      >
        Empezar mi día saludable
        <span className="sr-only">: {miDia.plan.nombre}</span>
        <Icono nombre="flecha" className="size-4" />
      </Link>
    </section>
  )
}
