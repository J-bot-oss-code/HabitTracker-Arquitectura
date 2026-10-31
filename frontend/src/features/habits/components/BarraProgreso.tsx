import { cn } from '@/shared/lib/cn'

const formato = new Intl.NumberFormat('es-DO', { maximumFractionDigits: 2 })

export function BarraProgreso({
  actual,
  objetivo,
  unidad,
  periodo,
  cumplida,
}: {
  actual: number
  objetivo: number
  unidad: string
  periodo: string
  cumplida: boolean
}) {
  const porcentaje = objetivo > 0 ? Math.min(100, Math.round((actual / objetivo) * 100)) : 0
  return (
    <div>
      <div className="mb-1.5 flex items-baseline justify-between text-sm">
        <span className="text-slate-600 dark:text-slate-400">
          {formato.format(actual)} / {formato.format(objetivo)} {unidad} {periodo}
        </span>
        <span className="font-medium">{porcentaje}%</span>
      </div>
      <div
        role="progressbar"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={porcentaje}
        aria-label={`Progreso ${periodo}`}
        className="h-2 overflow-hidden rounded-full bg-slate-200 dark:bg-slate-800"
      >
        <div
          className={cn(
            'h-full rounded-full transition-all',
            cumplida ? 'bg-emerald-500' : 'bg-brand-500',
          )}
          style={{ width: `${porcentaje}%` }}
        />
      </div>
    </div>
  )
}
