import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { problema } from '@/mocks/http'
import { server } from '@/mocks/server'
import { renderApp } from '@/test/render'

const SALUDO = /^(Buenos días|Buenas tardes|Buenas noches), Ana$/

describe('panel Hoy', () => {
  it('muestra el resumen del día de Ana con su plan', async () => {
    renderApp('/hoy', { como: 'estandar' })
    expect(await screen.findByRole('heading', { level: 1, name: SALUDO })).toBeInTheDocument()

    const resumen = screen.getByRole('region', { name: 'Resumen del día' })
    // Solo "Leer" está Activo y le faltan 12 páginas: 0 de 1 tarea.
    expect(within(resumen).getByText('0/1')).toBeInTheDocument()
    expect(within(resumen).getByRole('progressbar', { name: 'Tareas de hoy' })).toHaveAttribute(
      'aria-valuenow',
      '0',
    )
    expect(within(resumen).getByText('3 de 8')).toBeInTheDocument()
    expect(within(resumen).getByText('Plan · semana')).toBeInTheDocument()
    expect(within(resumen).getByText('Primeros 5 km')).toBeInTheDocument()
    expect(within(resumen).getByText(/récord/)).toBeInTheDocument()
  })

  it('lista solo los hábitos Activos, con lo que falta', async () => {
    renderApp('/hoy', { como: 'estandar' })
    const tareas = await screen.findByRole('region', { name: 'Tus tareas de hoy' })
    const lista = within(tareas).getByRole('list')
    expect(within(lista).getAllByRole('listitem')).toHaveLength(1)
    expect(within(lista).getByRole('link', { name: 'Leer' })).toHaveAttribute(
      'href',
      '/habitos/7c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e01',
    )
    expect(within(lista).getByText(/8 \/ 20/)).toBeInTheDocument()
    // Correr (Planificado) y Meditar (Pausado) no cuentan hoy.
    expect(screen.queryByRole('link', { name: 'Correr' })).not.toBeInTheDocument()
    expect(screen.getByText('Próxima tarea')).toBeInTheDocument()
  })

  it('"Marcar hecha" registra lo que falta y actualiza el resumen', async () => {
    const { user } = renderApp('/hoy', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Marcar hecha: Leer' }))

    expect(await screen.findByText('1/1')).toBeInTheDocument()
    expect(await screen.findByText('¡Leer cumplido!')).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Hecha' })).toBeInTheDocument()
    expect(screen.getByText('¡Todo hecho por hoy!')).toBeInTheDocument()
    // El botón desaparece: el foco pasa al nombre de la fila y no se pierde en <body>.
    expect(screen.getByRole('link', { name: 'Leer' })).toHaveFocus()
    // El avance quedó en el servidor simulado: 8 + 12 páginas hoy.
    const leer = db.habitos.find((h) => h.nombre === 'Leer')
    expect(leer?.registros.reduce((t, r) => t + r.cantidad, 0)).toBeGreaterThanOrEqual(20)
  })

  it('si el servidor rechaza el avance lo dice sin detalles internos y no marca la tarea', async () => {
    server.use(
      http.post('/api/habits/:id/progress', () =>
        problema(400, 'La operación no pudo completarse.', 'La operación no está permitida.'),
      ),
    )
    const { user } = renderApp('/hoy', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Marcar hecha: Leer' }))
    expect(await screen.findByText('La operación no está permitida.')).toBeInTheDocument()
    expect(screen.getByText('0/1')).toBeInTheDocument()
  })

  it('"Registrar cantidad" abre el formulario del hábito y se cierra al registrar', async () => {
    const { user } = renderApp('/hoy', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: /Registrar cantidad/ }))
    const dialogo = await screen.findByRole('dialog', { name: 'Registrar avance: Leer' })
    await user.type(within(dialogo).getByLabelText(/^Cantidad/), '3')
    await user.click(within(dialogo).getByRole('button', { name: 'Registrar avance' }))
    expect(await screen.findByText('Avance registrado.')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(await screen.findByText(/11 \/ 20/)).toBeInTheDocument()
  })

  it('"Registrar peso" lleva a Progreso', async () => {
    renderApp('/hoy', { como: 'estandar' })
    expect(await screen.findByRole('link', { name: 'Registrar peso' })).toHaveAttribute(
      'href',
      '/progreso?registrar=peso',
    )
  })

  it('"Añadir hábito" abre el formulario de creación', async () => {
    const { user, router } = renderApp('/hoy', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Añadir hábito' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Nuevo hábito' })
    expect(within(dialogo).getByLabelText('Nombre')).toBeInTheDocument()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/hoy')
  })

  it('sin hábitos invita a elegir una meta', async () => {
    db.habitos = []
    renderApp('/hoy', { como: 'estandar' })
    expect(await screen.findByText('Aún no tienes un plan — elige una meta')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Ver metas' })).toHaveAttribute('href', '/metas')
    expect(screen.getByText('0/0')).toBeInTheDocument()
  })

  it('con hábitos pero ninguno activo lo explica', async () => {
    for (const h of db.habitos) h.estado = 'Pausado'
    renderApp('/hoy', { como: 'estandar' })
    expect(await screen.findByText('Hoy no tienes hábitos activos')).toBeInTheDocument()
    expect(screen.getByText('Sin tareas pendientes')).toBeInTheDocument()
  })

  it('sin la pieza de perfil (backend real) el panel sigue funcionando', async () => {
    server.use(http.get('/api/profile', () => new HttpResponse(null, { status: 404 })))
    renderApp('/hoy', { como: 'estandar' })
    expect(await screen.findByRole('heading', { level: 1, name: /^Buen/ })).toBeInTheDocument()
    expect(screen.getByText('Sin plan')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Elige una meta' })).toHaveAttribute('href', '/metas')
  })

  it('un error al cargar ofrece reintentar', async () => {
    let falla = true
    server.use(
      http.get('/api/habits', () =>
        falla ? problema(500, 'Error interno.') : HttpResponse.json([]),
      ),
    )
    const { user } = renderApp('/hoy', { como: 'estandar' })
    expect(await screen.findByRole('alert')).toBeInTheDocument()
    falla = false
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByText('Aún no tienes un plan — elige una meta')).toBeInTheDocument()
  })

  it('el Administrador no tiene panel Hoy', async () => {
    renderApp('/hoy', { como: 'admin' })
    expect(await screen.findByText('No tienes acceso a esta sección')).toBeInTheDocument()
  })

  it('"Hoy" es la primera opción del menú del Estándar', async () => {
    renderApp('/hoy', { como: 'estandar' })
    const nav = await screen.findByRole('navigation', { name: 'Principal' })
    const primero = within(nav).getAllByRole('link')[0]
    expect(primero).toHaveAccessibleName('Hoy')
    expect(primero).toHaveAttribute('aria-current', 'page')
  })
})
