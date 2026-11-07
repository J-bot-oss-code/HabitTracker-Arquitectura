import { zodResolver } from '@hookform/resolvers/zod'
import { useId } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { codigoDeError, mensajeDeError } from '@/shared/api/problem'
import { useAuth } from '@/shared/auth/AuthContext'
import { etiquetaRol } from '@/shared/auth/roles'
import { Alert } from '@/shared/ui/Alert'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Input, PasswordInput } from '@/shared/ui/Field'
import { Icono } from '@/shared/ui/Icono'
import { CargandoPagina, Esqueleto } from '@/shared/ui/Spinner'
import {
  useCambiarPassword,
  useMe,
  useRecuperarPassword,
  useRestablecerPassword,
  useVinculos,
} from '../api'
import { RequisitosClave } from '../components/RequisitosClave'
import { SesionesActivas } from '../components/SesionesActivas'
import { CuentasVinculadas } from '../google/CuentasVinculadas'
import {
  cambiarPasswordSchema,
  crearPasswordSchema,
  passwordNuevaSchema,
  type CambiarPasswordForm,
  type CrearPasswordForm,
} from '../schemas'
import { cumple, estadoConfirmacion } from '../validacionEnVivo'

// Secciones de Configuración que pertenecen a Control de Acceso. El marco (título, menú de
// secciones) lo pone app/ajustes/AjustesLayout; aquí solo va el contenido de cada sección.

// Configuración › Cuenta: quién eres en el sistema.
export function AjustesCuentaPage() {
  const me = useMe()
  return (
    <div className="grid gap-6">
      <Card>
        <CardBody>
          <h2 className="text-lg font-bold">Datos de la cuenta</h2>
          <div className="mt-4">
            {me.isPending ? (
              <CargandoPagina />
            ) : me.isError ? (
              <ErrorCarga error={me.error} alReintentar={() => me.refetch()} />
            ) : (
              <dl className="grid gap-4 sm:grid-cols-2">
                <div>
                  <dt className="rotulo">Correo</dt>
                  <dd className="mt-1 break-all font-medium">{me.data.correo}</dd>
                </div>
                <div>
                  <dt className="rotulo">Rol</dt>
                  <dd className="mt-1">
                    <Badge tono="marca">{etiquetaRol[me.data.rol]}</Badge>
                  </dd>
                </div>
              </dl>
            )}
          </div>
        </CardBody>
      </Card>
    </div>
  )
}

// Configuración › Inicio de sesión: con qué se entra y desde dónde hay sesiones abiertas.
export function AjustesInicioSesionPage() {
  return (
    <div className="grid gap-6">
      <CuentasVinculadas />
      <SesionesActivas />
    </div>
  )
}

// Configuración › Contraseña y seguridad.
export function AjustesSeguridadPage() {
  // Una cuenta que solo entra con Google no tiene contraseña actual que confirmar (sin_contrasena):
  // hasta saberlo no se pinta ningún formulario, para que no salte de uno a otro.
  const vinculos = useVinculos()
  if (vinculos.isPending) return <Esqueleto className="h-72" />
  if (vinculos.isError) {
    return <ErrorCarga error={vinculos.error} alReintentar={() => vinculos.refetch()} />
  }
  return vinculos.data.tienePassword ? <CambiarContrasena /> : <CrearContrasena />
}

