import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { PageHeader, Seccion } from '@/shared/ui/Card'
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
        rotulo="Accesos"
        titulo="Mis permisos"
        descripcion="Solicita permisos para acciones protegidas y consulta su estado."
      />

      <Seccion titulo="Nueva solicitud">
        <SolicitarPermisoForm permisoInicial={params.get('solicitar') ?? undefined} />
      </Seccion>

      <section className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-xl font-bold uppercase tracking-wide">Mis solicitudes</h2>
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
