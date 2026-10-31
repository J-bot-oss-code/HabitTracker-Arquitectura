import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { AnilloProgreso } from './AnilloProgreso'

describe('AnilloProgreso', () => {
  it('expone el progreso como progressbar accesible', () => {
    render(<AnilloProgreso porcentaje={42.4} etiqueta="Progreso de hoy" />)
    const barra = screen.getByRole('progressbar', { name: 'Progreso de hoy' })
    expect(barra).toHaveAttribute('aria-valuenow', '42')
    expect(barra).toHaveTextContent('42%')
  })

  it.each([
    [-10, '0'],
    [250, '100'],
  ])('limita %s a 0–100', (entrada, esperado) => {
    render(<AnilloProgreso porcentaje={entrada} etiqueta="Progreso" />)
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', esperado)
  })
})
