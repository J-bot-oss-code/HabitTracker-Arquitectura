import { useId } from 'react'
import { cn } from '@/shared/lib/cn'
import { BarraProgreso } from '@/shared/ui/BarraProgreso'
import { Icono } from '@/shared/ui/Icono'
import { InsigniaRacha } from '@/shared/ui/InsigniaRacha'

// Hoja de ejemplo del panel Hoy en el hero (§5.1): muestra lo que verá el usuario, con la hora al margen.
// Los datos son de ejemplo y así lo dice el pie, que da nombre a la figura.
const tareas = [
  {
    hora: '8:00',
    nombre: 'Tomar agua durante la jornada',
    detalle: 'Al llegar al trabajo',
    hecha: true,
  },
  { hora: '10:15', nombre: 'Pausa activa', detalle: 'A media mañana', hecha: true },
  { hora: '13:30', nombre: 'Caminar 10 minutos', detalle: 'Después del almuerzo', hecha: false },
  { hora: '15:30', nombre: 'Descanso de la vista', detalle: 'A media tarde', hecha: false },
]

export function TarjetaEjemploHoy() {
  const idPie = useId()
  return (
    <figure
      aria-labelledby={idPie}
      className="w-full max-w-md rounded-2xl border border-borde bg-superficie p-5 sm:p-6"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="rotulo">Semana 2 de 4 · Mi día de oficina saludable</p>
          <p className="mt-1 font-display text-2xl font-extrabold">Hoy</p>
        </div>
        <InsigniaRacha dias={12} />
      </div>

      <div className="mt-5 flex items-center justify-between text-sm">
        <span className="font-medium text-texto-2">Tareas de hoy</span>
        <span className="cifra text-base">2/4</span>
      </div>
      <BarraProgreso porcentaje={50} etiqueta="Tareas de hoy del ejemplo" className="mt-2" />

      <ul className="mt-4 divide-y divide-borde border-t border-borde">
        {tareas.map((t) => (
          <li
            key={t.nombre}
            className="grid grid-cols-[3.25rem_minmax(0,1fr)_auto] items-center gap-3 py-3"
          >
            <span className="cifra text-base">{t.hora}</span>
            <span className="min-w-0">
              <span className={cn('block text-sm font-medium', t.hecha && 'text-atenuado')}>
                {t.nombre}
              </span>
              <span className="block text-xs text-atenuado">{t.detalle}</span>
            </span>
            <span
              aria-hidden="true"
              className={cn(
                'grid size-6 shrink-0 place-items-center rounded-md',
                t.hecha ? 'bg-marcador text-marcador-texto' : 'border-2 border-control',
              )}
            >
              {t.hecha && <Icono nombre="check" className="size-4" />}
            </span>
            <span className="sr-only">{t.hecha ? 'Hecha' : 'Pendiente'}</span>
          </li>
        ))}
      </ul>
      <figcaption id={idPie} className="mt-3 text-xs text-atenuado">
        Ejemplo del panel Hoy con datos ficticios.
      </figcaption>
    </figure>
  )
}
