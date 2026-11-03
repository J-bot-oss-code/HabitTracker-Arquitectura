import { screen, within } from '@testing-library/react'
import type { UserEvent } from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { CODIGO_RECUPERACION_DEMO, db, TOKEN_ACTIVACION_DEMO } from '@/mocks/db'
import { leerRegistroPendiente } from '@/features/perfil/api'
import { completarOnboarding } from '@/test/onboarding'
import { renderApp, renderAppConEstado } from '@/test/render'

describe('flujos de cuenta', () => {
  it('registro con meta → activación → login → onboarding → panel', async () => {
    const { user } = renderApp('/registro?meta=primeros-5-km')
    expect(await screen.findByText('Paso 1 de 5')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Nombre completo'), 'Nueva Persona')
    await user.type(screen.getByLabelText('Correo'), 'nueva@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), 'Clave1234')
    await user.type(screen.getByLabelText('Confirmar contraseña'), 'Clave1234')
    await user.click(screen.getByRole('checkbox', { name: /Términos de uso/ }))
    await user.click(screen.getByRole('checkbox', { name: /Política de privacidad/ }))
    await user.click(screen.getByRole('checkbox', { name: /correos con consejos/ }))
    await user.click(screen.getByRole('button', { name: 'Crear cuenta y continuar' }))
    expect(await screen.findByText('Cuenta creada')).toBeInTheDocument()

    await user.click(screen.getByRole('link', { name: 'Tengo el código de activación' }))
    await user.type(screen.getByLabelText('Código de activación'), TOKEN_ACTIVACION_DEMO)
    await user.click(screen.getByRole('button', { name: 'Activar' }))
    expect(await screen.findByText(/Tu cuenta está activa/)).toBeInTheDocument()

    await user.click(screen.getByRole('link', { name: 'Ir a iniciar sesión' }))
    await user.type(screen.getByLabelText('Correo'), 'nueva@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), 'Clave1234')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    // Sin perfil, el panel lleva al onboarding con la meta del registro.
    expect(await screen.findByRole('heading', { name: 'Tus datos básicos' })).toBeInTheDocument()
    expect(screen.getByText('Paso 3 de 5')).toBeInTheDocument()
    await completarOnboarding(user)
    expect(await screen.findByRole('heading', { name: 'Mis hábitos' })).toBeInTheDocument()

    const nueva = db.usuarios.find((u) => u.correo === 'nueva@demo.com')
    expect(db.perfiles.get(nueva?.id ?? '')).toMatchObject({
      edad: 31,
      sexo: null,
      metaSlug: 'primeros-5-km',
      avisoSaludAceptado: true,
    })
    // Los consentimientos del registro llegan al servidor sin volver a pedirlos.
    expect(db.consentimientos.get(nueva?.id ?? '')).toMatchObject({
      terminosVersion: 'v2.1',
      privacidadVersion: 'v1.4',
      datosSalud: true,
      marketing: true,
    })
  })

  it('no crea la cuenta sin los consentimientos obligatorios', async () => {
    const { user } = renderApp('/registro')
    await user.type(await screen.findByLabelText('Nombre completo'), 'Sin Consentir')
    await user.type(screen.getByLabelText('Correo'), 'sin@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), 'Clave1234')
    await user.type(screen.getByLabelText('Confirmar contraseña'), 'Clave1234')
    // Ninguna casilla llega marcada.
    for (const casilla of screen.getAllByRole('checkbox')) expect(casilla).not.toBeChecked()
    await user.click(screen.getByRole('button', { name: 'Crear cuenta y continuar' }))

    expect(
      await screen.findByText('Debes aceptar los Términos de uso y el Aviso de salud.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        'Debes aceptar la Política de privacidad y el tratamiento de datos de salud.',
      ),
    ).toBeInTheDocument()
    expect(db.usuarios.some((u) => u.correo === 'sin@demo.com')).toBe(false)
  })

  it('rechaza registrar un correo existente con un mensaje genérico', async () => {
    const { user } = renderApp('/registro')
    await user.type(screen.getByLabelText('Nombre completo'), 'Duplicado')
    await user.type(screen.getByLabelText('Correo'), 'estandar@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), 'Clave1234')
    await user.type(screen.getByLabelText('Confirmar contraseña'), 'Clave1234')
    await user.click(screen.getByRole('checkbox', { name: /Términos de uso/ }))
    await user.click(screen.getByRole('checkbox', { name: /Política de privacidad/ }))
    await user.click(screen.getByRole('button', { name: 'Crear cuenta y continuar' }))
    const alerta = await screen.findByRole('alert')
    // Orienta sin confirmar ni negar que el correo exista (RF-CA-01) y ofrece salidas.
    expect(alerta).toHaveTextContent('No pudimos crear la cuenta con estos datos.')
    expect(within(alerta).getByRole('link', { name: 'Iniciar sesión' })).toHaveAttribute(
      'href',
      '/login',
    )
    expect(within(alerta).getByRole('link', { name: 'Recuperar contraseña' })).toHaveAttribute(
      'href',
      '/recuperar',
    )
    expect(alerta.closest('[tabindex="-1"]')).toHaveFocus()
  })

  it('muestra la meta elegida en el panel (escritorio) y sobre el formulario (móvil)', async () => {
    renderApp('/registro?meta=dormir-mejor')
    const panel = await screen.findByRole('complementary')
    expect(within(panel).getByText('Dormir mejor')).toBeInTheDocument()
    expect(within(panel).getByRole('link', { name: 'Volver a explorar metas' })).toHaveAttribute(
      'href',
      '/metas',
    )
    expect(screen.getByText(/Meta elegida:/)).toHaveTextContent('Meta elegida: Dormir mejor')
  })

  it('una meta inventada en la URL no se muestra', async () => {
    renderApp('/registro?meta=%3Cscript%3E')
    expect(await screen.findByRole('heading', { name: 'Crea tu cuenta' })).toBeInTheDocument()
    expect(screen.queryByText(/Meta elegida/)).not.toBeInTheDocument()
  })

  it('los datos de la calculadora llegan al registro y se recuerdan para el onboarding', async () => {
    const { user } = renderApp('/calculadoras')
    await user.type(await screen.findByLabelText('Edad'), '33')
    await user.selectOptions(screen.getByLabelText('Sexo'), 'hombre')
    await user.type(screen.getByLabelText('Altura (cm)'), '181')
    await user.type(screen.getByLabelText('Peso (kg)'), '77')
    await user.selectOptions(screen.getByLabelText('Actividad'), 'alta')
    await user.click(screen.getByRole('button', { name: 'Calcular' }))
    await user.click(await screen.findByRole('link', { name: 'Guardar y crear mi plan' }))

    await registrarCuenta(user, 'calcula@demo.com')
    expect(leerRegistroPendiente('calcula@demo.com')?.datosCorporales).toEqual({
      edad: 33,
      sexo: 'hombre',
      alturaCm: 181,
      pesoKg: 77,
      actividad: 'alta',
    })
  })

  it('descarta datos de calculadora manipulados en el estado de navegación', async () => {
    const { user } = renderAppConEstado('/registro', {
      datosCalculadora: { edad: 5, sexo: 'x', alturaCm: 999, pesoKg: -1, actividad: 'nada' },
    })
    await registrarCuenta(user, 'manipula@demo.com')
    expect(leerRegistroPendiente('manipula@demo.com')).toMatchObject({ datosCorporales: null })
  })

  it('activa automáticamente con el token del enlace', async () => {
    renderApp(`/activar?token=${TOKEN_ACTIVACION_DEMO}`)
    expect(await screen.findByText(/Tu cuenta está activa/)).toBeInTheDocument()
  })

  it('recuperar → restablecer → login con la nueva contraseña', async () => {
    const { user } = renderApp('/recuperar')
    await user.type(screen.getByLabelText('Correo'), 'estandar@demo.com')
    await user.click(screen.getByRole('button', { name: 'Enviar código' }))
    expect(await screen.findByText(/Si el correo está registrado/)).toBeInTheDocument()

    await user.click(screen.getByRole('link', { name: 'Ya tengo el código' }))
    expect(screen.getByLabelText('Correo')).toHaveValue('estandar@demo.com')
    await user.type(screen.getByLabelText('Código'), CODIGO_RECUPERACION_DEMO)
    await user.type(screen.getByLabelText('Nueva contraseña'), 'NuevaClave9')
    await user.type(screen.getByLabelText('Confirmar contraseña'), 'NuevaClave9')
    await user.click(screen.getByRole('button', { name: 'Restablecer' }))

    expect(await screen.findByText(/Contraseña restablecida/)).toBeInTheDocument()
    await user.type(screen.getByLabelText('Correo'), 'estandar@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), 'NuevaClave9')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))
    expect(await screen.findByRole('heading', { name: 'Mis hábitos' })).toBeInTheDocument()
  })

  it('cambiar la contraseña cierra la sesión (RF-CA-12)', async () => {
    const { user, router } = renderApp('/cuenta', { como: 'estandar' })
    expect(await screen.findByText('estandar@demo.com', { selector: 'dd' })).toBeInTheDocument()
    await user.type(screen.getByLabelText('Contraseña actual'), 'Demo1234')
    await user.type(screen.getByLabelText('Nueva contraseña'), 'OtraClave7')
    await user.type(screen.getByLabelText('Confirmar nueva contraseña'), 'OtraClave7')
    await user.click(screen.getByRole('button', { name: 'Actualizar contraseña' }))

    expect(await screen.findByText(/Contraseña actualizada/)).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
  })

  it('cerrar sesión vuelve al login y revoca el token', async () => {
    const { user, router } = renderApp('/habitos', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Cerrar sesión' }))
    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
  })
})

async function registrarCuenta(user: UserEvent, correo: string) {
  await user.type(await screen.findByLabelText('Nombre completo'), 'Persona Nueva')
  await user.type(screen.getByLabelText('Correo'), correo)
  await user.type(screen.getByLabelText('Contraseña'), 'Clave1234')
  await user.type(screen.getByLabelText('Confirmar contraseña'), 'Clave1234')
  await user.click(screen.getByRole('checkbox', { name: /Términos de uso/ }))
  await user.click(screen.getByRole('checkbox', { name: /Política de privacidad/ }))
  await user.click(screen.getByRole('button', { name: 'Crear cuenta y continuar' }))
  expect(await screen.findByText('Cuenta creada')).toBeInTheDocument()
}
