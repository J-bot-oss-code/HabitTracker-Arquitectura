import type { HTMLAttributes, ReactNode } from 'react'
import { cn } from '@/shared/lib/cn'
import { Icono, type NombreIcono } from './Icono'

export function Card({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={cn('rounded-xl border border-borde bg-superficie shadow-tarjeta', className)}
      {...props}
    />
  )
}

export function CardBody({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('p-5', className)} {...props} />
}

// Bloque de contenido con título de sección, igual en todas las pantallas.
export function Seccion({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <Card>
      <CardBody>
        <h2 className="mb-4 text-xl font-bold uppercase tracking-wide">{titulo}</h2>
        {children}
      </CardBody>
    </Card>
  )
}

export function PageHeader({
  titulo,
  descripcion,
  acciones,
  rotulo,
}: {
  titulo: string
  descripcion?: ReactNode
  acciones?: ReactNode
  // Línea pequeña sobre el título, como el rótulo de un marcador.
  rotulo?: string
}) {
  return (
    <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
      <div>
        {rotulo && <p className="rotulo mb-1 text-acento-2">{rotulo}</p>}
        <h1 className="text-3xl font-bold uppercase tracking-wide sm:text-4xl">{titulo}</h1>
        {descripcion && <p className="mt-1.5 max-w-prose text-sm text-atenuado">{descripcion}</p>}
      </div>
      {acciones && <div className="flex flex-wrap gap-2">{acciones}</div>}
    </div>
  )
}

export function EmptyState({
  titulo,
  descripcion,
  accion,
  icono = 'rayo',
}: {
  titulo: string
  descripcion?: ReactNode
  accion?: ReactNode
  icono?: NombreIcono
}) {
  return (
    <div className="rounded-xl border border-dashed border-borde-fuerte px-6 py-12 text-center">
      <span className="mx-auto mb-4 grid size-12 place-items-center rounded-full bg-superficie-2 text-acento-2">
        <Icono nombre={icono} className="size-6" />
      </span>
      <p className="font-display text-xl font-semibold uppercase tracking-wide">{titulo}</p>
      {descripcion && <p className="mx-auto mt-1 max-w-md text-sm text-atenuado">{descripcion}</p>}
      {accion && <div className="mt-5">{accion}</div>}
    </div>
  )
}
