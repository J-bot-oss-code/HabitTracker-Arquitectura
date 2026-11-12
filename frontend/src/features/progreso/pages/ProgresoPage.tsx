import { useState } from 'react'
import { constanciaPorDia, logrosDe, recordsDe, useHabitos } from '@/features/habits/api'
import { usePerfil } from '@/features/perfil/api'
import { usePlanResumen } from '@/features/planes/api'
import { fechaLargaUtc } from '@/shared/lib/fechas'
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
import { etiquetaPeriodo, instanteFinDelPlan } from '../metricas'
import { PERIODOS, type Periodo } from '../types'

const SEMANAS_CONSTANCIA = 10

// Progreso (especificación §5.4): peso, medidas, fotos, constancia, récords y logros. Todo lo
// que no depende de datos nuevos se calcula con los hábitos reales; peso y medidas son
// contratos propuestos (features/progreso/types.ts).
export function ProgresoPage() {
  const [periodo, setPeriodo] = useState<Periodo>('3meses')
  const perfil = usePerfil()
  const { plan: meta } = usePlanResumen(perfil.data?.metaSlug)
  const fin = instanteFinDelPlan(perfil.data?.inicioPlan, meta?.semanas)

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

        <div className="grid items-start gap-6 lg:grid-cols-2">
          <MedidasCard />
          <FotosCard />
        </div>

        <ConstanciaYLogros />
      </div>
    </>
  )
}

// Fotos (§5.4): vive en el Manejador de documentos (RF-DOC-01), que aún no existe.
function FotosCard() {
  return (
    <Card>
      <CardBody>
        <div className="mb-3 flex flex-wrap items-center gap-3">
          <h2 className="text-lg font-bold">Fotos de progreso</h2>
          <Badge tono="aviso" icono="calendario">
            Próximamente
          </Badge>
        </div>
        <p className="mb-3 flex items-center gap-1.5 text-xs text-atenuado">
          <Icono nombre="candado" className="size-3.5" />
          Solo tú las verás.
        </p>
        <p className="text-sm text-texto-2">
          Podrás guardar fotos privadas y compararlas antes y después. Se habilitará cuando el
          servidor entregue el manejo de documentos.
        </p>
      </CardBody>
    </Card>
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
  const records = recordsDe(lista)
  const logros = logrosDe(records)
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

      <Card>
        <CardBody>
          <h2 className="mb-4 text-lg font-bold">Récords y logros</h2>
          {/* Variante compacta: tres tarjetas dentro de una tarjeta no caben en media columna. */}
          <dl className="grid grid-cols-3 divide-x divide-borde rounded-xl border border-borde">
            {[
              ['Mejor racha', records.rachaMasLarga, 'días'],
              ['Avances', records.avancesRegistrados, 'registrados'],
              ['Completados', records.habitosCompletados, 'hábitos'],
            ].map(([etiqueta, valor, detalle]) => (
              <div key={etiqueta} className="min-w-0 px-3 py-3 text-center">
                <dt className="break-words text-xs font-medium text-atenuado [overflow-wrap:anywhere]">
                  {etiqueta}
                </dt>
                <dd className="cifra mt-1 text-2xl">{valor}</dd>
                <dd className="text-xs text-atenuado">{detalle}</dd>
              </div>
            ))}
          </dl>
          <ul className="mt-5 divide-y divide-borde">
            {logros.map((l) => (
              <li key={l.clave} className="flex items-center justify-between gap-3 py-3">
                <div className="min-w-0">
                  <p className="font-semibold">{l.titulo}</p>
                  <p className="text-sm text-atenuado">{l.descripcion}</p>
                </div>
                {l.obtenido ? (
                  <Badge tono="exito" icono="check">
                    Obtenido
                  </Badge>
                ) : (
                  // Sin candado: en esta pantalla el candado ya significa "solo tú lo ves".
                  <span className="shrink-0 text-right text-xs text-atenuado">
                    <Badge tono="neutro">Bloqueado</Badge>
                    <span className="mt-1 block">
                      {l.actual} de {l.meta}
                    </span>
                  </span>
                )}
              </li>
            ))}
          </ul>
        </CardBody>
      </Card>
    </div>
  )
}
