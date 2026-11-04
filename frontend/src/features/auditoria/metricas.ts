import { celdaCsv } from './csv'

// Contrato propuesto: sin endpoint en Host/WebApi. Métricas AGREGADAS Y ANÓNIMAS para el
// Administrador (docs/mejoras §6.4 y especificación §5.6): solo cifras y conteos, nunca datos
// de una persona concreta. Lo simula mocks/handlers/propuestas/admin.ts.
export interface MetricasDto {
  // Instante UTC (RD-11) en que se calcularon.
  generadoEn: string
  // Usuarios distintos con inicio de sesión en los últimos 30 días.
  usuariosActivos30d: number
  // Cuentas creadas en los últimos 7 días.
  registros7d: number
  // % de quienes se registraron hace más de 30 días y siguen activos; null si no hay a quién medir.
  retencion30d: number | null
  // Cuentas con eliminación pedida que siguen en periodo de gracia.
  eliminacionesEnCurso: number
  // Metas elegidas en los perfiles, de más a menos (solo el slug del catálogo y el conteo).
  planesMasIniciados: { slug: string; cantidad: number }[]
}

// CSV de las métricas para "Exportar CSV" (§5.6). Mismo escape que la auditoría.
export function metricasACsv(m: MetricasDto, nombrePlan: (slug: string) => string): string {
  const filas: (string | number | null)[][] = [
    ['Métrica', 'Valor'],
    ['Generado (UTC)', m.generadoEn],
    ['Usuarios activos (30 días)', m.usuariosActivos30d],
    ['Registros (7 días)', m.registros7d],
    ['Retención a 30 días (%)', m.retencion30d],
    ['Eliminaciones en curso', m.eliminacionesEnCurso],
    ...m.planesMasIniciados.map((p) => [`Plan iniciado: ${nombrePlan(p.slug)}`, p.cantidad]),
  ]
  return filas.map((f) => f.map(celdaCsv).join(',')).join('\r\n')
}
