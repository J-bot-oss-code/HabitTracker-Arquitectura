import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { codigoDeError, mensajeDeError } from '@/shared/api/problem'
import { cn } from '@/shared/lib/cn'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Select, Textarea } from '@/shared/ui/Field'
import { useEnviarSugerencia } from '../api'
import { enviarSugerenciaSchema, type EnviarSugerenciaForm } from '../schemas'
import {
  CODIGO_LIMITE,
  LONGITUD_MAXIMA,
  LONGITUD_MINIMA,
  ROTULO_TIPO,
  TIPOS_SUGERENCIA,
} from '../types'

const ID_TEXTO = 'texto-sugerencia'
const ID_AYUDA = 'ayuda-sugerencia'
const ID_CONTADOR = 'contador-sugerencia'

// Formulario de «Quejas y mejoras»: tipo y texto (10 a 2000 caracteres, medidos tras recortar los espacios, como el
// servidor). Un rechazo del servidor se muestra con su propio mensaje encima de «Enviar»; el del tope diario no es un error
// de la persona, así que va en un aviso neutro, no en rojo, y deja «Enviar» deshabilitado.
export function FormularioSugerencia() {
  const enviar = useEnviarSugerencia()
  const [enviado, setEnviado] = useState(false)
  const {
    register,
    handleSubmit,
    reset,
    setFocus,
    control,
    formState: { errors },
  } = useForm<EnviarSugerenciaForm>({
    resolver: zodResolver(enviarSugerenciaSchema),
    defaultValues: { tipo: 'Mejora', texto: '' },
  })
  const escrito = useWatch({ control, name: 'texto' }) ?? ''
  const demas = escrito.length - LONGITUD_MAXIMA
  const limite = enviar.isError && codigoDeError(enviar.error) === CODIGO_LIMITE

  const alEnviar = (datos: EnviarSugerenciaForm) => {
    setEnviado(false)
    enviar.mutate(datos, {
      onSuccess: () => {
        reset({ tipo: datos.tipo, texto: '' })
        setEnviado(true)
      },
    })
  }

  // El campo se vuelve a registrar tras el reset: el foco se devuelve ya con el formulario repintado.
  useEffect(() => {
    if (enviado) setFocus('texto')
  }, [enviado, setFocus])

  return (
    <form
      onSubmit={handleSubmit(alEnviar)}
      noValidate
      aria-label="Enviar una sugerencia"
      className="space-y-4"
    >
      <Select etiqueta="Tipo" error={errors.tipo?.message} {...register('tipo')}>
        {TIPOS_SUGERENCIA.map((t) => (
          <option key={t} value={t}>
            {ROTULO_TIPO[t]}
          </option>
        ))}
      </Select>
      <div>
        <Textarea
          id={ID_TEXTO}
          etiqueta="Tu mensaje"
          rows={6}
          autoComplete="off"
          descritoPor={`${ID_AYUDA} ${ID_CONTADOR}`}
          error={errors.texto?.message}
          {...register('texto')}
        />
        {/* La ayuda y el contador siempre se ven, también cuando hay un error en el campo. */}
        <div className="mt-1.5 flex flex-wrap justify-between gap-x-4 gap-y-1 text-xs">
          <p id={ID_AYUDA} className="text-atenuado">
            Entre {LONGITUD_MINIMA} y {LONGITUD_MAXIMA} caracteres. No escribas contraseñas ni datos
            de salud.
          </p>
          <p
            id={ID_CONTADOR}
            className={cn(
              'tabular-nums',
              demas > 0 ? 'font-semibold text-peligro' : 'text-atenuado',
            )}
          >
            {escrito.length} / {LONGITUD_MAXIMA}
            {demas > 0 && ` · ${demas} de más`}
          </p>
        </div>
      </div>
      {enviar.isError && (
        <Alert tono={limite ? 'aviso' : 'peligro'}>
          {mensajeDeError(enviar.error, 'No se pudo enviar tu mensaje. Inténtalo de nuevo.')}
        </Alert>
      )}
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <Button type="submit" cargando={enviar.isPending} disabled={limite}>
          Enviar
        </Button>
        {/* Región fija: el aviso se anuncia al aparecer sin que cambie nada de sitio. */}
        <p role="status" className="min-w-0 text-sm text-texto-2">
          {enviado && !enviar.isError ? 'Enviado. Lo ves abajo, en «Lo que has enviado».' : null}
        </p>
      </div>
    </form>
  )
}
