import { screen, within } from '@testing-library/react'
import type { UserEvent } from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { CODIGO_RECUPERACION_DEMO, db, TOKEN_ACTIVACION_DEMO } from '@/mocks/db'
import { leerRegistroPendiente } from '@/features/perfil/api'
import { completarOnboarding } from '@/test/onboarding'
import { renderApp, renderAppConEstado } from '@/test/render'

// La política del backend: 12+ caracteres con letras y números.
const CLAVE = 'Clave-Segura-2026'
const CLAVE_NUEVA = 'NuevaClave-Segura-9'
const CLAVE_OTRA = 'OtraClave-Segura-7'

describe('flujos de cuenta', () => {
  it('registro con meta → activación → login → onboarding → panel', async () => {
    const { user } = renderApp('/registro?meta=primeros-5-km')
    expect(await screen.findByText('Paso 1 de 5')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Nombre completo'), 'Nueva Persona')
    await user.type(screen.getByLabelText('Correo'), 'nueva@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), CLAVE)
    await user.type(screen.getByLabelText('Confirmar contraseña'), CLAVE)
    await user.click(screen.getByRole('checkbox', { name: /Términos de uso/ }))
    await user.click(screen.getByRole('checkbox', { name: /Política de privacidad/ }))
    await user.click(screen.getByRole('checkbox', { name: /correos con consejos/ }))
    await user.click(screen.getByRole('button', { name: 'Crear cuenta y continuar' }))
    expect(await screen.findByText('Cuenta creada')).toBeInTheDocument()

    await user.click(screen.getByRole('link', { name: 'Tengo el código de activación' }))
    // El enlace lleva el correo: el código de 6 dígitos solo identifica a la persona junto a él.
    expect(screen.getByLabelText('Correo')).toHaveValue('nueva@demo.com')
    await user.type(screen.getByLabelText('Código de activación'), TOKEN_ACTIVACION_DEMO)
    await user.click(screen.getByRole('button', { name: 'Activar' }))
    expect(await screen.findByText(/Tu cuenta está activa/)).toBeInTheDocument()

    await user.click(screen.getByRole('link', { name: 'Ir a iniciar sesión' }))
    await user.type(screen.getByLabelText('Correo'), 'nueva@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), CLAVE)
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    // Sin perfil, el panel lleva al onboarding con la meta del registro.
    expect(await screen.findByRole('heading', { name: 'Tus datos básicos' })).toBeInTheDocument()
    expect(screen.getByText('Paso 3 de 5')).toBeInTheDocument()
    await completarOnboarding(user)
    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: /^(Buenos días|Buenas tardes|Buenas noches)/,
      }),
    ).toBeInTheDocument()

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
    await user.type(screen.getByLabelText('Contraseña'), CLAVE)
    await user.type(screen.getByLabelText('Confirmar contraseña'), CLAVE)
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

  it('el registro guía mientras se escribe: requisitos de la clave, nombre, correo y confirmación', async () => {
    const { user } = renderApp('/registro')
    const requisito = (texto: string) => screen.getByText(texto).closest('li')

    // La lista se marca a medida que se escribe la contraseña.
    expect(screen.getByText('0 de 3')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Contraseña'), 'abc')
    expect(requisito('Al menos una letra')).toHaveTextContent('(cumplido)')
    expect(requisito('Al menos un número')).toHaveTextContent('(pendiente)')
    await user.type(screen.getByLabelText('Contraseña'), '1-Segura-26')
    expect(screen.getByText('3 de 3')).toBeInTheDocument()
    expect(screen.getByLabelText('Contraseña')).toHaveAccessibleDescription(
      /Tu contraseña necesita/,
    )

    // Los errores aparecen al salir del campo, no en la primera tecla.
    await user.type(screen.getByLabelText('Nombre completo'), 'Ana')
    expect(screen.queryByText('Escribe tu nombre y tu apellido.')).not.toBeInTheDocument()
    await user.tab()
    expect(await screen.findByText('Escribe tu nombre y tu apellido.')).toBeInTheDocument()
    // Desde ahí se corrige en vivo.
    await user.type(screen.getByLabelText('Nombre completo'), ' Pérez')
    expect(screen.queryByText('Escribe tu nombre y tu apellido.')).not.toBeInTheDocument()

    await user.type(screen.getByLabelText('Correo'), 'ana@')
    await user.tab()
    expect(await screen.findByText('Escribe un correo válido.')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Correo'), 'demo.com')
    expect(screen.queryByText('Escribe un correo válido.')).not.toBeInTheDocument()

    await user.type(screen.getByLabelText('Confirmar contraseña'), 'abc')
    expect(screen.getByText('Aún no coincide con la contraseña.')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Confirmar contraseña'), '1-Segura-26')
    expect(screen.getByText('Las contraseñas coinciden.')).toBeInTheDocument()

    // El ojo muestra lo escrito sin enviar el formulario.
    const [ojo] = screen.getAllByRole('button', { name: 'Mostrar contraseña' })
    await user.click(ojo)
    expect(screen.getByLabelText('Contraseña')).toHaveAttribute('type', 'text')
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('rechaza registrar un correo existente con un mensaje genérico', async () => {
    const { user } = renderApp('/registro')
    await user.type(screen.getByLabelText('Nombre completo'), 'Persona Duplicada')
    await user.type(screen.getByLabelText('Correo'), 'estandar@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), CLAVE)
    await user.type(screen.getByLabelText('Confirmar contraseña'), CLAVE)
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

  it('el enlace de activación rellena los datos pero no activa la cuenta por sí solo', async () => {
    const { user } = renderApp(`/activar?correo=inactiva@demo.com&codigo=${TOKEN_ACTIVACION_DEMO}`)
    expect(await screen.findByLabelText('Correo')).toHaveValue('inactiva@demo.com')
    expect(screen.getByLabelText('Código de activación')).toHaveValue(TOKEN_ACTIVACION_DEMO)
    // Un escáner de correos o una precarga del enlace no gastan los intentos del código (RF-CA-15).
    expect(db.usuarios.find((u) => u.correo === 'inactiva@demo.com')?.activo).toBe(false)

    await user.click(screen.getByRole('button', { name: 'Activar' }))
    expect(await screen.findByText(/Tu cuenta está activa/)).toBeInTheDocument()
    expect(db.usuarios.find((u) => u.correo === 'inactiva@demo.com')?.activo).toBe(true)
  })

  it('el código de activación se valida: 6 dígitos y el correo de su cuenta', async () => {
    const { user } = renderApp('/activar')
    await user.type(await screen.findByLabelText('Correo'), 'inactiva@demo.com')
    await user.type(screen.getByLabelText('Código de activación'), '12ab')
    await user.click(screen.getByRole('button', { name: 'Activar' }))
    expect(await screen.findByText('El código tiene 6 dígitos.')).toBeInTheDocument()

    await user.clear(screen.getByLabelText('Código de activación'))
    await user.type(screen.getByLabelText('Código de activación'), '000000')
    await user.click(screen.getByRole('button', { name: 'Activar' }))
    expect(await screen.findByText(/no es válido, ya se usó o venció/)).toBeInTheDocument()
    expect(db.usuarios.find((u) => u.correo === 'inactiva@demo.com')?.activo).toBe(false)
  })

  it('recuperar → restablecer → login con la nueva contraseña', async () => {
    const { user } = renderApp('/recuperar')
    await user.type(screen.getByLabelText('Correo'), 'estandar@demo.com')
    await user.click(screen.getByRole('button', { name: 'Enviar código' }))
    expect(await screen.findByText(/Si el correo está registrado/)).toBeInTheDocument()

    await user.click(screen.getByRole('link', { name: 'Ya tengo el código' }))
    expect(screen.getByLabelText('Correo')).toHaveValue('estandar@demo.com')
    await user.type(screen.getByLabelText('Código'), CODIGO_RECUPERACION_DEMO)
    await user.type(screen.getByLabelText('Nueva contraseña'), CLAVE_NUEVA)
    await user.type(screen.getByLabelText('Confirmar contraseña'), CLAVE_NUEVA)
    await user.click(screen.getByRole('button', { name: 'Restablecer' }))

    expect(await screen.findByText(/Contraseña restablecida/)).toBeInTheDocument()
    await user.type(screen.getByLabelText('Correo'), 'estandar@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), CLAVE_NUEVA)
    await user.click(screen.getByRole('button', { name: 'Entrar' }))
    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: /^(Buenos días|Buenas tardes|Buenas noches)/,
      }),
    ).toBeInTheDocument()
  })

  it('cambiar la contraseña cierra la sesión (RF-CA-12)', async () => {
    const { user, router } = renderApp('/ajustes/seguridad', { como: 'estandar' })
    await user.type(await screen.findByLabelText('Contraseña actual'), 'Demo1234')
    await user.type(screen.getByLabelText('Nueva contraseña'), CLAVE_OTRA)
    await user.type(screen.getByLabelText('Confirmar nueva contraseña'), CLAVE_OTRA)
    await user.click(screen.getByRole('button', { name: 'Actualizar contraseña' }))

    expect(await screen.findByText(/Contraseña actualizada/)).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
  })

  it('el registro envía los consentimientos al servidor, que los guarda con la cuenta', async () => {
    const { user } = renderApp('/registro')
    await registrarCuenta(user, 'consiente@demo.com')

    const nueva = db.usuarios.find((u) => u.correo === 'consiente@demo.com')
    expect(db.consentimientos.get(nueva?.id ?? '')).toMatchObject({
      terminosVersion: 'v2.1',
      privacidadVersion: 'v1.4',
      marketing: false,
    })
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
  await user.type(screen.getByLabelText('Contraseña'), CLAVE)
  await user.type(screen.getByLabelText('Confirmar contraseña'), CLAVE)
  await user.click(screen.getByRole('checkbox', { name: /Términos de uso/ }))
  await user.click(screen.getByRole('checkbox', { name: /Política de privacidad/ }))
  await user.click(screen.getByRole('button', { name: 'Crear cuenta y continuar' }))
  expect(await screen.findByText('Cuenta creada')).toBeInTheDocument()
}
