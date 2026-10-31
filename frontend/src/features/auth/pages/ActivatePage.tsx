import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useRef } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useSearchParams } from 'react-router'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Field'
import { useActivar } from '../api'
import { AuthCard, claseEnlace } from '../components/AuthCard'
import { activarSchema, type ActivarForm } from '../schemas'

export function ActivatePage() {
  const [params] = useSearchParams()
  const tokenEnUrl = params.get('token') ?? ''
  const activar = useActivar()
  const intentado = useRef(false)

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ActivarForm>({
    resolver: zodResolver(activarSchema),
    defaultValues: { token: tokenEnUrl },
  })

  // Si llega desde el enlace del correo (?token=...), se activa sin pedir nada más.
  useEffect(() => {
    if (tokenEnUrl && !intentado.current) {
      intentado.current = true
      activar.mutate(tokenEnUrl)
    }
  }, [tokenEnUrl, activar])

  if (activar.isSuccess) {
    return (
      <AuthCard titulo="Cuenta activada">
        <Alert tono="exito">Tu cuenta está activa. Ya puedes iniciar sesión.</Alert>
        <Link to="/login" className={claseEnlace}>
          Ir a iniciar sesión
        </Link>
      </AuthCard>
    )
  }

  return (
    <AuthCard
      titulo="Activar cuenta"
      descripcion="Pega el código que recibiste en el correo de activación."
      pie={
        <Link to="/reenviar-activacion" className={claseEnlace}>
          ¿El código venció? Pide uno nuevo
        </Link>
      }
    >
      {activar.isError && (
        <Alert tono="peligro">El código no es válido o ya venció. Solicita uno nuevo.</Alert>
      )}
      <form
        onSubmit={handleSubmit((d) => activar.mutate(d.token))}
        noValidate
        className="space-y-4"
      >
        <Input
          etiqueta="Código de activación"
          error={errors.token?.message}
          {...register('token')}
        />
        <Button type="submit" className="w-full" cargando={activar.isPending}>
          Activar
        </Button>
      </form>
    </AuthCard>
  )
}
