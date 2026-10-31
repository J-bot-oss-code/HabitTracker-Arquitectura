import { formatearFecha, parsearFechaUtc } from '@/shared/lib/fechas'
import { EmptyState } from '@/shared/ui/Card'
import type { RegistroAvanceDto } from '../types'

const formato = new Intl.NumberFormat('es-DO', { maximumFractionDigits: 2 })

export function HistorialAvances({ registros }: { registros: RegistroAvanceDto[] }) {
  if (registros.length === 0) {
    return (
      <EmptyState
        titulo="Sin avances todavía"
        descripcion="Los avances registrados aparecerán aquí."
      />
    )
  }

  const ordenados = [...registros].sort(
    (a, b) => parsearFechaUtc(b.fecha).getTime() - parsearFechaUtc(a.fecha).getTime(),
  )

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-sm">
        <thead className="text-xs font-semibold uppercase tracking-wider text-atenuado">
          <tr>
            <th scope="col" className="py-2 pr-4 font-medium">
              Fecha
            </th>
            <th scope="col" className="py-2 font-medium">
              Cantidad
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-borde">
          {ordenados.map((r) => (
            <tr key={r.id}>
              <td className="py-2 pr-4">{formatearFecha(r.fecha)}</td>
              <td className="py-2">
                {formato.format(r.cantidad)} {r.unidad}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
