// Temas de la interfaz. Los colores viven en tokens CSS (src/index.css) y se activan con
// data-theme en <html>; aquí solo está el catálogo y la lógica de resolución.
// Un valor guardado de un tema retirado (p. ej. "calido") deja de ser válido y se usa el
// predeterminado (ver leerTemaGuardado).
export const TEMAS = ['sistema', 'claro', 'oscuro'] as const
export type Tema = (typeof TEMAS)[number]
export type TemaAplicado = Exclude<Tema, 'sistema'>

export const TEMA_POR_DEFECTO: Tema = 'sistema'
export const CLAVE_TEMA = 'habittracker.tema'

export interface InfoTema {
  clave: Tema
  nombre: string
  descripcion: string
  // Muestra de color para el selector: fondo, superficie y acento.
  muestra: [string, string, string]
}

export const INFO_TEMAS: InfoTema[] = [
  {
    clave: 'sistema',
    nombre: 'Sistema',
    descripcion: 'Sigue el modo de tu dispositivo.',
    muestra: ['#f7f8fb', '#0b1120', '#4f46e5'],
  },
  {
    clave: 'claro',
    nombre: 'Claro',
    descripcion: 'Gris claro con tarjetas blancas y acento índigo.',
    muestra: ['#f7f8fb', '#ffffff', '#4f46e5'],
  },
  {
    clave: 'oscuro',
    nombre: 'Oscuro',
    descripcion: 'Pizarra con acento índigo, para la noche.',
    muestra: ['#0b1120', '#131c2e', '#818cf8'],
  },
]

export function esTema(valor: unknown): valor is Tema {
  return typeof valor === 'string' && (TEMAS as readonly string[]).includes(valor)
}

export function prefiereOscuro(): boolean {
  try {
    return window.matchMedia('(prefers-color-scheme: dark)').matches
  } catch {
    return false
  }
}

export function resolverTema(tema: Tema): TemaAplicado {
  if (tema === 'sistema') return prefiereOscuro() ? 'oscuro' : 'claro'
  return tema
}

export function leerTemaGuardado(): Tema {
  try {
    const guardado = localStorage.getItem(CLAVE_TEMA)
    return esTema(guardado) ? guardado : TEMA_POR_DEFECTO
  } catch {
    return TEMA_POR_DEFECTO
  }
}

export function guardarTema(tema: Tema) {
  try {
    localStorage.setItem(CLAVE_TEMA, tema)
  } catch {
    // Almacenamiento bloqueado: el tema vale solo para esta sesión.
  }
}

export function aplicarTema(tema: Tema, raiz: HTMLElement = document.documentElement) {
  raiz.dataset.theme = resolverTema(tema)
  raiz.dataset.temaElegido = tema
}
