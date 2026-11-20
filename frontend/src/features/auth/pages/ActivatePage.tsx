import { zodResolver } from '@hookform/resolvers/zod'
import { useForm, useWatch } from 'react-hook-form'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { rutaInicial } from '@/app/navigation'
import { useAuth } from '@/shared/auth/AuthContext'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { claseBoton } from '@/shared/ui/claseBoton'
import { Input } from '@/shared/ui/Field'
import { useActivar } from '../api'
import { AuthCard, claseEnlace } from '@/shared/ui/AuthCard'
import { activarSchema, correoSchema, type ActivarForm } from '../schemas'
import { cumple } from '../validacionEnVivo'

// El código de 6 dígitos no es único entre usuarios: se activa siempre con el correo. Si llega desde el
// enlace (?correo=...&codigo=...) se rellena, pero NO se activa solo: un clic en el botón. Así un escáner
// de correos o una precarga del enlace no gasta los 5 intentos del código (RF-CA-15). Activar abre la sesión y
// lleva a la ruta inicial del rol (sin perfil, RequierePerfil manda al onboarding). Esta ruta no está bajo
// SoloInvitados: el enlace del correo puede abrirse con otra sesión abierta.
export function ActivatePage() {
  const [params] = useSearchParams()
  const activar = useActivar()
  const { iniciarSesion } = useAuth()
  const navigate = useNavigate()

  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<ActivarForm>({
    resolver: zodResolver(activarSchema),
    mode: 'onTouched',
    defaultValues: { correo: params.get('correo') ?? '', codigo: params.get('codigo') ?? '' },
  })
  const [correo, codigo] = useWatch({ control, name: ['correo', 'codigo'] })

  // Solo si la respuesta no trajo una sesión válida (no debería pasar): la cuenta ya está activa.
  if (activar.isSuccess) {
    return (
      <AuthCard titulo="Cuenta activada" paso={2}>
        <Alert tono="exito">Tu cuenta está activa. Inicia sesión para preparar tu plan.</Alert>
        <Link to="/login" className={claseBoton({ tamano: 'lg' })}>
          Ir a iniciar sesión
        </Link>
      </AuthCard>
    )
  }

  return (
    <AuthCard
      titulo="Activar cuenta"
      paso={2}
      descripcion="Escribe tu correo y el código de 6 dígitos que recibiste en el correo de activación."
      pie={
        <Link to="/reenviar-activacion" className={claseEnlace}>
          ¿El código venció? Pide uno nuevo
        </Link>
      }
    >
      {activar.isError && (
        <Alert tono="peligro">
          El código no es válido, ya se usó o venció. Tras 5 intentos fallidos hay que pedir uno
          nuevo.
        </Alert>
      )}
      <form
        onSubmit={handleSubmit((d) =>
          activar.mutate(d, {
            onSuccess: (respuesta) => {
              const sesion = respuesta.token ? iniciarSesion(respuesta.token) : null
              if (sesion) navigate(rutaInicial(sesion.rol), { replace: true })
            },
          }),
        )}
        noValidate
        className="space-y-4"
      >
        <Input
          etiqueta="Correo"
          type="email"
          inputMode="email"
          autoComplete="email"
          valido={cumple(correoSchema, correo)}
          error={errors.correo?.message}
          {...register('correo')}
        />
        <Input
          etiqueta="Código de activación"
          inputMode="numeric"
          autoComplete="one-time-code"
          maxLength={6}
          ayuda={`${(codigo ?? '').replace(/\D/g, '').length} de 6 dígitos`}
          valido={cumple(activarSchema.shape.codigo, codigo)}
          error={errors.codigo?.message}
          {...register('codigo')}
        />
        <Button type="submit" className="w-full" cargando={activar.isPending}>
          Activar
        </Button>
      </form>
    </AuthCard>
  )
}
