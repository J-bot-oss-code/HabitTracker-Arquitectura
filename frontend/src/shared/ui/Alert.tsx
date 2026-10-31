import type { ReactNode } from 'react'
import { cn } from '@/shared/lib/cn'

type Tono = 'info' | 'exito' | 'aviso' | 'peligro'

const tonos: Record<Tono, string> = {
  info: 'border-sky-200 bg-sky-50 text-sky-900 dark:border-sky-900 dark:bg-sky-950/50 dark:text-sky-200',
  exito:
    'border-emerald-200 bg-emerald-50 text-emerald-900 dark:border-emerald-900 dark:bg-emerald-950/50 dark:text-emerald-200',
  aviso:
    'border-amber-200 bg-amber-50 text-amber-900 dark:border-amber-900 dark:bg-amber-950/50 dark:text-amber-200',
  peligro:
    'border-rose-200 bg-rose-50 text-rose-900 dark:border-rose-900 dark:bg-rose-950/50 dark:text-rose-200',
}

export function Alert({
  tono = 'info',
  titulo,
  children,
  className,
}: {
  tono?: Tono
  titulo?: string
  children?: ReactNode
  className?: string
}) {
  return (
    <div
      role={tono === 'peligro' ? 'alert' : 'status'}
      className={cn('rounded-lg border px-4 py-3 text-sm', tonos[tono], className)}
    >
      {titulo && <p className="font-medium">{titulo}</p>}
      {children && <div className={titulo ? 'mt-1' : undefined}>{children}</div>}
    </div>
  )
}
