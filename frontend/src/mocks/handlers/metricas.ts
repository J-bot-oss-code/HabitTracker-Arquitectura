import { http, HttpResponse } from 'msw'
import type { MetricasDto } from '@/features/auditoria/metricas'
import { db } from '../db'
import { autenticar, latencia } from '../http'

// Réplica de MetricasAdminController y MetricasProductoService (+ FuenteMetricasAdapter): solo el Administrador (401 sin
// sesión, 403 a un Estándar), sin parámetros, y cifras AGREGADAS: nunca un correo, un id ni un dato de salud. Lo que cuenta
// personas se oculta con menos de COHORTE_MINIMA: usuariosActivos30d y retencion30d llegan null y un plan con menos
// personas no aparece. Las cuentas nuevas, las bajas y las quejas nuevas no se ocultan.

const DIA = 86_400_000
export const COHORTE_MINIMA = 5
const MAXIMO_PLANES = 5

export const metricasHandlers = [
  http.get('/api/admin/metrics', async ({ request }) => {
    await latencia()
    const { error } = autenticar(request, ['Administrador'])
    if (error) return error

    const ahora = Date.now()
    const hasta = ahora + 1 // incluye el instante actual
    // Todos los avances registrados: los de las cuentas de la demo y los de la población simulada del piloto.
    const avances = [
      ...db.habitos.flatMap((h) =>
        h.registros.map((r) => ({ persona: h.usuarioId, instante: Date.parse(r.fecha) })),
      ),
      ...db.poblacionSimulada.registros.map((r) => ({
        persona: r.persona,
        instante: Date.parse(r.fecha),
      })),
    ]
    // Personas distintas con un avance en [desde, hasta).
    const personasEn = (desde: number, fin: number) =>
      new Set(avances.filter((a) => a.instante >= desde && a.instante < fin).map((a) => a.persona))

    const activas = personasEn(ahora - 30 * DIA, hasta)
    // Retención: de quienes registraron algo hace 30 a 60 días, cuántas también en los últimos 30.
    const cohorte = personasEn(ahora - 60 * DIA, ahora - 30 * DIA)
    const retenidas = [...cohorte].filter((persona) => activas.has(persona)).length

    const porPlan = new Map<string, number>()
    for (const slug of [
      ...db.planesUsuario.map((p) => p.slug),
      ...db.poblacionSimulada.planesIniciados,
    ]) {
      if (slug) porPlan.set(slug, (porPlan.get(slug) ?? 0) + 1)
    }

    const metricas: MetricasDto = {
      generadoEn: new Date(ahora).toISOString(),
      cohorteMinima: COHORTE_MINIMA,
      cuentasNuevas7d: db.actividad.filter(
        (e) => e.tipo === 'Registro' && ahora - Date.parse(e.fecha) <= 7 * DIA,
      ).length,
      eliminacionesEnCurso: [...db.bajas.values()].filter((b) => Date.parse(b.borradoEn) > ahora)
        .length,
      sugerenciasNuevas: db.sugerencias.filter((s) => s.estado === 'Nueva').length,
      registrosPorCorreo7d: db.registrosPorCorreo.filter((f) => Date.parse(f) >= ahora - 7 * DIA)
        .length,
      usuariosActivos30d: activas.size >= COHORTE_MINIMA ? activas.size : null,
      retencion30d:
        cohorte.size >= COHORTE_MINIMA ? Math.round((100 * retenidas) / cohorte.size) : null,
      planesMasIniciados: [...porPlan.entries()]
        .map(([slug, cantidad]) => ({ slug, cantidad }))
        .sort((a, b) => b.cantidad - a.cantidad || a.slug.localeCompare(b.slug))
        .slice(0, MAXIMO_PLANES)
        .filter((p) => p.cantidad >= COHORTE_MINIMA),
    }
    return HttpResponse.json(metricas)
  }),
]
