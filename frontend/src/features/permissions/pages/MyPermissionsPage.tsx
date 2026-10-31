import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Card, CardBody, PageHeader } from '@/shared/ui/Card'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useSolicitudes } from '../api'
import { FiltroPendientes } from '../components/FiltroPendientes'
import { SolicitarPermisoForm } from '../components/SolicitarPermisoForm'
import { SolicitudesTabla } from '../components/SolicitudesTabla'

export function MyPermissionsPage() {
  const [params] = useSearchParams()
  const [soloPendientes, setSoloPendientes] = useState(false)
  const solicitudes = useSolicitudes(soloPendientes)

  return (
    <div className="space-y-6">
      <PageHeader
        titulo="Mis permisos"
        descripcion="Solicita permisos para acciones protegidas y consulta su estado."
      />

      <Card>
        <CardBody>
          <h2 className="mb-4 text-base font-semibold">Nueva solicitud</h2>
          <SolicitarPermisoForm permisoInicial={params.get('solicitar') ?? undefined} />
        </CardBody>
      </Card>

      <section className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-base font-semibold">Mis solicitudes</h2>
          <FiltroPendientes valor={soloPendientes} alCambiar={setSoloPendientes} />
        </div>
        {solicitudes.isPending ? (
          <CargandoPagina />
        ) : solicitudes.isError ? (
          <Alert tono="peligro">{mensajeDeError(solicitudes.error)}</Alert>
        ) : (
          <SolicitudesTabla
            solicitudes={solicitudes.data}
            vacio="Aún no has solicitado permisos."
          />
        )}
      </section>
    </div>
  )
}
