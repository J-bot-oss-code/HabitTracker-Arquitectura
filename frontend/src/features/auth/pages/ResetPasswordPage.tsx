import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Field'
import { useRestablecerPassword } from '../api'
import { AuthCard, claseEnlace } from '../components/AuthCard'
import { restablecerSchema, type RestablecerForm } from '../schemas'

export function ResetPasswordPage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const restablecer = useRestablecerPassword()

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<RestablecerForm>({
    resolver: zodResolver(restablecerSchema),
    defaultValues: { correo: params.get('correo') ?? '' },
  })

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
          autoComplete="email"
          error={errors.correo?.message}
          {...register('correo')}
        />
        <Input
          etiqueta="Código"
          autoComplete="one-time-code"
          error={errors.codigo?.message}
          {...register('codigo')}
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
          etiqueta="Confirmar contraseña"
          type="password"
          autoComplete="new-password"
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
