import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { consentimientoSchema, type ConsentimientoForm } from '@/features/perfil/api'
import { RUTAS_LEGALES, VERSION_PRIVACIDAD, VERSION_TERMINOS } from '@/features/publico/api'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { ConsentFieldset } from '@/shared/ui/ConsentFieldset'
import { PasswordInput } from '@/shared/ui/Field'
import type { ConsentimientosRegistro } from '../types'
import { BotonGoogle, type TextoBotonGoogle } from './BotonGoogle'
import { useEntradaGoogle } from './useEntradaGoogle'

const claveSchema = z.object({
  password: z
    .string()
    .min(1, 'Escribe tu contraseña.')
    .max(1024, 'La contraseña es demasiado larga.'),
})
type ClaveForm = z.infer<typeof claveSchema>

function FormularioDocumentos({
  cargando,
  alAceptar,
  alCancelar,
}: {
  cargando: boolean
  alAceptar: (consentimientos: ConsentimientosRegistro) => void
  alCancelar: () => void
}) {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ConsentimientoForm>({
    resolver: zodResolver(consentimientoSchema),
    defaultValues: { terminos: false, privacidad: false, marketing: false },
  })

  return (
    <form
      noValidate
      className="space-y-4"
      onSubmit={handleSubmit((datos) =>
        alAceptar({
          terminosVersion: VERSION_TERMINOS,
          privacidadVersion: VERSION_PRIVACIDAD,
          marketing: datos.marketing,
        }),
      )}
    >
      <Alert tono="info">
        Todavía no tienes una cuenta con este correo. Para crearla, acepta los documentos.
      </Alert>
      <ConsentFieldset
        rutas={RUTAS_LEGALES}
        versionTerminos={VERSION_TERMINOS}
        versionPrivacidad={VERSION_PRIVACIDAD}
        terminos={register('terminos')}
        privacidad={register('privacidad')}
        marketing={register('marketing')}
        errores={{ terminos: errors.terminos?.message, privacidad: errors.privacidad?.message }}
      />
      <div className="flex flex-wrap gap-2">
        <Button type="submit" cargando={cargando}>
          Crear cuenta con Google
        </Button>
        <Button type="button" variante="fantasma" onClick={alCancelar}>
          Cancelar
        </Button>
      </div>
    </form>
  )
}

export function FormularioClave({
  titulo,
  textoBoton,
  cargando,
  alEnviar,
  alCancelar,
}: {
  titulo: string
  textoBoton: string
  cargando: boolean
  alEnviar: (password: string) => void
  alCancelar: () => void
}) {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ClaveForm>({ resolver: zodResolver(claveSchema) })

  return (
    <form noValidate className="space-y-4" onSubmit={handleSubmit((d) => alEnviar(d.password))}>
      <Alert tono="info">{titulo}</Alert>
      <PasswordInput
        etiqueta="Contraseña"
        autoComplete="current-password"
        error={errors.password?.message}
        {...register('password')}
      />
      <div className="flex flex-wrap gap-2">
        <Button type="submit" cargando={cargando}>
          {textoBoton}
        </Button>
        <Button type="button" variante="fantasma" onClick={alCancelar}>
          Cancelar
        </Button>
      </div>
    </form>
  )
}

// «Continuar con Google» (login y registro). Nunca bloquea el acceso con correo y contraseña.
export function EntradaGoogle({
  texto,
  consentimientosIniciales,
}: {
  texto: TextoBotonGoogle
  // En el registro las casillas ya están en el formulario: si están marcadas se envían de una vez.
  consentimientosIniciales?: () => ConsentimientosRegistro | undefined
}) {
  const google = useEntradaGoogle(consentimientosIniciales)
  const paso = google.paso

  return (
    <div className="space-y-3">
      {paso.tipo === 'inactivo' && (
        <BotonGoogle
          texto={texto}
          alCredencial={google.recibirCredencial}
          deshabilitado={google.cargando}
        />
      )}

      {google.error && <Alert tono="peligro">{google.error}</Alert>}

      {paso.tipo === 'consentimientos' && (
        <FormularioDocumentos
          cargando={google.cargando}
          alAceptar={(c) => google.aceptarDocumentos(paso.idToken, c)}
          alCancelar={google.cancelar}
        />
      )}

      {paso.tipo === 'vincular' && (
        <FormularioClave
          titulo="Ya tienes una cuenta con el correo de tu cuenta de Google. Confirma tu contraseña para vincularlas."
          textoBoton="Vincular y entrar"
          cargando={google.cargando}
          alEnviar={(password) => google.confirmarVinculacion(paso.idToken, password)}
          alCancelar={google.cancelar}
        />
      )}
    </div>
  )
}
