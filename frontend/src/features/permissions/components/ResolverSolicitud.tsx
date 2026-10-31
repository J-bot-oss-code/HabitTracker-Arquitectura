import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { mensajeDeError } from '@/shared/api/problem'
import { useAuth } from '@/shared/auth/AuthContext'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { ConfirmarDialogo } from '@/shared/ui/ConfirmarDialogo'
import { Textarea } from '@/shared/ui/Field'
import { Modal } from '@/shared/ui/Modal'
import { useToast } from '@/shared/ui/Toast'
import { useAprobarSolicitud, useRechazarSolicitud } from '../api'
import { nombrePermiso } from '../catalogo'
import { rechazarSchema, type RechazarForm } from '../schemas'
import type { SolicitudPermisoDto } from '../types'

// Botones de Administrador para una solicitud Pendiente (RF-GP-03 / RF-GP-04).
export function ResolverSolicitud({ solicitud }: { solicitud: SolicitudPermisoDto }) {
  const { usuario } = useAuth()
  const { notificar } = useToast()
  const aprobar = useAprobarSolicitud()
  const [aprobando, setAprobando] = useState(false)
  const [rechazando, setRechazando] = useState(false)

  if (solicitud.estado !== 'Pendiente') return null

  // RF-GP-07: nadie resuelve su propia solicitud. El servidor también lo rechaza.
  const esPropia = usuario?.correo.toLowerCase() === solicitud.solicitante.toLowerCase()
  if (esPropia) {
    return <span className="text-xs text-atenuado">No puedes resolver tu propia solicitud</span>
  }

  // Cada botón de fila nombra su solicitud: "Aprobar Reconfiguración… de otro@demo.com".
  const contexto = `${nombrePermiso(solicitud.permisoSolicitado)} de ${solicitud.solicitante}`

  return (
    <div className="flex flex-wrap gap-2 md:justify-end">
      <Button tamano="sm" aria-label={`Aprobar ${contexto}`} onClick={() => setAprobando(true)}>
        Aprobar
      </Button>
      <Button
        tamano="sm"
        variante="secundario"
        aria-label={`Rechazar ${contexto}`}
        onClick={() => setRechazando(true)}
      >
        Rechazar
      </Button>

      <ConfirmarDialogo
        abierto={aprobando}
        titulo="Aprobar solicitud"
        mensaje={
          <p>
            Se concederá <strong>{nombrePermiso(solicitud.permisoSolicitado)}</strong> a{' '}
            <strong>{solicitud.solicitante}</strong>. El permiso se aplica de inmediato.
          </p>
        }
        textoConfirmar="Aprobar y aplicar"
        cargando={aprobar.isPending}
        error={
          aprobar.isError && (
            <Alert tono="peligro">{mensajeDeError(aprobar.error, 'No se pudo aprobar.')}</Alert>
          )
        }
        alConfirmar={() =>
          aprobar.mutate(solicitud.id, {
            onSuccess: () => {
              setAprobando(false)
              notificar('Solicitud aprobada y aplicada.')
              // La fila desaparece de la lista: el foco vuelve al contenido principal.
              document.getElementById('contenido')?.focus()
            },
          })
        }
        alCerrar={() => {
          aprobar.reset()
          setAprobando(false)
        }}
      />

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
      <p className="text-sm text-atenuado">
        {solicitud.solicitante} solicita{' '}
        <strong>{nombrePermiso(solicitud.permisoSolicitado)}</strong>.
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
