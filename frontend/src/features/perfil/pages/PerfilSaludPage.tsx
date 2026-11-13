import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { Link } from 'react-router'
import { LIMITES_CORPORALES } from '@/features/publico/types'
import { mensajeDeError } from '@/shared/api/problem'
import { formatearFecha } from '@/shared/lib/fechas'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { EmptyState, Seccion } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Input, Select } from '@/shared/ui/Field'
import { Segmentado } from '@/shared/ui/Segmentado'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useToast } from '@/shared/ui/Toast'
import {
  esConsentimientoSaludRequerido,
  esPerfilInexistente,
  useConsentimientos,
  useGuardarCondiciones,
  useGuardarConsentimientos,
  useGuardarObjetivos,
  useGuardarPerfil,
  useGuardarSeguridad,
  usePerfil,
  useSalud,
} from '../api'
import { OpcionesMultiples } from '../components/Opciones'
import {
  ETIQUETA_ACTIVIDAD,
  ETIQUETA_ALERGIA,
  ETIQUETA_CONDICION,
  ETIQUETA_EXPERIENCIA,
  ETIQUETA_LUGAR,
  ETIQUETA_OBJETIVO,
  ETIQUETA_PERFIL_LABORAL,
  ETIQUETA_PRECAUCION,
  ETIQUETA_RESTRICCION,
  ETIQUETA_SEXO,
  ETIQUETA_ZONA,
  PREGUNTA_SEGURIDAD,
  ZONAS_HORARIAS_COMUNES,
} from '../etiquetas'
import {
  condicionesSchema,
  LIMITES_PLAN,
  objetivosSchema,
  perfilSchema,
  type CondicionesForm,
  type ObjetivosForm,
  type PerfilForm,
  type PerfilValidado,
} from '../schemas'
import {
  ACTIVIDADES_PERFIL,
  ALERGIAS,
  CONDICIONES,
  EXPERIENCIAS,
  LUGARES,
  MAXIMO_OBJETIVOS,
  OBJETIVOS,
  PERFILES_LABORALES,
  PREGUNTAS_SEGURIDAD,
  SEXOS_PERFIL,
  ZONAS_CORPORALES,
  type BloqueSeguridad,
  type CuestionarioSaludDto,
  type PerfilDto,
} from '../types'

// Configuración › Perfil y salud (rectificación, Ley 172-13). El nivel 1 se cambia sin más; peso, altura y el
// cuestionario son datos de salud y piden el consentimiento datosSalud. Cada bloque del cuestionario se guarda por
// separado: los planes piden solo los que necesitan (P1).
export function PerfilSaludPage() {
  const perfil = usePerfil()
  const salud = useSalud()

  if (perfil.isPending || salud.isPending) return <CargandoPagina texto="Cargando tu perfil…" />
  if (perfil.isError && esPerfilInexistente(perfil.error)) {
    return (
      <EmptyState
        icono="cuenta"
        titulo="Aún no completaste tu perfil"
        descripcion="Son 3 pasos: tu jornada, tu zona horaria y el aviso de salud."
        accion={
          <Link to="/bienvenida" className="font-semibold text-acento underline">
            Completar mi perfil
          </Link>
        }
      />
    )
  }
  if (perfil.isError || salud.isError) {
    return (
      <ErrorCarga
        error={perfil.error ?? salud.error}
        porDefecto="No se pudo cargar tu perfil."
        alReintentar={() => {
          void perfil.refetch()
          void salud.refetch()
        }}
      />
    )
  }

  return (
    <div className="space-y-6">
      <AvisoConsentimiento />
      <PanelPerfil perfil={perfil.data} />
      <PanelSalud salud={salud.data} />
      <PanelEvaluacion salud={salud.data} />
    </div>
  )
}

