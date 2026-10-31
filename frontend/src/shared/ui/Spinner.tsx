import { cn } from '@/shared/lib/cn'

export function Spinner({ className }: { className?: string }) {
  return (
    <svg
      className={cn('animate-spin text-current', className ?? 'size-5')}
      viewBox="0 0 24 24"
      fill="none"
      aria-hidden="true"
    >
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeOpacity="0.25" strokeWidth="4" />
      <path
        d="M22 12a10 10 0 0 0-10-10"
        stroke="currentColor"
        strokeWidth="4"
        strokeLinecap="round"
      />
    </svg>
  )
}

// Bloque de carga con la forma del contenido que viene, para evitar saltos de diseño.
export function Esqueleto({ className }: { className?: string }) {
  return (
    <div aria-hidden="true" className={cn('esqueleto rounded-lg', className ?? 'h-4 w-full')} />
  )
}

export function CargandoPagina({ texto = 'Cargando…' }: { texto?: string }) {
  return (
    <div role="status" aria-live="polite" className="space-y-4 py-4">
      <span className="sr-only">{texto}</span>
      <Esqueleto className="h-8 w-56" />
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        <Esqueleto className="h-36" />
        <Esqueleto className="h-36" />
        <Esqueleto className="h-36 hidden sm:block" />
      </div>
    </div>
  )
}
