import { mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { useToast } from '@/shared/ui/Toast'
import { useTransicionHabito } from '../api'
import { accionesDisponibles } from '../estados'
import type { HabitoDto } from '../types'

export function AccionesTransicion({ habito }: { habito: HabitoDto }) {
  const transicion = useTransicionHabito(habito.id)
  const { notificar } = useToast()
  const acciones = accionesDisponibles(habito)

  if (acciones.length === 0) {
    return (
      <p className="text-sm text-slate-500">
        Estado terminal: un hábito completado no admite más transiciones.
      </p>
    )
  }

  const motivo = acciones.find((a) => !a.habilitada)?.motivo

  return (
    <div className="space-y-3">
      {transicion.isError && (
        <Alert tono="peligro">
          {mensajeDeError(transicion.error, 'No se pudo cambiar el estado.')}
        </Alert>
      )}
      <div className="flex flex-wrap gap-2">
        {acciones.map((accion) => (
          <Button
            key={accion.destino}
            variante={accion.destino === 'Pausado' ? 'secundario' : 'primario'}
            disabled={!accion.habilitada || transicion.isPending}
            cargando={transicion.isPending && transicion.variables === accion.destino}
            title={accion.motivo}
            onClick={() =>
              transicion.mutate(accion.destino, {
                onSuccess: () => notificar(`Hábito ${accion.destino.toLowerCase()}.`),
              })
            }
          >
            {accion.etiqueta}
          </Button>
        ))}
      </div>
      {motivo && <p className="text-xs text-slate-500">{motivo}</p>}
    </div>
  )
}
