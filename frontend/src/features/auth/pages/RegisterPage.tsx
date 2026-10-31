import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router'
import { mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Field'
import { useRegistro } from '../api'
import { AuthCard, claseEnlace } from '../components/AuthCard'
import { registroSchema, type RegistroForm } from '../schemas'

export function RegisterPage() {
  const registro = useRegistro()
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<RegistroForm>({ resolver: zodResolver(registroSchema) })

  const enviar = handleSubmit((datos) =>
    registro.mutate({ nombre: datos.nombre, correo: datos.correo, password: datos.password }),
  )

  if (registro.isSuccess) {
    return (
      <AuthCard titulo="Revisa tu correo">
        <Alert tono="exito" titulo="Cuenta creada">
          Te enviamos un enlace de activación a <strong>{registro.data.correo}</strong>. Debes
          activar la cuenta antes de iniciar sesión.
        </Alert>
        <div className="flex flex-col gap-2 text-sm">
          <Link to="/activar" className={claseEnlace}>
            Tengo un código de activación
          </Link>
          <Link to="/login" className={claseEnlace}>
            Volver a iniciar sesión
          </Link>
        </div>
      </AuthCard>
    )
  }

  return (
    <AuthCard
      titulo="Crear cuenta"
      descripcion="Las cuentas nuevas tienen rol Estándar."
      pie={
        <p className="text-slate-600 dark:text-slate-400">
          ¿Ya tienes cuenta?{' '}
          <Link to="/login" className={claseEnlace}>
            Inicia sesión
          </Link>
        </p>
      }
    >
      {registro.isError && (
        <Alert tono="peligro">
          {mensajeDeError(registro.error, 'No se pudo completar el registro.')}
        </Alert>
      )}
      <form onSubmit={enviar} noValidate className="space-y-4">
        <Input
          etiqueta="Nombre completo"
          autoComplete="name"
          error={errors.nombre?.message}
          {...register('nombre')}
        />
        <Input
          etiqueta="Correo"
          type="email"
          autoComplete="email"
          error={errors.correo?.message}
          {...register('correo')}
        />
        <Input
          etiqueta="Contraseña"
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
        <Button type="submit" className="w-full" cargando={registro.isPending}>
          Crear cuenta
        </Button>
      </form>
    </AuthCard>
  )
}
