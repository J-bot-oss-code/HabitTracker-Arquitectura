import { useQueryClient } from '@tanstack/react-query'
import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { alRecibirNoAutorizado } from '@/shared/api/http'
import { estaVencido, leerToken, type UsuarioSesion } from './jwt'
import type { Rol } from './roles'
import { tokenStorage } from './tokenStorage'

interface OpcionesCierre {
  // Motivo que verá el usuario en el login (sesión vencida, revocada, clave cambiada).
  aviso?: string
  // Cierre pedido por el usuario: al volver a entrar no se regresa a la ruta anterior.
  voluntario?: boolean
}

interface AuthContextValue {
  usuario: UsuarioSesion | null
  avisoCierre: string | null
  cierreVoluntario: boolean
  iniciarSesion: (token: string) => UsuarioSesion | null
  cerrarSesion: (opciones?: OpcionesCierre) => void
  tieneRol: (...roles: Rol[]) => boolean
}

const AVISO_SESION_EXPIRADA = 'Tu sesión expiró o fue cerrada. Inicia sesión de nuevo.'

const AuthContext = createContext<AuthContextValue | null>(null)

function sesionGuardada(): UsuarioSesion | null {
  const token = tokenStorage.leer()
  if (!token) return null
  const usuario = leerToken(token)
  if (!usuario || estaVencido(usuario)) {
    tokenStorage.borrar()
    return null
  }
  return usuario
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [usuario, setUsuario] = useState<UsuarioSesion | null>(sesionGuardada)
  const [cierre, setCierre] = useState<OpcionesCierre>({})

  // Al quedar sin usuario, RequireAuth lleva al login: no hace falta navegar desde aquí.
  const cerrarSesion = useCallback(
    (opciones: OpcionesCierre = {}) => {
      tokenStorage.borrar()
      queryClient.clear()
      setCierre(opciones)
      setUsuario(null)
    },
    [queryClient],
  )

  const iniciarSesion = useCallback((token: string) => {
    const leido = leerToken(token)
    if (!leido || estaVencido(leido)) return null
    tokenStorage.guardar(token)
    setCierre({})
    setUsuario(leido)
    return leido
  }, [])

  // Cualquier 401 con token (vencido, revocado o en blacklist) cierra la sesión.
  useEffect(
    () => alRecibirNoAutorizado(() => cerrarSesion({ aviso: AVISO_SESION_EXPIRADA })),
    [cerrarSesion],
  )

  // El token dura 60 min y no hay refresh: al vencer se cierra la sesión.
  useEffect(() => {
    if (!usuario) return
    const restante = usuario.expiraEn - Date.now()
    const temporizador = window.setTimeout(
      () => cerrarSesion({ aviso: AVISO_SESION_EXPIRADA }),
      Math.max(restante, 0),
    )
    return () => window.clearTimeout(temporizador)
  }, [usuario, cerrarSesion])

  const valor = useMemo<AuthContextValue>(
    () => ({
      usuario,
      avisoCierre: cierre.aviso ?? null,
      cierreVoluntario: cierre.voluntario ?? false,
      iniciarSesion,
      cerrarSesion,
      tieneRol: (...roles) => usuario !== null && roles.includes(usuario.rol),
    }),
    [usuario, cierre, iniciarSesion, cerrarSesion],
  )

  return <AuthContext.Provider value={valor}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const contexto = useContext(AuthContext)
  if (!contexto) throw new Error('useAuth debe usarse dentro de AuthProvider')
  return contexto
}
