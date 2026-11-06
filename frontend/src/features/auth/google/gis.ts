// Google Identity Services (GIS): el navegador obtiene un ID token y la API lo valida en el servidor
// (POST /api/auth/google). No hay secreto de cliente ni redirecciones.

export interface OpcionesBotonGoogle {
  type: 'standard'
  theme: 'outline' | 'filled_blue' | 'filled_black'
  size: 'large' | 'medium' | 'small'
  text: 'signin_with' | 'signup_with' | 'continue_with'
  shape: 'rectangular' | 'pill'
  logo_alignment: 'left' | 'center'
  width: number
  locale: string
}

export interface GoogleId {
  initialize: (config: {
    client_id: string
    callback: (respuesta: { credential: string }) => void
    ux_mode?: 'popup' | 'redirect'
    auto_select?: boolean
  }) => void
  renderButton: (elemento: HTMLElement, opciones: OpcionesBotonGoogle) => void
}

declare global {
  interface Window {
    google?: { accounts?: { id?: GoogleId } }
  }
}

export const URL_GIS = 'https://accounts.google.com/gsi/client'

let carga: Promise<GoogleId> | null = null

function disponible(): GoogleId | undefined {
  return window.google?.accounts?.id
}

// Descarga el script de Google una sola vez. Si falla (sin red, bloqueador de anuncios), la siguiente
// llamada vuelve a intentarlo.
export function cargarGoogle(): Promise<GoogleId> {
  const ya = disponible()
  if (ya) return Promise.resolve(ya)
  carga ??= new Promise<GoogleId>((resolver, rechazar) => {
    const script = document.createElement('script')
    script.src = URL_GIS
    script.async = true
    script.defer = true
    script.onload = () => {
      const id = disponible()
      if (id) resolver(id)
      else {
        carga = null
        rechazar(new Error('Google no quedó disponible.'))
      }
    }
    script.onerror = () => {
      carga = null
      script.remove()
      rechazar(new Error('No se pudo cargar Google.'))
    }
    document.head.appendChild(script)
  })
  return carga
}

// Solo para pruebas: olvida una carga anterior.
export function reiniciarCargaGoogle() {
  carga = null
}
