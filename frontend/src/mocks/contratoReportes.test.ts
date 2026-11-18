import { describe, expect, it } from 'vitest'
import type { ReporteConstanciaDto, ReportePermisosDto } from '@/features/reportes/types'
import { iniciarSesionComo } from '@/test/render'
import { db, sembrarPlanDemo } from './db'

// Reportes (/api/reports): la API simulada responde como ReportsController y ReportesService.
const url = (ruta: string) => `${window.location.origin}${ruta}`

const llamar = (ruta: string, token?: string, metodo = 'GET', body?: unknown) =>
  fetch(url(ruta), {
    method: metodo,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  })

const DIA = 86_400_000
const dia = (desplazamiento: number) =>
  new Date(Date.now() + desplazamiento * DIA).toISOString().slice(0, 10)
// Un rango que cubre los últimos 30 días y mañana (por si la zona de la persona va por delante de UTC).
const RANGO = `desde=${dia(-30)}&hasta=${dia(1)}`

const permisos = async (token: string, consulta = RANGO) => {
  const r = await llamar(`/api/reports/permissions?${consulta}`, token)
  expect(r.status).toBe(200)
  return (await r.json()) as ReportePermisosDto
}
const constancia = async (token: string, consulta = RANGO) => {
  const r = await llamar(`/api/reports/adherence?${consulta}`, token)
  expect(r.status).toBe(200)
  return (await r.json()) as ReporteConstanciaDto
}
const pedir = (token: string, permisoSolicitado: string) =>
  llamar('/api/permissions', token, 'POST', { permisoSolicitado })

describe.each(['permissions', 'adherence'])('GET /api/reports/%s: acceso y rango', (reporte) => {
  const ruta = (consulta: string) => `/api/reports/${reporte}?${consulta}`

  it('sin token responde 401', async () => {
    expect((await llamar(ruta(RANGO))).status).toBe(401)
  })

  it('cualquier rol con sesión puede consultarlo', async () => {
    for (const quien of ['estandar', 'admin'] as const) {
      expect((await llamar(ruta(RANGO), iniciarSesionComo(quien))).status).toBe(200)
    }
  })

  it.each([
    ['sin rango', ''],
    ['sin desde', `hasta=${dia(0)}`],
    ['sin hasta', `desde=${dia(0)}`],
    ['con "hasta" anterior a "desde"', `desde=${dia(0)}&hasta=${dia(-1)}`],
    ['de 367 días', `desde=2026-01-01&hasta=2027-01-02`],
    ['con una fecha mal formada', `desde=ayer&hasta=${dia(0)}`],
    ['con un día que no existe', `desde=2026-02-30&hasta=2026-03-05`],
    ['con un año anterior al 2000', `desde=1999-12-01&hasta=2000-01-05`],
  ])('un rango %s es 400', async (_caso, consulta) => {
    const r = await llamar(ruta(consulta), iniciarSesionComo('estandar'))
    expect(r.status).toBe(400)
    expect(r.headers.get('Content-Type')).toContain('application/problem+json')
  })

  it('un rango de 366 días (ambos días incluidos) es válido; un solo día también', async () => {
    const token = iniciarSesionComo('estandar')
    expect((await llamar(ruta('desde=2026-01-01&hasta=2027-01-01'), token)).status).toBe(200)
    expect((await llamar(ruta('desde=2026-05-05&hasta=2026-05-05'), token)).status).toBe(200)
  })

  it('devuelve el rango pedido tal cual, como AAAA-MM-DD', async () => {
    const r = await llamar(ruta('desde=2026-05-05&hasta=2026-06-01'), iniciarSesionComo('admin'))
    expect(await r.json()).toMatchObject({ desde: '2026-05-05', hasta: '2026-06-01' })
  })
})

describe('reporte de permisos por estado (RF-REP-01 y RF-REP-04)', () => {
  it('sin solicitudes en el rango no hay filas y el total es 0', async () => {
    const r = await permisos(iniciarSesionComo('admin'), 'desde=2020-01-01&hasta=2020-12-31')
    expect(r).toEqual({
      desde: '2020-01-01',
      hasta: '2020-12-31',
      alcance: 'todos',
      filas: [],
      total: 0,
    })
  })

  it('el Estándar cuenta solo las suyas (alcance propio); el Administrador, todas (alcance todos)', async () => {
    const estandar = iniciarSesionComo('estandar')
    const admin = iniciarSesionComo('admin')
    const { id } = await (await pedir(estandar, 'Habits.AdvancedReconfig')).json()
    await pedir(estandar, 'Habits.Otra')
    await llamar(`/api/permissions/${id}/approve`, admin, 'POST')

    // Ana: una Pendiente y una Aplicada. Administrador: además la Pendiente de Luis (datos de ejemplo).
    expect(await permisos(estandar)).toEqual({
      desde: dia(-30),
      hasta: dia(1),
      alcance: 'propio',
      filas: [
        { estado: 'Pendiente', cantidad: 1 },
        { estado: 'Aplicada', cantidad: 1 },
      ],
      total: 2,
    })
    const todos = await permisos(admin)
    expect(todos.alcance).toBe('todos')
    expect(todos.filas).toEqual([
      { estado: 'Pendiente', cantidad: 2 },
      { estado: 'Aplicada', cantidad: 1 },
    ])
    expect(todos.total).toBe(3)
    // Luis solo ve la suya.
    expect((await permisos(iniciarSesionComo('otro'))).total).toBe(1)
  })

  it('cuenta por fecha de creación con ambos días incluidos', async () => {
    const admin = iniciarSesionComo('admin')
    // La solicitud de ejemplo se creó hace 2 días.
    const creada = db.solicitudes[0].fechaCreacion.slice(0, 10)
    expect((await permisos(admin, `desde=${creada}&hasta=${creada}`)).total).toBe(1)
    expect((await permisos(admin, `desde=${dia(-1)}&hasta=${dia(1)}`)).total).toBe(0)
    // El día siguiente al de la creación ya no cuenta si "hasta" es el día anterior.
    expect((await permisos(admin, `desde=${dia(-10)}&hasta=${dia(-3)}`)).total).toBe(0)
  })
})