// Sin el consentimiento datosSalud no se guardan peso, altura ni el cuestionario (409). Se ofrece activarlo aquí
// mismo; retirarlo se hace en Privacidad y datos.
function AvisoConsentimiento() {
  const consentimientos = useConsentimientos()
  const guardar = useGuardarConsentimientos()
  // Solo con los consentimientos ya leídos: así no se acepta en nombre de la persona ninguna versión legal que no
  // haya visto ni se pisa ningún otro permiso. Sin versiones aceptadas, el permiso se da en Privacidad y datos.
  if (!consentimientos.isSuccess || consentimientos.data.datosSalud) return null
  const actual = consentimientos.data
  const { terminosVersion, privacidadVersion } = actual
  const activar = () =>
    terminosVersion &&
    privacidadVersion &&
    guardar.mutate({
      terminosVersion,
      privacidadVersion,
      datosSalud: true,
      marketing: actual.marketing,
      compartirCumplimiento: actual.compartirCumplimiento,
      estadisticasAnonimas: actual.estadisticasAnonimas,
    })
  return (
    <Alert tono="info">
      <p>
        Para guardar tu peso, tu altura o el cuestionario de salud necesitamos tu permiso para usar
        esos datos. Solo los usamos para elegir planes seguros para ti; nadie más los ve y puedes
        retirarlo cuando quieras en{' '}
        <Link to="/ajustes/privacidad" className="font-semibold underline">
          Privacidad y datos
        </Link>
        .
      </p>
      {guardar.isError && (
        <p className="mt-2">{mensajeDeError(guardar.error, 'No se pudo guardar el permiso.')}</p>
      )}
      {terminosVersion && privacidadVersion && (
        <Button
          className="mt-3"
          variante="secundario"
          cargando={guardar.isPending}
          onClick={activar}
        >
          Permitir usar mis datos de salud
        </Button>
      )}
    </Alert>
  )
}

const aTexto = (hora: string | null) => (hora ? hora.slice(0, 5) : '')

