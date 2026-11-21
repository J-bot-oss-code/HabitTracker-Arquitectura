import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { renderApp } from '@/test/render'
import { PASOS, PREGUNTAS, VENTAJAS } from '../guia'

async function paginaGuia(opciones?: Parameters<typeof renderApp>[1]) {
  const vista = renderApp('/como-funciona', opciones)
  await screen.findByRole('heading', { level: 1, name: 'Cómo funciona HabitTracker Nexus' })
  return vista
}

describe('guía «Cómo funciona» (/como-funciona)', () => {
  it('sin sesión: un solo H1, las tres secciones y la llamada final para registrarse', async () => {
    await paginaGuia()

    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
    const principal = screen.getByRole('main')
    const secciones = within(principal)
      .getAllByRole('heading', { level: 2 })
      .map((h) => h.textContent)
    expect(secciones).toEqual([
      'Qué te ofrece',
      'Cómo empezar',
      'Preguntas frecuentes',
      'Empieza hoy, a tu ritmo',
    ])

    // Qué ofrece: una tarjeta por ventaja, con su H3.
    for (const v of VENTAJAS) {
      expect(within(principal).getByRole('heading', { level: 3, name: v.titulo })).toBeVisible()
    }

    // Cómo empezar: lista numerada con los cinco pasos, en orden.
    const pasos = within(principal)
      .getAllByRole('listitem')
      .filter((li) => li.closest('ol'))
    expect(pasos).toHaveLength(PASOS.length)
    expect(pasos[0]).toHaveTextContent('Crea tu cuenta')
    expect(pasos[4]).toHaveTextContent('Haz tu primer check-in en «Hoy»')

    const final = screen.getByRole('region', { name: 'Empieza hoy, a tu ritmo' })
    expect(within(final).getByRole('link', { name: 'Crear cuenta' })).toHaveAttribute(
      'href',
      '/registro',
    )
    expect(within(final).getByRole('link', { name: 'Iniciar sesión' })).toHaveAttribute(
      'href',
      '/login',
    )
    expect(within(final).queryByRole('link', { name: 'Ir a Hoy' })).not.toBeInTheDocument()
  })

  it('las preguntas frecuentes son <details> nativos y se abren desde su resumen', async () => {
    const { user } = await paginaGuia()
    const principal = screen.getByRole('main')
    const detalles = principal.querySelectorAll('details')
    expect(detalles).toHaveLength(PREGUNTAS.length)

    const resumen = screen.getByText('¿Puedo entrar con Google?')
    const detalle = resumen.closest('details')!
    expect(detalle).not.toHaveAttribute('open')

    await user.click(resumen)
    expect(detalle).toHaveAttribute('open')
    expect(within(detalle).getByText(/contraseña una sola vez/)).toBeInTheDocument()
    expect(within(detalle).getByText(/¿Olvidaste tu contraseña?/)).toBeInTheDocument()
  })

  it('no promete nada sobre el peso ni las medidas (el cuerpo no se mide ni se premia)', async () => {
    await paginaGuia()
    expect(screen.getByRole('main')).not.toHaveTextContent(
      /\b(peso|kg|medidas|calorías|dieta|adelgaz|perder)\b/i,
    )
  })

  it('con sesión de Estándar: se ve igual y la llamada final lleva a Hoy', async () => {
    await paginaGuia({ como: 'estandar' })
    const final = screen.getByRole('region', { name: 'Sigue donde lo dejaste' })
    expect(within(final).getByRole('link', { name: 'Ir a Hoy' })).toHaveAttribute('href', '/hoy')
    expect(within(final).queryByRole('link', { name: 'Crear cuenta' })).not.toBeInTheDocument()
    expect(within(final).queryByRole('link', { name: 'Iniciar sesión' })).not.toBeInTheDocument()
    expect(screen.getByRole('main').querySelectorAll('details')).toHaveLength(PREGUNTAS.length)
  })

  it('con sesión de Administrador: la llamada final lleva a su panel, no a Hoy', async () => {
    await paginaGuia({ como: 'admin' })
    const final = screen.getByRole('region', { name: 'Sigue donde lo dejaste' })
    expect(within(final).getByRole('link', { name: 'Ir a mi panel' })).toHaveAttribute(
      'href',
      '/admin',
    )
    expect(within(final).queryByRole('link', { name: 'Ir a Hoy' })).not.toBeInTheDocument()
  })

  it('la cabecera pública enlaza la guía y la marca como página actual', async () => {
    await paginaGuia()
    const nav = screen.getByRole('navigation', { name: 'Principal' })
    expect(within(nav).getByRole('link', { name: 'Cómo funciona' })).toHaveAttribute(
      'aria-current',
      'page',
    )
  })
})

describe('enlaces hacia la guía', () => {
  it('la landing ofrece «Ver la guía completa» y navega a ella', async () => {
    const { user, router } = renderApp('/')
    await user.click(await screen.findByRole('link', { name: /Ver la guía completa/ }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/como-funciona'))
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Cómo funciona HabitTracker Nexus' }),
    ).toBeInTheDocument()
  })

  it('el menú de la cabecera pública lleva a la guía', async () => {
    const { user, router } = renderApp('/metas')
    const nav = await screen.findByRole('navigation', { name: 'Principal' })
    await user.click(within(nav).getByRole('link', { name: 'Cómo funciona' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/como-funciona'))
  })

  it('Hoy tiene un enlace discreto «¿Cómo funciona?» que abre la guía', async () => {
    const { user, router } = renderApp('/hoy', { como: 'estandar' })
    const enlace = await screen.findByRole('link', { name: '¿Cómo funciona?' })
    expect(enlace).toHaveAttribute('href', '/como-funciona')
    await user.click(enlace)
    await waitFor(() => expect(router.state.location.pathname).toBe('/como-funciona'))
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Cómo funciona HabitTracker Nexus' }),
    ).toBeInTheDocument()
  })
})
