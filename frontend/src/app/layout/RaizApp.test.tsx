import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { renderApp } from '@/test/render'

describe('raíz de la aplicación', () => {
  it('cada página pone su título en la pestaña (WCAG 2.4.2)', async () => {
    renderApp('/legal/terminos')
    await screen.findByRole('heading', { name: 'Términos de uso' })
    // El título se asigna en un efecto que React ejecuta después de pintar el encabezado: se espera
    // en lugar de leerlo al instante (en un runner lento la lectura inmediata veía el título vacío).
    await waitFor(() => expect(document.title).toBe('Documentos legales · HabitTracker Nexus'))
  })

  it('tras navegar, el foco pasa al título de la página nueva (WCAG 2.4.3)', async () => {
    const { user } = renderApp('/')
    await user.click(await screen.findByRole('link', { name: 'Calculadoras' }))
    const titulo = await screen.findByRole('heading', { level: 1, name: 'Calculadora de calorías' })
    await waitFor(() => expect(titulo).toHaveFocus())
    await waitFor(() => expect(document.title).toBe('Calculadora de calorías · HabitTracker Nexus'))
  })

  it('en la carga inicial no roba el foco', async () => {
    renderApp('/metas')
    await screen.findByRole('heading', { level: 1, name: 'Elige una meta' })
    expect(document.body).toHaveFocus()
  })
})
