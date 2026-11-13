import type { HTMLAttributes, ReactNode } from 'react'
import { cn } from '@/shared/lib/cn'
import { Icono, type NombreIcono } from './Icono'

export function Card({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={cn('rounded-2xl border border-borde bg-superficie', className)} {...props} />
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
        <h2 className="mb-4 text-lg font-bold">{titulo}</h2>
        {children}
      </CardBody>
    </Card>
  )
}

export function PageHeader({
  titulo,
  descripcion,
  acciones,
}: {
  titulo: string
  descripcion?: ReactNode
  acciones?: ReactNode
}) {
  return (
    <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
      <div>
        <h1 className="text-3xl font-extrabold sm:text-[2rem]">{titulo}</h1>
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
  nivelTitulo,
}: {
  titulo: string
  descripcion?: ReactNode
  accion?: ReactNode
  icono?: NombreIcono
  // Si el estado vacío es todo el contenido de la página, su título es el encabezado.
  nivelTitulo?: 1 | 2
}) {
  const Titulo = nivelTitulo === 1 ? 'h1' : nivelTitulo === 2 ? 'h2' : 'p'
  return (
    <div className="rounded-lg border border-dashed border-borde-fuerte px-6 py-12 text-center">
      <Icono nombre={icono} className="mx-auto mb-3 size-6 text-acento" />
      <Titulo className="font-display text-lg font-bold">{titulo}</Titulo>
      {descripcion && <p className="mx-auto mt-1 max-w-md text-sm text-atenuado">{descripcion}</p>}
      {accion && <div className="mt-5">{accion}</div>}
    </div>
  )
}
