import { http, HttpResponse } from 'msw'
import type { MetricasDto } from '@/features/auditoria/metricas'
import { db } from '../../db'
import { autenticar, latencia } from '../../http'
import type { ContratoPropuesto } from './index'

// Contrato propuesto para el backend (mejora futura): métricas del panel de administración
// (docs/mejoras §6.4), agregadas y anónimas. GET /api/audit ya es real: mocks/handlers/auditoria.ts.
export const contratosAdmin: ContratoPropuesto[] = [
  {
    metodo: 'GET',
    ruta: '/api/admin/metrics',
    proposito: 'Métricas agregadas y anónimas: activos, registros, retención y planes iniciados.',
    roles: ['Administrador'],
    origen: 'mejoras §6.4 (reportes del administrador)',
  },
]

const DIA = 86_400_000

export const adminHandlers = [
  http.get('/api/admin/metrics', async ({ request }) => {
    await latencia()
    const { error } = autenticar(request, ['Administrador'])
    if (error) return error

    const ahora = Date.now()
    // El backend real no audita registros ni inicios de sesión: la actividad se lleva aparte.
    const eventos = db.actividad
    const dentro = (fecha: string, dias: number) => ahora - Date.parse(fecha) <= dias * DIA

    const activos = new Set(
      eventos.filter((e) => e.tipo === 'InicioSesion' && dentro(e.fecha, 30)).map((e) => e.actor),
    )
    const registros = eventos.filter((e) => e.tipo === 'Registro')
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
]
