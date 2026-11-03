import { screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { problema } from '@/mocks/http'
import { server } from '@/mocks/server'
import { completarOnboarding } from '@/test/onboarding'
import { CORREOS, renderApp, renderAppConEstado } from '@/test/render'
import { guardarRegistroPendiente } from '../api'

// Luis (otro@demo.com) se usa como cuenta recién registrada: se le quita el perfil.
const luis = () => db.usuarios.find((u) => u.correo === CORREOS.otro)?.id ?? ''

function recordarRegistro(extra: Partial<Parameters<typeof guardarRegistroPendiente>[0]> = {}) {
  guardarRegistroPendiente({
    correo: CORREOS.otro,
    terminosVersion: 'v2.1',
    privacidadVersion: 'v1.4',
    marketing: false,
    metaSlug: 'fuerza-en-casa',
    datosCorporales: null,
    ...extra,
  })
}

beforeEach(() => {
  db.perfiles.delete(luis())
  db.consentimientos.delete(luis())
  localStorage.clear()
})

describe('onboarding (pasos 3 a 5 del alta)', () => {
  it('una cuenta sin perfil que entra al panel va al onboarding', async () => {
    const { router } = renderApp('/habitos', { como: 'otro' })
    expect(await screen.findByRole('heading', { name: 'Tus datos básicos' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/bienvenida')
  })

  it('quien ya tiene perfil no vuelve a ver el onboarding', async () => {
    const { router } = renderApp('/bienvenida', { como: 'estandar' })
    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: /^(Buenos días|Buenas tardes|Buenas noches)/,
      }),
    ).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/hoy')
  })

  it('el Administrador no tiene onboarding', async () => {
    renderApp('/bienvenida', { como: 'admin' })
    expect(await screen.findByText('No tienes acceso a esta sección')).toBeInTheDocument()
  })

  it('valida cada paso antes de avanzar y "Atrás" conserva lo escrito', async () => {
    recordarRegistro()
    const { user } = renderApp('/bienvenida', { como: 'otro' })
    await user.click(await screen.findByRole('button', { name: 'Continuar' }))
    expect(await screen.findByText('Escribe tu edad.')).toBeInTheDocument()
    expect(screen.getByText('Escribe tu altura.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Tus datos básicos' })).toBeInTheDocument()

    await user.type(screen.getByLabelText('Edad'), '15')
    await user.click(screen.getByRole('button', { name: 'Continuar' }))
    expect(await screen.findByText('Debe ser al menos 16 años.')).toBeInTheDocument()

    await user.clear(screen.getByLabelText('Edad'))
    await user.type(screen.getByLabelText('Edad'), '40')
    await user.selectOptions(screen.getByLabelText(/^Sexo/), 'hombre')
    await user.type(screen.getByLabelText('Altura (cm)'), '180')
    await user.type(screen.getByLabelText('Peso (kg)'), '82')
    await user.click(screen.getByRole('button', { name: 'Continuar' }))

    const titulo = await screen.findByRole('heading', { name: 'Tu actividad y tu tiempo' })
    expect(titulo).toHaveFocus()
    expect(screen.getByText('Paso 4 de 5')).toBeInTheDocument()
    // La meta recordada del registro viene elegida.
    expect(screen.getByLabelText(/^Tu meta/)).toHaveValue('fuerza-en-casa')

    await user.click(screen.getByRole('button', { name: 'Atrás' }))
    expect(await screen.findByLabelText('Edad')).toHaveValue(40)
  })

  it('propone los datos que se escribieron en la calculadora', async () => {
    recordarRegistro({
      datosCorporales: { edad: 27, sexo: 'mujer', alturaCm: 160, pesoKg: 55.5, actividad: 'alta' },
    })
    const { user } = renderApp('/bienvenida', { como: 'otro' })
    expect(await screen.findByLabelText('Edad')).toHaveValue(27)
    expect(screen.getByLabelText(/^Sexo/)).toHaveValue('mujer')
    expect(screen.getByLabelText('Peso (kg)')).toHaveValue(55.5)
    await user.click(screen.getByRole('button', { name: 'Continuar' }))
    expect(await screen.findByLabelText('Nivel de actividad')).toHaveValue('alta')
  })

  it('desde otro dispositivo vuelve a pedir los consentimientos y los registra', async () => {
    const { user } = renderApp('/bienvenida', { como: 'otro' })
    expect(await screen.findByRole('heading', { name: 'Tus datos básicos' })).toBeInTheDocument()

    // Sin aceptarlos, no se guarda nada.
    await completarOnboarding(user)
    expect(
      await screen.findByText('Debes aceptar los Términos de uso y el Aviso de salud.'),
    ).toBeInTheDocument()
    expect(db.perfiles.has(luis())).toBe(false)

    await user.click(screen.getByRole('checkbox', { name: /Términos de uso/ }))
    await user.click(screen.getByRole('checkbox', { name: /Política de privacidad/ }))
    await user.click(screen.getByRole('button', { name: 'Empezar mi plan' }))
    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: /^(Buenos días|Buenas tardes|Buenas noches)/,
      }),
    ).toBeInTheDocument()
    expect(db.consentimientos.get(luis())).toMatchObject({ datosSalud: true, marketing: false })
    expect(db.perfiles.get(luis())).toMatchObject({ metaSlug: null })
  })

  it('al terminar olvida el registro recordado en el dispositivo', async () => {
    recordarRegistro()
    const { user } = renderApp('/bienvenida', { como: 'otro' })
    await completarOnboarding(user)
    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: /^(Buenos días|Buenas tardes|Buenas noches)/,
      }),
    ).toBeInTheDocument()
    expect(localStorage.getItem('habittracker.registro-pendiente')).toBeNull()
    expect(db.perfiles.get(luis())).toMatchObject({ metaSlug: 'fuerza-en-casa' })
  })

  it('exige confirmar el aviso de salud', async () => {
    recordarRegistro()
    const { user } = renderApp('/bienvenida', { como: 'otro' })
    await user.type(await screen.findByLabelText('Edad'), '30')
    await user.selectOptions(screen.getByLabelText(/^Sexo/), 'mujer')
    await user.type(screen.getByLabelText('Altura (cm)'), '165')
    await user.type(screen.getByLabelText('Peso (kg)'), '60')
    await user.click(screen.getByRole('button', { name: 'Continuar' }))
    await user.selectOptions(await screen.findByLabelText('Nivel de actividad'), 'ligera')
    await user.click(screen.getByRole('button', { name: 'Continuar' }))
    await user.click(await screen.findByRole('button', { name: 'Empezar mi plan' }))
    expect(await screen.findByText('Confirma que leíste el aviso de salud.')).toBeInTheDocument()
    expect(db.perfiles.has(luis())).toBe(false)
  })

  it('con una versión nueva de los términos vuelve a pedir la aceptación', async () => {
    recordarRegistro({ terminosVersion: 'v2.0' })
    const { user } = renderApp('/bienvenida', { como: 'otro' })
    await completarOnboarding(user)
    expect(
      await screen.findByText('Debes aceptar los Términos de uso y el Aviso de salud.'),
    ).toBeInTheDocument()
    expect(db.consentimientos.has(luis())).toBe(false)
  })

  it('si falla el guardado muestra un mensaje genérico y no avanza', async () => {
    recordarRegistro()
    server.use(
      http.put('/api/profile', () =>
        problema(500, 'Error interno.', 'at PerfilService.Guardar() in /srv/api/Perfil.cs:42'),
      ),
    )
    const { user, router } = renderApp('/bienvenida', { como: 'otro' })
    await completarOnboarding(user)
    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo guardar tu plan.')
    expect(screen.queryByText(/PerfilService|Perfil\.cs/)).not.toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/bienvenida')
  })

  it('al terminar vuelve a la página que se había pedido', async () => {
    recordarRegistro()
    const { user, router } = renderApp('/permisos', { como: 'otro' })
    await completarOnboarding(user)
    expect(await screen.findByRole('heading', { name: 'Mis permisos' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/permisos')
  })

  it('no sigue un destino externo que llegue en el estado de navegación', async () => {
    recordarRegistro()
    const { user, router } = renderAppConEstado('/bienvenida', { desde: '//evil.example' }, 'otro')
    await completarOnboarding(user)
    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: /^(Buenos días|Buenas tardes|Buenas noches)/,
      }),
    ).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/hoy')
  })
})

describe('con el backend real (sin el contrato propuesto)', () => {
  // Una ruta que el backend no tiene responde 404 sin cuerpo.
  beforeEach(() =>
    server.use(http.get('/api/profile', () => new HttpResponse(null, { status: 404 }))),
  )

  it('los hábitos siguen disponibles: el guard no bloquea', async () => {
    const { router } = renderApp('/habitos', { como: 'otro' })
    expect(await screen.findByRole('heading', { name: 'Mis hábitos' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/habitos')
  })

  it('el onboarding avisa que la pieza llegará pronto', async () => {
    renderApp('/bienvenida', { como: 'otro' })
    expect(
      await screen.findByRole('heading', { name: 'El perfil llegará pronto' }),
    ).toBeInTheDocument()
  })
})
