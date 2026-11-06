import { useEffect, useRef, useState } from 'react'
import { env } from '@/shared/config/env'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { cargarGoogle, type OpcionesBotonGoogle } from './gis'
import { armarTokenDemo } from './tokenDemo'

export type TextoBotonGoogle = OpcionesBotonGoogle['text']

interface PropiedadesBoton {
  texto: TextoBotonGoogle
  // Recibe el ID token que entrega Google. La API lo valida; aquí no se interpreta.
  alCredencial: (idToken: string) => void
  deshabilitado?: boolean
}

const ANCHO_MIN = 200
const ANCHO_MAX = 340

// El botón de Google lo dibuja Google (así cumple su marca y el flujo es el oficial). Se ajusta al ancho
// disponible, porque GIS exige un ancho en píxeles.
export function BotonGoogleReal({
  clienteId,
  texto,
  alCredencial,
}: PropiedadesBoton & { clienteId: string }) {
  const contenedor = useRef<HTMLDivElement>(null)
  const alCredencialActual = useRef(alCredencial)
  const [estado, setEstado] = useState<'cargando' | 'listo' | 'error'>('cargando')

  useEffect(() => {
    alCredencialActual.current = alCredencial
  }, [alCredencial])

  useEffect(() => {
    let cancelado = false
    cargarGoogle()
      .then((google) => {
        const elemento = contenedor.current
        if (cancelado || !elemento) return
        google.initialize({
          client_id: clienteId,
          callback: (respuesta) => alCredencialActual.current(respuesta.credential),
          ux_mode: 'popup',
        })
        const disponible = elemento.parentElement?.clientWidth ?? ANCHO_MAX
        google.renderButton(elemento, {
          type: 'standard',
          theme: 'outline',
          size: 'large',
          text: texto,
          shape: 'rectangular',
          logo_alignment: 'left',
          width: Math.max(ANCHO_MIN, Math.min(ANCHO_MAX, disponible)),
          locale: 'es',
        })
        setEstado('listo')
      })
      .catch(() => {
        if (!cancelado) setEstado('error')
      })
    return () => {
      cancelado = true
    }
  }, [clienteId, texto])

  if (estado === 'error') {
    return (
      <Alert tono="aviso">
        No se pudo cargar el inicio con Google. Revisa tu conexión o un bloqueador de contenido, o
        usa tu correo y contraseña.
      </Alert>
    )
  }

  return (
    <div className="flex min-h-11 justify-center">
      <div ref={contenedor} data-testid="boton-google" />
      {estado === 'cargando' && (
        <span className="self-center text-sm text-atenuado" role="status">
          Cargando Google…
        </span>
      )}
    </div>
  )
}

// Modo demostración: sin Google de verdad se pide el correo de la cuenta simulada.
export function BotonGoogleDemo({ alCredencial, deshabilitado }: PropiedadesBoton) {
  return (
    <Button
      variante="secundario"
      tamano="lg"
      className="w-full"
      disabled={deshabilitado}
      onClick={() => {
        const correo = window.prompt(
          'Modo demostración. Correo de la cuenta de Google simulada:',
          'google.demo@example.com',
        )
        if (correo?.trim()) alCredencial(armarTokenDemo(correo))
      }}
    >
      Continuar con Google (demostración)
    </Button>
  )
}

function BotonGoogleSinConfigurar() {
  return (
    <div className="space-y-1">
      <Button variante="secundario" tamano="lg" className="w-full" disabled>
        Continuar con Google
      </Button>
      <p className="text-xs text-atenuado">
        El inicio con Google no está configurado en este entorno.
      </p>
    </div>
  )
}

export function BotonGoogle(props: PropiedadesBoton) {
  if (env.useMocks) return <BotonGoogleDemo {...props} />
  if (!env.googleClientId) return <BotonGoogleSinConfigurar />
  return <BotonGoogleReal clienteId={env.googleClientId} {...props} />
}
