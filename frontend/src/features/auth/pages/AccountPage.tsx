import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { esErrorHttp, mensajeDeError } from '@/shared/api/problem'
import { useAuth } from '@/shared/auth/AuthContext'
import { etiquetaRol } from '@/shared/auth/roles'
import { Alert } from '@/shared/ui/Alert'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody, PageHeader } from '@/shared/ui/Card'
import { Input } from '@/shared/ui/Field'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useCambiarPassword, useMe } from '../api'
import { cambiarPasswordSchema, type CambiarPasswordForm } from '../schemas'

export function AccountPage() {
  const me = useMe()
  const { cerrarSesion } = useAuth()
  const cambiar = useCambiarPassword()

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
      <PageHeader titulo="Mi cuenta" descripcion="Datos de tu sesión y seguridad." />

      <div className="grid gap-6">
        <Card>
          <CardBody>
            {me.isPending ? (
              <CargandoPagina />
            ) : me.isError ? (
              <Alert tono="peligro">{mensajeDeError(me.error)}</Alert>
            ) : (
              <dl className="grid gap-4 sm:grid-cols-2">
                <div>
                  <dt className="text-xs uppercase tracking-wide text-slate-500">Correo</dt>
                  <dd className="mt-1 font-medium">{me.data.correo}</dd>
                </div>
                <div>
                  <dt className="text-xs uppercase tracking-wide text-slate-500">Rol</dt>
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
            <h2 className="text-lg font-semibold">Cambiar contraseña</h2>
            <p className="mt-1 text-sm text-slate-500">
              Al cambiarla se cerrarán todas tus sesiones, incluida esta.
            </p>
            {cambiar.isError && (
              <Alert tono="peligro" className="mt-4">
                {esErrorHttp(cambiar.error, 401)
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
                ayuda="Mínimo 8 caracteres, con letras y números."
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
      </div>
    </div>
  )
}
