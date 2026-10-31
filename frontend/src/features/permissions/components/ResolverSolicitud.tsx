import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { mensajeDeError } from '@/shared/api/problem'
import { useAuth } from '@/shared/auth/AuthContext'
import { Button } from '@/shared/ui/Button'
import { Textarea } from '@/shared/ui/Field'
import { Modal } from '@/shared/ui/Modal'
import { useToast } from '@/shared/ui/Toast'
import { useAprobarSolicitud, useRechazarSolicitud } from '../api'
import { rechazarSchema, type RechazarForm } from '../schemas'
import type { SolicitudPermisoDto } from '../types'

// Botones de Administrador para una solicitud Pendiente (RF-GP-03 / RF-GP-04).
export function ResolverSolicitud({ solicitud }: { solicitud: SolicitudPermisoDto }) {
  const { usuario } = useAuth()
  const { notificar } = useToast()
  const aprobar = useAprobarSolicitud()
  const [rechazando, setRechazando] = useState(false)

  if (solicitud.estado !== 'Pendiente') return null

  // RF-GP-07: nadie resuelve su propia solicitud. El servidor también lo rechaza.
  const esPropia = usuario?.correo.toLowerCase() === solicitud.solicitante.toLowerCase()
  if (esPropia) {
    return <span className="text-xs text-slate-500">No puedes resolver tu propia solicitud</span>
  }

  return (
    <div className="flex justify-end gap-2">
      <Button
        tamano="sm"
        cargando={aprobar.isPending}
        onClick={() =>
          aprobar.mutate(solicitud.id, {
            onSuccess: () => notificar('Solicitud aprobada y aplicada.'),
            onError: (e) => notificar(mensajeDeError(e, 'No se pudo aprobar.'), 'peligro'),
          })
        }
      >
        Aprobar
      </Button>
      <Button tamano="sm" variante="secundario" onClick={() => setRechazando(true)}>
        Rechazar
      </Button>
      <Modal abierto={rechazando} titulo="Rechazar solicitud" alCerrar={() => setRechazando(false)}>
        <FormularioRechazo solicitud={solicitud} alTerminar={() => setRechazando(false)} />
      </Modal>
    </div>
  )
}

function FormularioRechazo({
  solicitud,
  alTerminar,
}: {
  solicitud: SolicitudPermisoDto
  alTerminar: () => void
}) {
  const rechazar = useRechazarSolicitud()
  const { notificar } = useToast()
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<RechazarForm>({ resolver: zodResolver(rechazarSchema) })

  return (
    <form
      onSubmit={handleSubmit((d) =>
        rechazar.mutate(
          { id: solicitud.id, motivoRechazo: d.motivoRechazo },
          {
            onSuccess: () => {
              notificar('Solicitud rechazada.')
              alTerminar()
            },
            onError: (e) => notificar(mensajeDeError(e, 'No se pudo rechazar.'), 'peligro'),
          },
        ),
      )}
      noValidate
      className="space-y-4 text-left"
    >
      <p className="text-sm text-slate-500">
        {solicitud.solicitante} solicita <code>{solicitud.permisoSolicitado}</code>.
      </p>
      <Textarea
        etiqueta="Motivo del rechazo"
        ayuda="El solicitante podrá verlo."
        error={errors.motivoRechazo?.message}
        {...register('motivoRechazo')}
      />
      <div className="flex justify-end gap-2">
        <Button variante="fantasma" onClick={alTerminar}>
          Cancelar
        </Button>
        <Button type="submit" variante="peligro" cargando={rechazar.isPending}>
          Rechazar
        </Button>
      </div>
    </form>
  )
}