function PanelPerfil({ perfil }: { perfil: PerfilDto }) {
  const guardar = useGuardarPerfil()
  const { notificar } = useToast()
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<PerfilForm, unknown, PerfilValidado>({
    resolver: zodResolver(perfilSchema),
    defaultValues: {
      perfilLaboral: perfil.perfilLaboral,
      zonaHoraria: perfil.zonaHoraria,
      horaEntradaTrabajo: aTexto(perfil.horaEntradaTrabajo),
      horaSalidaTrabajo: aTexto(perfil.horaSalidaTrabajo),
      horaDespertar: aTexto(perfil.horaDespertar),
      horaDormir: aTexto(perfil.horaDormir),
      edad: perfil.edad ?? undefined,
      sexo: perfil.sexo ?? '',
      alturaCm: perfil.alturaCm ?? undefined,
      pesoKg: perfil.pesoKg ?? undefined,
      actividad: perfil.actividad ?? '',
      diasPorSemana: perfil.diasPorSemana ?? undefined,
      minutosPorSesion: perfil.minutosPorSesion ?? undefined,
    },
  })

  const enviar = handleSubmit(async (d) => {
    try {
      await guardar.mutateAsync({ ...d, avisoSaludAceptado: true })
      notificar('Perfil guardado.')
    } catch {
      // Se muestra junto al botón.
    }
  })

  const zonas = (ZONAS_HORARIAS_COMUNES as readonly string[]).includes(perfil.zonaHoraria)
    ? ZONAS_HORARIAS_COMUNES
    : [perfil.zonaHoraria, ...ZONAS_HORARIAS_COMUNES]
  const { edad, alturaCm, pesoKg } = LIMITES_CORPORALES

  return (
    <Seccion titulo="Tu jornada y tus datos">
      <form onSubmit={enviar} noValidate className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <Select
            etiqueta="Tu jornada"
            error={errors.perfilLaboral?.message}
            {...register('perfilLaboral')}
          >
            {PERFILES_LABORALES.map((p) => (
              <option key={p} value={p}>
                {ETIQUETA_PERFIL_LABORAL[p].titulo}
              </option>
            ))}
          </Select>
          <Select
            etiqueta="Zona horaria"
            error={errors.zonaHoraria?.message}
            {...register('zonaHoraria')}
          >
            {zonas.map((z) => (
              <option key={z} value={z}>
                {z.replaceAll('_', ' ')}
              </option>
            ))}
          </Select>
        </div>
        <fieldset className="grid gap-4 sm:grid-cols-2">
          <legend className="mb-2 text-sm font-semibold">Tus horarios (opcional)</legend>
          <Input
            etiqueta="Entras a trabajar"
            type="time"
            error={errors.horaEntradaTrabajo?.message}
            {...register('horaEntradaTrabajo')}
          />
          <Input
            etiqueta="Sales del trabajo"
            type="time"
            error={errors.horaSalidaTrabajo?.message}
            {...register('horaSalidaTrabajo')}
          />
          <Input
            etiqueta="Te despiertas"
            type="time"
            error={errors.horaDespertar?.message}
            {...register('horaDespertar')}
          />
          <Input
            etiqueta="Te acuestas"
            type="time"
            error={errors.horaDormir?.message}
            {...register('horaDormir')}
          />
        </fieldset>
        <fieldset className="grid gap-4 sm:grid-cols-2">
          <legend className="mb-1 text-sm font-semibold">Datos opcionales</legend>
          <p className="text-xs text-atenuado sm:col-span-2">
            Solo los pedimos para planes que los necesitan. El peso y la altura son datos de salud.
          </p>
          <Input
            etiqueta="Edad"
            type="number"
            inputMode="numeric"
            min={edad.min}
            max={edad.max}
            error={errors.edad?.message}
            {...register('edad', { valueAsNumber: true })}
          />
          <Select etiqueta="Sexo" error={errors.sexo?.message} {...register('sexo')}>
            <option value="">Prefiero no decirlo</option>
            {SEXOS_PERFIL.map((s) => (
              <option key={s} value={s}>
                {ETIQUETA_SEXO[s]}
              </option>
            ))}
          </Select>
          <Input
            etiqueta="Altura (cm)"
            type="number"
            inputMode="decimal"
            min={alturaCm.min}
            max={alturaCm.max}
            error={errors.alturaCm?.message}
            {...register('alturaCm', { valueAsNumber: true })}
          />
          <Input
            etiqueta="Peso (kg)"
            type="number"
            inputMode="decimal"
            step="0.1"
            min={pesoKg.min}
            max={pesoKg.max}
            error={errors.pesoKg?.message}
            {...register('pesoKg', { valueAsNumber: true })}
          />
          <Select
            etiqueta="Nivel de actividad"
            error={errors.actividad?.message}
            {...register('actividad')}
          >
            <option value="">Sin indicar</option>
            {ACTIVIDADES_PERFIL.map((a) => (
              <option key={a} value={a}>
                {ETIQUETA_ACTIVIDAD[a]}
              </option>
            ))}
          </Select>
          <Input
            etiqueta="Días por semana"
            type="number"
            inputMode="numeric"
            min={LIMITES_PLAN.diasPorSemana.min}
            max={LIMITES_PLAN.diasPorSemana.max}
            error={errors.diasPorSemana?.message}
            {...register('diasPorSemana', { valueAsNumber: true })}
          />
          <Input
            etiqueta="Minutos por sesión"
            type="number"
            inputMode="numeric"
            min={LIMITES_PLAN.minutosPorSesion.min}
            max={LIMITES_PLAN.minutosPorSesion.max}
            error={errors.minutosPorSesion?.message}
            {...register('minutosPorSesion', { valueAsNumber: true })}
          />
        </fieldset>
        {guardar.isError && <ErrorGuardado error={guardar.error} />}
        <div className="flex justify-end">
          <Button type="submit" cargando={isSubmitting}>
            Guardar perfil
          </Button>
        </div>
      </form>
    </Seccion>
  )
}

function ErrorGuardado({ error }: { error: unknown }) {
  return (
    <Alert tono="peligro">
      {esConsentimientoSaludRequerido(error)
        ? 'Para guardar datos de salud necesitas dar el permiso de arriba.'
        : mensajeDeError(error, 'No se pudo guardar.')}
    </Alert>
  )
}

const fecha = (iso: string | null) => (iso ? formatearFecha(iso) : null)

function PanelSalud({ salud }: { salud: CuestionarioSaludDto }) {
  return (
    <Seccion titulo="Tu salud">
      <p className="mb-4 text-sm text-atenuado">
        Respóndelo solo si vas a empezar un plan que lo pide. Son tres bloques y cada uno se guarda
        aparte, cifrado. Solo tú lo ves. No sustituye el consejo de un médico.
      </p>
      <div className="space-y-8">
        <BloqueObjetivosForm salud={salud} />
        <BloqueSeguridadForm salud={salud} />
        <BloqueCondicionesForm salud={salud} />
      </div>
    </Seccion>
  )
}

