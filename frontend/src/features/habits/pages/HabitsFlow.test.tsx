import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { problema } from '@/mocks/http'
import { server } from '@/mocks/server'
import { renderApp } from '@/test/render'

const LEER = '7c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e01'
const MEDITAR = '7c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e03'

describe('hábitos', () => {
  it('lista los hábitos del usuario y filtra por estado', async () => {
    const { user } = renderApp('/habitos', { como: 'estandar' })
    expect(await screen.findByText('Leer')).toBeInTheDocument()
    expect(screen.getByText('Correr')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /Pausado/ }))
    expect(screen.getByText('Meditar')).toBeInTheDocument()
    expect(screen.queryByText('Leer')).not.toBeInTheDocument()
  })

  it('crea un hábito que nace Planificado y lo activa', async () => {
    const { user } = renderApp('/habitos', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Nuevo hábito' }))
    const dialogo = screen.getByRole('dialog', { name: 'Nuevo hábito' })
    await user.type(within(dialogo).getByLabelText('Nombre'), 'Beber agua')
    await user.type(within(dialogo).getByLabelText('Objetivo'), '8')
    await user.type(within(dialogo).getByLabelText('Unidad'), 'vasos')
    // La meta es opcional desde HabitTracker v2: el formulario ya no pide un identificador.
    expect(within(dialogo).queryByLabelText('Meta')).not.toBeInTheDocument()
    await user.click(within(dialogo).getByRole('button', { name: 'Crear hábito' }))

    expect(await screen.findByRole('heading', { name: 'Beber agua', level: 1 })).toBeInTheDocument()
    expect(screen.getByText('Planificado')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Activar' }))
    expect(await screen.findByRole('button', { name: 'Pausar' })).toBeInTheDocument()
    expect(db.habitos.find((h) => h.nombre === 'Beber agua')?.estado).toBe('Activo')

    // Regresión: el avance de hoy cuenta aunque el ciclo haya empezado hoy mismo.
    await user.type(screen.getByLabelText('Cantidad (vasos)'), '8')
    await user.click(screen.getByRole('button', { name: 'Registrar avance' }))
    expect(await screen.findByText('Meta del período cumplida.')).toBeInTheDocument()
  })

  it('no ofrece completar hasta cumplir la meta y luego lo permite', async () => {
    const { user } = renderApp(`/habitos/${LEER}`, { como: 'estandar' })
    const completar = await screen.findByRole('button', { name: 'Completar' })
    expect(completar).toBeDisabled()

    await user.type(screen.getByLabelText('Cantidad (páginas)'), '12')
    await user.click(screen.getByRole('button', { name: 'Registrar avance' }))

    expect(await screen.findByText('Meta del período cumplida.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Completar' }))
    // Completar es definitivo: pide confirmación antes de aplicarse.
    const dialogo = await screen.findByRole('dialog', { name: 'Completar hábito' })
    await user.click(within(dialogo).getByRole('button', { name: 'Sí, completar' }))
    expect(await screen.findByText(/ya está completado/)).toBeInTheDocument()
  })

  it('solo permite registrar avance en estado Activo', async () => {
    renderApp(`/habitos/${MEDITAR}`, { como: 'estandar' })
    expect(
      await screen.findByText(/Solo se registra avance cuando el hábito está Activo/),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Reanudar' })).toBeInTheDocument()
  })

  it('sin el permiso aplicado, la reconfiguración pide solicitarlo', async () => {
    const { user, router } = renderApp(`/habitos/${LEER}`, { como: 'estandar' })
    expect(await screen.findByText('Necesitas un permiso')).toBeInTheDocument()
    await user.click(screen.getByRole('link', { name: 'Solicitar permiso' }))
    expect(await screen.findByRole('combobox', { name: 'Permiso' })).toHaveValue(
      'Habits.AdvancedReconfig',
    )
    expect(router.state.location.pathname).toBe('/permisos')
  })

  it('muestra un aviso cuando el hábito no existe', async () => {
    renderApp('/habitos/00000000-0000-0000-0000-000000000000', { como: 'estandar' })
    expect(await screen.findByText('Hábito no encontrado')).toBeInTheDocument()
  })

  it('un hábito de otra persona (403) también se muestra como no encontrado', async () => {
    renderApp(`/habitos/${LEER}`, { como: 'otro' })
    expect(await screen.findByText('Hábito no encontrado')).toBeInTheDocument()
  })

  it('un id que no es GUID se muestra como no encontrado sin consultar al servidor', async () => {
    renderApp('/habitos/no-es-un-guid', { como: 'estandar' })
    expect(await screen.findByText('Hábito no encontrado')).toBeInTheDocument()
  })

  it('un error del servidor en el detalle es un error de carga con reintento, no un "no encontrado"', async () => {
    server.use(
      http.get('/api/habits/:id', () =>
        problema(500, 'Error interno.', 'Ocurrió un error inesperado.'),
      ),
    )
    renderApp(`/habitos/${LEER}`, { como: 'estandar' })
    expect(await screen.findByRole('button', { name: 'Reintentar' })).toBeInTheDocument()
    expect(screen.queryByText('Hábito no encontrado')).not.toBeInTheDocument()
  })

  it('un 404 sin cuerpo (endpoint inexistente) no se confunde con un hábito no encontrado', async () => {
    server.use(http.get('/api/habits/:id', () => new HttpResponse(null, { status: 404 })))
    renderApp(`/habitos/${LEER}`, { como: 'estandar' })
    expect(await screen.findByRole('button', { name: 'Reintentar' })).toBeInTheDocument()
    expect(screen.queryByText('Hábito no encontrado')).not.toBeInTheDocument()
  })
})
