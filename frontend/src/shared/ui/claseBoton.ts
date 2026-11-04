import { cn } from '@/shared/lib/cn'

export type VarianteBoton = 'primario' | 'secundario' | 'peligro' | 'fantasma'
export type TamanoBoton = 'sm' | 'md' | 'lg'

const base =
  'inline-flex items-center justify-center gap-2 rounded-xl font-semibold transition-[background-color,border-color,color] duration-150'

const variantes: Record<VarianteBoton, string> = {
  // Deshabilitado: se ve plano y apagado (sin acento ni sombra), no solo más tenue.
  primario:
    'bg-acento text-acento-texto hover:bg-acento-fuerte disabled:border disabled:border-borde disabled:bg-superficie-2 disabled:text-atenuado',
  secundario:
    'border border-borde-fuerte bg-superficie text-texto hover:border-acento hover:bg-hover disabled:border-borde disabled:text-atenuado disabled:hover:bg-superficie',
  peligro:
    'bg-peligro text-superficie hover:brightness-90 disabled:border disabled:border-borde disabled:bg-superficie-2 disabled:text-atenuado disabled:brightness-100',
  fantasma: 'text-texto hover:bg-hover disabled:text-atenuado',
}

// Objetivo táctil de al menos 44 px en todos los tamaños (WCAG 2.2, 2.5.8).
const tamanos: Record<TamanoBoton, string> = {
  sm: 'min-h-11 px-3.5 text-sm',
  md: 'min-h-11 px-5 text-sm',
  // Acción principal de formularios públicos y móvil (48 px, §2.3).
  lg: 'min-h-12 px-6 text-base',
}

// Aspecto de botón. Lo usa <Button> y también un <Link> que navega: una navegación no debe
// ser un <button>.
export function claseBoton({
  variante = 'primario',
  tamano = 'md',
  className,
}: { variante?: VarianteBoton; tamano?: TamanoBoton; className?: string } = {}): string {
  return cn(base, variantes[variante], tamanos[tamano], className)
}
