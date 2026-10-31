import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { renderApp } from '@/test/render'

const enTabla = { selector: 'td div' }
const fila = async (correo: string) => (await screen.findByText(correo, enTabla)).closest('tr')!

describe('administración de usuarios', () => {
  it('lista los usuarios y filtra por búsqueda', async () => {
    const { user } = renderApp('/admin/usuarios', { como: 'admin' })
    expect(await screen.findByText('estandar@demo.com', enTabla)).toBeInTheDocument()
    await user.type(screen.getByLabelText('Buscar usuario'), 'luis')
    expect(screen.getByText('otro@demo.com', enTabla)).toBeInTheDocument()
    expect(screen.queryByText('estandar@demo.com', enTabla)).not.toBeInTheDocument()
  })

  it('cambia el rol de otro usuario', async () => {
    const { user } = renderApp('/admin/usuarios', { como: 'admin' })
    const f = await fila('otro@demo.com')
    await user.selectOptions(within(f).getByRole('combobox'), 'Administrador')
    // Dar acceso de administración pide confirmación explícita.
    const dialogo = await screen.findByRole('dialog', { name: 'Cambiar rol' })
    expect(dialogo).toHaveTextContent('Tendrá acceso a la administración')
    await user.click(within(dialogo).getByRole('button', { name: 'Confirmar cambio' }))
    expect(await screen.findByText('Rol actualizado.')).toBeInTheDocument()
    expect(db.usuarios.find((u) => u.correo === 'otro@demo.com')?.rol).toBe('Administrador')
  })

  it('desactiva una cuenta', async () => {
    const { user } = renderApp('/admin/usuarios', { como: 'admin' })
    const f = await fila('otro@demo.com')
    await user.click(within(f).getByRole('button', { name: /^Desactivar a/ }))
    const dialogo = await screen.findByRole('dialog', { name: 'Desactivar cuenta' })
    await user.click(within(dialogo).getByRole('button', { name: 'Desactivar' }))
    expect(await screen.findByText('Cuenta desactivada.')).toBeInTheDocument()
    expect(db.usuarios.find((u) => u.correo === 'otro@demo.com')?.activo).toBe(false)
  })

  it('fuerza el restablecimiento tras confirmar', async () => {
    const { user } = renderApp('/admin/usuarios', { como: 'admin' })
    const f = await fila('otro@demo.com')
    await user.click(within(f).getByRole('button', { name: /^Forzar restablecimiento de/ }))
    const dialogo = screen.getByRole('dialog', { name: 'Forzar restablecimiento' })
    await user.click(within(dialogo).getByRole('button', { name: 'Forzar restablecimiento' }))
    expect(await screen.findByText('Restablecimiento forzado.')).toBeInTheDocument()
    expect(db.usuarios.find((u) => u.correo === 'otro@demo.com')?.password).toBeNull()
  })

  it('no permite que el Administrador se modifique a sí mismo', async () => {
    renderApp('/admin/usuarios', { como: 'admin' })
    const f = await fila('admin@demo.com')
    expect(within(f).getByRole('combobox')).toBeDisabled()
    expect(within(f).getByRole('button', { name: /^Desactivar a/ })).toBeDisabled()
  })
})

describe('confirmaciones de cuentas', () => {
  it('cancelar el cambio de rol no modifica nada y el selector vuelve al valor actual', async () => {
    const { user } = renderApp('/admin/usuarios', { como: 'admin' })
    const f = await fila('otro@demo.com')
    await user.selectOptions(within(f).getByRole('combobox'), 'Administrador')
    const dialogo = await screen.findByRole('dialog', { name: 'Cambiar rol' })
    await user.click(within(dialogo).getByRole('button', { name: 'Cancelar' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(db.usuarios.find((u) => u.correo === 'otro@demo.com')?.rol).toBe('Estandar')
    expect(within(f).getByRole('combobox')).toHaveValue('Estandar')
  })

  it('reactivar una cuenta inactiva no necesita confirmación', async () => {
    const { user } = renderApp('/admin/usuarios', { como: 'admin' })
    const f = await fila('inactiva@demo.com')
    await user.click(within(f).getByRole('button', { name: /^Activar a/ }))
    expect(await screen.findByText('Cuenta activada.')).toBeInTheDocument()
  })

  it('muestra el resumen de cuentas', async () => {
    renderApp('/admin/usuarios', { como: 'admin' })
    const resumen = await screen.findByRole('region', { name: 'Resumen de usuarios' })
    expect(within(resumen).getByText('Cuentas')).toBeInTheDocument()
  })
})
