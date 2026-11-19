import { http, HttpResponse } from 'msw'
import { ESTADOS_SOLICITUD } from '@/features/permissions/types'
import type {
  FilaConstanciaDto,
  FilaPermisosDto,
  ReporteConstanciaDto,
  ReportePermisosDto,
} from '@/features/reportes/types'
import { db } from '../db'
import { autenticar, latencia, problema } from '../http'
import { diaLocal, zonaDe } from './misPlanes'

const DIA_MS = 86_400_000
const MAXIMO_DIAS_DEL_RANGO = 366
const PREGUNTA_CONSTANCIA = '¿Qué tan constante fui en cada plan durante este período?'

const argumentoInvalido = () =>
  problema(400, 'Solicitud inválida.', 'La solicitud contiene datos no válidos.')

// El binder de DateOnly rechaza un valor mal formado con ValidationProblemDetails (400).
const fechaMalFormada = (campo: string) =>
  HttpResponse.json(
    {
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: { [campo]: [`The value is not valid for ${campo}.`] },
    },
    { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
  )

const aDia = (texto: string) => {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(texto)) return null
  const ms = Date.parse(`${texto}T00:00:00Z`)
  return Number.isFinite(ms) && new Date(ms).toISOString().startsWith(texto) ? ms / DIA_MS : null
}

// ReportesService.ValidarRango. Devuelve el rango como número de día (desde 1970-01-01) o la respuesta 400.
function leerRango(url: URL) {
  const textos = { desde: url.searchParams.get('desde'), hasta: url.searchParams.get('hasta') }
  const dias = {
    desde: textos.desde ? aDia(textos.desde) : null,
    hasta: textos.hasta ? aDia(textos.hasta) : null,
  }
  for (const campo of ['desde', 'hasta'] as const) {
    if (textos[campo] && dias[campo] === null) return { error: fechaMalFormada(campo) }
  }
  const { desde, hasta } = dias
  if (desde === null || hasta === null) return { error: argumentoInvalido() }
  // Años anteriores al 2000, rango invertido o de más de 366 días (ambos días incluidos).
  const anio = (dia: number) => new Date(dia * DIA_MS).getUTCFullYear()
  if (
    anio(desde) < 2000 ||
    anio(hasta) < 2000 ||
    hasta < desde ||
    hasta - desde + 1 > MAXIMO_DIAS_DEL_RANGO
  )
    return { error: argumentoInvalido() }
  return { desde, hasta, textoDesde: textos.desde!, textoHasta: textos.hasta! }
}

// Math.Round(decimal, 1) de .NET redondea la mitad al par; Math.round de JS, hacia arriba.
function redondear(valor: number): number {
  const x = valor * 10
  const r = Math.round(x)
  return (Math.abs(x % 1) === 0.5 && r % 2 !== 0 ? r - 1 : r) / 10
}

interface Avance {
  dia: number
  minima: boolean
}
interface Pausa {
  desde: number
  hasta: number | null
}

// CalculadoraConstancia.Calcular: de los días del rango en que el plan estaba en marcha (sin los de pausa),
// en cuántos hubo avance y en cuántos solo la versión mínima.
function calcular(
  desde: number,
  hasta: number,
  inicioPlan: number,
  finPlan: number,
  hoy: number,
  pausas: Pausa[],
  avances: Avance[],
) {
  const primero = Math.max(desde, inicioPlan)
  const ultimo = Math.min(hasta, finPlan, hoy)
  let diasEnRango = 0
  let diasConAvance = 0
  let diasSoloMinimo = 0
  for (let dia = primero; dia <= ultimo; dia++) {
    if (pausas.some((p) => dia >= p.desde && (p.hasta === null || dia <= p.hasta))) continue
    diasEnRango++
    const delDia = avances.filter((a) => a.dia === dia)
    if (delDia.length > 0) {
      diasConAvance++
      if (delDia.every((a) => a.minima)) diasSoloMinimo++
    }
  }
  return { diasEnRango, diasConAvance, diasSoloMinimo }
}

const porcentaje = (conAvance: number, enRango: number) =>
  enRango === 0 ? null : redondear((conAvance * 100) / enRango)

// Un Estándar: sus planes (no cancelados) con los días locales de su zona.
function constanciaPersonal(usuarioId: string, desde: number, hasta: number): FilaConstanciaDto[] {
  const zona = zonaDe(usuarioId)
  const dia = (iso: string) => diaLocal(zona, Date.parse(iso))
  const hoy = diaLocal(zona, Date.now())
  const filas: FilaConstanciaDto[] = []
  const planes = db.planesUsuario
    .filter((p) => p.usuarioId === usuarioId && p.estado !== 'Cancelado')
    .sort((a, b) => Date.parse(a.fechaInicio) - Date.parse(b.fechaInicio))
  for (const plan of planes) {
    const avances = db.habitos
      .filter((h) => h.planUsuarioId === plan.id)
      .flatMap((h) =>
        h.registros.map((r) => ({ dia: dia(r.fecha), minima: r.esVersionMinima === true })),
      )
    const pausas = plan.pausas.map((x) => ({
      desde: dia(x.inicioUtc),
      hasta: x.finUtc === null ? null : dia(x.finUtc),
    }))
    const c = calcular(
      desde,
      hasta,
      dia(plan.fechaInicio),
      dia(plan.fechaFinPrevista),
      hoy,
      pausas,
      avances,
    )
    // Un plan que no estuvo en marcha en el rango no dice nada del rango.
    if (c.diasEnRango > 0)
      filas.push({
        plan: plan.nombre,
        personas: 1,
        ...c,
        porcentajeConstancia: porcentaje(c.diasConAvance, c.diasEnRango),
      })
  }
  return filas
}

