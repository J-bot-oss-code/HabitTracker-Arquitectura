import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link, useLocation } from 'react-router'
import { esErrorHttp, mensajeDeError } from '@/shared/api/problem'
import { useAuth } from '@/shared/auth/AuthContext'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Field'
import { useLogin } from '../api'
import { AuthCard, claseEnlace } from '../components/AuthCard'
import { loginSchema, type LoginForm } from '../schemas'

interface EstadoNavegacion {
  mensaje?: string
}

function mensajeLogin(error: unknown): string {
  // RF-CA-03: no se revela qué dato falló.
  if (esErrorHttp(error, 401)) return 'Correo o contraseña incorrectos.'
  // RF-CA-15 / RF-CA-19: el backend responde 400 genérico si la cuenta no está activada
  // o está bloqueada temporalmente por intentos fallidos.
  if (esErrorHttp(error, 400))
    return 'No se pudo iniciar sesión. Verifica que tu cuenta esté activada; si fallaste varias veces, espera 15 minutos.'
  // Sin servicio de autenticación (API caída o ruta inexistente) el problema no es del usuario.
  if (esErrorHttp(error, 404) || error instanceof TypeError)
    return 'No se pudo contactar con el servicio de inicio de sesión. Inténtalo de nuevo en unos minutos.'
  return mensajeDeError(error)
}

export function LoginPage() {
  const { iniciarSesion, avisoCierre } = useAuth()
  const location = useLocation()
  const mensaje = (location.state as EstadoNavegacion | null)?.mensaje ?? avisoCierre
  const login = useLogin()

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginForm>({ resolver: zodResolver(loginSchema) })

  // Con la sesión abierta, SoloInvitados redirige a la ruta pedida o a la inicial del rol.
  const enviar = handleSubmit((datos) =>
    login.mutate(datos, {
      onSuccess: (respuesta) => {
        if (respuesta.token) iniciarSesion(respuesta.token)
      },
    }),
  )

  return (
    <AuthCard
      titulo="Iniciar sesión"
      descripcion="Accede para gestionar tus hábitos y permisos."
      pie={
        <p className="text-atenuado">
          ¿No tienes cuenta?{' '}
          <Link to="/registro" className={claseEnlace}>
            Regístrate
          </Link>
        </p>
      }
    >
      {mensaje && <Alert tono="info">{mensaje}</Alert>}
      {login.isError && <Alert tono="peligro">{mensajeLogin(login.error)}</Alert>}

      <form onSubmit={enviar} noValidate className="space-y-4">
        <Input
          etiqueta="Correo"
          type="email"
          autoComplete="email"
          error={errors.email?.message}
          {...register('email')}
        />
        <Input
          etiqueta="Contraseña"
          type="password"
          autoComplete="current-password"
          error={errors.password?.message}
          {...register('password')}
        />
        <Button type="submit" className="w-full" cargando={login.isPending}>
          Entrar
        </Button>
      </form>

      <div className="flex flex-col gap-2 text-sm sm:flex-row sm:justify-between">
        <Link to="/recuperar" className={claseEnlace}>
          ¿Olvidaste tu contraseña?
        </Link>
        <Link to="/reenviar-activacion" className={claseEnlace}>
          Reenviar activación
        </Link>
      </div>
    </AuthCard>
  )
}