function Cabecera({
  titulo,
  por,
  respondido,
}: {
  titulo: string
  por: string
  respondido: string | null
}) {
  return (
    <div className="mb-3">
      <h3 className="font-semibold">{titulo}</h3>
      <p className="text-xs text-atenuado">
        {por} {respondido ? `Respondido el ${fecha(respondido)}.` : 'Sin responder.'}
      </p>
    </div>
  )
}

function BloqueObjetivosForm({ salud }: { salud: CuestionarioSaludDto }) {
  const guardar = useGuardarObjetivos()
  const { notificar } = useToast()
  const { control, register, handleSubmit, watch, formState } = useForm<ObjetivosForm>({
    resolver: zodResolver(objetivosSchema),
    defaultValues: {
      objetivos: salud.objetivos?.objetivos ?? [],
      zonas: salud.objetivos?.zonas ?? [],
      experiencia: salud.objetivos?.experiencia,
      lugar: salud.objetivos?.lugar,
    },
  })
  // eslint-disable-next-line react-hooks/incompatible-library -- necesario para mostrar las zonas solo con "definir".
  const definir = watch('objetivos')?.includes('DefinirTonificar')
  const enviar = handleSubmit(async (d) => {
    try {
      await guardar.mutateAsync(objetivosSchema.parse(d))
      notificar('Objetivos guardados.')
    } catch {
      // Se muestra junto al botón.
    }
  })
  return (
    <form onSubmit={enviar} noValidate className="space-y-4">
      <Cabecera
        titulo="1. Tus objetivos"
        por="Para recomendarte planes."
        respondido={salud.objetivosRespondidoEn}
      />
      <Controller
        name="objetivos"
        control={control}
        render={({ field }) => (
          <OpcionesMultiples
            leyenda={`¿Qué quieres lograr? (hasta ${MAXIMO_OBJETIVOS})`}
            opciones={OBJETIVOS.map((o) => ({ valor: o, titulo: ETIQUETA_OBJETIVO[o] }))}
            valores={field.value ?? []}
            alCambiar={field.onChange}
            maximo={MAXIMO_OBJETIVOS}
            error={formState.errors.objetivos?.message}
          />
        )}
      />
      {definir && (
        <Controller
          name="zonas"
          control={control}
          render={({ field }) => (
            <OpcionesMultiples
              leyenda="¿Qué zonas quieres trabajar?"
              opciones={ZONAS_CORPORALES.map((z) => ({ valor: z, titulo: ETIQUETA_ZONA[z] }))}
              valores={field.value ?? []}
              alCambiar={field.onChange}
            />
          )}
        />
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Select
          etiqueta="Tu experiencia"
          error={formState.errors.experiencia?.message}
          {...register('experiencia')}
        >
          <option value="">Elige una opción</option>
          {EXPERIENCIAS.map((e) => (
            <option key={e} value={e}>
              {ETIQUETA_EXPERIENCIA[e]}
            </option>
          ))}
        </Select>
        <Select
          etiqueta="Dónde entrenarías"
          error={formState.errors.lugar?.message}
          {...register('lugar')}
        >
          <option value="">Elige una opción</option>
          {LUGARES.map((l) => (
            <option key={l} value={l}>
              {ETIQUETA_LUGAR[l]}
            </option>
          ))}
        </Select>
      </div>
      {guardar.isError && <ErrorGuardado error={guardar.error} />}
      <div className="flex justify-end">
        <Button type="submit" variante="secundario" cargando={formState.isSubmitting}>
          Guardar objetivos
        </Button>
      </div>
    </form>
  )
}

const SEGURIDAD_VACIA = Object.fromEntries(PREGUNTAS_SEGURIDAD.map((p) => [p, null])) as Record<
  keyof BloqueSeguridad,
  boolean | null
>

function BloqueSeguridadForm({ salud }: { salud: CuestionarioSaludDto }) {
  const guardar = useGuardarSeguridad()
  const { notificar } = useToast()
  const [respuestas, setRespuestas] = useState<Record<keyof BloqueSeguridad, boolean | null>>(
    salud.seguridad ?? SEGURIDAD_VACIA,
  )
  const [faltan, setFaltan] = useState(false)
  const enviar = async (e: React.FormEvent) => {
    e.preventDefault()
    if (PREGUNTAS_SEGURIDAD.some((p) => respuestas[p] === null)) return setFaltan(true)
    setFaltan(false)
    try {
      await guardar.mutateAsync(respuestas as BloqueSeguridad)
      notificar('Revisión de seguridad guardada.')
    } catch {
      // Se muestra junto al botón.
    }
  }
  return (
    <form onSubmit={enviar} noValidate className="space-y-4">
      <Cabecera
        titulo="2. Revisión de seguridad"
        por="Para que el plan no te ponga en riesgo."
        respondido={salud.seguridadRespondidoEn}
      />
      {PREGUNTAS_SEGURIDAD.map((p) => (
        <div key={p} className="space-y-2">
          <p className="text-sm text-texto-2">{PREGUNTA_SEGURIDAD[p]}</p>
          <Segmentado<boolean>
            etiqueta={PREGUNTA_SEGURIDAD[p]}
            opciones={[
              { valor: false, etiqueta: 'No' },
              { valor: true, etiqueta: 'Sí' },
            ]}
            valor={respuestas[p] as boolean}
            alCambiar={(v) => setRespuestas((r) => ({ ...r, [p]: v }))}
          />
        </div>
      ))}
      {faltan && <Alert tono="peligro">Responde las 7 preguntas antes de guardar.</Alert>}
      {guardar.isError && <ErrorGuardado error={guardar.error} />}
      <div className="flex justify-end">
        <Button type="submit" variante="secundario" cargando={guardar.isPending}>
          Guardar revisión
        </Button>
      </div>
    </form>
  )
}

function BloqueCondicionesForm({ salud }: { salud: CuestionarioSaludDto }) {
  const guardar = useGuardarCondiciones()
  const { notificar } = useToast()
  const { control, handleSubmit, formState } = useForm<CondicionesForm>({
    resolver: zodResolver(condicionesSchema),
    defaultValues: {
      condiciones: salud.condiciones?.condiciones ?? [],
      alergias: salud.condiciones?.alergias ?? [],
    },
  })
  const enviar = handleSubmit(async (d) => {
    try {
      await guardar.mutateAsync(d)
      notificar('Condiciones y alergias guardadas.')
    } catch {
      // Se muestra junto al botón.
    }
  })
  return (
    <form onSubmit={enviar} noValidate className="space-y-4">
      <Cabecera
        titulo="3. Condiciones y alergias"
        por="Para adaptar o descartar planes."
        respondido={salud.condicionesRespondidoEn}
      />
      <Controller
        name="condiciones"
        control={control}
        render={({ field }) => (
          <OpcionesMultiples
            leyenda="¿Tienes alguna de estas condiciones?"
            ayuda="Marca todas las que apliquen o «Ninguna»."
            opciones={CONDICIONES.map((c) => ({ valor: c, titulo: ETIQUETA_CONDICION[c] }))}
            valores={field.value ?? []}
            alCambiar={field.onChange}
            error={formState.errors.condiciones?.message}
          />
        )}
      />
      <Controller
        name="alergias"
        control={control}
        render={({ field }) => (
          <OpcionesMultiples
            leyenda="¿Alergias o intolerancias?"
            opciones={ALERGIAS.map((a) => ({ valor: a, titulo: ETIQUETA_ALERGIA[a] }))}
            valores={field.value ?? []}
            alCambiar={field.onChange}
            error={formState.errors.alergias?.message}
          />
        )}
      />
      {guardar.isError && <ErrorGuardado error={guardar.error} />}
      <div className="flex justify-end">
        <Button type="submit" variante="secundario" cargando={formState.isSubmitting}>
          Guardar condiciones
        </Button>
      </div>
    </form>
  )
}

function PanelEvaluacion({ salud }: { salud: CuestionarioSaludDto }) {
  const { nivel, restricciones } = salud.evaluacion
  return (
    <Seccion titulo="Lo que tendremos en cuenta">
      {!salud.usaDatosDeSalud && (
        <p className="mb-2 text-sm text-atenuado">
          Retiraste el permiso para usar tus datos de salud: tus respuestas siguen aquí, pero no las
          usamos.
        </p>
      )}
      <p className="text-sm">{ETIQUETA_PRECAUCION[nivel]}</p>
      {restricciones.length > 0 && (
        <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-texto-2">
          {restricciones.map((r) => (
            <li key={r}>{ETIQUETA_RESTRICCION[r]}</li>
          ))}
        </ul>
      )}
    </Seccion>
  )
}
