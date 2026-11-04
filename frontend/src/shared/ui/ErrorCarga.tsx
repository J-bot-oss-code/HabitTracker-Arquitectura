import { mensajeDeError } from '@/shared/api/problem'
import { Alert } from './Alert'
import { Button } from './Button'

// Error al cargar datos de una pantalla (§7: "error con reintento"). El mensaje pasa por
// mensajeDeError (RD-08) y "Reintentar" vuelve a pedir los datos sin recargar la página.
export function ErrorCarga({
  error,
  porDefecto,
  alReintentar,
}: {
  error: unknown
  porDefecto?: string
  alReintentar: () => void
}) {
  return (
    <div className="space-y-3">
      <Alert tono="peligro">{mensajeDeError(error, porDefecto)}</Alert>
      <Button variante="secundario" onClick={alReintentar}>
        Reintentar
      </Button>
    </div>
  )
}
