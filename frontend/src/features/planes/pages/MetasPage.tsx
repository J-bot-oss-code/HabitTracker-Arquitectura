import { CatalogoPlanes } from '../components/CatalogoPlanes'

// Catálogo público como página propia (§4: /metas), con el título de la sección como H1.
export function MetasPage() {
  return (
    <CatalogoPlanes
      base="/metas"
      anclaId="metas"
      nivelTitulo={1}
      titulo="Elige una meta"
      descripcion="Explora los planes libremente. Para empezar uno, crea tu cuenta."
    />
  )
}
