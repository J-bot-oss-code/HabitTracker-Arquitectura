import { useQueryClient } from '@tanstack/react-query'
import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { alRecibirNoAutorizado } from '@/shared/api/http'
import { alRenovarToken, renovarToken } from '@/shared/api/sesion'
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
  // Mientras se intenta restaurar la sesión al cargar la página (cookie de refresco) todavía no se sabe
  // si hay usuario: las rutas protegidas esperan en lugar de redirigir al login.
  iniciando: boolean
  avisoCierre: string | null
  cierreVoluntario: boolean
  iniciarSesion: (token: string) => UsuarioSesion | null
  cerrarSesion: (opciones?: OpcionesCierre) => void
  tieneRol: (...roles: Rol[]) => boolean
}

const AVISO_SESION_EXPIRADA = 'Tu sesión expiró o fue cerrada. Inicia sesión de nuevo.'

// El token de acceso se renueva con este margen antes de vencer, para que ninguna petición lo use vencido.
const MARGEN_RENOVACION_MS = 60_000

const AuthContext = createContext<AuthContextValue | null>(null)

function sesionEnMemoria(): UsuarioSesion | null {
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
  const [usuario, setUsuario] = useState<UsuarioSesion | null>(sesionEnMemoria)
  const [cierre, setCierre] = useState<OpcionesCierre>({})
  // Sin token en memoria pero con una sesión previa en este navegador, se intenta restaurarla con la cookie.
  const [iniciando, setIniciando] = useState(() => !sesionEnMemoria() && tokenStorage.huboSesion())

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

  // Al cargar la página: la cookie HttpOnly del refresh token restaura la sesión sin pedir la contraseña.
  useEffect(() => {
    if (!iniciando) return
    let activo = true
    void renovarToken().then((token) => {
      if (!activo) return
      const leido = token ? leerToken(token) : null
      if (leido) setUsuario(leido)
      else tokenStorage.borrar()
      setIniciando(false)
    })
    return () => {
      activo = false
    }
  }, [iniciando])

  // Cada renovación (por el temporizador o por una petición con el token vencido) actualiza al usuario.
  useEffect(
    () =>
      alRenovarToken((token) => {
        const leido = leerToken(token)
        if (leido) setUsuario(leido)
      }),
    [],
  )

  // El token de acceso dura 15 min: se renueva con la cookie un poco antes de vencer. Si la renovación
  // falla (sesión revocada o cookie vencida), se cierra la sesión.
  useEffect(() => {
    if (!usuario) return
    const restante = usuario.expiraEn - Date.now() - MARGEN_RENOVACION_MS
    const temporizador = window.setTimeout(
      () => {
        void renovarToken().then((token) => {
          if (!token) cerrarSesion({ aviso: AVISO_SESION_EXPIRADA })
        })
      },
      Math.max(restante, 0),
    )
    return () => window.clearTimeout(temporizador)
  }, [usuario, cerrarSesion])

  const valor = useMemo<AuthContextValue>(
    () => ({
      usuario,
      iniciando,
      avisoCierre: cierre.aviso ?? null,
      cierreVoluntario: cierre.voluntario ?? false,
      iniciarSesion,
      cerrarSesion,
      tieneRol: (...roles) => usuario !== null && roles.includes(usuario.rol),
    }),
    [usuario, iniciando, cierre, iniciarSesion, cerrarSesion],
  )

  return <AuthContext.Provider value={valor}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const contexto = useContext(AuthContext)
  if (!contexto) throw new Error('useAuth debe usarse dentro de AuthProvider')
  return contexto
}
