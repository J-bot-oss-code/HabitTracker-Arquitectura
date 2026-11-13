import { useEffect, useRef } from 'react'
import { Link } from 'react-router'
import { mensajeDeError } from '@/shared/api/problem'
import { cn } from '@/shared/lib/cn'
import { Icono } from '@/shared/ui/Icono'
import { useToast } from '@/shared/ui/Toast'
import { useRegistrarAvance } from '../api'
import type { TareaDeHoy } from '../hoy'

const formatoCantidad = new Intl.NumberFormat('es-DO', { maximumFractionDigits: 2 })

// TaskRow de la especificación (§3): casilla, nombre, periodicidad y avance. "Marcar hecha"
// registra lo que falta para cumplir el período; el servidor valida el avance (RD-06) y su
// rechazo se muestra tal cual. Un avance no se deshace desde aquí: la API no lo permite.
export function FilaTarea({
  tarea,
  alRegistrarCantidad,
}: {
  tarea: TareaDeHoy
  alRegistrarCantidad: () => void
}) {
  const { habito, avance, objetivo, restante, hecha } = tarea
  const registrar = useRegistrarAvance(habito.id)
  const { notificar } = useToast()
  const enlace = useRef<HTMLAnchorElement>(null)
  const lasMarque = useRef(false)

  // Al marcar, el botón se reemplaza por el icono de "hecha": el foco pasa al nombre de la fila
  // para que quien navega con teclado o lector no lo pierda.
  useEffect(() => {
    if (hecha && lasMarque.current) enlace.current?.focus()
  }, [hecha])

  const marcar = () => {
    lasMarque.current = true
    registrar.mutate(
      { cantidad: restante, fechaUtc: new Date().toISOString() },
      {
        onSuccess: () => notificar(`¡${habito.nombre} cumplido!`),
        onError: (e) => notificar(mensajeDeError(e, 'No se pudo registrar el avance.'), 'peligro'),
      },
    )
  }

  return (
    <li className="flex items-center gap-3 py-3">
      {hecha ? (
        <span role="img" aria-label="Hecha" className="grid size-11 shrink-0 place-items-center">
          <span className="grid size-7 place-items-center rounded-lg bg-marcador text-marcador-texto">
            <Icono nombre="check" className="size-5" />
          </span>
        </span>
      ) : (
        // Objetivo táctil de 44 px alrededor de una casilla de 28 px.
        <button
          type="button"
          onClick={marcar}
          disabled={registrar.isPending}
          aria-label={`Marcar hecha: ${habito.nombre}`}
          className="group grid size-11 shrink-0 place-items-center rounded-lg disabled:opacity-60"
        >
          <span className="grid size-7 place-items-center rounded-lg border-2 border-control group-hover:border-acento">
            <Icono nombre="check" className="size-4 text-transparent group-hover:text-acento" />
          </span>
        </button>
      )}

      <div className="min-w-0 flex-1">
        <Link
          ref={enlace}
          to={`/habitos/${habito.id}`}
          className={cn(
            'flex min-h-11 items-center truncate font-semibold hover:underline',
            hecha && 'text-atenuado line-through',
          )}
        >
          {habito.nombre}
        </Link>
        <p className="text-sm text-atenuado">
          {habito.periodicidad === 'Diaria' ? 'Diario' : 'Semanal'} ·{' '}
          <span className="tabular-nums">
            {formatoCantidad.format(avance)} / {formatoCantidad.format(objetivo)}
          </span>{' '}
          {habito.unidad}
        </p>
      </div>

      {!hecha && (
        <button
          type="button"
          onClick={alRegistrarCantidad}
          className="inline-flex min-h-11 shrink-0 items-center rounded-lg px-2 text-sm font-semibold text-acento hover:underline"
        >
          Registrar cantidad
          <span className="sr-only">: {habito.nombre}</span>
        </button>
      )}
    </li>
  )
}
