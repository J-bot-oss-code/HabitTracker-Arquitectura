import { http, HttpResponse } from 'msw'
import { FECHA_MINIMA, LIMITES_MEDIDA } from '@/features/progreso/schemas'
import {
  ZONAS,
  type MedicionDto,
  type PesosDto,
  type RegistrarMedicionRequest,
  type RegistrarPesoRequest,
} from '@/features/progreso/types'
import { LIMITES_CORPORALES } from '@/features/publico/types'
import { db } from '../db'
import { autenticar, latencia, leerJson, problema, respuestas } from '../http'

// Réplica de ProgressController (HabitTracker v2, Fase 2): peso y medidas corporales. Son datos de
// salud: sin el consentimiento datosSalud no se guardan (409 consentimiento_salud_requerido) y cada
// usuario solo ve los suyos (RD-06). Un peso y una medida (por zona) por día UTC: el nuevo reemplaza.

// Mismo margen que el backend (FechasCorporales.ToleranciaReloj) para relojes un poco adelantados.
const TOLERANCIA_RELOJ_MS = 5 * 60_000

// Un ISO UTC válido desde 2000. Que no sea futura se comprueba aparte (400 fecha_futura).
function fechaValida(v: unknown): v is string {
  if (typeof v !== 'string' || !/Z$/.test(v)) return false
  const t = Date.parse(v)
  return Number.isFinite(t) && t >= Date.parse(FECHA_MINIMA)
}

const esFutura = (fecha: string) => Date.parse(fecha) > Date.now() + TOLERANCIA_RELOJ_MS
const fechaFutura = () => respuestas.codigo('fecha_futura', 'La fecha no puede ser futura.')

const enRango = (v: unknown, { min, max }: { min: number; max: number }) =>
  typeof v === 'number' && Number.isFinite(v) && v >= min && v <= max

function pesoValido(b: Partial<RegistrarPesoRequest> | null): b is RegistrarPesoRequest {
  return (
    !!b &&
    fechaValida(b.fecha) &&
    enRango(b.pesoKg, LIMITES_CORPORALES.pesoKg) &&
    (b.objetivoKg === undefined ||
      b.objetivoKg === null ||
      enRango(b.objetivoKg, LIMITES_CORPORALES.pesoKg))
  )
}

function medicionValida(
  b: Partial<RegistrarMedicionRequest> | null,
): b is RegistrarMedicionRequest {
  return (
    !!b &&
    fechaValida(b.fecha) &&
    b.zona !== undefined &&
    ZONAS.includes(b.zona) &&
    enRango(b.valor, LIMITES_MEDIDA[b.zona])
  )
}

export const sinConsentimientoSalud = (idUsuario: string) =>
  !db.consentimientos.get(idUsuario)?.datosSalud
    ? problema(
        409,
        'Falta el consentimiento.',
        'Falta el consentimiento para usar tus datos de salud.',
        'consentimiento_salud_requerido',
      )
    : null

const pesosDe = (id: string): PesosDto => db.pesos.get(id) ?? { objetivoKg: null, registros: [] }

export const progresoHandlers = [
  http.get('/api/progress/weights', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Estandar'])
    if (error) return error
    return HttpResponse.json(pesosDe(sesion.usuario.id))
  }),

  http.post('/api/progress/weights', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Estandar'])
    if (error) return error
    const body = await leerJson<RegistrarPesoRequest>(request)
    if (!pesoValido(body)) return respuestas.invalida()
    const rechazo = sinConsentimientoSalud(sesion.usuario.id)
    if (rechazo) return rechazo
    if (esFutura(body.fecha)) return fechaFutura()
    // Fijar un objetivo necesita el perfil, donde vive (400 perfil_requerido).
    const perfil = db.perfiles.get(sesion.usuario.id)
    if (body.objetivoKg !== undefined && !perfil) {
      return respuestas.codigo(
        'perfil_requerido',
        'Completa tu perfil antes de fijar un peso objetivo.',
      )
    }

    const actual = pesosDe(sesion.usuario.id)
    // Se guarda el instante en UTC: una fecha con otro desfase no cae en el día equivocado.
    const fecha = new Date(body.fecha).toISOString()
    const dia = fecha.slice(0, 10)
    // Un solo peso por día: registrar otro el mismo día lo reemplaza.
    // Como el backend, el peso del mismo día corrige el registro que ya había (mismo id y fecha).
    const delDia = actual.registros.find((r) => r.fecha.slice(0, 10) === dia)
    const registros = actual.registros
      .filter((r) => r !== delDia)
      .concat(
        delDia
          ? { ...delDia, pesoKg: body.pesoKg }
          : { id: crypto.randomUUID(), fecha, pesoKg: body.pesoKg },
      )
      .sort((a, b) => Date.parse(a.fecha) - Date.parse(b.fecha))
    const siguiente: PesosDto = {
      objetivoKg: body.objetivoKg === undefined ? actual.objetivoKg : body.objetivoKg,
      registros,
    }
    db.pesos.set(sesion.usuario.id, siguiente)
    // El peso del día más reciente pasa al perfil (un peso atrasado no lo pisa).
    if (perfil && registros[registros.length - 1]?.fecha.slice(0, 10) === dia) {
      perfil.pesoKg = body.pesoKg
      perfil.actualizadoEn = new Date().toISOString()
    }
    return HttpResponse.json(siguiente, { status: 201 })
  }),

  http.get('/api/progress/measurements', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Estandar'])
    if (error) return error
    return HttpResponse.json(db.mediciones.get(sesion.usuario.id) ?? [])
  }),

  http.post('/api/progress/measurements', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Estandar'])
    if (error) return error
    const body = await leerJson<RegistrarMedicionRequest>(request)
    if (!medicionValida(body)) return respuestas.invalida()
    const rechazo = sinConsentimientoSalud(sesion.usuario.id)
    if (rechazo) return rechazo
    if (esFutura(body.fecha)) return fechaFutura()

    const nueva: MedicionDto = {
      id: crypto.randomUUID(),
      fecha: new Date(body.fecha).toISOString(),
      zona: body.zona,
      valor: body.valor,
    }
    const dia = nueva.fecha.slice(0, 10)
    // Una medida por día UTC y zona: la nueva reemplaza a la del mismo día.
    const otras = (db.mediciones.get(sesion.usuario.id) ?? []).filter(
      (m) => !(m.zona === nueva.zona && m.fecha.slice(0, 10) === dia),
    )
    const lista = [...otras, nueva].sort((a, b) => Date.parse(a.fecha) - Date.parse(b.fecha))
    db.mediciones.set(sesion.usuario.id, lista)
    return HttpResponse.json(nueva, { status: 201 })
  }),
]
