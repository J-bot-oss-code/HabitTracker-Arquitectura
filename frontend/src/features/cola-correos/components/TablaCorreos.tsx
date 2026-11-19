import { formatearFechaHora } from '@/shared/lib/fechas'
import { Badge, type Tono } from '@/shared/ui/Badge'
import type { NombreIcono } from '@/shared/ui/Icono'
import { MAX_INTENTOS_CORREO, type CorreoColaDto, type EstadoCorreo } from '../types'

// Una sola estructura en el DOM: tabla desde `lg` y una tarjeta por correo en móvil (cada celda lleva su rótulo con
// data-etiqueta). Los roles explícitos conservan la semántica de tabla aunque el CSS cambie el `display`.
const celda = 'block px-4 py-1.5 lg:table-cell lg:py-3 lg:align-top'
const conRotulo =
  'before:mb-0.5 before:block before:rotulo before:content-[attr(data-etiqueta)] lg:before:hidden'
const encabezado = 'px-4 py-3 rotulo'

// El estado se dice con texto e icono, nunca solo con el color.
const ESTADOS: Record<EstadoCorreo, { tono: Tono; icono: NombreIcono }> = {
  Pendiente: { tono: 'neutro', icono: 'calendario' },
  Procesando: { tono: 'info', icono: 'rayo' },
  Enviado: { tono: 'exito', icono: 'check' },
  // Un fallido que aún se reintentará es un aviso; solo el que se rindió es un problema.
  Fallido: { tono: 'aviso', icono: 'alerta' },
}

// Una celda sin dato se oculta en las tarjetas de móvil (ahorra líneas) y se dice con texto en la tabla.
const ocultaSiVacia = (vacia: boolean) => (vacia ? 'max-lg:hidden' : '')
const fecha = 'lg:whitespace-nowrap'
const Fecha = ({ iso, vacio }: { iso: string | null; vacio: string }) =>
  iso ? <>{formatearFechaHora(iso)}</> : <span className="text-atenuado">{vacio}</span>

export function TablaCorreos({ correos }: { correos: CorreoColaDto[] }) {
  return (
    <div className="lg:overflow-x-auto lg:rounded-lg lg:border lg:border-borde lg:bg-superficie">
      <table
        role="table"
        aria-label="Correos en la cola"
        className="block w-full text-left text-sm lg:table"
      >
        <thead role="rowgroup" className="sr-only lg:not-sr-only lg:table-header-group">
          <tr role="row" className="lg:table-row">
            <th scope="col" className={encabezado}>
              Destinatario y asunto
            </th>
            <th scope="col" className={encabezado}>
              Estado
            </th>
            <th scope="col" className={encabezado}>
              Creado
            </th>
            <th scope="col" className={encabezado}>
              Último intento
            </th>
            <th scope="col" className={encabezado}>
              Enviado el
            </th>
            <th scope="col" className={encabezado}>
              Último error
            </th>
          </tr>
        </thead>
        <tbody
          role="rowgroup"
          className="block space-y-3 lg:table-row-group lg:space-y-0 lg:divide-y lg:divide-borde"
        >
          {correos.map((c) => (
            <tr
              key={c.id}
              role="row"
              className="block rounded-lg border border-borde bg-superficie py-3 lg:table-row lg:rounded-none lg:border-0 lg:bg-transparent lg:py-0"
            >
              <td role="cell" className={celda}>
                <p className="break-all font-semibold">{c.destinatario}</p>
                <p className="break-words text-atenuado">{c.asunto}</p>
              </td>
              <td role="cell" data-etiqueta="Estado" className={`${celda} ${conRotulo}`}>
                <Badge
                  tono={
                    c.estado === 'Fallido' && c.intentos >= MAX_INTENTOS_CORREO
                      ? 'peligro'
                      : ESTADOS[c.estado].tono
                  }
                  icono={ESTADOS[c.estado].icono}
                >
                  {c.estado}
                </Badge>
                <p className="mt-1 text-xs text-atenuado">
                  Intentos: {c.intentos} de {MAX_INTENTOS_CORREO}
                  {c.estado === 'Fallido' &&
                    c.intentos >= MAX_INTENTOS_CORREO &&
                    ' · ya no se reintenta'}
                </p>
              </td>
              <td role="cell" data-etiqueta="Creado" className={`${celda} ${conRotulo} ${fecha}`}>
                {formatearFechaHora(c.fechaCreacion)}
              </td>
              <td
                role="cell"
                data-etiqueta="Último intento"
                className={`${celda} ${conRotulo} ${fecha} ${ocultaSiVacia(!c.fechaUltimoIntento)}`}
              >
                <Fecha iso={c.fechaUltimoIntento} vacio="Sin intentos todavía" />
              </td>
              <td
                role="cell"
                data-etiqueta="Enviado el"
                className={`${celda} ${conRotulo} ${fecha} ${ocultaSiVacia(!c.fechaEnvio)}`}
              >
                <Fecha iso={c.fechaEnvio} vacio="Sin enviar" />
              </td>
              <td
                role="cell"
                data-etiqueta="Último error"
                className={`${celda} ${conRotulo} ${ocultaSiVacia(!c.ultimoError)}`}
              >
                {c.ultimoError ? (
                  <span className="line-clamp-2 break-words" title={c.ultimoError}>
                    {c.ultimoError}
                  </span>
                ) : (
                  <span className="text-atenuado">Ninguno</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
