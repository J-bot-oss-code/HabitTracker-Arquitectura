import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { FormEvent } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { PasswordInput } from './Field'

describe('PasswordInput', () => {
  it('oculta la contraseña y la muestra u oculta con el botón del ojo', async () => {
    const user = userEvent.setup()
    render(<PasswordInput etiqueta="Contraseña" />)
    const campo = screen.getByLabelText('Contraseña')
    expect(campo).toHaveAttribute('type', 'password')

    const ojo = screen.getByRole('button', { name: 'Mostrar contraseña' })
    expect(ojo).toHaveAttribute('aria-pressed', 'false')
    await user.click(ojo)
    expect(campo).toHaveAttribute('type', 'text')
    expect(ojo).toHaveAttribute('aria-pressed', 'true')

    await user.click(ojo)
    expect(campo).toHaveAttribute('type', 'password')
  })

  it('el botón del ojo no envía el formulario', async () => {
    const user = userEvent.setup()
    const enviar = vi.fn((e: FormEvent) => e.preventDefault())
    render(
      <form onSubmit={enviar}>
        <PasswordInput etiqueta="Contraseña" />
      </form>,
    )
    await user.click(screen.getByRole('button', { name: 'Mostrar contraseña' }))
    expect(enviar).not.toHaveBeenCalled()
  })

  it('al enviar vuelve a ocultarla para que el navegador no la guarde como texto', async () => {
    const user = userEvent.setup()
    render(
      <form onSubmit={(e) => e.preventDefault()}>
        <PasswordInput etiqueta="Contraseña" />
        <button type="submit">Enviar</button>
      </form>,
    )
    await user.click(screen.getByRole('button', { name: 'Mostrar contraseña' }))
    expect(screen.getByLabelText('Contraseña')).toHaveAttribute('type', 'text')
    await user.click(screen.getByRole('button', { name: 'Enviar' }))
    expect(screen.getByLabelText('Contraseña')).toHaveAttribute('type', 'password')
  })

  it('conserva lo escrito al cambiar la visibilidad', async () => {
    const user = userEvent.setup()
    render(<PasswordInput etiqueta="Contraseña" />)
    const campo = screen.getByLabelText('Contraseña')
    await user.type(campo, 'texto escrito')
    await user.click(screen.getByRole('button', { name: 'Mostrar contraseña' }))
    expect(campo).toHaveValue('texto escrito')
  })
})
