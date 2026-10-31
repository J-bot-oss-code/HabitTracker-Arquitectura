import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  useSyncExternalStore,
} from 'react'
import type { ReactNode } from 'react'
import {
  aplicarTema,
  guardarTema,
  leerTemaGuardado,
  prefiereOscuro,
  type Tema,
  type TemaAplicado,
} from './temas'

interface ThemeContextValue {
  tema: Tema
  temaAplicado: TemaAplicado
  cambiarTema: (tema: Tema) => void
}

const ThemeContext = createContext<ThemeContextValue | null>(null)

// Suscripción al modo del dispositivo: con "Sistema" el tema sigue el cambio en vivo.
function suscribirModoSistema(avisar: () => void) {
  if (typeof window.matchMedia !== 'function') return () => {}
  const consulta = window.matchMedia('(prefers-color-scheme: dark)')
  consulta.addEventListener('change', avisar)
  return () => consulta.removeEventListener('change', avisar)
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [tema, setTema] = useState<Tema>(leerTemaGuardado)
  const sistemaOscuro = useSyncExternalStore(suscribirModoSistema, prefiereOscuro, () => false)
  const temaAplicado: TemaAplicado =
    tema === 'sistema' ? (sistemaOscuro ? 'oscuro' : 'claro') : tema

  useEffect(() => {
    aplicarTema(tema)
  }, [tema, sistemaOscuro])

  const cambiarTema = useCallback((nuevo: Tema) => {
    guardarTema(nuevo)
    setTema(nuevo)
  }, [])

  const valor = useMemo(
    () => ({ tema, temaAplicado, cambiarTema }),
    [tema, temaAplicado, cambiarTema],
  )

  return <ThemeContext.Provider value={valor}>{children}</ThemeContext.Provider>
}

export function useTema(): ThemeContextValue {
  const contexto = useContext(ThemeContext)
  if (!contexto) throw new Error('useTema debe usarse dentro de ThemeProvider')
  return contexto
}
