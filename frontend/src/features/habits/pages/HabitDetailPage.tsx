import type { ReactNode } from 'react'
import { Link, useParams } from 'react-router'
import { mensajeDeError } from '@/shared/api/problem'
import { formatearFechaHora } from '@/shared/lib/fechas'
import { Alert } from '@/shared/ui/Alert'
import { Card, CardBody, EmptyState } from '@/shared/ui/Card'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useHabito } from '../api'
import { AccionesTransicion } from '../components/AccionesTransicion'
import { BarraProgreso } from '../components/BarraProgreso'
import { EstadoHabitoBadge } from '../components/EstadoHabitoBadge'
import { HistorialAvances } from '../components/HistorialAvances'
import { ReconfigurarHabito } from '../components/ReconfigurarHabito'
import { RegistrarAvanceForm } from '../components/RegistrarAvanceForm'
import { avanceDelPeriodo, etiquetaPeriodo } from '../estados'

function Seccion({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <Card>
      <CardBody>
        <h2 className="mb-4 text-base font-semibold">{titulo}</h2>
        {children}
      </CardBody>
    </Card>
  )
}

export function HabitDetailPage() {
  const { id = '' } = useParams()
  const consulta = useHabito(id)

  if (consulta.isPending) return <CargandoPagina texto="Cargando hábito…" />
  if (consulta.isError) {
    return (
      <Alert tono="peligro">{mensajeDeError(consulta.error, 'No se pudo cargar el hábito.')}</Alert>
    )
  }

  const habito = consulta.data
  if (!habito) {
    return (
      <EmptyState
        titulo="Hábito no encontrado"
        descripcion="No existe o no te pertenece."
        accion={
          <Link to="/habitos" className="font-medium text-brand-600">
            Volver a mis hábitos
          </Link>
        }
      />
    )
  }

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <Link to="/habitos" className="text-sm text-slate-500 hover:text-slate-700">
          ← Mis hábitos
        </Link>
        <div className="mt-2 flex flex-wrap items-center gap-3">
          <h1 className="text-2xl font-semibold tracking-tight">{habito.nombre}</h1>
          <EstadoHabitoBadge estado={habito.estado} />
        </div>
        <p className="mt-1 text-sm text-slate-500">
          {habito.periodicidad} · objetivo {habito.cantidadObjetivo} {habito.unidad} · ciclo desde{' '}
          {formatearFechaHora(habito.fechaInicioCiclo)}
        </p>
      </div>

      <Card>
        <CardBody className="space-y-5">
          <BarraProgreso
            actual={avanceDelPeriodo(habito)}
            objetivo={habito.cantidadObjetivo}
            unidad={habito.unidad}
            periodo={etiquetaPeriodo[habito.periodicidad]}
            cumplida={habito.cumpleMetaFinal}
          />
          {habito.cumpleMetaFinal && <Alert tono="exito">Meta del período cumplida.</Alert>}
          <AccionesTransicion habito={habito} />
        </CardBody>
      </Card>

      <div className="grid gap-6 lg:grid-cols-2">
        <Seccion titulo="Registrar avance">
          <RegistrarAvanceForm habito={habito} />
        </Seccion>
        <Seccion titulo="Historial de avances">
          <HistorialAvances registros={habito.registros} />
        </Seccion>
      </div>

      <Seccion titulo="Reconfiguración avanzada">
        <ReconfigurarHabito habito={habito} />
      </Seccion>
    </div>
  )
}
