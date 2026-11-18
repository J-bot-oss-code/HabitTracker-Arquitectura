import { z } from 'zod'

// ReportesService.ValidarRango: ambos días son obligatorios (AAAA-MM-DD), "hasta" no es anterior a "desde",
// el rango incluye los dos días y no pasa de 366.
export const MAXIMO_DIAS_RANGO = 366
const MS_DIA = 86_400_000

const aMs = (v: string) => Date.parse(`${v}T00:00:00Z`)

const esDiaReal = (v: string) => {
  const t = aMs(v)
  return Number.isFinite(t) && new Date(t).toISOString().startsWith(v)
}

// Días del rango contando ambos extremos.
export const diasDelRango = (desde: string, hasta: string) =>
  Math.round((aMs(hasta) - aMs(desde)) / MS_DIA) + 1

const FORMATO = /^\d{4}-\d{2}-\d{2}$/
// El servidor rechaza los años anteriores al 2000.
const ANIO_MINIMO = 2000
const esValido = (v: string) =>
  FORMATO.test(v) && esDiaReal(v) && Number(v.slice(0, 4)) >= ANIO_MINIMO

// Un solo mensaje por campo; las reglas del rango completo solo se evalúan con ambos días válidos.
const dia = (campo: string) =>
  z.string().superRefine((v, ctx) => {
    if (v === '') ctx.addIssue({ code: 'custom', message: `Indica el día ${campo}.` })
    else if (!FORMATO.test(v) || !esDiaReal(v))
      ctx.addIssue({ code: 'custom', message: 'Elige una fecha válida.' })
    else if (!esValido(v))
      ctx.addIssue({ code: 'custom', message: `Elige una fecha a partir del año ${ANIO_MINIMO}.` })
  })

export const rangoReporteSchema = z
  .object({ desde: dia('inicial'), hasta: dia('final') })
  .refine((r) => !esValido(r.desde) || !esValido(r.hasta) || aMs(r.hasta) >= aMs(r.desde), {
    message: 'La fecha final no puede ser anterior a la inicial.',
    path: ['hasta'],
  })
  .refine(
    (r) =>
      !esValido(r.desde) ||
      !esValido(r.hasta) ||
      aMs(r.hasta) < aMs(r.desde) ||
      diasDelRango(r.desde, r.hasta) <= MAXIMO_DIAS_RANGO,
    { message: `El período no puede pasar de ${MAXIMO_DIAS_RANGO} días.`, path: ['hasta'] },
  )

export type RangoReporteForm = z.infer<typeof rangoReporteSchema>
