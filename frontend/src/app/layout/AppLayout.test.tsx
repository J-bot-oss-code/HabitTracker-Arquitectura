import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { renderApp } from '@/test/render'

// Menú principal desde lg: un botón de tres líneas abre el panel lateral (en jsdom no hay media
// queries, así que se comprueba el comportamiento, no qué se ve en cada ancho).
describe('menú de tres líneas', () => {
  it('abre el panel, lleva el foco a la página actual y se cierra con Escape', async () => {
    const { user } = renderApp('/hoy', { como: 'estandar' })
    const boton = await screen.findByRole('button', { name: 'Menú' })
    expect(boton).toHaveAttribute('aria-expanded', 'false')

    await user.click(boton)
    expect(boton).toHaveAttribute('aria-expanded', 'true')
    const nav = screen.getByRole('navigation', { name: 'Principal' })
    expect(boton).toHaveAttribute('aria-controls', nav.id)
    expect(within(nav).getByRole('link', { name: 'Hoy' })).toHaveFocus()

    await user.keyboard('{Escape}')
    expect(boton).toHaveAttribute('aria-expanded', 'false')
    expect(boton).toHaveFocus()
  })

  it('se cierra al elegir una sección y con el botón Cerrar menú', async () => {
    const { user, router } = renderApp('/hoy', { como: 'estandar' })
    const boton = await screen.findByRole('button', { name: 'Menú' })

    await user.click(boton)
    const nav = screen.getByRole('navigation', { name: 'Principal' })
    await user.click(within(nav).getByRole('link', { name: 'Mis hábitos' }))
    // La ruta es perezosa: la navegación termina un instante después.
    await waitFor(() => expect(router.state.location.pathname).toBe('/habitos'))
    expect(boton).toHaveAttribute('aria-expanded', 'false')

    await user.click(boton)
    expect(document.body.style.overflow).toBe('hidden')
    expect(document.getElementById('contenido')).toHaveAttribute('inert')
    await user.click(screen.getByRole('button', { name: 'Cerrar menú' }))
    expect(document.body.style.overflow).toBe('')
    expect(document.getElementById('contenido')).not.toHaveAttribute('inert')
    expect(boton).toHaveAttribute('aria-expanded', 'false')
    expect(boton).toHaveFocus()
  })
})
