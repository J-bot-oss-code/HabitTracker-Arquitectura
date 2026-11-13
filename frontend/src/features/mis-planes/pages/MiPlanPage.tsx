import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { mensajeDeError } from '@/shared/api/problem'
import { fechaLargaUtc, parsearFechaUtc } from '@/shared/lib/fechas'
import { Alert } from '@/shared/ui/Alert'
import { Badge, type Tono } from '@/shared/ui/Badge'
import { BarraProgreso } from '@/shared/ui/BarraProgreso'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody, EmptyState, PageHeader } from '@/shared/ui/Card'
import { Casilla } from '@/shared/ui/Casilla'
import { ConfirmarDialogo } from '@/shared/ui/ConfirmarDialogo'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Input, Select } from '@/shared/ui/Field'
import { Interruptor } from '@/shared/ui/Interruptor'
import { Modal } from '@/shared/ui/Modal'
import { Esqueleto } from '@/shared/ui/Spinner'
import { useToast } from '@/shared/ui/Toast'
import { useAjustarPlan, useCambiarEstadoPlan, useMiPlan, useRecordatorioPlan } from '../api'
import { ArchivosDelPlan } from '../components/ArchivosDelPlan'
import {
  aPausarRequest,
  aRecordatorioRequest,
  pausarPlanSchema,
  recordatorioSchema,
} from '../schemas'
import {
  MOTIVOS_PAUSA,
  type CambiarEstadoPlanRequest,
  type EstadoPlanUsuario,
  type MotivoPausa,
  type PlanUsuarioDto,
} from '../types'

// Sin rojo para ningún estado: pausar, completar o cancelar son decisiones, no fallos (P3).
const ESTADO: Record<EstadoPlanUsuario, { texto: string; tono: Tono }> = {
  Activo: { texto: 'En curso', tono: 'exito' },
  Pausado: { texto: 'En pausa', tono: 'aviso' },
  Completado: { texto: 'Completado', tono: 'marca' },
  Cancelado: { texto: 'Cancelado', tono: 'neutro' },
}

const ETIQUETA_MOTIVO: Record<MotivoPausa, string> = {
  Lesion: 'Una lesión',
  Viaje: 'Un viaje',
  Enfermedad: 'Enfermedad',
  Trabajo: 'Trabajo',
  Otro: 'Otro motivo',
}

// Detalle de un plan que la persona sigue. El servidor decide qué cambios son válidos (RD-06); aquí solo se
// ofrecen los que tienen sentido para el estado y se muestra su rechazo.
export function MiPlanPage() {
  const { id = '' } = useParams()
  const consulta = useMiPlan(id)

  if (consulta.isPending) return <Esqueleto className="h-96" />
  if (consulta.isError) {
    return (
      <ErrorCarga
        error={consulta.error}
        porDefecto="No se pudo cargar tu plan."
        alReintentar={() => consulta.refetch()}
      />
    )
  }
  if (!consulta.data) {
    return (
      <EmptyState
        nivelTitulo={1}
        icono="buscar"
        titulo="No encontramos este plan"
        accion={
          <Link
            to="/planes"
            className="inline-flex min-h-11 items-center rounded-lg px-3 font-semibold text-acento underline"
          >
            Ver mis planes
          </Link>
        }
      />
    )
  }
  return <Plan plan={consulta.data} />
}

