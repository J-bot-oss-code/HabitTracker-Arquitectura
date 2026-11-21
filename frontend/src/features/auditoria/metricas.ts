import { celdaCsv } from './csv'

// Un plan del catálogo y cuántas personas lo iniciaron (solo el slug y el número): PlanIniciadoDto.
export interface PlanIniciadoDto {
  slug: string
  cantidad: number
}

// GET /api/admin/metrics (MetricasProductoDto, solo Administrador): cifras AGREGADAS Y ANÓNIMAS del producto, nunca
// datos de una persona concreta. Réplica de Reports.Application/DTOs/ReportesDtos.cs.
export interface MetricasDto {
  // Instante UTC (RD-11) en que se calcularon.
  generadoEn: string
  // Personas mínimas para mostrar una cifra que cuenta personas (hoy 5).
  cohorteMinima: number
  // Cuentas creadas en los últimos 7 días.
  cuentasNuevas7d: number
  // Bajas pedidas que siguen en el periodo de gracia.
  eliminacionesEnCurso: number
  // Quejas y mejoras sin leer.
  sugerenciasNuevas: number
  // Avances registrados con el botón «Ya lo hice» del correo en los últimos 7 días.
  registrosPorCorreo7d: number
  // Personas distintas con algún registro de avance en 30 días. Null si son menos de `cohorteMinima`.
  usuariosActivos30d: number | null
  // De quienes registraron algo hace 30 a 60 días, % (entero 0–100) que también registró en los últimos 30.
  // Null si esa cohorte es menor de `cohorteMinima`.
  retencion30d: number | null
  // Hasta 5 planes, de más a menos; solo los que iniciaron al menos `cohorteMinima` personas (puede venir vacía).
  planesMasIniciados: PlanIniciadoDto[]
}

// CSV de las métricas para «Exportar CSV» (§5.6). Mismo escape que la auditoría. Una cifra oculta por la cohorte mínima
// sale vacía (nunca 0): igual que en la pantalla, no es lo mismo que «nadie».
export function metricasACsv(m: MetricasDto, nombrePlan: (slug: string) => string): string {
  // Tres columnas siempre; la nota explica por qué un valor sale vacío.
  const nota = `Oculta: menos de ${m.cohorteMinima} personas`
  const fila = (metrica: string, valor: string | number | null): (string | number | null)[] => [
    metrica,
    valor,
    valor === null ? nota : '',
  ]
  const filas: (string | number | null)[][] = [
    ['Métrica', 'Valor', 'Nota'],
    fila('Generado (UTC)', m.generadoEn),
    fila('Cohorte mínima (personas)', m.cohorteMinima),
    fila('Cuentas nuevas (7 días)', m.cuentasNuevas7d),
    fila('Personas activas (30 días)', m.usuariosActivos30d),
    fila('Retención a 30 días (%)', m.retencion30d),
    fila('Registros por correo (7 días)', m.registrosPorCorreo7d),
    fila('Bajas en curso', m.eliminacionesEnCurso),
    fila('Quejas y mejoras nuevas', m.sugerenciasNuevas),
    ...m.planesMasIniciados.map((p) => fila(`Plan iniciado: ${nombrePlan(p.slug)}`, p.cantidad)),
  ]
  return filas.map((f) => f.map(celdaCsv).join(',')).join('\r\n')
}
