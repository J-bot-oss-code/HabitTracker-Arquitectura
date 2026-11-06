import { Link } from 'react-router'
import { useMe } from '@/features/auth/api'
import { mensajeDeError } from '@/shared/api/problem'
import { useAuth } from '@/shared/auth/AuthContext'
import { formatearFecha } from '@/shared/lib/fechas'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { useToast } from '@/shared/ui/Toast'
import { useBajaProgramada, useCancelarBaja } from '../api'

// Avisos que el servidor pide mostrar mientras la persona usa la aplicación:
//  - requiereAceptarDocumentos: cambiaron las versiones de los documentos legales (o la cuenta nunca los aceptó).
//    No bloquea nada: solo invita a revisarlos y aceptarlos en «Privacidad y datos».
//  - eliminación programada: la cuenta se borrará al terminar el periodo de gracia; se puede cancelar desde aquí.
// Son para el rol Estándar (los administradores no pueden darse de baja).
export function AvisosDeCuenta() {
  const { usuario } = useAuth()
  if (usuario?.rol !== 'Estandar') return null
  return <Avisos />
}

function Avisos() {
  const me = useMe()
  const baja = useBajaProgramada()
  const cancelar = useCancelarBaja()
  const { notificar } = useToast()

  const pideDocumentos = me.data?.requiereAceptarDocumentos === true
  const borradoEn = baja.data
  if (!pideDocumentos && !borradoEn) return null

  return (
    <div className="mb-6 space-y-3" data-no-imprimir>
      {pideDocumentos && (
        <Alert tono="info" titulo="Actualizamos nuestros documentos">
          Revisa y acepta los Términos y la Política de privacidad vigentes.{' '}
          <Link to="/ajustes/privacidad" className="font-semibold text-acento underline">
            Ir a Privacidad y datos
          </Link>
        </Alert>
      )}
      {borradoEn && (
        <Alert tono="aviso" titulo="Tu cuenta se eliminará">
          <span>
            Se borrará para siempre el {formatearFecha(borradoEn)}. Si cambiaste de opinión,
            cancélala ahora.
          </span>
          {cancelar.isError && <span className="mt-1 block">{mensajeDeError(cancelar.error)}</span>}
          <Button
            className="mt-3"
            variante="secundario"
            cargando={cancelar.isPending}
            onClick={() =>
              cancelar.mutate(undefined, {
                onSuccess: () => notificar('Eliminación cancelada: tu cuenta sigue activa.'),
              })
            }
          >
            Cancelar eliminación
          </Button>
        </Alert>
      )}
    </div>
  )
}