function Plan({ plan }: { plan: PlanUsuarioDto }) {
  const { progreso: p } = plan
  const estado = ESTADO[plan.estado]
  const enCurso = plan.estado === 'Activo' || plan.estado === 'Pausado'
  const semanaGuia = plan.guia.find((s) => s.semana === p.semanaActual)
  const unidad = p.unidad === 'Dias' ? 'días' : 'semanas'

  return (
    <>
      <Link
        to="/planes"
        className="mb-2 inline-flex min-h-11 items-center font-semibold text-acento underline-offset-4 hover:underline"
      >
        Volver a Planes
      </Link>
      <PageHeader
        titulo={plan.nombre}
        descripcion={`Termina el ${fechaLargaUtc(parsearFechaUtc(plan.fechaFinPrevista))}.`}
        acciones={<Badge tono={estado.tono}>{estado.texto}</Badge>}
      />

      <div className="space-y-6">
        {plan.estado === 'Pausado' && plan.motivoPausa && (
          <Alert tono="info" titulo="Plan en pausa">
            Lo pausaste por: {ETIQUETA_MOTIVO[plan.motivoPausa].toLowerCase()}. Cuando quieras, lo
            retomamos.
          </Alert>
        )}

        <Card>
          <CardBody className="space-y-4">
            <div>
              <p className="font-semibold">
                Semana {p.semanaActual} de {p.semanasTotales}
                {enCurso && ` · ${p.diasRestantes} días restantes`}
              </p>
              <BarraProgreso
                className="mt-2"
                etiqueta="Avance del plan"
                porcentaje={(p.semanaActual / p.semanasTotales) * 100}
              />
            </div>
            <dl className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
              <Dato etiqueta={`Racha (${unidad})`} valor={p.racha} />
              <Dato etiqueta={`Mejor racha (${unidad})`} valor={p.rachaMasLarga} />
              <Dato
                etiqueta="Últimos 7 días"
                valor={`${Math.round(p.siete.porcentajeCompleto)} %`}
                detalle={`${Math.round(p.siete.porcentajeConMinimo)} % contando lo mínimo`}
              />
              <Dato
                etiqueta="Últimos 30 días"
                valor={`${Math.round(p.treinta.porcentajeCompleto)} %`}
                detalle={`${Math.round(p.treinta.porcentajeConMinimo)} % contando lo mínimo`}
              />
            </dl>
          </CardBody>
        </Card>

        {semanaGuia && (
          <Card>
            <CardBody>
              <h2 className="text-lg font-bold">Esta semana</h2>
              <p className="mt-1 text-sm text-texto-2">{semanaGuia.texto}</p>
            </CardBody>
          </Card>
        )}

        <Card>
          <CardBody>
            <h2 className="text-lg font-bold">Tus tareas</h2>
            <ul className="mt-2 divide-y divide-borde">
              {plan.habitos.map((h) => (
                <li key={h.habitoId} className="py-3">
                  <Link to={`/habitos/${h.habitoId}`} className="font-semibold hover:underline">
                    {h.nombre}
                  </Link>
                  <p className="text-sm text-atenuado">
                    {h.cantidadObjetivo} {h.unidad} ·{' '}
                    {h.periodicidad === 'Diaria' ? 'diario' : 'semanal'}
                    {h.horaSugerida && ` · ${h.horaSugerida}`}
                  </p>
                  {h.versionMinima && (
                    <p className="text-sm text-atenuado">Lo mínimo: {h.versionMinima}</p>
                  )}
                </li>
              ))}
            </ul>
          </CardBody>
        </Card>

        {enCurso && <Acciones plan={plan} />}
        {enCurso && <Recordatorio plan={plan} />}
        <ArchivosDelPlan planId={plan.id} enCurso={enCurso} />
      </div>
    </>
  )
}

function Dato({
  etiqueta,
  valor,
  detalle,
}: {
  etiqueta: string
  valor: string | number
  detalle?: string
}) {
  return (
    <div>
      <dt className="text-atenuado">{etiqueta}</dt>
      <dd className="cifra text-xl">{valor}</dd>
      {detalle && <dd className="text-xs text-atenuado">{detalle}</dd>}
    </div>
  )
}

type Dialogo = 'pausar' | 'completar' | 'cancelar' | null

