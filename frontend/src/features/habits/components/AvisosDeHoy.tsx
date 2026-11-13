import { useState } from 'react'
import { useAjustarPlan, useMarcarLogrosVistos } from '@/features/mis-planes/api'
import {
  TEXTO_LOGRO,
  type ClaveLogro,
  type PlanHoyDto,
  type SugerenciaAjusteDto,
} from '@/features/mis-planes/types'
import { mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody } from '@/shared/ui/Card'
import { Icono } from '@/shared/ui/Icono'
import { Link } from 'react-router'
import { useToast } from '@/shared/ui/Toast'

// El backend devuelve la celebración toda la semana siguiente y no marca «vista»: se recuerda en esta pestaña.
const claveVista = (usuarioId: string, semana: number) => `celebrada:${usuarioId}:${semana}`
function yaVista(clave: string) {
  try {
    return sessionStorage.getItem(clave) === '1'
  } catch {
    return false
  }
}
function recordarVista(clave: string) {
  try {
    sessionStorage.setItem(clave, '1')
  } catch {
    // Sin almacenamiento, la celebración puede volver a salir en esta sesión: no es grave.
  }
}

// La semana cerrada (con su tip) y los logros nuevos, juntos en una sola tarjeta que se cierra una vez.
export function CelebracionDeHoy({
  usuarioId,
  semana,
  tipExtra,
  logros,
}: {
  usuarioId: string
  semana: number | null
  tipExtra: string | null
  logros: ClaveLogro[]
}) {
  const clave = semana === null ? null : claveVista(usuarioId, semana)
  const [cerrada, setCerrada] = useState(false)
  const marcarVistos = useMarcarLogrosVistos()
  const hayAlgo = (clave !== null && !yaVista(clave)) || logros.length > 0
  if (cerrada || !hayAlgo) return null

  function cerrar() {
    if (clave) recordarVista(clave)
    if (logros.length > 0) marcarVistos.mutate()
    setCerrada(true)
  }

  return (
    <Card className="mb-6 border-acento">
      <CardBody>
        <div className="flex items-start gap-3">
          <Icono nombre="trofeo" className="mt-1 size-6 shrink-0 text-acento" />
          <div className="min-w-0 flex-1 space-y-2">
            {semana !== null && clave && !yaVista(clave) && (
              <>
                <h2 className="text-lg font-bold">¡Cerraste la semana {semana}!</h2>
                {tipExtra && <p className="text-sm text-texto-2">{tipExtra}</p>}
              </>
            )}
            {logros.length > 0 && (
              <ul className="space-y-1">
                {logros.map((l) => (
                  <li key={l} className="text-sm">
                    <span className="font-semibold">{TEXTO_LOGRO[l].titulo}</span>
                    {' · '}
                    {TEXTO_LOGRO[l].descripcion}
                  </li>
                ))}
              </ul>
            )}
          </div>
          <Button variante="fantasma" tamano="sm" onClick={cerrar}>
            Gracias
          </Button>
        </div>
      </CardBody>
    </Card>
  )
}

// Solo propone: aplicar un ajuste lo decide la persona. Pausar lleva a la pantalla del plan, que pide el motivo.
export function SugerenciaDeAjuste({
  plan,
  sugerencia,
}: {
  plan: PlanHoyDto
  sugerencia: SugerenciaAjusteDto
}) {
  const ajustar = useAjustarPlan(plan.planId)
  const [oculta, setOculta] = useState(false)
  const { notificar } = useToast()
  if (oculta) return null
  const aplicar = (tipo: 'ModoSuave' | 'Extender') =>
    ajustar.mutate(tipo, {
      onSuccess: () =>
        notificar(
          tipo === 'ModoSuave' ? 'Listo: modo suave por 7 días.' : 'Listo: una semana más.',
        ),
    })
  return (
    <Alert
      tono="info"
      titulo={`¿Ajustamos ${plan.nombre}?`}
      className="mb-4"
      alCerrar={() => setOculta(true)}
      etiquetaCerrar="Ahora no"
    >
      <p>Esta semana ha sido difícil y está bien. Podemos hacerlo más liviano:</p>
      <div className="mt-2 flex flex-wrap items-center gap-2">
        {sugerencia.opciones.includes('ModoSuave') && (
          <Button
            tamano="sm"
            variante="secundario"
            disabled={ajustar.isPending}
            onClick={() => aplicar('ModoSuave')}
          >
            Modo suave 7 días
          </Button>
        )}
        {sugerencia.opciones.includes('Extender') && (
          <Button
            tamano="sm"
            variante="secundario"
            disabled={ajustar.isPending}
            onClick={() => aplicar('Extender')}
          >
            Extender una semana
          </Button>
        )}
        {sugerencia.opciones.includes('Pausar') && (
          <Link
            to={`/mis-planes/${plan.planId}`}
            className="inline-flex min-h-11 items-center font-semibold text-acento underline"
          >
            Pausar con un motivo
          </Link>
        )}
      </div>
      {ajustar.isError && (
        <p className="mt-2">{mensajeDeError(ajustar.error, 'No se pudo ajustar el plan.')}</p>
      )}
    </Alert>
  )
}
