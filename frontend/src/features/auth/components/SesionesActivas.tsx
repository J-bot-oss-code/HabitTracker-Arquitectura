import { Link } from 'react-router'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { mensajeDeError } from '@/shared/api/problem'
import { useAuth } from '@/shared/auth/AuthContext'
import { formatearFechaHora } from '@/shared/lib/fechas'
import { Alert } from '@/shared/ui/Alert'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody } from '@/shared/ui/Card'
import { ConfirmarDialogo } from '@/shared/ui/ConfirmarDialogo'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Esqueleto } from '@/shared/ui/Spinner'
import { authApi, authKeys, useSesiones } from '../api'

// «Sesiones activas» de Mi cuenta: los dispositivos donde la cuenta tiene la sesión abierta. Cerrar una
// revoca su refresh token; «Cerrar las demás» cierra todas menos esta; «Cerrar en todos los dispositivos»
// corta además los accesos ya emitidos, incluido el de este navegador.
export function SesionesActivas() {
  const sesiones = useSesiones()
  const queryClient = useQueryClient()
  const { cerrarSesion } = useAuth()
  const [confirmandoTodas, setConfirmandoTodas] = useState(false)
  const [aviso, setAviso] = useState<string | null>(null)

  const refrescar = () => queryClient.invalidateQueries({ queryKey: authKeys.sesiones })

  const cerrarUna = useMutation({
    mutationFn: authApi.cerrarSesionRemota,
    onSuccess: () => setAviso('Se cerró esa sesión.'),
    // También tras un error: si la sesión ya se había cerrado desde otro lado, la fila no debe seguir ahí.
    onSettled: refrescar,
  })

  // No hay un endpoint «cerrar las demás»: se cierra cada una de las otras (cada una es solo de esta persona).
  const cerrarLasDemas = useMutation({
    mutationFn: async (ids: string[]) => {
      await Promise.all(ids.map((id) => authApi.cerrarSesionRemota(id)))
    },
    onSuccess: () => setAviso('Se cerraron las demás sesiones.'),
    onSettled: refrescar,
  })

  const cerrarTodas = useMutation({
    mutationFn: authApi.cerrarTodasLasSesiones,
    // Cortó también el acceso de este navegador: hay que volver a iniciar sesión.
    onSuccess: () =>
      cerrarSesion({ voluntario: true, aviso: 'Cerraste la sesión en todos los dispositivos.' }),
  })

  const otras = sesiones.data?.filter((s) => !s.actual) ?? []
  const error = cerrarUna.error ?? cerrarLasDemas.error

  return (
    <Card>
      <CardBody>
        <h2 className="text-lg font-bold">Sesiones activas</h2>
        <p className="mt-1 text-sm text-atenuado">
          Los dispositivos donde tu cuenta tiene la sesión abierta. Si no reconoces alguno, ciérralo
          y{' '}
          <Link to="/ajustes/seguridad" className="font-semibold text-acento underline">
            cambia tu contraseña
          </Link>
          .
        </p>

        {sesiones.isPending ? (
          <Esqueleto className="mt-4 h-24" />
        ) : sesiones.isError ? (
          <ErrorCarga
            error={sesiones.error}
            porDefecto="No se pudieron cargar tus sesiones."
            alReintentar={() => sesiones.refetch()}
          />
        ) : (
          <div className="mt-4 space-y-4">
            {aviso && <Alert tono="exito">{aviso}</Alert>}
            {error && (
              <Alert tono="peligro">{mensajeDeError(error, 'No se pudo cerrar la sesión.')}</Alert>
            )}

            <ul className="divide-y divide-borde rounded-xl border border-borde">
              {sesiones.data.map((s) => (
                <li key={s.id} className="flex flex-wrap items-center gap-3 p-3">
                  <div className="min-w-0 flex-1">
                    <p className="flex flex-wrap items-center gap-2 font-medium">
                      {/* El dispositivo es texto que mandó el navegador: React lo muestra como texto. */}
                      <span className="break-words">{s.dispositivo}</span>
                      {s.actual && <Badge tono="marca">Este dispositivo</Badge>}
                    </p>
                    <p className="text-sm text-atenuado">
                      Inicio: {formatearFechaHora(s.creadaEn)} · Último uso:{' '}
                      {formatearFechaHora(s.ultimoUsoEn)}
                    </p>
                  </div>
                  {!s.actual && (
                    <Button
                      variante="secundario"
                      aria-label={`Cerrar la sesión de ${s.dispositivo}`}
                      cargando={cerrarUna.isPending && cerrarUna.variables === s.id}
                      disabled={cerrarLasDemas.isPending || cerrarUna.isPending}
                      onClick={() => {
                        setAviso(null)
                        cerrarUna.mutate(s.id)
                      }}
                    >
                      Cerrar
                    </Button>
                  )}
                </li>
              ))}
            </ul>

            <div className="flex flex-wrap gap-3">
              {otras.length > 0 && (
                <Button
                  variante="secundario"
                  cargando={cerrarLasDemas.isPending}
                  disabled={cerrarUna.isPending}
                  onClick={() => {
                    setAviso(null)
                    cerrarLasDemas.mutate(otras.map((s) => s.id))
                  }}
                >
                  Cerrar las demás
                </Button>
              )}
              <Button variante="peligro" onClick={() => setConfirmandoTodas(true)}>
                Cerrar en todos los dispositivos
              </Button>
            </div>
          </div>
        )}

        <ConfirmarDialogo
          abierto={confirmandoTodas}
          titulo="¿Cerrar la sesión en todos los dispositivos?"
          mensaje="Se cerrarán todas tus sesiones, también la de este navegador, y tendrás que iniciar sesión de nuevo."
          textoConfirmar="Cerrar en todos"
          peligroso
          cargando={cerrarTodas.isPending}
          error={
            cerrarTodas.isError ? (
              <Alert tono="peligro">{mensajeDeError(cerrarTodas.error)}</Alert>
            ) : undefined
          }
          alConfirmar={() => cerrarTodas.mutate()}
          alCerrar={() => {
            setConfirmandoTodas(false)
            cerrarTodas.reset()
          }}
        />
      </CardBody>
    </Card>
  )
}
