import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useForm, type FieldErrors, type UseFormRegister } from 'react-hook-form'
import { Link, Navigate, useLocation } from 'react-router'
import { useCerrarSesion } from '@/features/auth/api'
import {
  INFO_LEGAL,
  METAS_CATALOGO,
  RUTAS_LEGALES,
  VERSION_PRIVACIDAD,
  VERSION_TERMINOS,
} from '@/features/publico/api'
import {
  etiquetaSexo,
  infoActividad,
  LIMITES_CORPORALES,
  NIVELES_ACTIVIDAD,
  SEXOS,
} from '@/features/publico/types'
import { esNoDisponible, mensajeDeError } from '@/shared/api/problem'
import { useAuth } from '@/shared/auth/AuthContext'
import { Alert } from '@/shared/ui/Alert'
import { AuthCard, claseEnlace } from '@/shared/ui/AuthCard'
import { BarraProgreso } from '@/shared/ui/BarraProgreso'
import { Button } from '@/shared/ui/Button'
import { Casilla } from '@/shared/ui/Casilla'
import { EmptyState } from '@/shared/ui/Card'
import { ConsentFieldset } from '@/shared/ui/ConsentFieldset'
import { Input, Select } from '@/shared/ui/Field'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useToast } from '@/shared/ui/Toast'
import {
  aceptoVersionesVigentes,
  borrarRegistroPendiente,
  esPerfilInexistente,
  leerRegistroPendiente,
  useGuardarConsentimientos,
  useGuardarPerfil,
  usePerfil,
  type RegistroPendiente,
} from '../api'
import {
  CAMPOS_POR_PASO,
  LIMITES_PLAN,
  onboardingSchema,
  SIN_META,
  type OnboardingForm,
} from '../schemas'

// Al terminar el onboarding el usuario aterriza en su panel, o vuelve a la página interna que
// pidió (RequierePerfil la pasa en state.desde).
export const DESTINO_TRAS_ONBOARDING = '/habitos'

function destinoPedido(state: unknown): string {
  const desde = (state as { desde?: unknown } | null)?.desde
  // Solo rutas internas: nada de //dominio ni /\dominio (redirección abierta).
  return typeof desde === 'string' &&
    desde.startsWith('/') &&
    !/^\/[\\/]/.test(desde) &&
    !desde.startsWith('/bienvenida')
    ? desde
    : DESTINO_TRAS_ONBOARDING
}

const TITULOS = ['Tus datos básicos', 'Tu actividad y tu tiempo', 'Antes de empezar'] as const

// Onboarding (docs/mejoras §3): pasos 3 a 5 del alta. Solo para cuentas sin perfil; quien ya
// lo completó va directo a su panel.
export function OnboardingPage() {
  const { usuario } = useAuth()
  const { state } = useLocation()
  const perfil = usePerfil()
  const destino = destinoPedido(state)

  if (perfil.isPending) return <CargandoPagina texto="Preparando tu plan…" />
  // También al terminar: guardar el perfil actualiza esta consulta y se navega desde aquí.
  if (perfil.isSuccess) return <Navigate to={destino} replace />
  if (esNoDisponible(perfil.error)) {
    return (
      <div className="w-full max-w-md">
        <EmptyState
          nivelTitulo={1}
          icono="calendario"
          titulo="El perfil llegará pronto"
          descripcion="Esta parte del alta aún no está disponible en el servidor. Mientras tanto puedes usar tus hábitos."
          accion={
            <Link to={DESTINO_TRAS_ONBOARDING} className={claseEnlace}>
              Ir a mis hábitos
            </Link>
          }
        />
      </div>
    )
  }
  if (!esPerfilInexistente(perfil.error)) {
    return (
      <AuthCard titulo="No pudimos cargar tu perfil">
        <Alert tono="peligro">{mensajeDeError(perfil.error, 'Inténtalo de nuevo.')}</Alert>
        <Button onClick={() => perfil.refetch()}>Reintentar</Button>
      </AuthCard>
    )
  }
  // La ruta está protegida por RequireAuth: siempre hay usuario.
  return <Asistente correo={usuario?.correo ?? ''} />
}

