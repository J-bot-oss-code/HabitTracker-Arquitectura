import { useState } from 'react'
import { mensajeDeError } from '@/shared/api/problem'
import { MENSAJE_PEDIDO_ENVIADO } from '@/features/permissions/catalogo'
import { Alert } from '@/shared/ui/Alert'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { ConfirmarDialogo } from '@/shared/ui/ConfirmarDialogo'
import { useToast } from '@/shared/ui/Toast'
import { usePermisosDeArchivos } from '../permisoArchivo'

// Eliminar un archivo (o quitarlo de un plan). Libre, salvo que esté en un plan en curso: entonces hace falta el permiso de
// ese archivo, que se pide aquí mismo y aprueba un Administrador. Con el permiso aplicado, la acción vuelve a estar libre.
export function BotonEliminarArchivo({
  documentoId,
  nombre,
  protegido,
  accion,
  titulo,
  mensaje,
  ejecutar,
  listo = true,
}: {
  documentoId: string
  nombre: string
  // El archivo está en un plan en curso.
  protegido: boolean
  // «Eliminar» o «Quitar del plan».
  accion: string
  titulo: string
  mensaje: string
  ejecutar: () => Promise<unknown>
  // Falta comprobar si el archivo está en un plan en curso: hasta saberlo no se ofrece eliminar (evita confirmar y recibir un 409).
  listo?: boolean
}) {
  const permisos = usePermisosDeArchivos()
  const { notificar } = useToast()
  const [abierto, setAbierto] = useState(false)
  const [haciendo, setHaciendo] = useState(false)
  const [error, setError] = useState<string>()
  const estado = protegido ? permisos.estadoDe(documentoId) : 'aplicado'

  if (!listo) {
    return (
      <Button tamano="sm" variante="fantasma" disabled aria-label={`${accion}: ${nombre}`}>
        {accion}
      </Button>
    )
  }

  if (estado === 'pendiente') {
    return <Badge tono="aviso">Pedido en revisión</Badge>
  }

  if (estado === 'ninguno' || estado === 'rechazado') {
    return (
      <span className="inline-flex flex-col items-start gap-1">
        <Button
          tamano="sm"
          variante="fantasma"
          disabled={permisos.cargando}
          cargando={permisos.pidiendo}
          aria-label={`Pedir permiso para ${accion.toLowerCase()}: ${nombre}`}
          onClick={() =>
            permisos.pedir(
              { permisoSolicitado: permisos.claveDe(documentoId) },
              {
                onSuccess: () => notificar(MENSAJE_PEDIDO_ENVIADO),
              },
            )
          }
        >
          Pedir permiso para {accion.toLowerCase()}
        </Button>
        {estado === 'rechazado' && (
          <span className="text-sm text-atenuado">
            Tu pedido anterior no se aprobó. Puedes pedirlo de nuevo.
          </span>
        )}
        {permisos.errorAlPedir && (
          <Alert tono="aviso">
            {mensajeDeError(permisos.errorAlPedir, 'No se pudo pedir el permiso.')}
          </Alert>
        )}
      </span>
    )
  }

  async function confirmar() {
    setHaciendo(true)
    setError(undefined)
    try {
      await ejecutar()
      setAbierto(false)
    } catch (e) {
      setError(mensajeDeError(e, 'No se pudo completar la acción.'))
    } finally {
      setHaciendo(false)
    }
  }

  return (
    <>
      {protegido && <span className="text-sm text-atenuado">Permiso aprobado</span>}
      <Button
        tamano="sm"
        variante="fantasma"
        aria-label={`${accion}: ${nombre}`}
        onClick={() => setAbierto(true)}
      >
        {accion}
      </Button>
      <ConfirmarDialogo
        abierto={abierto}
        titulo={titulo}
        mensaje={mensaje}
        textoConfirmar={accion}
        cargando={haciendo}
        error={error && <Alert tono="aviso">{error}</Alert>}
        alConfirmar={confirmar}
        alCerrar={() => {
          setAbierto(false)
          setError(undefined)
        }}
      />
    </>
  )
}
