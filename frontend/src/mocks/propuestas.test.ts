import { matchRequestUrl, type HttpHandler } from 'msw'
import { describe, expect, it } from 'vitest'
import { adminUsersHandlers } from './handlers/adminUsers'
import { authHandlers } from './handlers/auth'
import { habitsHandlers } from './handlers/habits'
import { permissionsHandlers } from './handlers/permissions'
import { CONTRATOS_PROPUESTOS, propuestasHandlers } from './handlers/propuestas'

// El registro de contratos propuestos es la lista de lo que el backend tendrá que implementar
// después de las prácticas: debe coincidir exactamente con los handlers simulados.
const firma = (h: HttpHandler) => `${String(h.info.method)} ${String(h.info.path)}`

const reales = [...authHandlers, ...habitsHandlers, ...permissionsHandlers, ...adminUsersHandlers]

// MSW responde con el primer handler que coincide y las propuestas van al final: un handler
// real con parámetros (p. ej. GET /api/permissions/:id) capturaría /api/permissions/resumen.
// Devuelve la firma del handler real que atendería la ruta propuesta, si existe.
function capturadoPorReal(metodo: string, ruta: string): string | undefined {
  // Ruta concreta para una ruta con parámetros (/api/x/:id → /api/x/ejemplo).
  const url = new URL(ruta.replace(/:\w+/g, 'ejemplo'), 'http://localhost')
  const captura = reales.find((h) => {
    const patron = h.info.path
    // info.path también puede ser un predicado; los handlers de este proyecto usan rutas.
    if (typeof patron === 'function' || h.info.method !== metodo) return false
    return matchRequestUrl(url, patron, 'http://localhost').matches
  })
  return captura && firma(captura)
}

describe('contratos propuestos (endpoints sin backend)', () => {
  it('cada handler propuesto está documentado en CONTRATOS_PROPUESTOS y viceversa', () => {
    const handlers = propuestasHandlers.map(firma).sort()
    const contratos = CONTRATOS_PROPUESTOS.map((c) => `${c.metodo} ${c.ruta}`).sort()
    expect(handlers).toEqual(contratos)
  })

  it('ninguna propuesta queda capturada por un endpoint real de Host/WebApi', () => {
    for (const contrato of CONTRATOS_PROPUESTOS) {
      expect(
        capturadoPorReal(contrato.metodo, contrato.ruta),
        `${contrato.metodo} ${contrato.ruta}`,
      ).toBeUndefined()
    }
  })

  it('cada contrato declara propósito, roles y su origen en docs/mejoras', () => {
    for (const contrato of CONTRATOS_PROPUESTOS) {
      expect(contrato.ruta).toMatch(/^\/api\//)
      expect(contrato.proposito.length).toBeGreaterThan(0)
      expect(contrato.roles === 'publico' || contrato.roles.length > 0).toBe(true)
      expect(contrato.origen.length).toBeGreaterThan(0)
    }
  })

  it('la comprobación detecta una ruta que pisaría un parámetro real', () => {
    expect(capturadoPorReal('GET', '/api/permissions/resumen')).toBe('GET /api/permissions/:id')
    expect(capturadoPorReal('GET', '/api/profile')).toBeUndefined()
  })
})
