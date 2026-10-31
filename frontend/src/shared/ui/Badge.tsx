import type { ReactNode } from 'react'
import { cn } from '@/shared/lib/cn'

export type Tono = 'neutro' | 'info' | 'exito' | 'aviso' | 'peligro' | 'marca'

const tonos: Record<Tono, string> = {
  neutro: 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300',
  info: 'bg-sky-100 text-sky-800 dark:bg-sky-950 dark:text-sky-300',
  exito: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300',
  aviso: 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300',
  peligro: 'bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-300',
  marca: 'bg-brand-100 text-brand-700 dark:bg-brand-700/30 dark:text-brand-100',
}

export function Badge({ tono = 'neutro', children }: { tono?: Tono; children: ReactNode }) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium',
        tonos[tono],
      )}
    >
      {children}
    </span>
  )
}