function Asistente({ correo }: { correo: string }) {
  const { salir } = useCerrarSesion()
  // Se lee una sola vez: si otra pestaña cambia el almacenamiento, el asistente no cambia a
  // mitad de camino.
  const [pendiente] = useState<RegistroPendiente | null>(() => leerRegistroPendiente(correo))
  const [paso, setPaso] = useState(0)
  const titulo = useRef<HTMLDivElement>(null)
  const guardarConsentimientos = useGuardarConsentimientos()
  const guardarPerfil = useGuardarPerfil()
  // Se vuelven a pedir los consentimientos si no hay registro recordado en este dispositivo o si
  // desde entonces se publicó otra versión de los términos o de la privacidad.
  const pedirConsentimiento = pendiente === null || !aceptoVersionesVigentes(pendiente)
  const esquema = useMemo(() => onboardingSchema(pedirConsentimiento), [pedirConsentimiento])
  const datos = pendiente?.datosCorporales

  const {
    register,
    handleSubmit,
    trigger,
    formState: { errors, isSubmitting },
  } = useForm<OnboardingForm>({
    resolver: zodResolver(esquema),
    defaultValues: {
      edad: datos?.edad,
      // Opcional: sin dato de la calculadora queda en "Prefiero no decirlo".
      sexo: datos?.sexo ?? 'no-decir',
      alturaCm: datos?.alturaCm,
      pesoKg: datos?.pesoKg,
      actividad: datos?.actividad,
      diasPorSemana: 3,
      minutosPorSesion: 30,
      // Del catálogo (tipo exacto del slug) o "Elegir más tarde".
      metaSlug: METAS_CATALOGO.find((m) => m.slug === pendiente?.metaSlug)?.slug ?? SIN_META,
      avisoSalud: false,
      terminos: false,
      privacidad: false,
      marketing: pendiente?.marketing ?? false,
    },
  })

  // Al cambiar de paso, el foco va al título para que se anuncie el paso nuevo.
  useEffect(() => {
    if (paso > 0) titulo.current?.querySelector('h1')?.focus()
  }, [paso])

  const siguiente = async () => {
    if (await trigger([...CAMPOS_POR_PASO[paso]], { shouldFocus: true })) setPaso((p) => p + 1)
  }
  const alertaError = useRef<HTMLDivElement>(null)
  const { notificar } = useToast()

  // Primero el consentimiento y después los datos de salud: el servidor no guarda un perfil
  // sin consentimiento (contrato propuesto).
  const enviar = handleSubmit(async (d) => {
    try {
      await guardarConsentimientos.mutateAsync({
        terminosVersion: VERSION_TERMINOS,
        privacidadVersion: VERSION_PRIVACIDAD,
        datosSalud: true,
        marketing: d.marketing,
        compartirCumplimiento: false,
        estadisticasAnonimas: false,
      })
      await guardarPerfil.mutateAsync({
        edad: d.edad,
        sexo: d.sexo === 'no-decir' ? null : d.sexo,
        alturaCm: d.alturaCm,
        pesoKg: d.pesoKg,
        actividad: d.actividad,
        diasPorSemana: d.diasPorSemana,
        minutosPorSesion: d.minutosPorSesion,
        metaSlug: d.metaSlug === SIN_META ? null : d.metaSlug,
        avisoSaludAceptado: true,
      })
      // Con el perfil guardado, OnboardingPage lleva al destino pedido.
      borrarRegistroPendiente()
      const meta = METAS_CATALOGO.find((m) => m.slug === d.metaSlug)
      notificar(
        meta ? `Listo: tu plan «${meta.nombre}» empieza hoy.` : 'Listo: tu perfil quedó guardado.',
      )
    } catch {
      // El error queda en el estado de la mutación y se muestra arriba del formulario.
    }
  })

  const errorEnvio = guardarConsentimientos.error ?? guardarPerfil.error
  // El error aparece junto al botón: se lleva a la vista y recibe el foco.
  useEffect(() => {
    if (errorEnvio) alertaError.current?.focus()
  }, [errorEnvio])

  return (
    <div ref={titulo} className="w-full max-w-md">
      <AuthCard
        titulo={TITULOS[paso]}
        paso={3 + paso}
        pie={
          <button type="button" onClick={salir} className={claseEnlace}>
            Cerrar sesión y terminar después
          </button>
        }
      >
        {/* Pasos completados: en el 3.º aún falta enviar, así que no marca 100 %. */}
        <BarraProgreso porcentaje={((2 + paso) / 5) * 100} etiqueta="Progreso del alta" />
        {/* Enter avanza de paso (o envía en el último). */}
        <form
          onSubmit={(e) => {
            e.preventDefault()
            if (paso < 2) void siguiente()
            else void enviar(e)
          }}
          noValidate
          className="space-y-4"
        >
          {paso === 0 && <PasoDatosBasicos register={register} errors={errors} />}
          {paso === 1 && <PasoActividad register={register} errors={errors} />}
          {paso === 2 && (
            <PasoAvisoSalud
              register={register}
              errors={errors}
              pedirConsentimiento={pedirConsentimiento}
            />
          )}

          {errorEnvio && (
            <div ref={alertaError} tabIndex={-1} className="outline-none">
              <Alert tono="peligro">
                {mensajeDeError(errorEnvio, 'No se pudo guardar tu plan.')} Tus datos siguen aquí:
                puedes volver a intentarlo.
              </Alert>
            </div>
          )}
          <div className="flex flex-col-reverse gap-3 pt-2 sm:flex-row">
            {paso > 0 && (
              <Button variante="secundario" tamano="lg" onClick={() => setPaso((p) => p - 1)}>
                Atrás
              </Button>
            )}
            <Button
              type="submit"
              tamano="lg"
              className="flex-1 whitespace-nowrap"
              cargando={isSubmitting}
            >
              {paso < 2 ? 'Continuar' : 'Empezar mi plan'}
            </Button>
          </div>
        </form>
      </AuthCard>
    </div>
  )
}

