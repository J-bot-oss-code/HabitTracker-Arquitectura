import { cn } from '@/shared/lib/cn'
import { Icono } from '@/shared/ui/Icono'
import { REQUISITOS_CLAVE } from '../schemas'

// Lista de requisitos de la contraseña que se marca a medida que se escribe (RF-CA-14). Solo
// orienta: el servidor vuelve a validar y además rechaza claves filtradas (RD-07).
// Va enlazada al campo con aria-describedby (id); no es una región viva para no anunciar cada tecla.
export function RequisitosClave({ id, clave }: { id: string; clave: string }) {
  const cumplidos = REQUISITOS_CLAVE.filter((r) => r.cumple(clave)).length
  const total = REQUISITOS_CLAVE.length
  return (
    <div id={id} className="rounded-xl border border-borde bg-superficie-2 p-3">
      <div className="mb-2 flex items-center justify-between gap-3">
        <p className="text-xs font-semibold text-texto-2">Tu contraseña necesita, como mínimo</p>
        <p className="text-xs tabular-nums text-atenuado">
          {cumplidos} de {total}
        </p>
      </div>
      <div className="mb-3 flex gap-1" aria-hidden="true">
        {REQUISITOS_CLAVE.map((r, i) => (
          <span
            key={r.id}
            className={cn(
              'h-1 flex-1 rounded-full transition-colors',
              i < cumplidos ? (cumplidos === total ? 'bg-exito' : 'bg-aviso') : 'bg-borde',
            )}
          />
        ))}
      </div>
      <ul className="grid gap-1.5">
        {REQUISITOS_CLAVE.map((r) => {
          const ok = r.cumple(clave)
          return (
            <li
              key={r.id}
              className={cn(
                'flex items-center gap-1.5 text-xs',
                ok ? 'font-medium text-exito' : 'text-atenuado',
              )}
            >
              <Icono
                nombre={ok ? 'check' : 'circulo'}
                className={cn('size-4 shrink-0', ok ? 'text-exito' : 'text-control')}
              />
              <span>{r.texto}</span>
              <span className="sr-only">{ok ? '(cumplido)' : '(pendiente)'}</span>
            </li>
          )
        })}
      </ul>
      <p className="mt-2.5 border-t border-borde pt-2 text-xs text-atenuado">
        Además comprobamos que no aparezca en filtraciones conocidas.
      </p>
    </div>
  )
}
