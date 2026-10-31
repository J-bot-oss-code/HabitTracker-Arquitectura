import { cn } from '@/shared/lib/cn'

// Logotipo: marca de verificación dentro de un escudo cortado, como un emblema deportivo.
// El nombre se oculta bajo `sm` para no desbordar la cabecera a 360 px, salvo que se fuerce.
export function Marca({
  className,
  forzarTexto = false,
}: {
  className?: string
  forzarTexto?: boolean
}) {
  return (
    <div className={cn('flex items-center gap-2.5', className)}>
      <span className="corte grid size-10 shrink-0 place-items-center bg-acento text-acento-texto">
        <svg viewBox="0 0 24 24" className="size-6" fill="none" aria-hidden="true">
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
          'font-display text-2xl font-bold uppercase leading-none tracking-wider',
          !forzarTexto && 'hidden sm:inline',
        )}
      >
        Habit<span className="text-acento">Tracker</span>
        <span className="ml-1 text-acento-2">Nexus</span>
      </span>
    </div>
  )
}
