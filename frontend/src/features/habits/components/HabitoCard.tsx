import { Link } from 'react-router'
import { cn } from '@/shared/lib/cn'
import { AnilloProgreso } from '@/shared/ui/AnilloProgreso'
import { Icono } from '@/shared/ui/Icono'
import { avanceDelPeriodo, etiquetaPeriodo, rachaActual } from '../estados'
import type { HabitoDto } from '../types'
import { EstadoHabitoBadge } from './EstadoHabitoBadge'

const formato = new Intl.NumberFormat('es-DO', { maximumFractionDigits: 2 })

export function HabitoCard({ habito }: { habito: HabitoDto }) {
  const avance = avanceDelPeriodo(habito)
  const porcentaje = habito.cantidadObjetivo > 0 ? (avance / habito.cantidadObjetivo) * 100 : 0
  const racha = rachaActual(habito)
  const enMarcha = habito.estado === 'Activo'

  return (
    <Link
      to={`/habitos/${habito.id}`}
      className={cn(
        'group flex min-h-11 items-center gap-4 rounded-xl border bg-superficie p-4 shadow-tarjeta transition-[border-color,box-shadow,transform] duration-200 hover:-translate-y-0.5 hover:border-acento hover:shadow-brillo',
        enMarcha ? 'border-borde-fuerte' : 'border-borde',
        habito.cumpleMetaFinal && enMarcha && 'border-exito',
      )}
    >
      <AnilloProgreso
        porcentaje={porcentaje}
        tamano={84}
        grosor={8}
        cumplido={habito.cumpleMetaFinal}
        etiqueta={`Progreso ${etiquetaPeriodo[habito.periodicidad]} de ${habito.nombre}`}
      />
      <div className="min-w-0 flex-1 space-y-2">
        <div className="flex items-start justify-between gap-2">
          <h3 className="truncate text-xl font-bold uppercase leading-tight">{habito.nombre}</h3>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <EstadoHabitoBadge estado={habito.estado} />
          {habito.cumpleMetaFinal && enMarcha && (
            <span className="inline-flex items-center gap-1 text-xs font-semibold uppercase tracking-wider text-exito">
              <Icono nombre="trofeo" className="size-3.5" />
              Meta cumplida
            </span>
          )}
        </div>
        <p className="text-sm text-atenuado">
          <span className="cifra text-lg text-texto">{formato.format(avance)}</span>
          <span className="cifra text-lg text-atenuado">
            {' '}
            / {formato.format(habito.cantidadObjetivo)}
          </span>{' '}
          {habito.unidad} {etiquetaPeriodo[habito.periodicidad]}
        </p>
        {racha > 0 && (
          <p className="flex items-center gap-1 text-sm font-semibold text-acento-2">
            <Icono nombre="llama" className="size-4" />
            Racha de {racha}{' '}
            {habito.periodicidad === 'Diaria'
              ? racha === 1
                ? 'día'
                : 'días'
              : racha === 1
                ? 'semana'
                : 'semanas'}
          </p>
        )}
      </div>
      <Icono
        nombre="flecha"
        className="size-5 shrink-0 text-atenuado transition-transform group-hover:translate-x-1 group-hover:text-acento"
      />
    </Link>
  )
}
