// Temas de la interfaz. Los colores viven en tokens CSS (src/index.css) y se activan con
// data-theme en <html>; aquí solo está el catálogo y la lógica de resolución.
export const TEMAS = ['sistema', 'claro', 'oscuro', 'calido', 'estadio'] as const
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
    muestra: ['#f3f5fb', '#0b0f17', '#2f4fe6'],
  },
  {
    clave: 'claro',
    nombre: 'Claro',
    descripcion: 'Limpio y luminoso, con acento eléctrico.',
    muestra: ['#f3f5fb', '#ffffff', '#2f4fe6'],
  },
  {
    clave: 'oscuro',
    nombre: 'Oscuro',
    descripcion: 'Grafito con acento lima, para entrenar de noche.',
    muestra: ['#0b0f17', '#121826', '#c6ff3d'],
  },
  {
    clave: 'calido',
    nombre: 'Cálido',
    descripcion: 'Tonos arena y ámbar, menos cansancio visual.',
    muestra: ['#f5ecdc', '#fffaf0', '#b53a0b'],
  },
  {
    clave: 'estadio',
    nombre: 'Estadio',
    descripcion: 'Negro profundo con neón y alto contraste.',
    muestra: ['#04050a', '#0b0e18', '#00e5ff'],
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
