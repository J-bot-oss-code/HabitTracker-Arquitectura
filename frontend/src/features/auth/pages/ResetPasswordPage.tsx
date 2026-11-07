import { zodResolver } from '@hookform/resolvers/zod'
import { useId } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Input, PasswordInput } from '@/shared/ui/Field'
import { useRestablecerPassword } from '../api'
import { AuthCard, claseEnlace } from '@/shared/ui/AuthCard'
import { RequisitosClave } from '../components/RequisitosClave'
import {
  correoSchema,
  passwordNuevaSchema,
  restablecerSchema,
  type RestablecerForm,
} from '../schemas'
import { cumple, estadoConfirmacion } from '../validacionEnVivo'

export function ResetPasswordPage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const restablecer = useRestablecerPassword()

  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<RestablecerForm>({
    resolver: zodResolver(restablecerSchema),
    mode: 'onTouched',
    defaultValues: {
      correo: params.get('correo') ?? '',
      codigo: '',
      password: '',
      confirmacion: '',
    },
  })
  const [correo, password, confirmacion] = useWatch({
    control,
    name: ['correo', 'password', 'confirmacion'],
  })
  const idRequisitos = useId()
  const confirmada = estadoConfirmacion(password, confirmacion)

  const enviar = handleSubmit((d) =>
    restablecer.mutate(
      { correo: d.correo, codigo: d.codigo, nuevaPassword: d.password },
      {
        onSuccess: () =>
          navigate('/login', {
            replace: true,
            state: { mensaje: 'Contraseña restablecida. Inicia sesión con la nueva.' },
          }),
      },
    ),
  )

  return (
    <AuthCard
      titulo="Restablecer contraseña"
      descripcion="Usa el código de un solo uso que recibiste por correo."
      pie={
        <Link to="/recuperar" className={claseEnlace}>
          Pedir un código nuevo
        </Link>
      }
    >
      {restablecer.isError && <Alert tono="peligro">El código no es válido o ya venció.</Alert>}
      <form onSubmit={enviar} noValidate className="space-y-4">
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
          etiqueta="Código"
          autoComplete="one-time-code"
          ayuda="Lo encuentras en el correo de recuperación."
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
        <Button type="submit" className="w-full" cargando={restablecer.isPending}>
          Restablecer
        </Button>
      </form>
    </AuthCard>
  )
}
