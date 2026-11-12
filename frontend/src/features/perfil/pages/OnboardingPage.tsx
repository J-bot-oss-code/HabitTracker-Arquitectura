import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useMemo, useRef, useState } from 'react'
import { Controller, useForm, type FieldErrors, type UseFormRegister } from 'react-hook-form'
import { Link, Navigate, useLocation } from 'react-router'
import { useCerrarSesion } from '@/features/auth/api'
import { usePlanes } from '@/features/planes/api'
import {
  INFO_LEGAL,
  RUTAS_LEGALES,
  VERSION_PRIVACIDAD,
  VERSION_TERMINOS,
} from '@/features/publico/api'
import { esErrorHttp, esNoDisponible, mensajeDeError } from '@/shared/api/problem'
import { useAuth } from '@/shared/auth/AuthContext'
import { Alert } from '@/shared/ui/Alert'
import { AuthCard, claseEnlace } from '@/shared/ui/AuthCard'
import { BarraProgreso } from '@/shared/ui/BarraProgreso'
import { Button } from '@/shared/ui/Button'
import { Casilla } from '@/shared/ui/Casilla'
import { EmptyState } from '@/shared/ui/Card'
import { ConsentFieldset } from '@/shared/ui/ConsentFieldset'
import { Select } from '@/shared/ui/Field'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useToast } from '@/shared/ui/Toast'
import {
  aceptoVersionesVigentes,
  borrarRegistroPendiente,
  esPerfilInexistente,
  leerRegistroPendiente,
  useConsentimientos,
  useGuardarConsentimientos,
  useGuardarPerfil,
  usePerfil,
  type RegistroPendiente,
} from '../api'
import { OpcionesTarjeta } from '../components/Opciones'
import { ETIQUETA_PERFIL_LABORAL, ZONAS_HORARIAS_COMUNES, zonaDelNavegador } from '../etiquetas'
import { CAMPOS_POR_PASO, onboardingSchema, type OnboardingForm } from '../schemas'
import { PERFILES_LABORALES } from '../types'

// Al terminar el onboarding el usuario aterriza en su panel, o vuelve a la página interna que
// pidió (RequierePerfil la pasa en state.desde).
export const DESTINO_TRAS_ONBOARDING = '/hoy'

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

const TITULOS = ['¿Cómo es tu jornada?', 'Tu zona horaria', 'Antes de empezar'] as const

