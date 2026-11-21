// El texto lo escribe cualquier persona: se pinta SIEMPRE como texto de React (escapado), sin enlaces automáticos ni HTML.
// Los saltos de línea se respetan y una palabra larguísima se parte en vez de desbordar la pantalla (360 px).
export function TextoSugerencia({ texto }: { texto: string }) {
  return <p className="mt-2 whitespace-pre-wrap break-words text-sm text-texto">{texto}</p>
}