type PropsPaso = {
  register: UseFormRegister<OnboardingForm>
  errors: FieldErrors<OnboardingForm>
}

function PasoDatosBasicos({ register, errors }: PropsPaso) {
  const { edad, alturaCm, pesoKg } = LIMITES_CORPORALES
  return (
    <>
      <p className="text-sm text-atenuado">
        Con estos datos ajustamos tu plan. Solo tú los ves y puedes cambiarlos cuando quieras.
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
      <Select
        etiqueta="Sexo"
        ayuda="Opcional. Solo se usa para estimar tus calorías."
        error={errors.sexo?.message}
        {...register('sexo')}
      >
        {SEXOS.map((s) => (
          <option key={s} value={s}>
            {etiquetaSexo[s]}
          </option>
        ))}
        <option value="no-decir">Prefiero no decirlo</option>
      </Select>
      <div className="grid gap-4 sm:grid-cols-2">
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
      </div>
    </>
  )
}

function PasoActividad({ register, errors }: PropsPaso) {
  const { diasPorSemana, minutosPorSesion } = LIMITES_PLAN
  return (
    <>
      <Select
        etiqueta="Nivel de actividad"
        error={errors.actividad?.message}
        {...register('actividad')}
      >
        <option value="">Elige tu nivel de actividad</option>
        {NIVELES_ACTIVIDAD.map((n) => (
          <option key={n} value={n}>
            {infoActividad[n].etiqueta}
          </option>
        ))}
      </Select>
      <div className="grid gap-4 sm:grid-cols-2">
        <Input
          etiqueta="Días por semana"
          type="number"
          inputMode="numeric"
          min={diasPorSemana.min}
          max={diasPorSemana.max}
          error={errors.diasPorSemana?.message}
          {...register('diasPorSemana', { valueAsNumber: true })}
        />
        <Input
          etiqueta="Minutos por sesión"
          type="number"
          inputMode="numeric"
          min={minutosPorSesion.min}
          max={minutosPorSesion.max}
          error={errors.minutosPorSesion?.message}
          {...register('minutosPorSesion', { valueAsNumber: true })}
        />
      </div>
      <Select
        etiqueta="Tu meta"
        ayuda="Puedes cambiarla después desde tu perfil."
        error={errors.metaSlug?.message}
        {...register('metaSlug')}
      >
        {METAS_CATALOGO.map((m) => (
          <option key={m.slug} value={m.slug}>
            {m.nombre}
          </option>
        ))}
        <option value={SIN_META}>Elegir más tarde</option>
      </Select>
    </>
  )
}

function PasoAvisoSalud({
  register,
  errors,
  pedirConsentimiento,
}: PropsPaso & { pedirConsentimiento: boolean }) {
  return (
    <>
      <div className="rounded-xl border border-nota-borde bg-nota-fondo px-4 py-3 text-sm text-nota-texto">
        <p className="font-semibold">{INFO_LEGAL.salud.secciones[0].titulo}</p>
        {INFO_LEGAL.salud.secciones[0].parrafos.map((p) => (
          <p key={p} className="mt-1">
            {p}
          </p>
        ))}
      </div>
      <Casilla error={errors.avisoSalud?.message} {...register('avisoSalud')}>
        He leído el aviso de salud y entiendo que el plan es orientativo.
      </Casilla>
      {pedirConsentimiento && (
        <>
          <p className="text-sm text-atenuado">
            Confirma tus consentimientos: no encontramos los que aceptaste al registrarte en este
            dispositivo.
          </p>
          <ConsentFieldset
            rutas={RUTAS_LEGALES}
            versionTerminos={VERSION_TERMINOS}
            versionPrivacidad={VERSION_PRIVACIDAD}
            terminos={register('terminos')}
            privacidad={register('privacidad')}
            marketing={register('marketing')}
            errores={{ terminos: errors.terminos?.message, privacidad: errors.privacidad?.message }}
          />
        </>
      )}
    </>
  )
}
