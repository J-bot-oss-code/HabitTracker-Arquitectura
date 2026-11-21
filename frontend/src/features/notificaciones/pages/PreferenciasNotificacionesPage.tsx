import { Link } from 'react-router'
import { mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Card, CardBody } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Interruptor } from '@/shared/ui/Interruptor'
import { Esqueleto } from '@/shared/ui/Spinner'
import { useToast } from '@/shared/ui/Toast'
import { useGuardarPreferencias, usePreferenciasNotificaciones } from '../api'

// Configuración › Notificaciones (RF-NOT-07): apagar el correo no pierde nada, la bandeja interna siempre se conserva.
export function PreferenciasNotificacionesPage() {
  const preferencias = usePreferenciasNotificaciones()
  const guardar = useGuardarPreferencias()
  const { notificar } = useToast()

  const cambiar = (correoActivo: boolean) =>
    guardar.mutate(correoActivo, {
      onSuccess: (r) =>
        notificar(
          r.correoActivo
            ? 'Recibirás los avisos también por correo.'
            : 'Dejarás de recibir los avisos por correo.',
        ),
    })

  return (
    <Card>
      <CardBody>
        <h2 className="text-lg font-bold">Avisos por correo</h2>
        <p className="mt-1 text-sm text-atenuado">
          Tus avisos siempre se guardan en{' '}
          <Link to="/notificaciones" className="font-semibold text-acento underline">
            Notificaciones
          </Link>
          , aunque no los recibas por correo.
        </p>
        {guardar.isError && (
          <Alert tono="peligro" className="mt-3">
            {mensajeDeError(guardar.error, 'No se pudo guardar tu preferencia.')}
          </Alert>
        )}
        <div className="mt-2">
          {preferencias.isPending ? (
            <Esqueleto className="h-16" />
          ) : preferencias.isError ? (
            <ErrorCarga
              error={preferencias.error}
              porDefecto="No se pudo cargar tu preferencia."
              alReintentar={() => preferencias.refetch()}
            />
          ) : (
            <Interruptor
              etiqueta="Recibir también por correo"
              descripcion="Te enviamos por correo los mismos avisos que ves en la bandeja. Si lo apagas, tampoco te llegan los recordatorios de tus planes."
              activado={preferencias.data.correoActivo}
              deshabilitado={guardar.isPending}
              alCambiar={cambiar}
            />
          )}
        </div>
      </CardBody>
    </Card>
  )
}
