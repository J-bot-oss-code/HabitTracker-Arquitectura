import { useEffect, useRef } from 'react'
import { Outlet, ScrollRestoration, useLocation, useMatches } from 'react-router'

// Título de la pestaña de cada ruta (handle de routes.tsx).
export interface HandleRuta {
  titulo?: string
  // Línea bajo el título (la usan las secciones de Configuración).
  descripcion?: string
  // Contexto para la pestaña del navegador: «Cuenta · Configuración · HabitTracker Nexus».
  seccion?: string
}

const NOMBRE = 'HabitTracker Nexus'

// Handle de la ruta más profunda que declara título (lo usan RaizApp y AjustesLayout).
// La sección (p. ej. «Configuración») puede venir de una ruta padre.
export function useHandleRuta(): HandleRuta | undefined {
  const handles = useMatches()
    .map((m) => m.handle as HandleRuta | undefined)
    .reverse()
  const propio = handles.find((h) => h?.titulo !== undefined)
  const seccion = handles.find((h) => h?.seccion !== undefined)?.seccion
  return propio || seccion ? { ...propio, seccion } : undefined
}

// Raíz de todas las rutas. Tras cada navegación:
// - el desplazamiento vuelve arriba, o al ancla si la URL la trae (ScrollRestoration);
// - la pestaña dice en qué página se está (WCAG 2.4.2);
// - el foco pasa al título de la página, para que un lector de pantalla anuncie el cambio
//   (WCAG 2.4.3). En la carga inicial no se mueve: el usuario aún no hizo nada.
export function RaizApp() {
  const handle = useHandleRuta()
  const { key, hash } = useLocation()
  const primeraCarga = useRef(true)

  const titulo = handle?.titulo

  useEffect(() => {
    document.title = [titulo, handle?.seccion, NOMBRE].filter(Boolean).join(' · ')
  }, [titulo, handle?.seccion])

  useEffect(() => {
    if (primeraCarga.current) {
      primeraCarga.current = false
      return
    }
    // Con ancla, el destino es la sección: el foco no debe devolver la página arriba.
    if (hash) return
    const destino =
      document.querySelector<HTMLElement>('main h1') ?? document.getElementById('contenido')
    if (!destino) return
    if (!destino.hasAttribute('tabindex')) destino.setAttribute('tabindex', '-1')
    destino.focus({ preventScroll: true })
  }, [key, hash])

  return (
    <>
      <ScrollRestoration />
      <Outlet />
    </>
  )
}
