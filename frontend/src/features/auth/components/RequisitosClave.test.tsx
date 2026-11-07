import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { RequisitosClave } from './RequisitosClave'

describe('RequisitosClave', () => {
  it('cuenta los requisitos cumplidos y anuncia el estado de cada uno', () => {
    const { rerender } = render(<RequisitosClave id="req" clave="" />)
    expect(screen.getByText('0 de 3')).toBeInTheDocument()
    expect(screen.getAllByText('(pendiente)')).toHaveLength(3)

    rerender(<RequisitosClave id="req" clave="abcdefghijk1" />)
    expect(screen.getByText('3 de 3')).toBeInTheDocument()
    expect(screen.getAllByText('(cumplido)')).toHaveLength(3)
  })
})
