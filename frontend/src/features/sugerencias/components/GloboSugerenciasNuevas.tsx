import { useContadorSugerenciasNuevas } from '../api'

// Conteo de mensajes nuevos junto al ítem «Quejas y mejoras» del menú del Administrador. El número se ve y, para el
// lector de pantalla, se dice con palabras. Sin nuevas (o si la consulta falla) no se pinta nada.
export function GloboSugerenciasNuevas() {
  const { data } = useContadorSugerenciasNuevas()
  if (!data) return null
  return (
    <>
      <span
        aria-hidden="true"
        className="absolute right-1.5 top-1 grid h-5 min-w-5 place-items-center rounded-md bg-acento px-1 text-xs font-bold leading-none text-acento-texto lg:static"
      >
        {data > 99 ? '99+' : data}
      </span>
      <span className="sr-only">
        ({data} {data === 1 ? 'nueva' : 'nuevas'})
      </span>
    </>
  )
}
