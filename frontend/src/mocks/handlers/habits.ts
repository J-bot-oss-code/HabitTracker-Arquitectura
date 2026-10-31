import { http, HttpResponse } from 'msw'
import {
  ESTADOS_HABITO,
  PERIODICIDADES,
  type EstadoHabito,
  type HabitoDto,
  type Periodicidad,
} from '@/features/habits/types'
import { db, type HabitoMock } from '../db'
import { autenticar, comoFechaSql, esTexto, latencia, leerJson, respuestas } from '../http'
import { tienePermisoAplicado } from './permissions'

// --- Réplica de las reglas de HabitTracker.Domain (Habito + TransicionesHabito) ---

function inicioSemanaIso(ahora: Date): number {
  const dia = Date.UTC(ahora.getUTCFullYear(), ahora.getUTCMonth(), ahora.getUTCDate())
  return dia - ((ahora.getUTCDay() + 6) % 7) * 86_400_000
}

function cumpleMetaFinal(h: HabitoMock, ahora = new Date()): boolean {
  const inicioCiclo = Date.parse(h.fechaInicioCiclo)
  const desde =
    h.periodicidad === 'Diaria'
      ? Date.UTC(ahora.getUTCFullYear(), ahora.getUTCMonth(), ahora.getUTCDate())
      : inicioSemanaIso(ahora)
  const hasta = desde + (h.periodicidad === 'Diaria' ? 1 : 7) * 86_400_000
  const suma = h.registros
    .map((r) => ({ ...r, t: Date.parse(r.fecha) }))
    .filter((r) => r.t >= inicioCiclo && r.t >= desde && r.t < hasta)
    .reduce((total, r) => total + r.cantidad, 0)
  return suma >= h.cantidadObjetivo
}

function esTransicionValida(
  desde: EstadoHabito,
  hacia: EstadoHabito,
  cumpleMeta: boolean,
): boolean {
  if (desde === 'Completado') return false
  if (desde === 'Planificado') return hacia === 'Activo'
  if (desde === 'Activo') return hacia === 'Pausado' || (hacia === 'Completado' && cumpleMeta)
  if (desde === 'Pausado') return hacia === 'Activo'
  return false
}

function aDto(h: HabitoMock): HabitoDto {
  return {
    id: h.id,
    nombre: h.nombre,
    periodicidad: h.periodicidad,
    cantidadObjetivo: h.cantidadObjetivo,
    unidad: h.unidad,
    estado: h.estado,
    cumpleMetaFinal: cumpleMetaFinal(h),
    fechaInicioCiclo: comoFechaSql(h.fechaInicioCiclo),
    registros: h.registros.map((r) => ({ ...r, fecha: comoFechaSql(r.fecha) })),
  }
}

const esPeriodicidad = (v: unknown): v is Periodicidad =>
  (PERIODICIDADES as readonly unknown[]).includes(v)
const esCantidad = (v: unknown): v is number =>
  typeof v === 'number' && Number.isFinite(v) && v >= 0.01
const esGuid = (v: unknown): v is string =>
  typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(v)

// HabitoService.ObtenerHabitoPropietarioAsync: 404 si no existe, 403 si es de otro usuario.
function habitoDelUsuario(id: string, usuarioId: string): HabitoMock | Response {
  const habito = db.habitos.find((h) => h.id === id)
  if (!habito) return respuestas.noEncontrado('El hábito no existe.')
  if (habito.usuarioId !== usuarioId) return respuestas.prohibido()
  return habito
}

// [Authorize(Roles = "Estandar")]: el Administrador recibe 403.
const ROLES = ['Estandar' as const]