// Onboarding (docs/mejoras §3): pasos 3 a 5 del alta, en 3 toques. Solo para cuentas sin perfil; quien
// ya lo completó va directo a su panel.
export function OnboardingPage() {
  const { usuario } = useAuth()
  const { state } = useLocation()
  const perfil = usePerfil()
  const destino = destinoPedido(state)

  if (perfil.isPending) return <CargandoPagina texto="Preparando tu día…" />
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
              Ir a mi panel
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

// Onboarding de 3 toques (P1, P2): jornada → zona horaria → aviso de salud. No pide edad, peso ni
// el cuestionario de salud: eso se pide solo cuando un plan lo necesita (Configuración › Perfil y salud).
function Asistente({ correo }: { correo: string }) {
  const { salir } = useCerrarSesion()
  // Se lee una sola vez: si otra pestaña cambia el almacenamiento, el asistente no cambia a
  // mitad de camino.
  const [pendiente] = useState<RegistroPendiente | null>(() => leerRegistroPendiente(correo))
  const [paso, setPaso] = useState(0)
  const titulo = useRef<HTMLDivElement>(null)
  const consentimientos = useConsentimientos()
  const guardarConsentimientos = useGuardarConsentimientos()
  const guardarPerfil = useGuardarPerfil()
  // Catálogo del servidor: el plan recordado del registro solo se guarda si existe.
  const planes = usePlanes()
  // Se vuelven a pedir los consentimientos si no hay registro recordado en este dispositivo o si
  // desde entonces se publicó otra versión de los términos o de la privacidad.
  const pedirConsentimiento = pendiente === null || !aceptoVersionesVigentes(pendiente)
  const esquema = useMemo(() => onboardingSchema(pedirConsentimiento), [pedirConsentimiento])
  const zonaDetectada = useMemo(() => zonaDelNavegador(), [])

  const {
    register,
    handleSubmit,
    trigger,
    control,
    formState: { errors, isSubmitting },
  } = useForm<OnboardingForm>({
    resolver: zodResolver(esquema),
    defaultValues: {
      zonaHoraria: zonaDetectada,
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

  // Para conservar los permisos que ya existían hay que haberlos leído (un 404 es "nunca dio ninguno").
  const consentimientosLeidos = consentimientos.isSuccess || esErrorHttp(consentimientos.error, 404)
  const [sinConsentimientos, setSinConsentimientos] = useState(false)

  const enviar = handleSubmit(async (d) => {
    if (pedirConsentimiento && !consentimientosLeidos) {
      setSinConsentimientos(true)
      void consentimientos.refetch()
      return
    }
    setSinConsentimientos(false)
    try {
      if (pedirConsentimiento) {
        // Solo términos, privacidad y marketing: el de datos de salud se conserva como estaba (no se
        // da por hecho en el alta, P1).
        await guardarConsentimientos.mutateAsync({
          terminosVersion: VERSION_TERMINOS,
          privacidadVersion: VERSION_PRIVACIDAD,
          datosSalud: consentimientos.data?.datosSalud ?? false,
          marketing: d.marketing,
          compartirCumplimiento: consentimientos.data?.compartirCumplimiento ?? false,
          estadisticasAnonimas: consentimientos.data?.estadisticasAnonimas ?? false,
        })
      }
      await guardarPerfil.mutateAsync({
        ...PERFIL_VACIO,
        perfilLaboral: d.perfilLaboral,
        zonaHoraria: d.zonaHoraria,
        avisoSaludAceptado: true,
        // El plan que eligió en la landing queda como interés (lo usa "Hoy").
        // Ya viene validado por formato; con el catálogo cargado también debe existir. Si no cargó, se conserva.
        metaSlug: planes.data
          ? (planes.data.find((p) => p.slug === pendiente?.metaSlug)?.slug ?? null)
          : (pendiente?.metaSlug ?? null),
      })
      // Con el perfil guardado, OnboardingPage lleva al destino pedido.
      borrarRegistroPendiente()
      notificar('Listo: ya puedes empezar tu día.')
    } catch {
      // El error queda en el estado de la mutación y se muestra arriba del botón.
    }
  })

  const errorEnvio = guardarConsentimientos.error ?? guardarPerfil.error
  // El error aparece junto al botón: se lleva a la vista y recibe el foco.
  useEffect(() => {
    if (errorEnvio) alertaError.current?.focus()
  }, [errorEnvio])

  const zonas = ZONAS_HORARIAS_COMUNES.includes(
    zonaDetectada as (typeof ZONAS_HORARIAS_COMUNES)[number],
  )
    ? ZONAS_HORARIAS_COMUNES
    : [zonaDetectada, ...ZONAS_HORARIAS_COMUNES]

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
          {paso === 0 && (
            <>
              <p className="text-sm text-atenuado">
                Armamos tu día saludable alrededor de tu trabajo: agua, pausas y descanso a tu
                ritmo.
              </p>
              <Controller
                name="perfilLaboral"
                control={control}
                render={({ field }) => (
                  <OpcionesTarjeta
                    leyenda="Elige la que más se parece a tu día"
                    opciones={PERFILES_LABORALES.map((p) => ({
                      valor: p,
                      ...ETIQUETA_PERFIL_LABORAL[p],
                    }))}
                    valor={field.value}
                    alCambiar={field.onChange}
                    error={errors.perfilLaboral?.message}
                  />
                )}
              />
            </>
          )}
          {paso === 1 && (
            <>
              <p className="text-sm text-atenuado">
                La usamos para que tus recordatorios lleguen a tu hora. La detectamos de tu
                dispositivo: cámbiala solo si no es la tuya.
              </p>
              <Select
                etiqueta="Zona horaria"
                error={errors.zonaHoraria?.message}
                {...register('zonaHoraria')}
              >
                {zonas.map((z) => (
                  <option key={z} value={z}>
                    {z.replaceAll('_', ' ')}
                    {z === zonaDetectada ? ' (detectada)' : ''}
                  </option>
                ))}
              </Select>
            </>
          )}
          {paso === 2 && (
            <PasoAvisoSalud
              register={register}
              errors={errors}
              pedirConsentimiento={pedirConsentimiento}
            />
          )}

          {sinConsentimientos && (
            <Alert tono="peligro">
              No pudimos leer tus consentimientos actuales. Espera un momento y vuelve a intentarlo.
            </Alert>
          )}
          {errorEnvio && (
            <div ref={alertaError} tabIndex={-1} className="outline-none">
              <Alert tono="peligro">
                {mensajeDeError(errorEnvio, 'No se pudo guardar tu perfil.')} Tus respuestas siguen
                aquí: puedes volver a intentarlo.
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
              {paso < 2 ? 'Continuar' : 'Empezar mi día'}
            </Button>
          </div>
        </form>
      </AuthCard>
    </div>
  )
}

// Campos del nivel 2 vacíos: el onboarding solo guarda el nivel 1.
const PERFIL_VACIO = {
  horaEntradaTrabajo: null,
  horaSalidaTrabajo: null,
  horaDespertar: null,
  horaDormir: null,
  edad: null,
  sexo: null,
  alturaCm: null,
  pesoKg: null,
  actividad: null,
  diasPorSemana: null,
  minutosPorSesion: null,
} as const

type PropsPaso = {
  register: UseFormRegister<OnboardingForm>
  errors: FieldErrors<OnboardingForm>
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
