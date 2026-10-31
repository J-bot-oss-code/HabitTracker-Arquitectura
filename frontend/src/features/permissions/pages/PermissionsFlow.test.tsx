import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { renderApp } from '@/test/render'

const LEER = '7c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e01'

describe('permisos', () => {
  it('el Estándar solicita un permiso y lo ve como Pendiente', async () => {
    const { user } = renderApp('/permisos', { como: 'estandar' })
    expect(await screen.findByText('Aún no has solicitado permisos.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Enviar solicitud' }))

    expect(await screen.findByText('Pendiente')).toBeInTheDocument()
    expect(db.solicitudes.some((s) => s.solicitante === 'estandar@demo.com')).toBe(true)
  })

  it('el Administrador aprueba y el Estándar puede reconfigurar (RF-GP-05)', async () => {
    db.solicitudes.push({
      id: 'b1c2d3e4-0000-4000-8000-000000000099',
      solicitante: 'estandar@demo.com',
      permisoSolicitado: 'Habits.AdvancedReconfig',
      estado: 'Pendiente',
      aprobador: null,
      fechaCreacion: new Date().toISOString(),
      fechaResolucion: null,
      motivoRechazo: null,
    })

    const admin = renderApp('/admin/permisos', { como: 'admin' })
    const fila = (await screen.findByText('estandar@demo.com', { selector: 'td' })).closest('tr')!
    await admin.user.click(within(fila).getByRole('button', { name: 'Aprobar' }))
    expect(await screen.findByText('Solicitud aprobada y aplicada.')).toBeInTheDocument()
    expect(db.solicitudes.find((s) => s.solicitante === 'estandar@demo.com')?.estado).toBe(
      'Aplicada',
    )
  })

  it('con el permiso aplicado se puede reconfigurar el hábito', async () => {
    db.solicitudes.push({
      id: 'b1c2d3e4-0000-4000-8000-000000000098',
      solicitante: 'estandar@demo.com',
      permisoSolicitado: 'Habits.AdvancedReconfig',
      estado: 'Aplicada',
      aprobador: 'admin@demo.com',
      fechaCreacion: new Date().toISOString(),
      fechaResolucion: new Date().toISOString(),
      motivoRechazo: null,
    })
    const { user } = renderApp(`/habitos/${LEER}`, { como: 'estandar' })
    const objetivo = await screen.findByRole('spinbutton', { name: 'Objetivo' })
    await user.clear(objetivo)
    await user.type(objetivo, '30')
    await user.click(screen.getByRole('button', { name: 'Guardar reconfiguración' }))

    expect(await screen.findByText(/Comienza un ciclo nuevo/)).toBeInTheDocument()
    expect(db.habitos.find((h) => h.id === LEER)?.cantidadObjetivo).toBe(30)
  })

  it('el Administrador rechaza con motivo', async () => {
    const { user } = renderApp('/admin/permisos', { como: 'admin' })
    const fila = (await screen.findByText('otro@demo.com', { selector: 'td' })).closest('tr')!
    await user.click(within(fila).getByRole('button', { name: 'Rechazar' }))

    const dialogo = screen.getByRole('dialog', { name: 'Rechazar solicitud' })
    await user.click(within(dialogo).getByRole('button', { name: 'Rechazar' }))
    expect(await within(dialogo).findByText('Indica el motivo del rechazo.')).toBeInTheDocument()

    await user.type(within(dialogo).getByLabelText('Motivo del rechazo'), 'No justificado')
    await user.click(within(dialogo).getByRole('button', { name: 'Rechazar' }))
    expect(await screen.findByText('Solicitud rechazada.')).toBeInTheDocument()
    expect(db.solicitudes[0]).toMatchObject({
      estado: 'Rechazada',
      motivoRechazo: 'No justificado',
    })
  })

  it('nadie resuelve su propia solicitud (RF-GP-07)', async () => {
    db.solicitudes.push({
      id: 'b1c2d3e4-0000-4000-8000-000000000097',
      solicitante: 'admin@demo.com',
      permisoSolicitado: 'Habits.AdvancedReconfig',
      estado: 'Pendiente',
      aprobador: null,
      fechaCreacion: new Date().toISOString(),
      fechaResolucion: null,
      motivoRechazo: null,
    })
    renderApp('/admin/permisos', { como: 'admin' })
    const fila = (await screen.findByText('admin@demo.com', { selector: 'td' })).closest('tr')!
    expect(within(fila).getByText('No puedes resolver tu propia solicitud')).toBeInTheDocument()
    expect(within(fila).queryByRole('button', { name: 'Aprobar' })).not.toBeInTheDocument()
  })
})
