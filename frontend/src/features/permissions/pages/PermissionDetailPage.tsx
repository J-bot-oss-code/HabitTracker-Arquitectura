import type { ReactNode } from 'react'
import { Link, useParams } from 'react-router'
import { esErrorHttp, mensajeDeError } from '@/shared/api/problem'
import { useAuth } from '@/shared/auth/AuthContext'
import { formatearFechaHora } from '@/shared/lib/fechas'
import { Alert } from '@/shared/ui/Alert'
import { Card, CardBody } from '@/shared/ui/Card'
import { Icono } from '@/shared/ui/Icono'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useSolicitud } from '../api'
import { nombrePermiso } from '../catalogo'
import { EstadoSolicitudBadge } from '../components/EstadoSolicitudBadge'
import { ResolverSolicitud } from '../components/ResolverSolicitud'

function Dato({ etiqueta, children }: { etiqueta: string; children: ReactNode }) {
  return (
    <div>
      <dt className="text-xs font-semibold uppercase tracking-wider text-atenuado">{etiqueta}</dt>
      <dd className="mt-1">{children}</dd>
    </div>
  )
}

export function PermissionDetailPage() {
  const { id = '' } = useParams()
  const { tieneRol } = useAuth()
  const solicitud = useSolicitud(id)
  const volver = tieneRol('Administrador') ? '/admin/permisos' : '/permisos'

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <Link
        to={volver}
        className="inline-flex min-h-11 items-center gap-1.5 text-sm text-atenuado hover:text-texto"
      >
        <Icono nombre="volver" className="size-4" />
        Volver a solicitudes
      </Link>

      {solicitud.isPending ? (
        <CargandoPagina />
      ) : solicitud.isError ? (
        <Alert tono="peligro">
          {esErrorHttp(solicitud.error, 404)
            ? 'La solicitud no existe.'
            : mensajeDeError(solicitud.error, 'No se pudo cargar la solicitud.')}
        </Alert>
      ) : (
        <Card>
          <CardBody className="space-y-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h1 className="text-3xl font-bold uppercase tracking-wide">
                  {nombrePermiso(solicitud.data.permisoSolicitado)}
                </h1>
                {tieneRol('Administrador') && (
                  <p className="text-sm text-atenuado">{solicitud.data.permisoSolicitado}</p>
                )}
              </div>
              <EstadoSolicitudBadge estado={solicitud.data.estado} />
            </div>
            <dl className="grid gap-4 sm:grid-cols-2">
              <Dato etiqueta="Solicitante">{solicitud.data.solicitante}</Dato>
              <Dato etiqueta="Creada">{formatearFechaHora(solicitud.data.fechaCreacion)}</Dato>
              <Dato etiqueta="Resuelta por">{solicitud.data.aprobador ?? '—'}</Dato>
              <Dato etiqueta="Fecha de resolución">
                {solicitud.data.fechaResolucion
                  ? formatearFechaHora(solicitud.data.fechaResolucion)
                  : '—'}
              </Dato>
            </dl>
            {solicitud.data.motivoRechazo && (
              <Alert tono="peligro" titulo="Motivo del rechazo">
                {solicitud.data.motivoRechazo}
              </Alert>
            )}
            {tieneRol('Administrador') && <ResolverSolicitud solicitud={solicitud.data} />}
          </CardBody>
        </Card>
      )}
    </div>
  )
}
