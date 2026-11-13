import { http, HttpResponse } from 'msw'
import { ACCIONES_AUDITORIA, type AccionAuditoria } from '@/features/auditoria/types'
import type { MetricasDto } from '@/features/auditoria/metricas'
import { esDiaReal } from '@/features/auditoria/schemas'
import { db } from '../../db'
import { autenticar, latencia, respuestas } from '../../http'
import type { ContratoPropuesto } from './index'

// Contrato propuesto para el backend (mejora futura): panel de administración (docs/mejoras
// §6.1 y §6.4). Las métricas son agregadas y anónimas; la auditoría es de solo lectura.
export const contratosAdmin: ContratoPropuesto[] = [
  {
    metodo: 'GET',
    ruta: '/api/admin/metrics',
    proposito: 'Métricas agregadas y anónimas: activos, registros, retención y planes iniciados.',
    roles: ['Administrador'],
    origen: 'mejoras §6.4 (reportes del administrador)',
  },
  {
    metodo: 'GET',
    ruta: '/api/audit',
    proposito:
      'Registro de auditoría, filtrable por usuario, acción y rango de fechas (RF-AUD-07).',
    roles: ['Administrador'],
    origen: 'mejoras §6.1 (auditoría)',
  },
]

const DIA = 86_400_000
const LIMITE_POR_DEFECTO = 200
const LIMITE_MAXIMO = 500

const esDia = (v: string | null): v is string =>
  v !== null && /^\d{4}-\d{2}-\d{2}$/.test(v) && esDiaReal(v)

export const adminHandlers = [
  http.get('/api/admin/metrics', async ({ request }) => {
    await latencia()
    const { error } = autenticar(request, ['Administrador'])
    if (error) return error

    const ahora = Date.now()
    const eventos = db.auditoria
    const dentro = (fecha: string, dias: number) => ahora - Date.parse(fecha) <= dias * DIA

    const activos = new Set(
      eventos.filter((e) => e.accion === 'InicioSesion' && dentro(e.fecha, 30)).map((e) => e.actor),
    )
    const registros = eventos.filter((e) => e.accion === 'Registro')
    // Retención: de quienes se registraron hace más de 30 días, cuántos iniciaron sesión desde entonces.
    const antiguos = new Set(registros.filter((e) => !dentro(e.fecha, 30)).map((e) => e.actor))
    const retenidos = [...antiguos].filter((actor) => activos.has(actor)).length

    const porPlan = new Map<string, number>()
    for (const plan of db.planesUsuario) {
      if (plan.slug) porPlan.set(plan.slug, (porPlan.get(plan.slug) ?? 0) + 1)
    }

    const metricas: MetricasDto = {
      generadoEn: new Date(ahora).toISOString(),
      usuariosActivos30d: activos.size,
      registros7d: registros.filter((e) => dentro(e.fecha, 7)).length,
      retencion30d: antiguos.size === 0 ? null : Math.round((retenidos / antiguos.size) * 100),
      eliminacionesEnCurso: [...db.bajas.values()].filter((b) => Date.parse(b.borradoEn) > ahora)
        .length,
      planesMasIniciados: [...porPlan.entries()]
        .map(([slug, cantidad]) => ({ slug, cantidad }))
        .sort((a, b) => b.cantidad - a.cantidad),
    }
    return HttpResponse.json(metricas)
  }),

  http.get('/api/audit', async ({ request }) => {
    await latencia()
    const { error } = autenticar(request, ['Administrador'])
    if (error) return error

    const q = new URL(request.url).searchParams
    const actor = q.get('actor')?.trim().toLowerCase() ?? ''
    const accion = q.get('accion')
    const desde = q.get('desde')
    const hasta = q.get('hasta')
    const limite = Number(q.get('limite') ?? LIMITE_POR_DEFECTO)
    // RD-07: el "servidor" valida cada filtro antes de usarlo.
    if (
      actor.length > 150 ||
      (accion !== null && !ACCIONES_AUDITORIA.includes(accion as AccionAuditoria)) ||
      (desde !== null && !esDia(desde)) ||
      (hasta !== null && !esDia(hasta)) ||
      (desde !== null && hasta !== null && desde > hasta) ||
      !Number.isInteger(limite) ||
      limite < 1 ||
      limite > LIMITE_MAXIMO
    ) {
      return respuestas.invalida()
    }

    const desdeMs = desde ? Date.parse(`${desde}T00:00:00Z`) : -Infinity
    // "hasta" incluye todo ese día UTC.
    const hastaMs = hasta ? Date.parse(`${hasta}T00:00:00Z`) + DIA : Infinity
    const resultado = db.auditoria
      .filter((e) => {
        const t = Date.parse(e.fecha)
        return (
          t >= desdeMs &&
          t < hastaMs &&
          (!accion || e.accion === accion) &&
          (!actor || e.actor.toLowerCase().includes(actor))
        )
      })
      .sort((a, b) => Date.parse(b.fecha) - Date.parse(a.fecha))
      .slice(0, limite)
    return HttpResponse.json(resultado)
  }),
]
