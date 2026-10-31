import { useState } from 'react'
import { mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { ConfirmarDialogo } from '@/shared/ui/ConfirmarDialogo'
import { Icono, type NombreIcono } from '@/shared/ui/Icono'
import { useToast } from '@/shared/ui/Toast'
import { useTransicionHabito } from '../api'
import { accionesDisponibles } from '../estados'
import type { EstadoHabito, HabitoDto } from '../types'

const iconoAccion: Record<EstadoHabito, NombreIcono> = {
  Planificado: 'calendario',
  Activo: 'play',
  Pausado: 'pausa',
  Completado: 'trofeo',
}

export function AccionesTransicion({ habito }: { habito: HabitoDto }) {
  const transicion = useTransicionHabito(habito.id)
  const { notificar } = useToast()
  const acciones = accionesDisponibles(habito)
  const [confirmando, setConfirmando] = useState(false)

  const ejecutar = (destino: EstadoHabito) =>
    transicion.mutate(destino, {
      onSuccess: () => {
        setConfirmando(false)
        notificar(`Hábito ${destino.toLowerCase()}.`)
      },
    })

  if (acciones.length === 0) {
    return (
      <p className="flex items-center gap-2 text-sm text-atenuado">
        <Icono nombre="trofeo" className="size-4 text-acento" />
        Este hábito ya está completado y no admite más cambios de estado.
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
              accion.destino === 'Completado' ? setConfirmando(true) : ejecutar(accion.destino)
            }
          >
            <Icono nombre={iconoAccion[accion.destino]} className="size-4" />
            {accion.etiqueta}
          </Button>
        ))}
      </div>
      {motivo && <p className="text-xs text-atenuado">{motivo}</p>}
      <ConfirmarDialogo
        abierto={confirmando}
        titulo="Completar hábito"
        mensaje={
          <p>
            Completar «{habito.nombre}» es definitivo: ya no podrás activarlo, pausarlo ni
            reconfigurarlo.
          </p>
        }
        textoConfirmar="Sí, completar"
        cargando={transicion.isPending}
        error={
          transicion.isError && (
            <Alert tono="peligro">
              {mensajeDeError(transicion.error, 'No se pudo completar el hábito.')}
            </Alert>
          )
        }
        alConfirmar={() => ejecutar('Completado')}
        alCerrar={() => {
          transicion.reset()
          setConfirmando(false)
        }}
      />
    </div>
  )
}
