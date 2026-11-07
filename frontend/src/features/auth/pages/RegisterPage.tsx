import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useId, useRef } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { Link, useLocation, useSearchParams } from 'react-router'
import { z } from 'zod'
import { consentimientoSchema, guardarRegistroPendiente } from '@/features/perfil/api'
import {
  buscarMeta,
  validarDatosCorporales,
  RUTAS_LEGALES,
  VERSION_PRIVACIDAD,
  VERSION_TERMINOS,
} from '@/features/publico/api'
import { esErrorHttp, mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { claseBoton } from '@/shared/ui/claseBoton'
import { ConsentFieldset } from '@/shared/ui/ConsentFieldset'
import { Input, PasswordInput } from '@/shared/ui/Field'
import { useRegistro } from '../api'
import { AuthCard, claseEnlace } from '@/shared/ui/AuthCard'
import { EntradaGoogle } from '../google/EntradaGoogle'
import { RequisitosClave } from '../components/RequisitosClave'
import { correoSchema, nombreCompletoSchema, passwordNuevaSchema, registroSchema } from '../schemas'
import { cumple, estadoConfirmacion } from '../validacionEnVivo'

// Datos de la cuenta (límites del backend) + consentimientos obligatorios antes de crearla.
const esquema = z.intersection(registroSchema, consentimientoSchema)
type Formulario = z.infer<typeof esquema>

// Registro (§5.2): la meta elegida viaja en ?meta= y se conserva hasta el onboarding. El backend exige
// las versiones vigentes de los documentos legales EN el registro (`consentimientos`): sin ellas no crea
// la cuenta. Lo demás (meta, datos corporales) se recuerda en el dispositivo hasta el onboarding
// (ver perfil/registroPendiente.ts).
// RF-CA-01: el correo es único, pero la respuesta es genérica y no revela si existe. Se
// orienta al usuario sin confirmar ni negar nada.
function mensajeRegistro(error: unknown): string {
  if (esErrorHttp(error, 400)) {
    return 'No pudimos crear la cuenta con estos datos. Si ya tienes una con este correo,'
  }
  return mensajeDeError(error, 'No se pudo completar el registro.')
}

export function RegisterPage() {
  const registro = useRegistro()
  const alertaError = useRef<HTMLDivElement>(null)
  const [params] = useSearchParams()
  const location = useLocation()
  const meta = buscarMeta(params.get('meta'))
  const datosCalculadora = validarDatosCorporales(
    (location.state as { datosCalculadora?: unknown } | null)?.datosCalculadora,
  )

  const {
    register,
    handleSubmit,
    getValues,
    control,
    formState: { errors },
  } = useForm<Formulario>({
    resolver: zodResolver(esquema),
    // Errores al salir del campo y, desde ahí, en cada tecla: no se regaña antes de terminar.
    mode: 'onTouched',
    defaultValues: {
      nombre: '',
      correo: '',
      password: '',
      confirmacion: '',
      terminos: false,
      privacidad: false,
      marketing: false,
    },
  })
  const [nombre, correo, password, confirmacion] = useWatch({
    control,
    name: ['nombre', 'correo', 'password', 'confirmacion'],
  })
  const idRequisitos = useId()
  const confirmada = estadoConfirmacion(password, confirmacion)

  // El error aparece junto al botón: se lleva a la vista y recibe el foco.
  useEffect(() => {
    if (registro.isError) alertaError.current?.focus()
  }, [registro.isError])

  const enviar = handleSubmit((datos) =>
    registro.mutate(
      {
        nombre: datos.nombre,
        correo: datos.correo,
        password: datos.password,
        consentimientos: {
          terminosVersion: VERSION_TERMINOS,
          privacidadVersion: VERSION_PRIVACIDAD,
          marketing: datos.marketing,
        },
      },
      {
        onSuccess: () =>
          guardarRegistroPendiente({
            correo: datos.correo,
            terminosVersion: VERSION_TERMINOS,
            privacidadVersion: VERSION_PRIVACIDAD,
            marketing: datos.marketing,
            metaSlug: meta?.slug ?? null,
            datosCorporales: datosCalculadora,
          }),
      },
    ),
  )

  if (registro.isSuccess) {
    return (
      <AuthCard titulo="Revisa tu correo" paso={2}>
        <Alert tono="exito" titulo="Cuenta creada">
          Te enviamos un código de activación a <strong>{registro.data.correo}</strong>. Actívala e
          inicia sesión para terminar de preparar tu plan.
        </Alert>
        <div className="flex flex-col gap-2">
          <Link
            to={`/activar?correo=${encodeURIComponent(registro.data.correo)}`}
            className={claseBoton({ tamano: 'lg' })}
          >
            Tengo el código de activación
          </Link>
          <Link to="/login" className={claseEnlace}>
            Ir a iniciar sesión
          </Link>
        </div>
      </AuthCard>
    )
  }

  return (
    <AuthCard
      titulo="Crea tu cuenta"
      paso={1}
      descripcion={
        <>
          ¿Ya tienes una?{' '}
          <Link to="/login" className={claseEnlace}>
            Inicia sesión
          </Link>
        </>
      }
    >
      {/* En móvil el panel de la meta no se ve: se recuerda aquí. */}
      {meta && (
        <p className="rounded-xl bg-acento-suave px-4 py-3 text-sm text-acento-fuerte lg:hidden">
          Meta elegida: <strong>{meta.nombre}</strong>
        </p>
      )}

      {/* Si las casillas de los documentos ya están marcadas se envían con el token; si no, se piden después. */}
      <EntradaGoogle
        texto="signup_with"
        consentimientosIniciales={() => {
          const { terminos, privacidad, marketing } = getValues()
          return terminos && privacidad
            ? {
                terminosVersion: VERSION_TERMINOS,
                privacidadVersion: VERSION_PRIVACIDAD,
                marketing: marketing === true,
              }
            : undefined
        }}
      />

      <div className="flex items-center gap-3 text-xs text-atenuado" aria-hidden="true">
        <span className="h-px flex-1 bg-borde" />o con tu correo
        <span className="h-px flex-1 bg-borde" />
      </div>

      <form onSubmit={enviar} noValidate className="space-y-4">
        <fieldset className="min-w-0 space-y-4">
          <legend className="rotulo float-left w-full">Tus datos</legend>
          <Input
            etiqueta="Nombre completo"
            autoComplete="name"
            placeholder="Ej.: Ana Pérez"
            ayuda="Tu nombre y tu apellido."
            valido={cumple(nombreCompletoSchema, nombre)}
            error={errors.nombre?.message}
            {...register('nombre')}
          />
          <Input
            etiqueta="Correo"
            type="email"
            inputMode="email"
            autoComplete="email"
            placeholder="tu@correo.com"
            valido={cumple(correoSchema, correo)}
            error={errors.correo?.message}
            {...register('correo')}
          />
        </fieldset>
        <fieldset className="min-w-0 space-y-4 border-t border-borde pt-5">
          <legend className="rotulo float-left w-full">Tu contraseña</legend>
          <PasswordInput
            etiqueta="Contraseña"
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
        </fieldset>
        <ConsentFieldset
          rutas={RUTAS_LEGALES}
          versionTerminos={VERSION_TERMINOS}
          versionPrivacidad={VERSION_PRIVACIDAD}
          terminos={register('terminos')}
          privacidad={register('privacidad')}
          marketing={register('marketing')}
          errores={{ terminos: errors.terminos?.message, privacidad: errors.privacidad?.message }}
        />
        {registro.isError && (
          <div ref={alertaError} tabIndex={-1} className="outline-none">
            <Alert tono="peligro">
              {mensajeRegistro(registro.error)}{' '}
              <Link to="/login" className={claseEnlace}>
                Iniciar sesión
              </Link>{' '}
              <Link to="/recuperar" className={claseEnlace}>
                Recuperar contraseña
              </Link>
            </Alert>
          </div>
        )}
        <Button type="submit" tamano="lg" className="w-full" cargando={registro.isPending}>
          Crear cuenta y continuar
        </Button>
        <p className="text-xs text-atenuado">
          Te enviaremos un correo para verificar tu cuenta. Hasta que termines el alta recordamos en
          este navegador tus consentimientos y tu meta (como máximo 7 días). Puedes eliminar tu
          cuenta en cualquier momento desde tu perfil.
        </p>
      </form>
    </AuthCard>
  )
}