// El Administrador: un plan del catálogo con las personas que lo siguieron, por día UTC y sin datos personales.
function constanciaGlobal(desde: number, hasta: number): FilaConstanciaDto[] {
  const dia = (iso: string) => Math.floor(Date.parse(iso) / DIA_MS)
  const hoy = Math.floor(Date.now() / DIA_MS)
  const grupos = new Map<
    string,
    { usuarios: Set<string>; calculos: ReturnType<typeof calcular>[] }
  >()
  for (const plan of db.planesUsuario) {
    // ReporteConstanciaRepository: no cancelados que se solapan con el rango.
    if (
      plan.estado === 'Cancelado' ||
      dia(plan.fechaInicio) > hasta ||
      dia(plan.fechaFinPrevista) < desde
    )
      continue
    const avances = db.habitos
      .filter((h) => h.planUsuarioId === plan.id)
      .flatMap((h) =>
        h.registros.map((r) => ({ dia: dia(r.fecha), minima: r.esVersionMinima === true })),
      )
    const pausas = plan.pausas.map((x) => ({
      desde: dia(x.inicioUtc),
      hasta: x.finUtc === null ? null : dia(x.finUtc),
    }))
    const c = calcular(
      desde,
      hasta,
      dia(plan.fechaInicio),
      dia(plan.fechaFinPrevista),
      hoy,
      pausas,
      avances,
    )
    if (c.diasEnRango === 0) continue
    const grupo = grupos.get(plan.nombre) ?? { usuarios: new Set<string>(), calculos: [] }
    grupo.usuarios.add(plan.usuarioId)
    grupo.calculos.push(c)
    grupos.set(plan.nombre, grupo)
  }
  const suma = (
    calculos: ReturnType<typeof calcular>[],
    campo: keyof ReturnType<typeof calcular>,
  ) => calculos.reduce((total, c) => total + c[campo], 0)
  return [...grupos]
    .sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0))
    .map(([plan, g]) => ({
      plan,
      personas: g.usuarios.size,
      diasConAvance: redondear(suma(g.calculos, 'diasConAvance') / g.calculos.length),
      diasSoloMinimo: redondear(suma(g.calculos, 'diasSoloMinimo') / g.calculos.length),
      diasEnRango: redondear(suma(g.calculos, 'diasEnRango') / g.calculos.length),
      // Ponderado: el total de días con avance sobre el total de días en marcha.
      porcentajeConstancia: porcentaje(
        suma(g.calculos, 'diasConAvance'),
        suma(g.calculos, 'diasEnRango'),
      ),
    }))
}

export const reportesHandlers = [
  http.get('/api/reports/permissions', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    const rango = leerRango(new URL(request.url))
    if (rango.error) return rango.error

    const esAdmin = sesion.usuario.rol === 'Administrador'
    const desdeMs = rango.desde * DIA_MS
    const hastaMsExclusivo = (rango.hasta + 1) * DIA_MS
    // El alcance lo decide el rol del token (RF-REP-04): un Estándar solo cuenta lo suyo.
    const delRango = db.solicitudes.filter((s) => {
      const creada = Date.parse(s.fechaCreacion)
      return (
        creada >= desdeMs &&
        creada < hastaMsExclusivo &&
        (esAdmin || s.solicitante.toLowerCase() === sesion.usuario.correo.toLowerCase())
      )
    })
    const filas: FilaPermisosDto[] = ESTADOS_SOLICITUD.map((estado) => ({
      estado,
      cantidad: delRango.filter((s) => s.estado === estado).length,
    })).filter((f) => f.cantidad > 0)

    const reporte: ReportePermisosDto = {
      desde: rango.textoDesde,
      hasta: rango.textoHasta,
      alcance: esAdmin ? 'todos' : 'propio',
      filas,
      total: delRango.length,
    }
    return HttpResponse.json(reporte)
  }),

  http.get('/api/reports/adherence', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    const rango = leerRango(new URL(request.url))
    if (rango.error) return rango.error

    const esAdmin = sesion.usuario.rol === 'Administrador'
    const reporte: ReporteConstanciaDto = {
      desde: rango.textoDesde,
      hasta: rango.textoHasta,
      alcance: esAdmin ? 'todos' : 'propio',
      pregunta: PREGUNTA_CONSTANCIA,
      filas: esAdmin
        ? constanciaGlobal(rango.desde, rango.hasta)
        : constanciaPersonal(sesion.usuario.id, rango.desde, rango.hasta),
    }
    return HttpResponse.json(reporte)
  }),
]
