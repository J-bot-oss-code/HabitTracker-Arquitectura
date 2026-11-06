import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router'
import { codigoDeError, mensajeDeError } from '@/shared/api/problem'
import { useAuth } from '@/shared/auth/AuthContext'
import { etiquetaRol } from '@/shared/auth/roles'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Alert } from '@/shared/ui/Alert'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody, PageHeader } from '@/shared/ui/Card'
import { claseEnlace } from '@/shared/ui/AuthCard'
import { Input } from '@/shared/ui/Field'
import { SelectorTemaTarjetas } from '@/shared/theme/SelectorTema'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useCambiarPassword, useMe, useVinculos } from '../api'
import { CuentasVinculadas } from '../google/CuentasVinculadas'
import { cambiarPasswordSchema, type CambiarPasswordForm } from '../schemas'

export function AccountPage() {
  const me = useMe()
  const { cerrarSesion } = useAuth()
  const cambiar = useCambiarPassword()
  // Una cuenta que solo entra con Google no tiene contraseña actual que confirmar (sin_contrasena).
  const vinculos = useVinculos()
  const sinContrasena = vinculos.data?.tienePassword === false

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CambiarPasswordForm>({ resolver: zodResolver(cambiarPasswordSchema) })

  // RF-CA-12: cambiar la clave invalida las sesiones previas, incluida la actual.
  const enviar = handleSubmit((d) =>
    cambiar.mutate(
      { actual: d.actual, nueva: d.password },
      {
        onSuccess: () => cerrarSesion({ aviso: 'Contraseña actualizada. Inicia sesión de nuevo.' }),
      },
    ),
  )

  return (
    <div className="mx-auto max-w-3xl">
      <PageHeader
        rotulo="Perfil"
        titulo="Mi cuenta"
        descripcion="Datos de tu sesión y seguridad."
      />

      <div className="grid gap-6">
        <Card>
          <CardBody>
            {me.isPending ? (
              <CargandoPagina />
            ) : me.isError ? (
              <ErrorCarga error={me.error} alReintentar={() => me.refetch()} />
            ) : (
              <dl className="grid gap-4 sm:grid-cols-2">
                <div>
                  <dt className="rotulo">Correo</dt>
                  <dd className="mt-1 font-medium">{me.data.correo}</dd>
                </div>
                <div>
                  <dt className="rotulo">Rol</dt>
                  <dd className="mt-1">
                    <Badge tono="marca">{etiquetaRol[me.data.rol]}</Badge>
                  </dd>
                </div>
              </dl>
            )}
          </CardBody>
        </Card>

        <Card>
          <CardBody>
            <h2 className="text-lg font-bold">Apariencia</h2>
            <p className="mb-4 mt-1 text-sm text-atenuado">
              Elige el tema de la interfaz. Se guarda en este navegador.
            </p>
            <SelectorTemaTarjetas />
          </CardBody>
        </Card>

        <CuentasVinculadas />

        {sinContrasena ? (
          <Card>
            <CardBody>
              <h2 className="text-lg font-bold">Contraseña</h2>
              <p className="mt-1 text-sm text-atenuado">
                Tu cuenta entra con Google y todavía no tiene contraseña. Puedes crear una con un
                código de recuperación que te enviamos por correo.
              </p>
              <Link to="/recuperar" className={`${claseEnlace} mt-3`}>
                Crear una contraseña
              </Link>
            </CardBody>
          </Card>
        ) : (
          <Card>
            <CardBody>
              <h2 className="text-lg font-bold">Cambiar contraseña</h2>
              <p className="mt-1 text-sm text-atenuado">
                Al cambiarla se cerrarán todas tus sesiones, incluida esta.
              </p>
              {cambiar.isError && (
                <Alert tono="peligro" className="mt-4">
                  {codigoDeError(cambiar.error) === 'clave_actual_incorrecta'
                    ? 'La contraseña actual es incorrecta.'
                    : mensajeDeError(cambiar.error)}
                </Alert>
              )}
              <form onSubmit={enviar} noValidate className="mt-4 grid gap-4 sm:max-w-md">
                <Input
                  etiqueta="Contraseña actual"
                  type="password"
                  autoComplete="current-password"
                  error={errors.actual?.message}
                  {...register('actual')}
                />
                <Input
                  etiqueta="Nueva contraseña"
                  type="password"
                  autoComplete="new-password"
                  ayuda="Mínimo 12 caracteres, con letras y números."
                  error={errors.password?.message}
                  {...register('password')}
                />
                <Input
                  etiqueta="Confirmar nueva contraseña"
                  type="password"
                  autoComplete="new-password"
                  error={errors.confirmacion?.message}
                  {...register('confirmacion')}
                />
                <div>
                  <Button type="submit" cargando={cambiar.isPending}>
                    Actualizar contraseña
                  </Button>
                </div>
              </form>
            </CardBody>
          </Card>
        )}
      </div>
    </div>
  )
}
