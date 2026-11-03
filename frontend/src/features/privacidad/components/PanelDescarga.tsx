import { mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody } from '@/shared/ui/Card'
import { Icono } from '@/shared/ui/Icono'
import { useToast } from '@/shared/ui/Toast'
import { useDescargarDatos } from '../api'

// Descargar mis datos (§5.5): acceso y portabilidad. El archivo se arma en el navegador con lo
// que la API ya entrega al usuario; no hay una cola de "te avisamos por correo".
export function PanelDescarga() {
  const descargar = useDescargarDatos()
  const { notificar } = useToast()

  return (
    <Card>
      <CardBody>
        <h2 className="text-lg font-bold">Descargar mis datos</h2>
        <p className="mt-1 text-sm text-atenuado">
          Recibirás un archivo JSON con tu cuenta, tus hábitos y avances, tus solicitudes de
          permiso, tu perfil, tu peso y medidas, y tus consentimientos. Se genera en tu navegador.
        </p>
        <p className="mt-2 text-sm text-atenuado">
          El archivo no está cifrado y incluye datos de salud: guárdalo en un lugar seguro.
        </p>
        {descargar.isError && (
          <Alert tono="peligro" className="mt-4">
            {mensajeDeError(descargar.error, 'No se pudo preparar la descarga.')}
          </Alert>
        )}
        <Button
          className="mt-4"
          variante="secundario"
          cargando={descargar.isPending}
          onClick={() =>
            descargar.mutate(undefined, { onSuccess: () => notificar('Descarga lista.') })
          }
        >
          <Icono nombre="documentos" className="size-4" />
          Descargar mis datos
        </Button>
      </CardBody>
    </Card>
  )
}
