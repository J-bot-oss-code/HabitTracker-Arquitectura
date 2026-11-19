import { useRef, useState, type ChangeEvent } from 'react'
import { mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Icono } from '@/shared/ui/Icono'
import { useToast } from '@/shared/ui/Toast'
import { useSubirDocumento } from '../api'
import { errorDeArchivo } from '../schemas'
import { EXTENSIONES_PERMITIDAS } from '../types'

// Subir un archivo propio. Se valida aquí (tamaño y tipo) para avisar antes de enviar; el servidor vuelve a validar (RD-07).
export function SubirArchivo({ alSubir }: { alSubir?: (id: string) => void }) {
  const subir = useSubirDocumento()
  const { notificar } = useToast()
  const entrada = useRef<HTMLInputElement>(null)
  const [error, setError] = useState<string>()

  function alElegir(e: ChangeEvent<HTMLInputElement>) {
    const archivo = e.target.files?.[0]
    e.target.value = '' // permite volver a elegir el mismo archivo
    if (!archivo) return
    const problema = errorDeArchivo(archivo)
    if (problema) {
      setError(problema)
      return
    }
    setError(undefined)
    subir.mutate(archivo, {
      onSuccess: (documento) => {
        // Desde un plan, quien llama dice lo que pasó después; un solo aviso.
        if (alSubir) alSubir(documento.id)
        else notificar('Archivo guardado.')
      },
      onError: (err) => setError(mensajeDeError(err, 'No se pudo subir el archivo.')),
    })
  }

  return (
    <div className="space-y-2">
      <input
        ref={entrada}
        type="file"
        accept={EXTENSIONES_PERMITIDAS.join(',')}
        className="sr-only"
        tabIndex={-1}
        aria-hidden="true"
        onChange={alElegir}
      />
      <div className="flex flex-wrap items-center gap-3">
        <Button
          cargando={subir.isPending}
          aria-describedby="ayuda-subir-archivo"
          onClick={() => entrada.current?.click()}
        >
          <Icono nombre="mas" className="size-4" />
          {subir.isPending ? 'Subiendo…' : 'Subir archivo'}
        </Button>
        <p id="ayuda-subir-archivo" className="text-sm text-atenuado">
          PDF, imágenes, texto, Word o Excel, hasta 10 MB.
        </p>
      </div>
      {error && <Alert tono="aviso">{error}</Alert>}
    </div>
  )
}
