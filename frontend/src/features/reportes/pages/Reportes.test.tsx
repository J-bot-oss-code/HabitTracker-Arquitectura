import { fireEvent, screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { db, PLAN_DEMO_ID, sembrarPlanDemo } from '@/mocks/db'
import { server } from '@/mocks/server'
import { renderApp } from '@/test/render'

const CULPA = /fallaste|perdiste|culpa|castigo/i
const PREGUNTA = '¿Qué tan constante fui en cada plan durante este período?'

function solicitud(solicitante: string, estado: 'Pendiente' | 'Aplicada' | 'Rechazada') {
  db.solicitudes.push({
    id: crypto.randomUUID(),
    solicitante,
    permisoSolicitado: 'Habits.AdvancedReconfig',
    estado,
    aprobador: null,
    fechaCreacion: new Date().toISOString(),
    fechaResolucion: null,
    motivoRechazo: null,
  })
}

const consultas = (espia: { mock: { calls: unknown[][] } }) =>
  espia.mock.calls.filter(([entrada]) =>
    String(entrada instanceof Request ? entrada.url : entrada).includes('/api/reports'),
  ).length

afterEach(() => vi.restoreAllMocks())

describe('Reportes', () => {
  it('abre con los últimos 7 días y, sin datos, muestra los estados vacíos', async () => {
    renderApp('/reportes', { como: 'estandar' })
    expect(await screen.findByText('Sin solicitudes en este rango')).toBeInTheDocument()
    expect(await screen.findByText('Sin planes en marcha en este rango')).toBeInTheDocument()
    expect(screen.getByText(/Mostrando del/)).toHaveTextContent(/\(7 días\)/)
  })

  it('el Estándar ve solo lo suyo y su constancia sin culpa', async () => {
    solicitud('estandar@demo.com', 'Pendiente')
    solicitud('estandar@demo.com', 'Aplicada')
    solicitud('otro@demo.com', 'Pendiente')
    sembrarPlanDemo()
    renderApp('/reportes', { como: 'estandar' })

    const permisos = within(
      await screen.findByRole('region', { name: 'Solicitudes de permiso por estado' }),
    )
    expect(await permisos.findByText('Solo tus solicitudes.')).toBeInTheDocument()
    expect(within(permisos.getByRole('row', { name: /Total/ })).getByText('2')).toBeInTheDocument()

    const constancia = within(await screen.findByRole('region', { name: PREGUNTA }))
    expect(await constancia.findByText('Solo tus planes.')).toBeInTheDocument()
    expect(constancia.getByRole('progressbar')).toBeInTheDocument()
    // El rango es de 7 días UTC; la constancia se mide por el día local (UTC-4): de noche, en UTC ya es mañana y hoy no cuenta.
    expect(constancia.getByText(/de [67] días con avance/)).toBeInTheDocument()
    expect(document.body.textContent).not.toMatch(CULPA)
  })

  it('el Administrador ve el alcance de todas las personas y promedios sin datos personales', async () => {
    solicitud('estandar@demo.com', 'Pendiente')
    solicitud('otro@demo.com', 'Rechazada')
    sembrarPlanDemo()
    // Con todas las de la base (también las de ejemplo), no solo las de este usuario.
    expect(db.solicitudes.length).toBeGreaterThan(2)
    renderApp('/reportes', { como: 'admin' })

    const permisos = within(
      await screen.findByRole('region', { name: 'Solicitudes de permiso por estado' }),
    )
    expect(await permisos.findByText('Las solicitudes de todas las personas.')).toBeInTheDocument()
    expect(
      within(permisos.getByRole('row', { name: /Total/ })).getByText(String(db.solicitudes.length)),
    ).toBeInTheDocument()

    expect(await screen.findByText('Promedio de 1 persona')).toBeInTheDocument()
    expect(screen.getByText(/En promedio: /)).toBeInTheDocument()
    // La pregunta del servidor está en primera persona: el Administrador ve un titular neutro.
    const constancia = within(screen.getByRole('region', { name: 'Constancia promedio por plan' }))
    expect(constancia.queryByText(/estandar@demo\.com/)).not.toBeInTheDocument()
    expect(screen.queryByText(PREGUNTA)).not.toBeInTheDocument()
  })

  it('un rango inválido muestra el error del esquema y no consulta', async () => {
    const { user } = renderApp('/reportes', { como: 'estandar' })
    await screen.findByText('Sin solicitudes en este rango')
    const espia = vi.spyOn(globalThis, 'fetch')

    fireEvent.change(screen.getByLabelText('Desde'), { target: { value: '2099-01-01' } })
    await user.click(screen.getByRole('button', { name: 'Ver reporte' }))
    expect(
      await screen.findByText('La fecha final no puede ser anterior a la inicial.'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Hasta')).toHaveAttribute('aria-invalid', 'true')

    fireEvent.change(screen.getByLabelText('Desde'), { target: { value: '' } })
    await user.click(screen.getByRole('button', { name: 'Ver reporte' }))
    expect(await screen.findByText('Indica el día inicial.')).toBeInTheDocument()
    expect(consultas(espia)).toBe(0)
  })

  it('un rango válido y los botones rápidos actualizan el período mostrado', async () => {
    const { user } = renderApp('/reportes', { como: 'estandar' })
    await screen.findByText('Sin planes en marcha en este rango')

    expect(screen.getByRole('group', { name: 'Atajos de período' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Últimos 7 días' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
    expect(screen.getByText('Fechas en UTC. Máximo 366 días.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Últimos 30 días' }))
    expect(screen.getByRole('button', { name: 'Últimos 30 días' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
    expect(await screen.findByText(/\(30 días\)/)).toBeInTheDocument()
    expect(screen.getByLabelText('Desde')).toHaveValue(
      new Date(Date.now() - 29 * 86_400_000).toISOString().slice(0, 10),
    )

    const hoy = new Date().toISOString().slice(0, 10)
    fireEvent.change(screen.getByLabelText('Desde'), { target: { value: hoy } })
    await user.click(screen.getByRole('button', { name: 'Ver reporte' }))
    expect(await screen.findByText(/\(1 día\)/)).toBeInTheDocument()
  })
})

const hoyUtc = () => new Date().toISOString().slice(0, 10)

describe('Reportes: validación del rango', () => {
  it.each([
    ['sin día final', { desde: '2026-05-01', hasta: '' }, ['Indica el día final.'], 'Hasta'],
    [
      'sin ninguna fecha',
      { desde: '', hasta: '' },
      ['Indica el día inicial.', 'Indica el día final.'],
      'Desde',
    ],
    [
      'de más de 366 días',
      { desde: '2025-01-01', hasta: '2026-06-01' },
      ['El período no puede pasar de 366 días.'],
      'Hasta',
    ],
    [
      'con un año anterior al 2000',
      { desde: '1999-12-31', hasta: '2026-01-01' },
      ['Elige una fecha a partir del año 2000.'],
      'Desde',
    ],
  ])(
    'un rango %s muestra el error del esquema y no consulta',
    async (_caso, rango, errores, campo) => {
      const { user } = renderApp('/reportes', { como: 'estandar' })
      await screen.findByText('Sin planes en marcha en este rango')
      const periodo = screen.getByText(/Mostrando del/).textContent
      const espia = vi.spyOn(globalThis, 'fetch')

      fireEvent.change(screen.getByLabelText('Desde'), { target: { value: rango.desde } })
      fireEvent.change(screen.getByLabelText('Hasta'), { target: { value: rango.hasta } })
      await user.click(screen.getByRole('button', { name: 'Ver reporte' }))

      for (const error of errores) expect(await screen.findByText(error)).toBeInTheDocument()
      expect(screen.getByLabelText(campo)).toHaveAttribute('aria-invalid', 'true')
      // El período mostrado no cambia y ninguna consulta sale hacia el servidor (RD-07).
      expect(screen.getByText(/Mostrando del/)).toHaveTextContent(periodo!)
      expect(consultas(espia)).toBe(0)
    },
  )

  it('366 días es el máximo que se acepta: se consulta y se muestra', async () => {
    const { user } = renderApp('/reportes', { como: 'estandar' })
    await screen.findByText('Sin planes en marcha en este rango')

    fireEvent.change(screen.getByLabelText('Desde'), { target: { value: '2026-01-01' } })
    fireEvent.change(screen.getByLabelText('Hasta'), { target: { value: '2027-01-01' } })
    await user.click(screen.getByRole('button', { name: 'Ver reporte' }))
    expect(await screen.findByText(/\(366 días\)/)).toBeInTheDocument()
    expect(screen.queryByText(/no puede pasar de 366/)).not.toBeInTheDocument()
  })

  it('los atajos marcan solo el período vigente y un rango propio no marca ninguno', async () => {
    const { user } = renderApp('/reportes', { como: 'estandar' })
    await screen.findByText('Sin planes en marcha en este rango')
    const siete = () => screen.getByRole('button', { name: 'Últimos 7 días' })
    const treinta = () => screen.getByRole('button', { name: 'Últimos 30 días' })
    expect([siete(), treinta()].map((b) => b.getAttribute('aria-pressed'))).toEqual([
      'true',
      'false',
    ])

    await user.click(treinta())
    expect([siete(), treinta()].map((b) => b.getAttribute('aria-pressed'))).toEqual([
      'false',
      'true',
    ])
    expect(await screen.findByText(/\(30 días\)/)).toBeInTheDocument()

    await user.click(siete())
    expect([siete(), treinta()].map((b) => b.getAttribute('aria-pressed'))).toEqual([
      'true',
      'false',
    ])
    expect(await screen.findByText(/\(7 días\)/)).toBeInTheDocument()
    expect(screen.getByLabelText('Hasta')).toHaveValue(hoyUtc())

    fireEvent.change(screen.getByLabelText('Desde'), {
      target: { value: new Date(Date.now() - 2 * 86_400_000).toISOString().slice(0, 10) },
    })
    await user.click(screen.getByRole('button', { name: 'Ver reporte' }))
    expect(await screen.findByText(/\(3 días\)/)).toBeInTheDocument()
    expect([siete(), treinta()].map((b) => b.getAttribute('aria-pressed'))).toEqual([
      'false',
      'false',
    ])
  })
})

describe('Reportes: cifras sin culpa y sin datos personales', () => {
  it('un 0 % se dice como punto de partida y conserva su barra vacía', async () => {
    sembrarPlanDemo()
    for (const h of db.habitos.filter((x) => x.planUsuarioId === PLAN_DEMO_ID)) h.registros = []
    renderApp('/reportes', { como: 'estandar' })

    const constancia = within(await screen.findByRole('region', { name: PREGUNTA }))
    expect(await constancia.findByText('0 %')).toHaveClass('text-atenuado')
    expect(constancia.getByRole('progressbar', { name: /^Constancia en/ })).toHaveAttribute(
      'aria-valuenow',
      '0',
    )
    expect(constancia.getByText('Sin avances en este período')).toBeInTheDocument()
    expect(constancia.getByText(/0 de [67] días con avance\./)).toBeInTheDocument() // día local frente a día UTC
    expect(constancia.queryByText(/versión mínima/)).not.toBeInTheDocument()
    expect(document.body.textContent).not.toMatch(CULPA)
  })

  it('un porcentaje null no pinta cifra ni barra, y lo dice con texto', async () => {
    server.use(
      http.get('/api/reports/adherence', ({ request }) => {
        const url = new URL(request.url)
        return HttpResponse.json({
          desde: url.searchParams.get('desde'),
          hasta: url.searchParams.get('hasta'),
          alcance: 'propio',
          pregunta: PREGUNTA,
          filas: [
            {
              plan: 'Plan que aún no empieza',
              personas: 1,
              diasConAvance: 0,
              diasSoloMinimo: 0,
              diasEnRango: 0,
              porcentajeConstancia: null,
            },
          ],
        })
      }),
    )
    renderApp('/reportes', { como: 'estandar' })

    const constancia = within(await screen.findByRole('region', { name: PREGUNTA }))
    expect(await constancia.findByText('Plan que aún no empieza')).toBeInTheDocument()
    expect(constancia.getByText('Sin días en marcha en este período.')).toBeInTheDocument()
    expect(constancia.queryByRole('progressbar')).not.toBeInTheDocument()
    expect(constancia.queryByText(/%|NaN/)).not.toBeInTheDocument()
  })

  it('el Administrador no ve correos, nombres ni identificadores de nadie en ningún bloque', async () => {
    solicitud('estandar@demo.com', 'Aplicada')
    solicitud('otro@demo.com', 'Rechazada')
    sembrarPlanDemo()
    renderApp('/reportes', { como: 'admin' })

    const permisos = await screen.findByRole('region', {
      name: 'Solicitudes de permiso por estado',
    })
    await within(permisos).findByText('Las solicitudes de todas las personas.')
    const constancia = screen.getByRole('region', { name: 'Constancia promedio por plan' })
    await within(constancia).findByText('Promedio de 1 persona')

    const texto = `${permisos.textContent} ${constancia.textContent}`
    for (const u of db.usuarios) {
      expect(texto).not.toContain(u.correo)
      expect(texto).not.toContain(u.nombre)
      expect(texto).not.toContain(u.id)
    }
    // Ni una columna de personas en la tabla: solo estado y cantidad.
    expect(
      within(permisos)
        .getAllByRole('columnheader')
        .map((c) => c.textContent),
    ).toEqual(['Estado', 'Cantidad'])
  })
})

describe('Reportes: un bloque que falla no tumba al otro', () => {
  it.each([
    [
      'permissions',
      'Solicitudes de permiso por estado',
      'Sin solicitudes en este rango',
      'Sin planes en marcha en este rango',
    ],
    ['adherence', PREGUNTA, 'Sin planes en marcha en este rango', 'Sin solicitudes en este rango'],
  ])(
    'si falla «%s» solo ese bloque muestra el error y «Reintentar» lo recupera',
    async (ruta, nombre, vacioPropio, vacioOtro) => {
      let fallar = true
      server.use(
        http.get(`/api/reports/${ruta}`, () => {
          if (fallar)
            return HttpResponse.json({ status: 500, title: 'Detalle interno' }, { status: 500 })
        }),
      )
      const { user } = renderApp('/reportes', { como: 'estandar' })

      const bloque = within(await screen.findByRole('region', { name: nombre }))
      expect(await bloque.findByRole('alert')).toHaveTextContent('No se pudo cargar este reporte.')
      // El otro bloque sigue vivo y el error no se repite ni muestra el detalle interno.
      expect(await screen.findByText(vacioOtro)).toBeInTheDocument()
      expect(screen.getAllByRole('alert')).toHaveLength(1)
      expect(screen.queryByText(/Detalle interno/)).not.toBeInTheDocument()

      fallar = false
      await user.click(bloque.getByRole('button', { name: 'Reintentar' }))
      expect(await bloque.findByText(vacioPropio)).toBeInTheDocument()
      expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    },
  )
})
