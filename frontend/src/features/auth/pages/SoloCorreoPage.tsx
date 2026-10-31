import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Field'
import { useReenviarActivacion, useRecuperarPassword } from '../api'
import { AuthCard, claseEnlace } from '../components/AuthCard'
import { soloCorreoSchema, type SoloCorreoForm } from '../schemas'

// Ambos endpoints responden siempre 200 con el mismo mensaje para no revelar qué correos
// están registrados (RF-CA-09), así que comparten pantalla.
function FormularioCorreo({
  titulo,
  descripcion,
  boton,
  confirmacion,
  siguiente,
  usarMutacion,
}: {
  titulo: string
  descripcion: string
  boton: string
  confirmacion: string
  siguiente: { a: (correo: string) => string; texto: string }
  usarMutacion: typeof useRecuperarPassword
}) {
  const mutacion = usarMutacion()
  const {
    register,
    handleSubmit,
    getValues,
    formState: { errors },
  } = useForm<SoloCorreoForm>({ resolver: zodResolver(soloCorreoSchema) })

  return (
    <AuthCard
      titulo={titulo}
      descripcion={descripcion}
      pie={
        <Link to="/login" className={claseEnlace}>
          Volver a iniciar sesión
        </Link>
      }
    >
      {mutacion.isSuccess ? (
        <>
          <Alert tono="exito">{confirmacion}</Alert>
          <Link to={siguiente.a(getValues('correo'))} className={claseEnlace}>
            {siguiente.texto}
          </Link>
        </>
      ) : (
        <>
          {mutacion.isError && (
            <Alert tono="peligro">No se pudo procesar la solicitud. Inténtalo de nuevo.</Alert>
          )}
          <form
            onSubmit={handleSubmit((d) => mutacion.mutate(d.correo))}
            noValidate
            className="space-y-4"
          >
            <Input
              etiqueta="Correo"
              type="email"
              autoComplete="email"
              error={errors.correo?.message}
              {...register('correo')}
            />
            <Button type="submit" className="w-full" cargando={mutacion.isPending}>
              {boton}
            </Button>
          </form>
        </>
      )}
    </AuthCard>
  )
}

export function ForgotPasswordPage() {
  return (
    <FormularioCorreo
      titulo="Recuperar contraseña"
      descripcion="Te enviaremos un código de un solo uso para restablecerla."
      boton="Enviar código"
      confirmacion="Si el correo está registrado, recibirás un código de recuperación en unos minutos."
      siguiente={{
        a: (correo) => `/restablecer?correo=${encodeURIComponent(correo)}`,
        texto: 'Ya tengo el código',
      }}
      usarMutacion={useRecuperarPassword}
    />
  )
}

export function ResendActivationPage() {
  return (
    <FormularioCorreo
      titulo="Reenviar activación"
      descripcion="Te enviaremos un nuevo enlace de activación."
      boton="Reenviar"
      confirmacion="Si la cuenta existe y aún no está activada, recibirás un nuevo correo de activación."
      siguiente={{ a: () => '/activar', texto: 'Tengo un código de activación' }}
      usarMutacion={useReenviarActivacion}
    />
  )
}
