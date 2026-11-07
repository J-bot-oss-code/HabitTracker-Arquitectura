import { Link } from 'react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useAuth } from '@/shared/auth/AuthContext'
import { formatearFecha } from '@/shared/lib/fechas'
import { Alert } from '@/shared/ui/Alert'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody } from '@/shared/ui/Card'
import { ConfirmarDialogo } from '@/shared/ui/ConfirmarDialogo'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { authApi, authKeys, useVinculos } from '../api'
import { BotonGoogle } from './BotonGoogle'
import { FormularioClave } from './EntradaGoogle'
import { mensajeGoogle } from './mensajesGoogle'

// «Cuentas vinculadas» de Mi cuenta: ver, vincular y desvincular Google. El backend impide desvincular Google
// si es la única forma de entrar (la cuenta no tiene contraseña).
export function CuentasVinculadas() {
  const vinculos = useVinculos()
  const queryClient = useQueryClient()
  const { iniciarSesion } = useAuth()
  const [idToken, setIdToken] = useState<string | null>(null)
  const [confirmando, setConfirmando] = useState(false)
  const [aviso, setAviso] = useState<string | null>(null)

  const refrescar = () => queryClient.invalidateQueries({ queryKey: authKeys.vinculos })

  const desvincular = useMutation({
    mutationFn: authApi.desvincularGoogle,
    onSuccess: () => {
      setConfirmando(false)
      setAviso('Google se desvinculó de tu cuenta.')
      return refrescar()
    },
  })

  const vincular = useMutation({
    mutationFn: authApi.vincularGoogle,
    onSuccess: (respuesta) => {
      // El backend abre una sesión nueva para la misma persona al confirmar la contraseña.
      if (respuesta.token) iniciarSesion(respuesta.token)
      setIdToken(null)
      setAviso('Google quedó vinculado a tu cuenta.')
      return refrescar()
    },
  })

  return (
    <Card>
      <CardBody>
        <h2 className="text-lg font-bold">Cuentas vinculadas</h2>
        <p className="mt-1 text-sm text-atenuado">Formas de entrar a tu cuenta.</p>

        {vinculos.isPending ? (
          <CargandoPagina />
        ) : vinculos.isError ? (
          <ErrorCarga error={vinculos.error} alReintentar={() => vinculos.refetch()} />
        ) : (
          <div className="mt-4 space-y-4">
            {aviso && <Alert tono="exito">{aviso}</Alert>}

            <dl className="grid gap-4 sm:grid-cols-2">
              <div>
                <dt className="rotulo">Contraseña</dt>
                <dd className="mt-1">
                  <Badge tono={vinculos.data.tienePassword ? 'exito' : 'aviso'}>
                    {vinculos.data.tienePassword ? 'Configurada' : 'Sin contraseña'}
                  </Badge>
                </dd>
              </div>
              <div>
                <dt className="rotulo">Google</dt>
                <dd className="mt-1">
                  {vinculos.data.vinculos[0] ? (
                    <Badge tono="exito">
                      Vinculada el {formatearFecha(vinculos.data.vinculos[0].vinculadoEn)}
                    </Badge>
                  ) : (
                    <Badge>No vinculada</Badge>
                  )}
                </dd>
              </div>
            </dl>

            {vinculos.data.vinculos[0] ? (
              <div className="space-y-2">
                <Button
                  variante="secundario"
                  disabled={!vinculos.data.tienePassword}
                  onClick={() => {
                    setAviso(null)
                    setConfirmando(true)
                  }}
                >
                  Desvincular Google
                </Button>
                {!vinculos.data.tienePassword && (
                  <p className="text-sm text-atenuado">
                    Google es tu única forma de entrar. Para desvincularla,{' '}
                    <Link to="/ajustes/seguridad" className="font-semibold text-acento underline">
                      crea primero una contraseña
                    </Link>
                    .
                  </p>
                )}
              </div>
            ) : idToken ? (
              <FormularioClave
                titulo="Confirma tu contraseña para vincular esa cuenta de Google. Debe usar el mismo correo que tu cuenta."
                textoBoton="Vincular Google"
                cargando={vincular.isPending}
                alEnviar={(password) => vincular.mutate({ idToken, password })}
                alCancelar={() => {
                  setIdToken(null)
                  vincular.reset()
                }}
              />
            ) : (
              <div className="max-w-sm space-y-2">
                <p className="text-sm text-texto-2">
                  Vincula tu cuenta de Google para entrar con un clic.
                </p>
                <BotonGoogle
                  texto="continue_with"
                  alCredencial={(token) => {
                    setAviso(null)
                    setIdToken(token)
                  }}
                />
              </div>
            )}

            {vincular.isError && <Alert tono="peligro">{mensajeGoogle(vincular.error)}</Alert>}
          </div>
        )}

        <ConfirmarDialogo
          abierto={confirmando}
          titulo="¿Desvincular Google?"
          mensaje="Dejarás de poder entrar con Google. Seguirás entrando con tu correo y tu contraseña."
          textoConfirmar="Desvincular"
          cargando={desvincular.isPending}
          error={
            desvincular.isError ? (
              <Alert tono="peligro">{mensajeGoogle(desvincular.error)}</Alert>
            ) : undefined
          }
          alConfirmar={() => desvincular.mutate()}
          alCerrar={() => {
            setConfirmando(false)
            desvincular.reset()
          }}
        />
      </CardBody>
    </Card>
  )
}
