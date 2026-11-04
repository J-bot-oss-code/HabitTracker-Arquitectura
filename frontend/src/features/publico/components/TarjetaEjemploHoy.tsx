import { useId } from 'react'
import { cn } from '@/shared/lib/cn'
import { BarraProgreso } from '@/shared/ui/BarraProgreso'
import { Icono } from '@/shared/ui/Icono'
import { InsigniaRacha } from '@/shared/ui/InsigniaRacha'

// Tarjeta de ejemplo del panel Hoy en el hero (§5.1): muestra lo que verá el usuario. Los
// datos son de ejemplo y así lo dice el pie, que da nombre a la figura. Es la única
// superficie con sombra del sistema.
const tareas = [
  { nombre: 'Trotar 25 min a ritmo suave', detalle: 'Físico · 7:00', hecha: true },
  { nombre: 'Estiramientos', detalle: 'Físico · 10 min', hecha: true },
  { nombre: 'Beber agua', detalle: '5 de 8 vasos', hecha: false },
  { nombre: 'Dormir 7 horas', detalle: 'Recordatorio 22:30', hecha: false },
]

export function TarjetaEjemploHoy() {
  const idPie = useId()
  return (
    <figure
      aria-labelledby={idPie}
      className="w-full max-w-md rounded-[20px] border border-borde bg-superficie p-5 shadow-flotante sm:p-6"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="rotulo">Semana 3 de 8 · Primeros 5 km</p>
          <p className="mt-1 font-display text-xl font-bold">Hoy</p>
        </div>
        <InsigniaRacha dias={12} />
      </div>

      <div className="mt-5 flex items-center justify-between text-sm">
        <span className="font-medium text-texto-2">Tareas de hoy</span>
        <span className="cifra text-base">4/6</span>
      </div>
      <BarraProgreso porcentaje={67} etiqueta="Tareas de hoy del ejemplo" className="mt-2" />

      <ul className="mt-5 divide-y divide-borde">
        {tareas.map((t) => (
          <li key={t.nombre} className="flex items-center gap-3 py-2.5">
            <span
              aria-hidden="true"
              className={cn(
                'grid size-6 shrink-0 place-items-center rounded-md border-2',
                t.hecha ? 'border-acento bg-acento text-acento-texto' : 'border-control',
              )}
            >
              {t.hecha && <Icono nombre="check" className="size-4" />}
            </span>
            <span className="min-w-0 flex-1">
              <span
                className={cn('block text-sm font-medium', t.hecha && 'text-atenuado line-through')}
              >
                {t.nombre}
              </span>
              <span className="block text-xs text-atenuado">{t.detalle}</span>
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
