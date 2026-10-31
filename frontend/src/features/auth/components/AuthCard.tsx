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
    <Card className="w-full max-w-md">
      <CardBody className="space-y-6 p-6 sm:p-8">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">{titulo}</h1>
          {descripcion && <p className="mt-1 text-sm text-slate-500">{descripcion}</p>}
        </div>
        {children}
        {pie && (
          <div className="border-t border-slate-200 pt-4 text-sm dark:border-slate-800">{pie}</div>
        )}
      </CardBody>
    </Card>
  )
}

export const claseEnlace = 'font-medium text-brand-600 hover:text-brand-700 dark:text-brand-500'