function Acciones({ plan }: { plan: PlanUsuarioDto }) {
  const [dialogo, setDialogo] = useState<Dialogo>(null)
  const [conservar, setConservar] = useState(true)
  const [motivo, setMotivo] = useState<string>('')
  const [errorForm, setErrorForm] = useState<string>()
  const cambiar = useCambiarEstadoPlan(plan.id)
  const ajustar = useAjustarPlan(plan.id)
  const { notificar } = useToast()

  function cerrar() {
    setDialogo(null)
    setErrorForm(undefined)
    cambiar.reset()
  }

  async function enviar(body: CambiarEstadoPlanRequest, aviso: string) {
    try {
      await cambiar.mutateAsync(body)
      notificar(aviso)
      cerrar()
    } catch {
      // El error queda en la mutación y se muestra en el diálogo.
    }
  }

  function pausar(e: FormEvent) {
    e.preventDefault()
    const datos = pausarPlanSchema.safeParse({ motivoPausa: motivo })
    if (!datos.success) {
      setErrorForm(datos.error.issues[0].message)
      return
    }
    void enviar(aPausarRequest(datos.data), 'Plan en pausa. Aquí te esperamos.')
  }

  const error = cambiar.isError && (
    <Alert tono="peligro">{mensajeDeError(cambiar.error, 'No se pudo cambiar el plan.')}</Alert>
  )
  const sugiere = plan.sugerencia !== null

  return (
    <Card>
      <CardBody className="space-y-4">
        <h2 className="text-lg font-bold">Cambiar mi plan</h2>
        {sugiere && (
          <p className="text-sm text-texto-2">
            Si esta semana se complicó, un ajuste puede ayudarte. No hay prisa.
          </p>
        )}
        <div className="flex flex-wrap gap-2">
          {plan.estado === 'Activo' ? (
            <>
              <Button variante="secundario" onClick={() => setDialogo('pausar')}>
                Pausar
              </Button>
              <Button
                variante="secundario"
                disabled={ajustar.isPending}
                onClick={() =>
                  ajustar.mutate('ModoSuave', {
                    onSuccess: () => notificar('Listo: modo suave por 7 días.'),
                  })
                }
              >
                Modo suave 7 días
              </Button>
              <Button
                variante="secundario"
                disabled={ajustar.isPending}
                onClick={() =>
                  ajustar.mutate('Extender', {
                    onSuccess: () => notificar('Listo: una semana más.'),
                  })
                }
              >
                Extender una semana
              </Button>
              {plan.progreso.diasRestantes === 0 && (
                <Button variante="secundario" onClick={() => setDialogo('completar')}>
                  Completar
                </Button>
              )}
            </>
          ) : (
            <Button
              cargando={cambiar.isPending}
              onClick={() => enviar({ nuevoEstado: 'Activo' }, '¡Qué bueno verte de vuelta!')}
            >
              Reanudar
            </Button>
          )}
          <Button variante="fantasma" onClick={() => setDialogo('cancelar')}>
            Cancelar plan
          </Button>
        </div>
        {ajustar.isError && (
          <Alert tono="peligro">
            {mensajeDeError(ajustar.error, 'No se pudo ajustar el plan.')}
          </Alert>
        )}
        {plan.estado !== 'Activo' && error}
      </CardBody>

      <Modal abierto={dialogo === 'pausar'} titulo="Pausar el plan" alCerrar={cerrar}>
        <form onSubmit={pausar} className="space-y-4" noValidate>
          <p className="text-sm text-texto-2">
            Una pausa no corta tu racha. Cuéntanos el motivo para acompañarte mejor.
          </p>
          <Select
            etiqueta="Motivo"
            value={motivo}
            error={errorForm}
            onChange={(e) => setMotivo(e.target.value)}
          >
            <option value="">Elige uno…</option>
            {MOTIVOS_PAUSA.map((m) => (
              <option key={m} value={m}>
                {ETIQUETA_MOTIVO[m]}
              </option>
            ))}
          </Select>
          {error}
          <div className="flex flex-wrap justify-end gap-2">
            <Button variante="fantasma" onClick={cerrar}>
              Ahora no
            </Button>
            <Button type="submit" cargando={cambiar.isPending}>
              Pausar
            </Button>
          </div>
        </form>
      </Modal>

      <ConfirmarDialogo
        abierto={dialogo === 'completar' || dialogo === 'cancelar'}
        titulo={dialogo === 'completar' ? 'Completar el plan' : 'Dejar este plan'}
        mensaje={
          <div className="space-y-2">
            <p>
              {dialogo === 'completar'
                ? 'Lo cerramos como completado. Solo se puede al llegar al final del plan.'
                : 'Dejarás de seguir este plan. Tu historial se queda guardado.'}
            </p>
            <Casilla checked={conservar} onChange={(e) => setConservar(e.target.checked)}>
              Conservar mis hábitos para seguir con ellos
            </Casilla>
          </div>
        }
        textoConfirmar={dialogo === 'completar' ? 'Completar' : 'Dejar el plan'}
        cargando={cambiar.isPending}
        error={error}
        alCerrar={cerrar}
        alConfirmar={() =>
          enviar(
            {
              nuevoEstado: dialogo === 'completar' ? 'Completado' : 'Cancelado',
              conservarHabitos: conservar,
            },
            dialogo === 'completar' ? '¡Plan completado!' : 'Plan cancelado.',
          )
        }
      />
    </Card>
  )
}

function Recordatorio({ plan }: { plan: PlanUsuarioDto }) {
  const [activos, setActivos] = useState(plan.recordatoriosActivos)
  const [hora, setHora] = useState(plan.horaRecordatorio ?? '')
  const [errorForm, setErrorForm] = useState<string>()
  const guardar = useRecordatorioPlan(plan.id)
  const { notificar } = useToast()

  async function enviar(e: FormEvent) {
    e.preventDefault()
    const datos = recordatorioSchema.safeParse({ recordatoriosActivos: activos, hora })
    if (!datos.success) {
      setErrorForm(datos.error.issues[0].message)
      return
    }
    setErrorForm(undefined)
    try {
      await guardar.mutateAsync(aRecordatorioRequest(datos.data))
      notificar('Recordatorio guardado.')
    } catch {
      // El error queda en la mutación y se muestra debajo.
    }
  }

  return (
    <Card>
      <CardBody>
        <form onSubmit={enviar} className="space-y-3" noValidate>
          <h2 className="text-lg font-bold">Recordatorio</h2>
          <Interruptor
            etiqueta="Recibir recordatorios de este plan"
            activado={activos}
            alCambiar={setActivos}
          />
          <Input
            etiqueta="Hora"
            type="time"
            value={hora}
            error={errorForm}
            onChange={(e) => setHora(e.target.value)}
          />
          {guardar.isError && (
            <Alert tono="peligro">
              {mensajeDeError(guardar.error, 'No se pudo guardar el recordatorio.')}
            </Alert>
          )}
          <Button type="submit" variante="secundario" cargando={guardar.isPending}>
            Guardar recordatorio
          </Button>
        </form>
      </CardBody>
    </Card>
  )
}