export const habitsHandlers = [
  http.get('/api/habits', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    return HttpResponse.json(db.habitos.filter((h) => h.usuarioId === sesion.usuario.id).map(aDto))
  }),

  http.post('/api/habits', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const body = await leerJson<{
      nombre: string
      periodicidad: Periodicidad
      cantidadObjetivo: number
      unidad: string
      metaId: string
    }>(request)
    if (
      !body ||
      !esTexto(body.nombre, 1, 200) ||
      !esPeriodicidad(body.periodicidad) ||
      !esCantidad(body.cantidadObjetivo) ||
      !esTexto(body.unidad, 1, 50) ||
      !esGuid(body.metaId) ||
      !db.metas.has(body.metaId.toLowerCase())
    ) {
      return respuestas.invalida()
    }
    const habito: HabitoMock = {
      id: crypto.randomUUID(),
      usuarioId: sesion.usuario.id,
      nombre: body.nombre.trim(),
      periodicidad: body.periodicidad,
      cantidadObjetivo: body.cantidadObjetivo,
      unidad: body.unidad.trim(),
      estado: 'Planificado',
      metaId: body.metaId.toLowerCase(),
      fechaInicioCiclo: new Date().toISOString(),
      registros: [],
    }
    db.habitos.push(habito)
    return HttpResponse.json({ id: habito.id }, { status: 201 })
  }),

  http.post<{ id: string }>('/api/habits/:id/transition', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const body = await leerJson<{ nuevoEstado: EstadoHabito }>(request)
    if (!body || !(ESTADOS_HABITO as readonly unknown[]).includes(body.nuevoEstado)) {
      return respuestas.invalida()
    }
    const habito = habitoDelUsuario(params.id, sesion.usuario.id)
    if (habito instanceof Response) return habito

    const nuevo = body.nuevoEstado as EstadoHabito
    const cumple =
      habito.estado === 'Activo' && nuevo === 'Completado' ? cumpleMetaFinal(habito) : false
    if (nuevo === 'Planificado' || !esTransicionValida(habito.estado, nuevo, cumple)) {
      return respuestas.operacion()
    }
    habito.estado = nuevo
    return new HttpResponse(null, { status: 204 })
  }),

  http.post<{ id: string }>('/api/habits/:id/progress', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const body = await leerJson<{ cantidad: number; fechaUtc: string }>(request)
    const fecha = typeof body?.fechaUtc === 'string' ? body.fechaUtc : ''
    // El backend exige DateTimeKind.Utc: la fecha debe llevar "Z".
    if (
      !body ||
      !esCantidad(body.cantidad) ||
      !/Z$/.test(fecha) ||
      Number.isNaN(Date.parse(fecha))
    ) {
      return respuestas.invalida()
    }
    const habito = habitoDelUsuario(params.id, sesion.usuario.id)
    if (habito instanceof Response) return habito
    if (habito.estado !== 'Activo') return respuestas.operacion()

    habito.registros.push({
      id: crypto.randomUUID(),
      cantidad: body.cantidad,
      fecha: new Date(fecha).toISOString(),
      unidad: habito.unidad,
    })
    return new HttpResponse(null, { status: 204 })
  }),

  http.post<{ id: string }>('/api/habits/:id/advanced-reconfig', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const body = await leerJson<{
      periodicidad: Periodicidad
      cantidadObjetivo: number
      unidad: string
    }>(request)
    if (
      !body ||
      !esPeriodicidad(body.periodicidad) ||
      !esCantidad(body.cantidadObjetivo) ||
      !esTexto(body.unidad, 1, 50)
    ) {
      return respuestas.invalida()
    }
    const habito = habitoDelUsuario(params.id, sesion.usuario.id)
    if (habito instanceof Response) return habito
    if (habito.estado === 'Completado') return respuestas.operacion()
    if (!tienePermisoAplicado(sesion.usuario.correo, 'Habits.AdvancedReconfig')) {
      return respuestas.prohibido()
    }
    habito.periodicidad = body.periodicidad
    habito.cantidadObjetivo = body.cantidadObjetivo
    habito.unidad = body.unidad.trim()
    habito.fechaInicioCiclo = new Date().toISOString()
    return new HttpResponse(null, { status: 204 })
  }),
]
