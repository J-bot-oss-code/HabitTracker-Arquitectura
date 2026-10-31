import type { ReactNode } from 'react'
import { cn } from '@/shared/lib/cn'

// Anillo de progreso en SVG. El valor se limita a 0–100 y la meta cumplida se distingue por
// color y por el texto del centro (no solo por color).
export function AnilloProgreso({
  porcentaje,
  tamano = 96,
  grosor = 9,
  cumplido = false,
  etiqueta,
  children,
}: {
  porcentaje: number
  tamano?: number
  grosor?: number
  cumplido?: boolean
  // Nombre accesible del progressbar, p. ej. "Progreso de hoy".
  etiqueta: string
  children?: ReactNode
}) {
  const valor = Math.max(0, Math.min(100, Math.round(porcentaje)))
  const radio = (tamano - grosor) / 2
  const circunferencia = 2 * Math.PI * radio
  const avance = (valor / 100) * circunferencia

  return (
    <div
      role="progressbar"
      aria-label={etiqueta}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={valor}
      className="relative inline-grid shrink-0 place-items-center"
      style={{ width: tamano, height: tamano }}
    >
      <svg
        width={tamano}
        height={tamano}
        viewBox={`0 0 ${tamano} ${tamano}`}
        className="-rotate-90"
      >
        <circle
          cx={tamano / 2}
          cy={tamano / 2}
          r={radio}
          fill="none"
          strokeWidth={grosor}
          className="stroke-superficie-2"
        />
        <circle
          cx={tamano / 2}
          cy={tamano / 2}
          r={radio}
          fill="none"
          strokeWidth={grosor}
          strokeLinecap="round"
          strokeDasharray={`${avance} ${circunferencia}`}
          className={cn(
            'transition-[stroke-dasharray] duration-500 ease-out',
            cumplido ? 'stroke-exito' : 'stroke-acento',
          )}
          style={{ filter: valor > 0 ? 'drop-shadow(0 0 6px var(--c-brillo))' : undefined }}
        />
      </svg>
      <div className="absolute inset-0 grid place-items-center text-center">
        {children ?? <span className="cifra text-2xl">{valor}%</span>}
      </div>
    </div>
  )
}
