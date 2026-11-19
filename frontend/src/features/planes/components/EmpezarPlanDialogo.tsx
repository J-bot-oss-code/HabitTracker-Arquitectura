import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { aInscribirseRequest, inscribirseSchema } from '@/features/mis-planes/schemas'
import { useInscribirse } from '@/features/mis-planes/api'
import { codigoDeError, mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Input, Select } from '@/shared/ui/Field'
import { Modal } from '@/shared/ui/Modal'
import { useToast } from '@/shared/ui/Toast'
import { ETIQUETA_ANCLA } from '../etiquetas'
import { ANCLAS, type PlanDetalleDto } from '../types'

// «¿Cuándo lo harás?»: el ancla (momento del día) y, si quiere, una hora. Empieza con la ancla de la primera
// tarea del plan. El servidor decide si el plan es para la persona (plan_no_apto, limite_planes…).
export function EmpezarPlanDialogo({
  plan,
  abierto,
  alCerrar,
}: {
  plan: PlanDetalleDto
  abierto: boolean
  alCerrar: () => void
}) {
  const [ancla, setAncla] = useState<string>(plan.tareas[0]?.anclaSugerida ?? ANCLAS[0])
  const [hora, setHora] = useState('')
  const [errorForm, setErrorForm] = useState<string>()
  const inscribirse = useInscribirse()
  const navigate = useNavigate()
  const { notificar } = useToast()

  async function enviar(e: FormEvent) {
    e.preventDefault()
    const datos = inscribirseSchema.safeParse({ slug: plan.slug, ancla, hora })
    if (!datos.success) {
      setErrorForm(datos.error.issues[0].message)
      return
    }
    setErrorForm(undefined)
    try {
      await inscribirse.mutateAsync(aInscribirseRequest(datos.data))
      notificar('¡Listo! Tu plan ya está en Hoy.')
      navigate('/hoy')
    } catch {
      // El error queda en el estado de la mutación y se muestra en el diálogo.
    }
  }

  const codigo = codigoDeError(inscribirse.error)
  // Las reglas esperadas (límite, ya activo, no apto) no son un fallo: aviso, no rojo (P3).
  const esRegla = ['limite_planes', 'plan_ya_activo', 'plan_no_apto'].includes(codigo ?? '')
  function cerrar() {
    setErrorForm(undefined)
    inscribirse.reset()
    alCerrar()
  }
  return (
    <Modal abierto={abierto} titulo="¿Cuándo lo harás?" alCerrar={cerrar}>
      <form onSubmit={enviar} className="space-y-4" noValidate>
        <p className="text-sm text-texto-2">
          Elegir un momento del día ayuda a que se vuelva costumbre. Puedes cambiarlo después.
        </p>
        <Select etiqueta="Momento del día" value={ancla} onChange={(e) => setAncla(e.target.value)}>
          {ANCLAS.map((a) => (
            <option key={a} value={a}>
              {ETIQUETA_ANCLA[a]}
            </option>
          ))}
        </Select>
        <Input
          etiqueta="Hora del recordatorio (opcional)"
          type="time"
          value={hora}
          onChange={(e) => setHora(e.target.value)}
          ayuda="Si la eliges, manda sobre el momento del día."
        />
        {(errorForm || inscribirse.isError) && (
          <Alert tono={esRegla ? 'aviso' : 'peligro'}>
            <p>{errorForm ?? mensajeDeError(inscribirse.error, 'No se pudo empezar el plan.')}</p>
            {(codigo === 'limite_planes' || codigo === 'plan_ya_activo') && (
              <Link to="/planes" className="font-semibold underline">
                Ver mis planes
              </Link>
            )}
          </Alert>
        )}
        <div className="flex flex-wrap justify-end gap-2">
          <Button variante="fantasma" onClick={cerrar}>
            Ahora no
          </Button>
          <Button type="submit" cargando={inscribirse.isPending}>
            Empezar este plan
          </Button>
        </div>
      </form>
    </Modal>
  )
}
