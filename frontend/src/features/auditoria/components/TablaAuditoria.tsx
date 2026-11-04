import { parsearFechaUtc } from '@/shared/lib/fechas'
import { etiquetaAccion, type EventoAuditoriaDto } from '../types'

// "1 nov 2026, 15:27": 24 horas y sin repetir "UTC" en cada fila (la cabecera ya lo dice).
const formatoCuando = new Intl.DateTimeFormat('es-DO', {
  dateStyle: 'medium',
  timeStyle: 'short',
  hourCycle: 'h23',
  timeZone: 'UTC',
})

// Qué cambió: "Administrador → Estandar", o "→ desactivar" si solo hay valor nuevo.
function cambio(e: EventoAuditoriaDto): string | null {
  if (e.valorAnterior && e.valorNuevo) return `${e.valorAnterior} → ${e.valorNuevo}`
  if (e.valorNuevo) return `→ ${e.valorNuevo}`
  return null
}

// Tabla de auditoría (§5.6): Acción · Quién · Cuándo (UTC) · Detalle · Origen. Lo que importa
// va primero, así en un móvil —donde la tabla se desplaza— lo primero que se ve es qué pasó. El
// texto se pinta tal cual (React lo escapa): el contenido de un evento nunca es HTML.
export function TablaAuditoria({
  eventos,
  conDetalle = true,
}: {
  eventos: EventoAuditoriaDto[]
  // El resumen del panel muestra solo Acción, Quién, Cuándo y Origen (§5.6).
  conDetalle?: boolean
}) {
  return (
    // Región con scroll horizontal: enfocable para poder desplazarla con el teclado (WCAG 2.1.1).
    <div
      role="region"
      aria-label="Tabla de eventos de auditoría"
      tabIndex={0}
      className="overflow-x-auto rounded-xl border border-borde"
    >
      <table className="w-full text-left text-sm">
        <caption className="sr-only">Eventos de auditoría, del más reciente al más antiguo</caption>
        <thead className="rotulo bg-superficie-2">
          <tr>
            <th scope="col" className="px-4 py-3">
              Acción
            </th>
            <th scope="col" className="px-4 py-3">
              Quién
            </th>
            <th scope="col" className="px-4 py-3">
              Cuándo (UTC)
            </th>
            {conDetalle && (
              <th scope="col" className="px-4 py-3">
                Detalle
              </th>
            )}
            <th scope="col" className="px-4 py-3">
              Origen
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-borde">
          {eventos.map((e) => {
            const detalle = cambio(e)
            return (
              <tr key={e.id}>
                <td className="whitespace-nowrap px-4 py-3 font-semibold">
                  {etiquetaAccion[e.accion]}
                </td>
                <td className="px-4 py-3">{e.actor}</td>
                <td className="whitespace-nowrap px-4 py-3 tabular-nums text-texto-2">
                  {formatoCuando.format(parsearFechaUtc(e.fecha))}
                </td>
                {conDetalle && (
                  <td className="px-4 py-3 text-texto-2">
                    <span className="block">{e.entidad}</span>
                    {detalle && <span className="block text-atenuado">{detalle}</span>}
                  </td>
                )}
                <td className="px-4 py-3 text-atenuado">{e.origen}</td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
