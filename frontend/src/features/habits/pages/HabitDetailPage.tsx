import { Link, useParams } from 'react-router'
import { mensajeDeError } from '@/shared/api/problem'
import { formatearFechaHora } from '@/shared/lib/fechas'
import { Alert } from '@/shared/ui/Alert'
import { AnilloProgreso } from '@/shared/ui/AnilloProgreso'
import { Card, CardBody, EmptyState, Seccion } from '@/shared/ui/Card'
import { Estadistica } from '@/shared/ui/Estadistica'
import { Icono } from '@/shared/ui/Icono'
import { MiniBarras } from '@/shared/ui/MiniBarras'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useHabito } from '../api'
import { AccionesTransicion } from '../components/AccionesTransicion'
import { EstadoHabitoBadge } from '../components/EstadoHabitoBadge'
import { HistorialAvances } from '../components/HistorialAvances'
import { ReconfigurarHabito } from '../components/ReconfigurarHabito'
import { RegistrarAvanceForm } from '../components/RegistrarAvanceForm'
import {
  avanceDelPeriodo,
  avancePorDia,
  etiquetaPeriodo,
  rachaActual,
  totalDelCiclo,
} from '../estados'

const formato = new Intl.NumberFormat('es-DO', { maximumFractionDigits: 2 })

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
        icono="alerta"
        accion={
          <Link to="/habitos" className="font-semibold text-acento underline">
            Volver a mis hábitos
          </Link>
        }
      />
    )
  }

  const avance = avanceDelPeriodo(habito)
  const porcentaje = habito.cantidadObjetivo > 0 ? (avance / habito.cantidadObjetivo) * 100 : 0
  const racha = rachaActual(habito)
  const dias = avancePorDia(habito, 7)
  const unidadRacha = habito.periodicidad === 'Diaria' ? 'días' : 'semanas'
  const resumenDias = dias.map((d) => `${d.etiqueta}: ${formato.format(d.valor)}`).join(', ')

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <Link
          to="/habitos"
          className="inline-flex min-h-11 items-center gap-1.5 text-sm text-atenuado hover:text-texto"
        >
          <Icono nombre="volver" className="size-4" />
          Mis hábitos
        </Link>
        <div className="mt-1 flex flex-wrap items-center gap-3">
          <h1 className="text-4xl font-bold uppercase tracking-wide sm:text-5xl">
            {habito.nombre}
          </h1>
          <EstadoHabitoBadge estado={habito.estado} />
        </div>
        <p className="mt-1.5 text-sm text-atenuado">
          {habito.periodicidad} · objetivo {formato.format(habito.cantidadObjetivo)} {habito.unidad}{' '}
          · ciclo desde {formatearFechaHora(habito.fechaInicioCiclo)}
        </p>
      </div>

      <Card className={habito.cumpleMetaFinal ? 'border-exito shadow-brillo' : undefined}>
        <CardBody className="grid gap-6 md:grid-cols-[auto_1fr] md:items-center">
          <div className="flex justify-center">
            <AnilloProgreso
              porcentaje={porcentaje}
              tamano={168}
              grosor={13}
              cumplido={habito.cumpleMetaFinal}
              etiqueta={`Progreso ${etiquetaPeriodo[habito.periodicidad]}`}
            >
              <div>
                <p className="cifra text-5xl">{Math.round(porcentaje)}%</p>
                <p className="rotulo mt-1">{etiquetaPeriodo[habito.periodicidad]}</p>
              </div>
            </AnilloProgreso>
          </div>
          <div className="space-y-5">
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
              <Estadistica
                etiqueta="Racha"
                icono="llama"
                valor={racha}
                detalle={unidadRacha}
                destacada={racha > 0}
              />
              <Estadistica
                etiqueta={`Avance ${etiquetaPeriodo[habito.periodicidad]}`}
                valor={formato.format(avance)}
                detalle={`de ${formato.format(habito.cantidadObjetivo)} ${habito.unidad}`}
              />
              <Estadistica
                etiqueta="Total del ciclo"
                icono="reportes"
                valor={formato.format(totalDelCiclo(habito))}
                detalle={`${habito.registros.length} registros`}
              />
            </div>
            {habito.cumpleMetaFinal && <Alert tono="exito">Meta del período cumplida.</Alert>}
            <AccionesTransicion habito={habito} />
          </div>
        </CardBody>
      </Card>

      <div className="grid gap-6 lg:grid-cols-2">
        <Seccion titulo="Registrar avance">
          <RegistrarAvanceForm habito={habito} />
        </Seccion>
        <Seccion titulo="Últimos 7 días">
          <MiniBarras
            datos={dias}
            objetivo={habito.periodicidad === 'Diaria' ? habito.cantidadObjetivo : undefined}
            resumen={`Avance de los últimos 7 días. ${resumenDias}`}
          />
        </Seccion>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Seccion titulo="Historial de avances">
          <HistorialAvances registros={habito.registros} />
        </Seccion>
        <Seccion titulo="Reconfiguración avanzada">
          <ReconfigurarHabito habito={habito} />
        </Seccion>
      </div>
    </div>
  )
}
