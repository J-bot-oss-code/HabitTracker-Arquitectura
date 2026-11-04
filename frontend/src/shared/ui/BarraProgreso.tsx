import { cn } from '@/shared/lib/cn'

// Barra de progreso de la especificación (§3, ProgressBar): 8 px y radio completo. El valor
// se limita a 0–100 y se expone como progressbar con su nombre accesible.
export function BarraProgreso({
  porcentaje,
  etiqueta,
  cumplido = false,
  className,
}: {
  porcentaje: number
  // Nombre accesible, p. ej. "Tareas de hoy completadas".
  etiqueta: string
  cumplido?: boolean
  className?: string
}) {
  // Un cociente 0/0 (sin tareas) llega como NaN: se muestra vacía, nunca "NaN%".
  const valor = Number.isFinite(porcentaje) ? Math.max(0, Math.min(100, Math.round(porcentaje))) : 0
  return (
    <div
      role="progressbar"
      aria-label={etiqueta}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={valor}
      className={cn('h-2 w-full overflow-hidden rounded-full bg-borde', className)}
    >
      <div
        className={cn(
          'h-full rounded-full transition-[width] duration-500 ease-out',
          cumplido ? 'bg-exito' : 'bg-acento',
        )}
        style={{ width: `${valor}%` }}
      />
    </div>
  )
}
