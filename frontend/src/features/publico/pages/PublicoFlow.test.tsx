import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { renderApp } from '@/test/render'

describe('inicio público (sin sesión)', () => {
  it('muestra la promesa, las acciones y la tarjeta de ejemplo', async () => {
    renderApp('/')
    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: 'Convierte tu meta en un plan diario que sí puedes cumplir.',
      }),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Elegir mi meta' })).toHaveAttribute('href', '/metas')
    expect(screen.getByRole('link', { name: 'Probar sin cuenta' })).toHaveAttribute(
      'href',
      '/calculadoras',
    )
    expect(screen.getByRole('figure', { name: /Ejemplo del panel Hoy/ })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Crear cuenta' })).toHaveAttribute('href', '/registro')
  })

  it('filtra el catálogo por área y cada meta lleva al registro con su slug', async () => {
    const { user } = renderApp('/metas')
    const filtros = await screen.findByRole('group', { name: 'Filtrar metas por área' })
    expect(within(filtros).getByRole('button', { name: /Todas/ })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
    expect(screen.getAllByRole('article')).toHaveLength(6)

    await user.click(within(filtros).getByRole('button', { name: /Descanso/ }))
    const tarjetas = screen.getAllByRole('article')
    expect(tarjetas).toHaveLength(1)
    expect(within(tarjetas[0]).getByRole('heading', { name: 'Dormir mejor' })).toBeInTheDocument()

    await user.click(within(filtros).getByRole('button', { name: /Todas/ }))
    expect(screen.getByRole('link', { name: 'Empezar este plan: Primeros 5 km' })).toHaveAttribute(
      'href',
      '/registro?meta=primeros-5-km',
    )
  })

  it('la calculadora valida los datos y muestra la estimación con su aviso', async () => {
    const { user } = renderApp('/calculadoras')
    await user.click(await screen.findByRole('button', { name: 'Calcular' }))
    expect(await screen.findByText('Escribe la edad.')).toBeInTheDocument()
    expect(screen.getByText('Elige una opción.')).toBeInTheDocument()

    await user.type(screen.getByLabelText('Edad'), '30')
    await user.selectOptions(screen.getByLabelText('Sexo'), 'mujer')
    await user.type(screen.getByLabelText('Altura (cm)'), '165')
    await user.type(screen.getByLabelText('Peso (kg)'), '60')
    await user.selectOptions(screen.getByLabelText('Actividad'), 'moderada')
    await user.click(screen.getByRole('button', { name: 'Calcular' }))

    // es-DO agrupa los miles con coma (2,050); se acepta cualquier separador del locale.
    expect(await screen.findByText(/^2[.,\s]?050$/)).toBeInTheDocument()
    expect(screen.getByText(/no una indicación médica/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Guardar y crear mi plan' })).toHaveAttribute(
      'href',
      '/registro',
    )
  })

  it('al corregir mal un dato retira la estimación anterior', async () => {
    const { user } = renderApp('/calculadoras')
    await user.type(await screen.findByLabelText('Edad'), '25')
    await user.selectOptions(screen.getByLabelText('Sexo'), 'hombre')
    await user.type(screen.getByLabelText('Altura (cm)'), '180')
    await user.type(screen.getByLabelText('Peso (kg)'), '80')
    await user.selectOptions(screen.getByLabelText('Actividad'), 'sedentaria')
    await user.click(screen.getByRole('button', { name: 'Calcular' }))
    expect(await screen.findByText(/^2[.,\s]?170$/)).toBeInTheDocument()

    await user.clear(screen.getByLabelText('Peso (kg)'))
    await user.type(screen.getByLabelText('Peso (kg)'), '500')
    await user.click(screen.getByRole('button', { name: 'Calcular' }))
    expect(
      await screen.findByText('Debe ser como máximo 300 kg.', { exact: false }),
    ).toBeInTheDocument()
    expect(screen.queryByText(/^2[.,\s]?170$/)).not.toBeInTheDocument()
    expect(screen.getByText('Completa tus datos para ver una estimación.')).toBeInTheDocument()
  })

  it('rechaza una edad fuera de rango con un mensaje claro', async () => {
    const { user } = renderApp('/calculadoras')
    await user.type(await screen.findByLabelText('Edad'), '12')
    await user.click(screen.getByRole('button', { name: 'Calcular' }))
    expect(await screen.findByText('La edad debe ser al menos 16 años.')).toBeInTheDocument()
  })

  it('el menú móvil se abre, enlaza las secciones y se cierra al navegar', async () => {
    const { user, router } = renderApp('/')
    const boton = await screen.findByRole('button', { name: 'Menú' })
    expect(boton).toHaveAttribute('aria-expanded', 'false')
    expect(boton).not.toHaveAttribute('aria-controls')

    await user.click(boton)
    expect(boton).toHaveAttribute('aria-expanded', 'true')
    const menu = screen.getByRole('navigation', { name: 'Principal (móvil)' })
    expect(boton).toHaveAttribute('aria-controls', menu.id)

    await user.click(within(menu).getByRole('link', { name: 'Calculadoras' }))
    // /calculadoras se carga bajo demanda: el menú se cierra al completarse la navegación.
    await waitFor(() =>
      expect(
        screen.queryByRole('navigation', { name: 'Principal (móvil)' }),
      ).not.toBeInTheDocument(),
    )
    expect(router.state.location.pathname).toBe('/calculadoras')
  })

  it('Escape cierra el menú móvil y devuelve el foco al botón', async () => {
    const { user } = renderApp('/')
    const boton = await screen.findByRole('button', { name: 'Menú' })
    await user.click(boton)
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('navigation', { name: 'Principal (móvil)' })).not.toBeInTheDocument()
    expect(boton).toHaveFocus()
  })

  it('un hash mal formado no rompe la página ni muestra detalles internos (RD-08)', async () => {
    renderApp('/#%E0%A4%A')
    expect(
      await screen.findByRole('heading', { level: 1, name: /Convierte tu meta/ }),
    ).toBeInTheDocument()
    expect(screen.queryByText(/URIError|at /)).not.toBeInTheDocument()
  })

  it('en /metas el catálogo es el H1 y cada meta un H2', async () => {
    renderApp('/metas')
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Elige una meta' }),
    ).toBeInTheDocument()
    expect(screen.getAllByRole('heading', { level: 2 })).toHaveLength(6)
  })

  it('una dirección desconocida muestra la página no encontrada, sin pedir sesión', async () => {
    const { router } = renderApp('/metass')
    expect(await screen.findByRole('heading', { name: 'Página no encontrada' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/metass')
    expect(screen.getByRole('link', { name: 'Ir al inicio' })).toHaveAttribute('href', '/')
  })
})

describe('inicio público con sesión', () => {
  it.each([
    ['estandar', '/habitos'],
    ['admin', '/admin/permisos'],
  ] as const)('%s: ofrece ir a su panel en lugar del registro', async (como, panel) => {
    renderApp('/', { como })
    expect(await screen.findByRole('link', { name: 'Ir a mi panel' })).toHaveAttribute(
      'href',
      panel,
    )
    expect(screen.queryByRole('link', { name: 'Crear cuenta' })).not.toBeInTheDocument()
  })

  it('en una dirección desconocida, "Ir a mi panel" vuelve al panel del rol', async () => {
    renderApp('/no-existe', { como: 'estandar' })
    expect(await screen.findByRole('heading', { name: 'Página no encontrada' })).toBeInTheDocument()
    expect(
      within(screen.getByRole('main')).getByRole('link', { name: 'Ir a mi panel' }),
    ).toHaveAttribute('href', '/habitos')
  })
})

describe('documentos legales', () => {
  it('muestran su versión y vigencia', async () => {
    renderApp('/legal/terminos')
    expect(await screen.findByRole('heading', { name: 'Términos de uso' })).toBeInTheDocument()
    // La vigencia se muestra en UTC (RD-11): 2026-08-01T00:00:00Z es el 1 de agosto en
    // cualquier zona horaria del navegador.
    expect(screen.getByText(/Versión v2\.1 · vigente desde el 1 ago 2026/)).toBeInTheDocument()
  })

  it('un documento inexistente muestra un estado vacío con su encabezado', async () => {
    renderApp('/legal/inventado')
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Documento no encontrado' }),
    ).toBeInTheDocument()
  })

  it('el pie enlaza los cuatro documentos', async () => {
    renderApp('/')
    const pie = await screen.findByRole('navigation', { name: 'Documentos legales' })
    expect(within(pie).getAllByRole('link')).toHaveLength(4)
  })
})
