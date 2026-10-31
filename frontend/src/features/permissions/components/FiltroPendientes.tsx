import { cn } from '@/shared/lib/cn'

export function FiltroPendientes({
  valor,
  alCambiar,
}: {
  valor: boolean
  alCambiar: (soloPendientes: boolean) => void
}) {
  const opciones = [
    { valor: true, etiqueta: 'Pendientes' },
    { valor: false, etiqueta: 'Todas' },
  ]
  return (
    <div
      role="tablist"
      aria-label="Filtrar solicitudes"
      className="inline-flex rounded-lg bg-slate-100 p-1 dark:bg-slate-800"
    >
      {opciones.map((o) => (
        <button
          key={o.etiqueta}
          type="button"
          role="tab"
          aria-selected={valor === o.valor}
          onClick={() => alCambiar(o.valor)}
          className={cn(
            'rounded-md px-3 py-1 text-sm font-medium',
            valor === o.valor
              ? 'bg-white shadow-xs dark:bg-slate-950'
              : 'text-slate-600 dark:text-slate-400',
          )}
        >
          {o.etiqueta}
        </button>
      ))}
    </div>
  )
}
