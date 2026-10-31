import { Badge } from './Badge'
import { Card, CardBody } from './Card'

export interface Requisito {
  id: string
  texto: string
}

// Marcador de una pieza que el backend aún no implementa. Cuando se entregue la práctica
// correspondiente, la ruta se reemplaza por la feature real.
export function ComingSoon({
  titulo,
  descripcion,
  entrega,
  requisitos,
}: {
  titulo: string
  descripcion: string
  entrega: string
  requisitos: Requisito[]
}) {
  return (
    <div className="mx-auto max-w-3xl">
      <div className="mb-6 flex flex-wrap items-center gap-3">
        <h1 className="text-2xl font-semibold tracking-tight">{titulo}</h1>
        <Badge tono="aviso">Próximamente</Badge>
      </div>
      <Card>
        <CardBody className="space-y-5">
          <p className="text-slate-600 dark:text-slate-300">{descripcion}</p>
          <p className="text-sm text-slate-500">
            Se habilitará cuando el backend entregue: <strong>{entrega}</strong>.
          </p>
          <div>
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">
              Requisitos que cubrirá
            </h2>
            <ul className="space-y-2">
              {requisitos.map((r) => (
                <li key={r.id} className="flex gap-3 text-sm">
                  <code className="shrink-0 rounded bg-slate-100 px-1.5 py-0.5 text-xs font-semibold text-slate-700 dark:bg-slate-800 dark:text-slate-300">
                    {r.id}
                  </code>
                  <span>{r.texto}</span>
                </li>
              ))}
            </ul>
          </div>
        </CardBody>
      </Card>
    </div>
  )
}
