import type { ReactNode } from 'react'
import { Card, CardBody } from '@/shared/ui/Card'

export function AuthCard({
  titulo,
  descripcion,
  children,
  pie,
}: {
  titulo: string
  descripcion?: ReactNode
  children: ReactNode
  pie?: ReactNode
}) {
  return (
    <Card className="anim-aparecer w-full max-w-md">
      <CardBody className="space-y-6 p-6 sm:p-8">
        <div>
          <h1 className="text-4xl font-bold uppercase tracking-wide">{titulo}</h1>
          {descripcion && <p className="mt-1.5 text-sm text-atenuado">{descripcion}</p>}
        </div>
        {children}
        {pie && <div className="border-t border-borde pt-4 text-sm">{pie}</div>}
      </CardBody>
    </Card>
  )
}

export const claseEnlace =
  'inline-flex min-h-11 items-center font-semibold text-acento underline-offset-4 hover:underline'