// Crear la primera contraseña sin salir de la sesión: se pide un código al correo de la cuenta
// (POST /recuperar-password) y con él se define la clave (POST /restablecer-password, RF-CA-11).
// /recuperar es solo para invitados (SoloInvitados), por eso el flujo vive aquí.
function CrearContrasena() {
  const me = useMe()
  const { cerrarSesion } = useAuth()
  const pedir = useRecuperarPassword()
  const crear = useRestablecerPassword()
  const correo = me.data?.correo ?? ''
  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<CrearPasswordForm>({
    resolver: zodResolver(crearPasswordSchema),
    mode: 'onTouched',
    defaultValues: { codigo: '', password: '', confirmacion: '' },
  })
  const [password, confirmacion] = useWatch({ control, name: ['password', 'confirmacion'] })
  const idRequisitos = useId()
  const confirmada = estadoConfirmacion(password, confirmacion)

  // Restablecer invalida las sesiones abiertas (RF-CA-12): se vuelve a entrar con la clave nueva.
  const enviar = handleSubmit((d) =>
    crear.mutate(
      { correo, codigo: d.codigo, nuevaPassword: d.password },
      {
        onSuccess: () =>
          cerrarSesion({ aviso: 'Contraseña creada. Inicia sesión con tu correo y la nueva.' }),
      },
    ),
  )

  return (
    <Card>
      <CardBody>
        <h2 className="text-lg font-bold">Crear una contraseña</h2>
        <p className="mt-1 text-sm text-atenuado">
          Tu cuenta entra con Google y todavía no tiene contraseña. Con una podrás entrar también
          con tu correo y, si quieres, desvincular Google.
        </p>

        {!pedir.isSuccess ? (
          <div className="mt-5 space-y-3">
            {pedir.isError && (
              <Alert tono="peligro">No se pudo enviar el código. Inténtalo de nuevo.</Alert>
            )}
            <Button
              variante="secundario"
              cargando={pedir.isPending}
              disabled={!correo}
              onClick={() => pedir.mutate(correo)}
            >
              <Icono nombre="correo" className="size-4" />
              Enviarme un código
            </Button>
            {correo && (
              <p className="text-xs text-atenuado">
                Lo enviaremos a <span className="font-medium text-texto-2">{correo}</span>.
              </p>
            )}
          </div>
        ) : (
          <>
            <Alert tono="exito" className="mt-4">
              Te enviamos un código a {correo}. Escríbelo aquí junto con tu nueva contraseña.
            </Alert>
            {crear.isError && (
              <Alert tono="peligro" className="mt-3">
                El código no es válido o ya venció. Pide uno nuevo.
              </Alert>
            )}
            <form onSubmit={enviar} noValidate className="mt-5 grid gap-4 sm:max-w-md">
              <Input
                etiqueta="Código"
                autoComplete="one-time-code"
                ayuda="Lo encuentras en el correo que te acabamos de enviar."
                error={errors.codigo?.message}
                {...register('codigo')}
              />
              <PasswordInput
                etiqueta="Nueva contraseña"
                autoComplete="new-password"
                descritoPor={idRequisitos}
                valido={cumple(passwordNuevaSchema, password)}
                error={errors.password?.message}
                {...register('password')}
              />
              <RequisitosClave id={idRequisitos} clave={password ?? ''} />
              <PasswordInput
                etiqueta="Confirmar contraseña"
                autoComplete="new-password"
                valido={confirmada.valido}
                ayuda={confirmada.ayuda}
                ayudaEnVivo
                error={errors.confirmacion?.message}
                {...register('confirmacion')}
              />
              <div className="flex flex-wrap gap-2">
                <Button type="submit" cargando={crear.isPending}>
                  Crear contraseña
                </Button>
                <Button
                  type="button"
                  variante="secundario"
                  cargando={pedir.isPending}
                  onClick={() => pedir.mutate(correo)}
                >
                  Enviar otro código
                </Button>
              </div>
            </form>
          </>
        )}
      </CardBody>
    </Card>
  )
}

function CambiarContrasena() {
  const { cerrarSesion } = useAuth()
  const cambiar = useCambiarPassword()
  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<CambiarPasswordForm>({
    resolver: zodResolver(cambiarPasswordSchema),
    mode: 'onTouched',
    defaultValues: { actual: '', password: '', confirmacion: '' },
  })
  const [actual, password, confirmacion] = useWatch({
    control,
    name: ['actual', 'password', 'confirmacion'],
  })
  const idRequisitos = useId()
  const confirmada = estadoConfirmacion(password, confirmacion)

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
        <form onSubmit={enviar} noValidate className="mt-5 grid gap-4 sm:max-w-md">
          <PasswordInput
            etiqueta="Contraseña actual"
            autoComplete="current-password"
            error={errors.actual?.message}
            {...register('actual')}
          />
          <PasswordInput
            etiqueta="Nueva contraseña"
            autoComplete="new-password"
            descritoPor={idRequisitos}
            valido={cumple(passwordNuevaSchema, password) && password !== actual}
            error={errors.password?.message}
            {...register('password')}
          />
          <RequisitosClave id={idRequisitos} clave={password ?? ''} />
          <PasswordInput
            etiqueta="Confirmar nueva contraseña"
            autoComplete="new-password"
            valido={confirmada.valido}
            ayuda={confirmada.ayuda}
            ayudaEnVivo
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
  )
}
