import { useState } from 'react'
import { mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { PageHeader } from '@/shared/ui/Card'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useSolicitudes } from '../api'
import { FiltroPendientes } from '../components/FiltroPendientes'
import { ResolverSolicitud } from '../components/ResolverSolicitud'
import { SolicitudesTabla } from '../components/SolicitudesTabla'

export function PermissionsInboxPage() {
  const [soloPendientes, setSoloPendientes] = useState(true)
  const solicitudes = useSolicitudes(soloPendientes)

  return (
    <>
      <PageHeader
        titulo="Solicitudes de permiso"
        descripcion="Aprueba o rechaza las solicitudes de los usuarios."
        acciones={<FiltroPendientes valor={soloPendientes} alCambiar={setSoloPendientes} />}
      />
      {solicitudes.isPending ? (
        <CargandoPagina />
      ) : solicitudes.isError ? (
        <Alert tono="peligro">{mensajeDeError(solicitudes.error)}</Alert>
      ) : (
        <SolicitudesTabla
          solicitudes={solicitudes.data}
          mostrarSolicitante
          acciones={(s) => <ResolverSolicitud solicitud={s} />}
          vacio={soloPendientes ? 'No hay solicitudes pendientes.' : 'No hay solicitudes.'}
        />
      )}
    </>
  )
}
