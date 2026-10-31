import { Badge } from './Badge'
import { Card, CardBody } from './Card'
import { Icono, type NombreIcono } from './Icono'

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
  icono = 'rayo',
}: {
  titulo: string
  descripcion: string
  entrega: string
  requisitos: Requisito[]
  icono?: NombreIcono
}) {
  return (
    <div className="mx-auto max-w-3xl">
      <div className="mb-6 flex flex-wrap items-center gap-3">
        <span className="grid size-12 place-items-center rounded-xl bg-superficie-2 text-acento-2">
          <Icono nombre={icono} className="size-6" />
        </span>
        <h1 className="text-3xl font-bold uppercase tracking-wide sm:text-4xl">{titulo}</h1>
        <Badge tono="aviso" icono="calendario">
          Próximamente
        </Badge>
      </div>
      <Card>
        <CardBody className="space-y-6">
          <p className="text-texto">{descripcion}</p>
          <p className="rounded-lg bg-superficie-2 px-4 py-3 text-sm text-atenuado">
            Se habilitará cuando el backend entregue:{' '}
            <strong className="text-texto">{entrega}</strong>.
          </p>
          <div>
            <h2 className="rotulo mb-3">Requisitos que cubrirá</h2>
            <ol className="relative space-y-3 border-l border-borde-fuerte pl-5">
              {requisitos.map((r) => (
                <li key={r.id} className="relative text-sm">
                  <span
                    aria-hidden="true"
                    className="absolute -left-[1.62rem] top-1.5 size-2.5 rounded-full bg-acento"
                  />
                  <code className="mr-2 font-semibold text-acento-2">{r.id}</code>
                  <span>{r.texto}</span>
                </li>
              ))}
            </ol>
          </div>
        </CardBody>
      </Card>
    </div>
  )
}
