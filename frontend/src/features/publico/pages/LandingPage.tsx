import { useEffect } from 'react'
import { useLocation } from 'react-router'
import { BandaDatosTuyos } from '../components/BandaDatosTuyos'
import { CalculadoraCalorias } from '../components/CalculadoraCalorias'
import { CatalogoMetas } from '../components/CatalogoMetas'
import { ComoFunciona } from '../components/ComoFunciona'
import { Hero } from '../components/Hero'

// Solo estas anclas existen en la página. El hash llega de la URL, que cualquiera puede
// construir: no se decodifica ni se usa para buscar otra cosa.
const ANCLAS = new Set(['metas', 'calculadora', 'como-funciona'])

// Inicio público (§5.1): se explora sin cuenta; elegir un plan pide registro.
export function LandingPage() {
  const { hash, key } = useLocation()

  // React Router no desplaza a un ancla (/#como-funciona) por sí solo. Depende también de la
  // navegación (key) para que un segundo clic en el mismo enlace vuelva a llevar a la sección.
  useEffect(() => {
    const id = hash.slice(1)
    if (ANCLAS.has(id)) document.getElementById(id)?.scrollIntoView?.()
  }, [hash, key])

  return (
    <>
      <Hero />
      <CatalogoMetas />
      <CalculadoraCalorias />
      <ComoFunciona />
      <BandaDatosTuyos />
    </>
  )
}
