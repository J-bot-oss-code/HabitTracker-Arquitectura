import { cn } from '@/shared/lib/cn'

// Logotipo (§1 de la especificación): cuadro índigo con un check y el wordmark
// "HabitTracker Nexus". El texto hereda el color, así sirve sobre fondo claro y sobre los
// paneles de tinta. El nombre se oculta bajo `sm` para no desbordar la cabecera a 360 px,
// salvo que se fuerce.
export function Marca({
  className,
  forzarTexto = false,
}: {
  className?: string
  forzarTexto?: boolean
}) {
  return (
    <div className={cn('flex items-center gap-2.5', className)}>
      <span className="grid size-9 shrink-0 place-items-center rounded-[10px] bg-acento text-acento-texto">
        <svg viewBox="0 0 24 24" className="size-5" fill="none" aria-hidden="true">
          <path
            d="M5 12.5l4.5 4.5L19 7.5"
            stroke="currentColor"
            strokeWidth="3"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
      </span>
      <span
        className={cn(
          'font-display text-lg leading-none tracking-tight',
          !forzarTexto && 'hidden sm:inline',
        )}
      >
        <span className="font-semibold">HabitTracker</span>{' '}
        <span className="font-extrabold">Nexus</span>
      </span>
    </div>
  )
}