describe('reporte de constancia por plan (RF-REP-02 y RF-REP-04)', () => {
  it('sin planes en marcha en el rango no hay filas, pero la pregunta y el alcance sí', async () => {
    const r = await constancia(iniciarSesionComo('estandar'))
    expect(r).toEqual({
      desde: dia(-30),
      hasta: dia(1),
      alcance: 'propio',
      pregunta: '¿Qué tan constante fui en cada plan durante este período?',
      filas: [],
    })
  })

  it('el Estándar ve sus planes con personas = 1 y los días que hubo avance', async () => {
    sembrarPlanDemo()
    // El plan de ejemplo lleva 10 días en marcha: 9 con avance (uno sin registro) y ninguno solo con la mínima.
    const r = await constancia(iniciarSesionComo('estandar'))
    expect(r.alcance).toBe('propio')
    expect(r.filas).toEqual([
      {
        plan: db.planesUsuario[0].nombre,
        personas: 1,
        diasConAvance: 9,
        diasSoloMinimo: 0,
        diasEnRango: 10,
        porcentajeConstancia: 90,
      },
    ])
    // Luis no ve el plan de Ana.
    expect((await constancia(iniciarSesionComo('otro'))).filas).toEqual([])
  })

  it('una pausa no resta días ni constancia', async () => {
    sembrarPlanDemo()
    const plan = db.planesUsuario[0]
    // Los 3 días anteriores a hoy en pausa (el día sin registro es el cuarto desde el inicio).
    plan.pausas.push({
      inicioUtc: new Date(Date.now() - 6 * DIA).toISOString(),
      finUtc: new Date(Date.now() - 4 * DIA).toISOString(),
      motivo: 'Viaje',
    })
    const [fila] = (await constancia(iniciarSesionComo('estandar'))).filas
    expect(fila.diasEnRango).toBeLessThan(10)
    expect(fila.porcentajeConstancia).toBe(
      Math.round((fila.diasConAvance * 1000) / fila.diasEnRango) / 10,
    )
  })

  it('un plan cancelado no aparece', async () => {
    sembrarPlanDemo()
    db.planesUsuario[0].estado = 'Cancelado'
    expect((await constancia(iniciarSesionComo('estandar'))).filas).toEqual([])
    expect((await constancia(iniciarSesionComo('admin'))).filas).toEqual([])
  })

  it('el Administrador recibe el agregado por plan sin ningún dato personal', async () => {
    sembrarPlanDemo()
    const r = await constancia(iniciarSesionComo('admin'))
    expect(r.alcance).toBe('todos')
    expect(r.filas).toEqual([
      {
        plan: db.planesUsuario[0].nombre,
        personas: 1,
        diasConAvance: 9,
        diasSoloMinimo: 0,
        diasEnRango: 10,
        porcentajeConstancia: 90,
      },
    ])
    const texto = JSON.stringify(r)
    expect(texto).not.toContain('estandar@demo.com')
    expect(texto).not.toContain(db.usuarios[0].id)
  })

  it('agrupa las personas de un mismo plan, con días en promedio y el porcentaje ponderado', async () => {
    sembrarPlanDemo()
    const original = db.planesUsuario[0]
    // Luis sigue el mismo plan, pero solo avanzó 2 de los 10 días.
    const luis = db.usuarios[2].id
    const copia = { ...original, id: crypto.randomUUID(), usuarioId: luis }
    db.planesUsuario.push(copia)
    const tarea = db.habitos.find((h) => h.planUsuarioId === original.id)!
    db.habitos.push({
      ...tarea,
      id: crypto.randomUUID(),
      usuarioId: luis,
      planUsuarioId: copia.id,
      registros: tarea.registros.slice(0, 2),
    })
    const [fila] = (await constancia(iniciarSesionComo('admin'))).filas
    expect(fila).toEqual({
      plan: original.nombre,
      personas: 2,
      // (9 + 2) / 2 = 5.5 y 100 × 11 / 20 = 55.
      diasConAvance: 5.5,
      diasSoloMinimo: 0,
      diasEnRango: 10,
      porcentajeConstancia: 55,
    })
  })
})
