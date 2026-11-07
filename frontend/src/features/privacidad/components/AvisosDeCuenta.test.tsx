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

  it('la X cierra el aviso y no vuelve a salir en esta pestaña', async () => {
    db.consentimientos.set(ana().id, {
      ...db.consentimientos.get(ana().id)!,
      terminosVersion: 'v1.0',
    })
    const { user } = renderApp('/hoy', { como: 'estandar' })
    expect(await screen.findByText('Actualizamos nuestros documentos')).toBeInTheDocument()

    await user.click(
      screen.getByRole('button', { name: 'Cerrar aviso: Actualizamos nuestros documentos' }),
    )
    expect(screen.queryByText('Actualizamos nuestros documentos')).toBeNull()
    expect(JSON.parse(sessionStorage.getItem('habittracker.avisos-descartados') ?? '[]')).toEqual([
      'documentos',
    ])
    // El foco no cae en <body>: pasa al título de la página.
    await waitFor(() =>
      expect(document.activeElement).toBe(screen.getByRole('heading', { level: 1 })),
    )

    // Al navegar a otra pantalla sigue cerrado.
    await user.click(screen.getByRole('link', { name: 'Mis hábitos' }))
    await screen.findByRole('heading', { level: 1, name: 'Mis hábitos' })
    expect(screen.queryByText('Actualizamos nuestros documentos')).toBeNull()
  })

  it('un almacenamiento manipulado no rompe los avisos', async () => {
    sessionStorage.setItem('habittracker.avisos-descartados', '{"no":"es una lista"}')
    db.consentimientos.set(ana().id, {
      ...db.consentimientos.get(ana().id)!,
      terminosVersion: 'v1.0',
    })
    renderApp('/hoy', { como: 'estandar' })
    expect(await screen.findByText('Actualizamos nuestros documentos')).toBeInTheDocument()
  })

  it('al cerrar sesión se olvida lo descartado: otra cuenta del navegador vuelve a ver el aviso', async () => {
    sessionStorage.setItem('habittracker.avisos-descartados', '["documentos"]')
    db.consentimientos.set(ana().id, {
      ...db.consentimientos.get(ana().id)!,
      terminosVersion: 'v1.0',
    })
    const { user } = renderApp('/hoy', { como: 'estandar' })
    await screen.findByRole('heading', {
      level: 1,
      name: /^(Buenos días|Buenas tardes|Buenas noches)/,
    })
    expect(screen.queryByText('Actualizamos nuestros documentos')).toBeNull()

    await user.click(screen.getByRole('button', { name: 'Cerrar sesión' }))
    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(sessionStorage.getItem('habittracker.avisos-descartados')).toBeNull()
  })

  it('en Privacidad y datos el aviso no enlaza a la misma página', async () => {
    db.consentimientos.set(ana().id, {
      ...db.consentimientos.get(ana().id)!,
      terminosVersion: 'v1.0',
    })
    renderApp('/ajustes/privacidad', { como: 'estandar' })
    expect(await screen.findByText('Actualizamos nuestros documentos')).toBeInTheDocument()
    expect(screen.getByText(/Los encuentras abajo, en Consentimientos/)).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Ir a Privacidad y datos' })).toBeNull()
  })
})
