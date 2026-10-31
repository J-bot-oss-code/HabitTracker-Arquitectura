import type { ReactNode } from 'react'
import { Button } from './Button'
import { Modal } from './Modal'

// Confirmación para acciones de alto impacto o irreversibles (prevención de errores).
export function ConfirmarDialogo({
  abierto,
  titulo,
  mensaje,
  textoConfirmar,
  peligroso = false,
  cargando = false,
  error,
  alConfirmar,
  alCerrar,
}: {
  abierto: boolean
  titulo: string
  mensaje: ReactNode
  textoConfirmar: string
  peligroso?: boolean
  cargando?: boolean
  error?: ReactNode
  alConfirmar: () => void
  alCerrar: () => void
}) {
  return (
    <Modal abierto={abierto} titulo={titulo} alCerrar={alCerrar}>
      <div className="space-y-4">
        <div className="text-sm">{mensaje}</div>
        {error}
        <div className="flex flex-wrap justify-end gap-2">
          <Button variante="fantasma" onClick={alCerrar}>
            Cancelar
          </Button>
          <Button
            variante={peligroso ? 'peligro' : 'primario'}
            cargando={cargando}
            onClick={alConfirmar}
          >
            {textoConfirmar}
          </Button>
        </div>
      </div>
    </Modal>
  )
}
