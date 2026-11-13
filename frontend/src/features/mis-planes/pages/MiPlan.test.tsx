import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { PLAN_DEMO_ID, db, sembrarPlanDemo } from '@/mocks/db'
import { server } from '@/mocks/server'
import { renderApp } from '@/test/render'
import type { HoyDto } from '../types'

const CULPA = /fallaste|perdiste|culpa|castigo|atrasad|racha rota/i
const plan = () => db.planesUsuario.find((p) => p.id === PLAN_DEMO_ID)!

beforeEach(() => sembrarPlanDemo())

describe('Mis planes: detalle', () => {
  it('muestra el avance del plan y sus tareas, sin palabras de culpa', async () => {
    renderApp(`/mis-planes/${PLAN_DEMO_ID}`, { como: 'estandar' })

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Mi día de oficina saludable' }),
    ).toBeInTheDocument()
    expect(screen.getByText(/^Semana 2 de 4/)).toBeInTheDocument()
    expect(screen.getByRole('progressbar', { name: 'Avance del plan' })).toBeInTheDocument()
    expect(screen.getByText('Tus tareas')).toBeInTheDocument()
    expect(document.body.textContent).not.toMatch(CULPA)
  })

  it('un plan ajeno o inexistente dice que no lo encuentra', async () => {
    renderApp(`/mis-planes/${PLAN_DEMO_ID}`, { como: 'otro' })
    expect(await screen.findByText('No encontramos este plan')).toBeInTheDocument()
  })

  it('pausar exige un motivo y deja el plan en pausa; reanudar lo activa', async () => {
    const { user } = renderApp(`/mis-planes/${PLAN_DEMO_ID}`, { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Pausar' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Pausar el plan' })

    await user.click(within(dialogo).getByRole('button', { name: 'Pausar' }))
    expect(await within(dialogo).findByText('Elige el motivo de la pausa.')).toBeInTheDocument()

    await user.selectOptions(within(dialogo).getByLabelText('Motivo'), 'Viaje')
    await user.click(within(dialogo).getByRole('button', { name: 'Pausar' }))

    expect(await screen.findByText('Plan en pausa')).toBeInTheDocument()
    expect(plan()).toMatchObject({ estado: 'Pausado', motivoPausa: 'Viaje' })

    await user.click(await screen.findByRole('button', { name: 'Reanudar' }))
    await waitFor(() => expect(plan().estado).toBe('Activo'))
  })

  it('ajustar con modo suave lo aplica solo cuando la persona lo pide', async () => {
    const { user } = renderApp(`/mis-planes/${PLAN_DEMO_ID}`, { como: 'estandar' })
    await screen.findByText('Cambiar mi plan')
    expect(plan().modoSuaveHasta).toBeNull()

    await user.click(screen.getByRole('button', { name: 'Modo suave 7 días' }))

    await waitFor(() => expect(plan().modoSuaveHasta).not.toBeNull())
  })

  it('«Completar» solo se ofrece al llegar al final del plan', async () => {
    renderApp(`/mis-planes/${PLAN_DEMO_ID}`, { como: 'estandar' })
    await screen.findByText('Cambiar mi plan')
    expect(screen.queryByRole('button', { name: 'Completar' })).not.toBeInTheDocument()
  })

  it('cancelar pide confirmación y respeta «conservar mis hábitos»', async () => {
    const { user } = renderApp(`/mis-planes/${PLAN_DEMO_ID}`, { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Cancelar plan' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Dejar este plan' })
    expect(plan().estado).toBe('Activo')

    await user.click(within(dialogo).getByRole('button', { name: 'Dejar el plan' }))

    await waitFor(() => expect(plan().estado).toBe('Cancelado'))
    expect(await screen.findByText('Plan cancelado.')).toBeInTheDocument()
  })

  it('el recordatorio pide una hora para activarse', async () => {
    const { user } = renderApp(`/mis-planes/${PLAN_DEMO_ID}`, { como: 'estandar' })
    await screen.findByText('Recordatorio')
    await user.clear(screen.getByLabelText('Hora'))
    if (!screen.getByRole('switch').getAttribute('aria-checked')?.includes('true'))
      await user.click(screen.getByRole('switch'))

    await user.click(screen.getByRole('button', { name: 'Guardar recordatorio' }))

    expect(await screen.findByText('Elige a qué hora quieres el recordatorio.')).toBeInTheDocument()
  })
})

describe('Planes: Mis planes', () => {
  it('lista los planes en curso con su avance y enlaza al detalle', async () => {
    renderApp('/planes', { como: 'estandar' })
    const seccion = await screen.findByRole('region', { name: 'Mis planes' })
    expect(within(seccion).getByText(/Semana 2 de 4/)).toBeInTheDocument()
    expect(
      within(seccion).getByRole('link', { name: 'Mi día de oficina saludable' }),
    ).toHaveAttribute('href', `/mis-planes/${PLAN_DEMO_ID}`)
  })
})

describe('Hoy: celebración y ajustes', () => {
  const hoy = (cambios: Partial<HoyDto>): HoyDto => ({
    fecha: '2026-11-12',
    tareas: [],
    planes: [],
    mensaje: null,
    celebracionSemana: null,
    tipExtra: null,
    logrosNuevos: [],
    ...cambios,
  })

  it('celebra la semana y los logros nuevos una sola vez y avisa al servidor', async () => {
    let vistos = 0
    server.use(
      http.get('/api/today', () =>
        HttpResponse.json(
          hoy({
            celebracionSemana: 1,
            tipExtra: 'Un vaso de agua al despertar ayuda.',
            logrosNuevos: ['Regreso'],
          }),
        ),
      ),
      http.post('/api/progress/achievements/seen', () => {
        vistos++
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { user } = renderApp('/hoy', { como: 'estandar' })

    expect(await screen.findByText('¡Cerraste la semana 1!')).toBeInTheDocument()
    expect(screen.getByText('Un vaso de agua al despertar ayuda.')).toBeInTheDocument()
    expect(screen.getByText('Volviste')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Gracias' }))

    expect(screen.queryByText('¡Cerraste la semana 1!')).not.toBeInTheDocument()
    await waitFor(() => expect(vistos).toBe(1))
  })

  it('la sugerencia de ajuste solo se aplica cuando la persona elige una opción', async () => {
    server.use(
      http.get('/api/today', () =>
        HttpResponse.json(
          hoy({
            planes: [
              {
                planId: PLAN_DEMO_ID,
                nombre: 'Mi día de oficina saludable',
                semanaActual: 2,
                semanasTotales: 4,
                diasRestantes: 19,
                racha: 0,
                sugerencia: { opciones: ['ModoSuave', 'Extender', 'Pausar'], diasSinRegistro: 4 },
              },
            ],
          }),
        ),
      ),
    )
    const { user } = renderApp('/hoy', { como: 'estandar' })

    expect(await screen.findByText('¿Ajustamos Mi día de oficina saludable?')).toBeInTheDocument()
    expect(plan().modoSuaveHasta).toBeNull()
    expect(screen.getByRole('link', { name: 'Pausar con un motivo' })).toHaveAttribute(
      'href',
      `/mis-planes/${PLAN_DEMO_ID}`,
    )

    await user.click(screen.getByRole('button', { name: 'Modo suave 7 días' }))

    await waitFor(() => expect(plan().modoSuaveHasta).not.toBeNull())
    expect(document.body.textContent).not.toMatch(CULPA)
  })
})
