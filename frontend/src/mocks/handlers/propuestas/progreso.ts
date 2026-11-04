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
import { db } from '../../db'
import { autenticar, latencia, leerJson, problema, respuestas } from '../../http'
import type { ContratoPropuesto } from './index'

// Contrato propuesto para el backend (mejora futura): peso y medidas corporales (docs/mejoras
// §4.5). Son datos de salud: sin el consentimiento correspondiente no se guardan (409), y cada
// usuario solo ve los suyos (RD-06).
export const contratosProgreso: ContratoPropuesto[] = [
  {
    metodo: 'GET',
    ruta: '/api/progress/weights',
    proposito: 'Pesos registrados del usuario y su peso objetivo.',
    roles: ['Estandar'],
    origen: 'mejoras §4.5 (peso y medidas)',
  },
  {
    metodo: 'POST',
    ruta: '/api/progress/weights',
    proposito: 'Registra un peso (y opcionalmente el objetivo). Exige consentimiento de salud.',
    roles: ['Estandar'],
    origen: 'mejoras §4.5 (peso y medidas)',
  },
  {
    metodo: 'GET',
    ruta: '/api/progress/measurements',
    proposito: 'Mediciones corporales del usuario (cintura, pecho, brazo y % de grasa).',
    roles: ['Estandar'],
    origen: 'mejoras §4.5 (peso y medidas)',
  },
  {
    metodo: 'POST',
    ruta: '/api/progress/measurements',
    proposito: 'Registra una medición. Exige consentimiento de salud.',
    roles: ['Estandar'],
    origen: 'mejoras §4.5 (peso y medidas)',
  },
]

// Un ISO válido que no sea del futuro (con un día de margen por las zonas horarias).
function fechaValida(v: unknown): v is string {
  if (typeof v !== 'string') return false
  const t = Date.parse(v)
  return Number.isFinite(t) && t >= Date.parse(FECHA_MINIMA) && t <= Date.now() + 86_400_000
}

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

const sinConsentimiento = (idUsuario: string) =>
  !db.consentimientos.get(idUsuario)?.datosSalud
    ? problema(
        409,
        'Falta el consentimiento.',
        'Acepta el tratamiento de datos de salud para guardar tu progreso.',
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
    const rechazo = sinConsentimiento(sesion.usuario.id)
    if (rechazo) return rechazo

    const actual = pesosDe(sesion.usuario.id)
    // Se guarda el instante en UTC: una fecha con otro desfase no cae en el día equivocado.
    const fecha = new Date(body.fecha).toISOString()
    const dia = fecha.slice(0, 10)
    // Un solo peso por día: registrar otro el mismo día lo reemplaza.
    const registros = actual.registros
      .filter((r) => r.fecha.slice(0, 10) !== dia)
      .concat({ id: crypto.randomUUID(), fecha, pesoKg: body.pesoKg })
      .sort((a, b) => Date.parse(a.fecha) - Date.parse(b.fecha))
    const siguiente: PesosDto = {
      objetivoKg: body.objetivoKg === undefined ? actual.objetivoKg : body.objetivoKg,
      registros,
    }
    db.pesos.set(sesion.usuario.id, siguiente)
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
    const rechazo = sinConsentimiento(sesion.usuario.id)
    if (rechazo) return rechazo

    const nueva: MedicionDto = {
      id: crypto.randomUUID(),
      fecha: new Date(body.fecha).toISOString(),
      zona: body.zona,
      valor: body.valor,
    }
    const lista = [...(db.mediciones.get(sesion.usuario.id) ?? []), nueva].sort(
      (a, b) => Date.parse(a.fecha) - Date.parse(b.fecha),
    )
    db.mediciones.set(sesion.usuario.id, lista)
    return HttpResponse.json(nueva, { status: 201 })
  }),
]
