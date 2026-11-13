import { useState } from 'react'
import { constanciaPorDia, useHabitos } from '@/features/habits/api'
import { useMisPlanes, useResumenProgreso } from '@/features/mis-planes/api'
import { TEXTO_LOGRO } from '@/features/mis-planes/types'
import { fechaLargaUtc, parsearFechaUtc } from '@/shared/lib/fechas'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { CalendarioCalor, type CeldaCalor } from '@/shared/ui/CalendarioCalor'
import { Card, CardBody, PageHeader } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Icono } from '@/shared/ui/Icono'
import { Segmentado } from '@/shared/ui/Segmentado'
import { Esqueleto } from '@/shared/ui/Spinner'
import { MedidasCard } from '../components/MedidasCard'
import { PesoCard } from '../components/PesoCard'
import { etiquetaPeriodo } from '../metricas'
import { PERIODOS, type Periodo } from '../types'

const SEMANAS_CONSTANCIA = 10

// Progreso (especificación §5.4): peso, medidas, fotos, constancia, récords y logros. Todo lo
// que no depende de datos nuevos se calcula con los hábitos reales; peso y medidas son
// contratos propuestos (features/progreso/types.ts).
export function ProgresoPage() {
  const [periodo, setPeriodo] = useState<Periodo>('3meses')
  // La línea de la meta de peso termina cuando termina el plan en curso que más dura.
  const misPlanes = useMisPlanes()
  const finales = (misPlanes.data ?? [])
    .filter((p) => p.estado === 'Activo' || p.estado === 'Pausado')
    .map((p) => parsearFechaUtc(p.fechaFinPrevista).getTime())
  const fin = finales.length > 0 ? Math.max(...finales) : null

  return (
    <>
      <PageHeader
        titulo="Tu progreso"
        acciones={
          <div data-no-imprimir className="flex flex-wrap items-center gap-3">
            <Segmentado
              etiqueta="Periodo del peso"
              valor={periodo}
              alCambiar={setPeriodo}
              opciones={PERIODOS.map((p) => ({ valor: p, etiqueta: etiquetaPeriodo[p] }))}
            />
            <Button variante="secundario" onClick={() => window.print()}>
              <Icono nombre="documentos" className="size-4" />
              Exportar PDF
            </Button>
          </div>
        }
      />

      <div className="space-y-6">
        <PesoCard periodo={periodo} finDelPlan={fin} />

        <MedidasCard />

        <ConstanciaYLogros />
      </div>
    </>
  )
}

function ConstanciaYLogros() {
  const habitos = useHabitos()

  if (habitos.isPending) return <Esqueleto className="h-64" />
  if (habitos.isError) {
    return (
      <ErrorCarga
        error={habitos.error}
        porDefecto="No se pudo cargar tu constancia."
        alReintentar={() => habitos.refetch()}
      />
    )
  }

  const lista = habitos.data
  const dias = constanciaPorDia(lista, SEMANAS_CONSTANCIA)
  const hoy = dias.find((d) => d.hoy)?.fecha.getTime() ?? 0
  // Resumen de la semana en curso (las 7 últimas celdas), para quien no puede leer el mapa.
  const estaSemana = dias.slice(-7).filter((d) => d.fecha.getTime() <= hoy)
  const completos = estaSemana.filter((d) => d.nivel === 3).length

  const celdas: CeldaCalor[] = dias.map((d) => {
    const dia = fechaLargaUtc(d.fecha)
    const futuro = d.fecha.getTime() > hoy
    return {
      clave: d.fecha.toISOString().slice(0, 10),
      nivel: d.nivel,
      hoy: d.hoy,
      futuro,
      descripcion: futuro
        ? `${dia}: aún no llega`
        : d.total === 0
          ? `${dia}: sin hábitos diarios activos`
          : `${dia}: ${d.cumplidos} de ${d.total} ${d.total === 1 ? 'hábito' : 'hábitos'}`,
    }
  })

  return (
    <div className="grid items-start gap-6 lg:grid-cols-2">
      <Card>
        <CardBody>
          <h2 className="text-lg font-bold">Constancia</h2>
          <p className="mb-4 mt-1 text-sm text-atenuado">
            Hábitos diarios cumplidos en las últimas {SEMANAS_CONSTANCIA} semanas. Cada columna es
            una semana, de lunes a domingo.
          </p>
          <p className="mb-4 text-sm font-semibold">
            Esta semana: {completos} de {estaSemana.length}{' '}
            {estaSemana.length === 1 ? 'día' : 'días'} con todo hecho.
          </p>
          <CalendarioCalor
            celdas={celdas}
            columnas={SEMANAS_CONSTANCIA}
            etiqueta="Constancia por día"
            etiquetasFilas={['L', 'M', 'M', 'J', 'V', 'S', 'D']}
          />
        </CardBody>
      </Card>

      <LogrosYMes />
    </div>
  )
}

// Todo sale del servidor (GET /api/progress/summary): constancia de los planes, nunca peso ni medidas (P4).
function LogrosYMes() {
  const resumen = useResumenProgreso()
  if (resumen.isPending) return <Esqueleto className="h-64" />
  if (resumen.isError) {
    return (
      <ErrorCarga
        error={resumen.error}
        porDefecto="No se pudieron cargar tus logros."
        alReintentar={() => resumen.refetch()}
      />
    )
  }
  const { mes, logros } = resumen.data
  return (
    <Card>
      <CardBody>
        <h2 className="mb-1 text-lg font-bold">Tu mes en números</h2>
        <p className="mb-4 text-sm text-atenuado">
          Lo que sumaste este mes con tus planes. Volver también cuenta.
        </p>
        <dl className="grid grid-cols-3 divide-x divide-borde rounded-xl border border-borde">
          {[
            ['Días cumplidos', mes.diasCumplidos],
            ['Hábitos que mantienes', mes.habitosMantenidos],
            ['Veces que volviste', mes.regresos],
          ].map(([etiqueta, valor]) => (
            <div key={etiqueta} className="min-w-0 px-3 py-3 text-center">
              <dt className="break-words text-xs font-medium text-atenuado [overflow-wrap:anywhere]">
                {etiqueta}
              </dt>
              <dd className="cifra mt-1 text-2xl">{valor}</dd>
            </div>
          ))}
        </dl>
        <h2 className="mb-2 mt-5 text-lg font-bold">Tus logros</h2>
        {logros.length === 0 ? (
          <p className="text-sm text-atenuado">
            Tu primer logro llega con tu primer avance en un plan.
          </p>
        ) : (
          <ul className="divide-y divide-borde">
            {logros.map((l) => (
              <li key={l.clave} className="flex items-center justify-between gap-3 py-3">
                <div className="min-w-0">
                  <p className="font-semibold">{TEXTO_LOGRO[l.clave].titulo}</p>
                  <p className="text-sm text-atenuado">{TEXTO_LOGRO[l.clave].descripcion}</p>
                </div>
                <Badge tono="exito" icono="check">
                  Obtenido
                </Badge>
              </li>
            ))}
          </ul>
        )}
      </CardBody>
    </Card>
  )
}
