import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { renderApp } from '@/test/render'

const ana = () => db.usuarios[0]
const DIA = 86_400_000

describe('avisos de cuenta (en toda la aplicación)', () => {
  it('sin nada pendiente no muestra avisos', async () => {
    renderApp('/hoy', { como: 'estandar' })
    await screen.findByRole('heading', {
      level: 1,
      name: /^(Buenos días|Buenas tardes|Buenas noches)/,
    })
    expect(screen.queryByText('Actualizamos nuestros documentos')).toBeNull()
    expect(screen.queryByText('Tu cuenta se eliminará')).toBeNull()
  })

  it('si cambiaron las versiones legales invita a aceptarlas, sin bloquear el uso', async () => {
    db.consentimientos.set(ana().id, {
      ...db.consentimientos.get(ana().id)!,
      terminosVersion: 'v1.0',
    })
    const { user, router } = renderApp('/hoy', { como: 'estandar' })

    expect(await screen.findByText('Actualizamos nuestros documentos')).toBeInTheDocument()
    // Se puede seguir usando la app: el aviso no tapa el contenido.
    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: /^(Buenos días|Buenas tardes|Buenas noches)/,
      }),
    ).toBeInTheDocument()

    await user.click(screen.getByRole('link', { name: 'Ir a Privacidad y datos' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/ajustes/privacidad'))
  })

  it('«Aceptar las versiones vigentes» actualiza el consentimiento y quita el aviso', async () => {
    db.consentimientos.set(ana().id, {
      ...db.consentimientos.get(ana().id)!,
      terminosVersion: 'v1.0',
      marketing: true,
    })
    const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })

    await user.click(await screen.findByRole('button', { name: 'Aceptar las versiones vigentes' }))

    await waitFor(() => expect(screen.queryByText('Actualizamos nuestros documentos')).toBeNull())
    expect(screen.queryByRole('button', { name: 'Aceptar las versiones vigentes' })).toBeNull()
    const vigente = db.consentimientos.get(ana().id)
    expect(vigente?.terminosVersion).not.toBe('v1.0')
    // Aceptar las versiones no cambia las demás decisiones.
    expect(vigente?.marketing).toBe(true)
  })

  it('con la eliminación programada avisa de la fecha y permite cancelarla', async () => {
    db.bajas.set(ana().id, {
      solicitadaEn: new Date().toISOString(),
      borradoEn: new Date(Date.now() + 12 * DIA).toISOString(),
    })
    const { user } = renderApp('/hoy', { como: 'estandar' })

    expect(await screen.findByText('Tu cuenta se eliminará')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Cancelar eliminación' }))

    await waitFor(() => expect(screen.queryByText('Tu cuenta se eliminará')).toBeNull())
    expect(db.bajas.size).toBe(0)
  })

  it('el Administrador no ve estos avisos (no puede darse de baja)', async () => {
    db.consentimientos.clear()
    renderApp('/admin', { como: 'admin' })
    await screen.findByRole('heading', { level: 1, name: 'Resumen' })
    // Margen para que /me y la baja lleguen: la ausencia solo vale una vez que ya habrían respondido.
    await new Promise((r) => setTimeout(r, 300))
    expect(screen.queryByText('Actualizamos nuestros documentos')).toBeNull()
    expect(screen.queryByText('Tu cuenta se eliminará')).toBeNull()
  })
})
