import { useRef, useState } from 'react'
import { Link, useLocation } from 'react-router'
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
// Cada aviso se puede cerrar con la X: queda descartado en esta pestaña (sessionStorage) y vuelve a
// salir en la próxima sesión del navegador, porque el servidor lo sigue pidiendo. El de la baja lleva
// la fecha en la clave: si se programa otra, se avisa de nuevo. Al cerrar sesión, AuthContext borra
// estas claves (habittracker.*) para que otra cuenta del mismo navegador no herede lo descartado.
const CLAVE = 'habittracker.avisos-descartados'

function leerDescartados(): string[] {
  try {
    const valor: unknown = JSON.parse(sessionStorage.getItem(CLAVE) ?? '[]')
    return Array.isArray(valor) ? valor.filter((v): v is string => typeof v === 'string') : []
  } catch {
    return []
  }
}

function useDescartados() {
  const [descartados, setDescartados] = useState(leerDescartados)
  const descartar = (id: string) => {
    const nuevos = [...new Set([...descartados, id])]
    setDescartados(nuevos)
    try {
      sessionStorage.setItem(CLAVE, JSON.stringify(nuevos))
    } catch {
      // Sin almacenamiento (modo privado estricto): se descarta solo hasta recargar.
    }
  }
  return { descartado: (id: string) => descartados.includes(id), descartar }
}

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
  const { descartado, descartar: guardarDescarte } = useDescartados()
  const { pathname } = useLocation()
  const contenedor = useRef<HTMLDivElement>(null)

  // La X desaparece con su aviso: el foco pasa a la X del siguiente o, si no queda ninguno, al
  // título de la página (WCAG 2.4.3), en lugar de caer en <body>.
  const descartar = (id: string) => {
    const otra = Array.from(
      contenedor.current?.querySelectorAll<HTMLElement>('[data-cerrar-aviso]') ?? [],
    ).find((b) => b !== document.activeElement)
    guardarDescarte(id)
    requestAnimationFrame(() => {
      const destino =
        (otra?.isConnected ? otra : null) ??
        document.querySelector<HTMLElement>('main h1') ??
        document.getElementById('contenido')
      if (!destino) return
      if (destino.tagName === 'H1' && !destino.hasAttribute('tabindex')) {
        destino.setAttribute('tabindex', '-1')
      }
      destino.focus({ preventScroll: true })
    })
  }

  const pideDocumentos = me.data?.requiereAceptarDocumentos === true && !descartado('documentos')
  const idBaja = baja.data ? `baja:${baja.data}` : null
  const borradoEn = idBaja && !descartado(idBaja) ? baja.data : null
  if (!pideDocumentos && !borradoEn) return null

  return (
    <div ref={contenedor} className="mb-6 space-y-3" data-no-imprimir>
      {pideDocumentos && (
        <Alert
          tono="info"
          titulo="Actualizamos nuestros documentos"
          alCerrar={() => descartar('documentos')}
        >
          Revisa y acepta los Términos y la Política de privacidad vigentes.{' '}
          {/* En la propia página de privacidad el enlace llevaría a donde ya se está. */}
          {pathname === '/ajustes/privacidad' ? (
            'Los encuentras abajo, en Consentimientos.'
          ) : (
            <Link to="/ajustes/privacidad" className="font-semibold text-acento underline">
              Ir a Privacidad y datos
            </Link>
          )}
        </Alert>
      )}
      {borradoEn && (
        <Alert
          tono="aviso"
          titulo="Tu cuenta se eliminará"
          alCerrar={() => {
            if (!idBaja) return
            descartar(idBaja)
            notificar(
              'Lo verás de nuevo al volver a entrar. Puedes cancelar la eliminación en Configuración › Privacidad y datos.',
            )
          }}
        >
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
