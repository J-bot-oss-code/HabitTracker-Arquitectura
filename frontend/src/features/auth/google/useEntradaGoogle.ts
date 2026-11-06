import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { codigoDeError } from '@/shared/api/problem'
import { useAuth } from '@/shared/auth/AuthContext'
import { authApi } from '../api'
import type { ConsentimientosRegistro, UsuarioResponseDto } from '../types'
import { mensajeGoogle } from './mensajesGoogle'

// En qué punto está la entrada con Google. El ID token se conserva solo mientras hace falta completar el paso
// (aceptar los documentos o confirmar la contraseña): vive en memoria y caduca en Google al cabo de una hora.
export type PasoGoogle =
  | { tipo: 'inactivo' }
  | { tipo: 'consentimientos'; idToken: string }
  | { tipo: 'vincular'; idToken: string }

// Orquesta POST /api/auth/google:
//  - 200/201: sesión iniciada (cuenta vinculada o creada);
//  - 400 consentimiento_requerido: cuenta nueva, hay que aceptar los documentos y repetir;
//  - 409 vinculacion_requerida: el correo ya tiene cuenta con contraseña, hay que confirmarla.
export function useEntradaGoogle(
  consentimientosIniciales?: () => ConsentimientosRegistro | undefined,
) {
  const { iniciarSesion } = useAuth()
  const [paso, setPaso] = useState<PasoGoogle>({ tipo: 'inactivo' })
  const [error, setError] = useState<string | null>(null)

  const entrar = useMutation({ mutationFn: authApi.google })
  const vincular = useMutation({ mutationFn: authApi.vincularGoogle })

  const abrirSesion = (respuesta: UsuarioResponseDto) => {
    if (respuesta.token) iniciarSesion(respuesta.token)
    setPaso({ tipo: 'inactivo' })
  }

  const intentar = async (idToken: string, consentimientos?: ConsentimientosRegistro) => {
    setError(null)
    try {
      abrirSesion(await entrar.mutateAsync({ idToken, consentimientos }))
    } catch (fallo) {
      const codigo = codigoDeError(fallo)
      if (codigo === 'consentimiento_requerido') setPaso({ tipo: 'consentimientos', idToken })
      else if (codigo === 'vinculacion_requerida') setPaso({ tipo: 'vincular', idToken })
      else {
        setPaso({ tipo: 'inactivo' })
        setError(mensajeGoogle(fallo))
      }
    }
  }

  return {
    paso,
    error,
    cargando: entrar.isPending || vincular.isPending,
    // Google entregó el ID token.
    recibirCredencial: (idToken: string) => intentar(idToken, consentimientosIniciales?.()),
    // La persona aceptó los documentos para crear la cuenta.
    aceptarDocumentos: (idToken: string, consentimientos: ConsentimientosRegistro) =>
      intentar(idToken, consentimientos),
    // La persona confirmó la contraseña de la cuenta existente.
    confirmarVinculacion: async (idToken: string, password: string) => {
      setError(null)
      try {
        abrirSesion(await vincular.mutateAsync({ idToken, password }))
      } catch (fallo) {
        setError(mensajeGoogle(fallo))
      }
    },
    cancelar: () => {
      setPaso({ tipo: 'inactivo' })
      setError(null)
    },
  }
}
