import { screen, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { CODIGO_RECUPERACION_DEMO, db } from '@/mocks/db'
import { emitirToken } from '@/mocks/http'
import { renderApp } from '@/test/render'
import { tokenStorage } from '@/shared/auth/tokenStorage'
import { armarTokenDemo } from './tokenDemo'

// El botón de la demostración pide el correo de la cuenta de Google simulada con window.prompt.
function cuentaDeGoogle(correo: string | null) {
  vi.spyOn(window, 'prompt').mockReturnValue(correo)
}

const BOTON = 'Continuar con Google (demostración)'
// Política del backend: 12+ caracteres con letras y números.
const CLAVE_CREADA = 'Primera-Clave-2026'

beforeEach(() => vi.restoreAllMocks())
afterEach(() => vi.restoreAllMocks())

describe('Continuar con Google en el inicio de sesión', () => {
  it('una cuenta nueva pide aceptar los documentos y luego crea la cuenta y abre la sesión', async () => {
    cuentaDeGoogle('nueva.persona@example.com')
    const { user, router } = renderApp('/login')

    await user.click(await screen.findByRole('button', { name: BOTON }))

    expect(
      await screen.findByText(/Todavía no tienes una cuenta con este correo/),
    ).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Crear cuenta con Google' }))
    // Sin aceptar los documentos obligatorios no se envía nada.
    expect(await screen.findByText(/Debes aceptar los Términos de uso/)).toBeInTheDocument()
    expect(db.usuarios.some((u) => u.correo === 'nueva.persona@example.com')).toBe(false)

    await user.click(screen.getByRole('checkbox', { name: /Términos de uso/ }))
    await user.click(screen.getByRole('checkbox', { name: /Política de privacidad/ }))
    await user.click(screen.getByRole('checkbox', { name: /correos con consejos/ }))
    await user.click(screen.getByRole('button', { name: 'Crear cuenta con Google' }))

    // Una cuenta nueva no tiene perfil: el panel la lleva al onboarding.
    expect(await screen.findByRole('heading', { name: '¿Cómo es tu jornada?' })).toBeInTheDocument()
    expect(router.state.location.pathname).not.toBe('/login')
    const creada = db.usuarios.find((u) => u.correo === 'nueva.persona@example.com')
    expect(creada).toMatchObject({ activo: true, password: null })
    expect(db.consentimientos.get(creada?.id ?? '')).toMatchObject({ marketing: true })
    expect(tokenStorage.leer()).not.toBeNull()
  })

  it('una cuenta ya vinculada entra directamente, sin pedir nada más', async () => {
    const ana = db.usuarios.find((u) => u.correo === 'estandar@demo.com')
    db.vinculosGoogle.set(ana?.id ?? '', {
      subject: 'sub-estandar@demo.com',
      vinculadoEn: new Date().toISOString(),
    })
    cuentaDeGoogle('estandar@demo.com')
    const { user } = renderApp('/login')

    await user.click(await screen.findByRole('button', { name: BOTON }))

    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: /^(Buenos días|Buenas tardes|Buenas noches)/,
      }),
    ).toBeInTheDocument()
  })

  it('un correo que ya tiene cuenta pide la contraseña para vincular y no entra con una incorrecta', async () => {
    cuentaDeGoogle('estandar@demo.com')
    const { user } = renderApp('/login')

    await user.click(await screen.findByRole('button', { name: BOTON }))

    expect(
      await screen.findByText(/Ya tienes una cuenta con el correo de tu cuenta de Google/),
    ).toBeInTheDocument()
    const formulario = screen
      .getByRole('button', { name: 'Vincular y entrar' })
      .closest('form') as HTMLElement
    await user.type(within(formulario).getByLabelText('Contraseña'), 'incorrecta-12345')
    await user.click(screen.getByRole('button', { name: 'Vincular y entrar' }))

    expect(await screen.findByText(/La contraseña es incorrecta/)).toBeInTheDocument()
    expect(db.vinculosGoogle.size).toBe(0)
    expect(tokenStorage.leer()).toBeNull()
  })

  it('con la contraseña correcta vincula Google y abre la sesión', async () => {
    cuentaDeGoogle('estandar@demo.com')
    const { user } = renderApp('/login')

    await user.click(await screen.findByRole('button', { name: BOTON }))
    const formulario = (await screen.findByRole('button', { name: 'Vincular y entrar' })).closest(
      'form',
    ) as HTMLElement
    await user.type(within(formulario).getByLabelText('Contraseña'), 'Demo1234')
    await user.click(screen.getByRole('button', { name: 'Vincular y entrar' }))

    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: /^(Buenos días|Buenas tardes|Buenas noches)/,
      }),
    ).toBeInTheDocument()
    const ana = db.usuarios.find((u) => u.correo === 'estandar@demo.com')
    expect(db.vinculosGoogle.has(ana?.id ?? '')).toBe(true)
  })

  it('cancelar la vinculación vuelve al botón sin haber iniciado sesión', async () => {
    cuentaDeGoogle('estandar@demo.com')
    const { user } = renderApp('/login')
    await user.click(await screen.findByRole('button', { name: BOTON }))

    await user.click(await screen.findByRole('button', { name: 'Cancelar' }))

    expect(screen.getByRole('button', { name: BOTON })).toBeInTheDocument()
    expect(screen.queryByText(/Ya tienes una cuenta/)).not.toBeInTheDocument()
    expect(tokenStorage.leer()).toBeNull()
  })

  it('si la persona cierra la ventana de Google (sin correo) no pasa nada', async () => {
    cuentaDeGoogle(null)
    const { user } = renderApp('/login')

    await user.click(await screen.findByRole('button', { name: BOTON }))

    expect(screen.getByRole('button', { name: BOTON })).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('una cuenta desactivada por un administrador recibe ese aviso', async () => {
    const ana = db.usuarios.find((u) => u.correo === 'estandar@demo.com')
    db.vinculosGoogle.set(ana?.id ?? '', {
      subject: 'sub-estandar@demo.com',
      vinculadoEn: new Date().toISOString(),
    })
    if (ana) ana.activo = false
    cuentaDeGoogle('estandar@demo.com')
    const { user } = renderApp('/login')

    await user.click(await screen.findByRole('button', { name: BOTON }))

    expect(await screen.findByText(/desactivada por un administrador/)).toBeInTheDocument()
  })
})

describe('Continuar con Google en el registro', () => {
  it('con las casillas ya marcadas crea la cuenta de una vez, sin volver a pedir los documentos', async () => {
    cuentaDeGoogle('registro.google@example.com')
    const { user } = renderApp('/registro')
    await user.click(await screen.findByRole('checkbox', { name: /Términos de uso/ }))
    await user.click(screen.getByRole('checkbox', { name: /Política de privacidad/ }))

    await user.click(screen.getByRole('button', { name: BOTON }))

    expect(await screen.findByRole('heading', { name: '¿Cómo es tu jornada?' })).toBeInTheDocument()
    expect(
      screen.queryByText(/Todavía no tienes una cuenta con este correo/),
    ).not.toBeInTheDocument()
    const creada = db.usuarios.find((u) => u.correo === 'registro.google@example.com')
    expect(db.consentimientos.get(creada?.id ?? '')).toMatchObject({
      marketing: false,
      terminosVersion: 'v2.1',
    })
  })

  it('con las casillas sin marcar pide los documentos antes de crear la cuenta', async () => {
    cuentaDeGoogle('registro.google@example.com')
    const { user } = renderApp('/registro')

    await user.click(await screen.findByRole('button', { name: BOTON }))

    expect(
      await screen.findByText(/Todavía no tienes una cuenta con este correo/),
    ).toBeInTheDocument()
    expect(db.usuarios.some((u) => u.correo === 'registro.google@example.com')).toBe(false)
  })
})

describe('Cuentas vinculadas en Configuración › Inicio de sesión', () => {
  it('vincula Google confirmando la contraseña y lo muestra como vinculado', async () => {
    cuentaDeGoogle('estandar@demo.com')
    const { user } = renderApp('/ajustes/inicio-sesion', { como: 'estandar' })

    expect(await screen.findByText('No vinculada')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: BOTON }))
    const formulario = (await screen.findByRole('button', { name: 'Vincular Google' })).closest(
      'form',
    ) as HTMLElement
    await user.type(within(formulario).getByLabelText('Contraseña'), 'Demo1234')
    await user.click(screen.getByRole('button', { name: 'Vincular Google' }))

    expect(await screen.findByText('Google quedó vinculado a tu cuenta.')).toBeInTheDocument()
    expect(await screen.findByText(/Vinculada el/)).toBeInTheDocument()
  })

  it('una contraseña incorrecta al vincular no vincula ni cierra la sesión', async () => {
    cuentaDeGoogle('estandar@demo.com')
    const { user, router } = renderApp('/ajustes/inicio-sesion', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: BOTON }))
    const formulario = (await screen.findByRole('button', { name: 'Vincular Google' })).closest(
      'form',
    ) as HTMLElement
    await user.type(within(formulario).getByLabelText('Contraseña'), 'incorrecta-12345')
    await user.click(screen.getByRole('button', { name: 'Vincular Google' }))

    expect(await screen.findByText(/La contraseña es incorrecta/)).toBeInTheDocument()
    expect(db.vinculosGoogle.size).toBe(0)
    expect(router.state.location.pathname).toBe('/ajustes/inicio-sesion')
    expect(tokenStorage.leer()).not.toBeNull()
  })

  it('desvincula Google tras confirmar', async () => {
    const ana = db.usuarios.find((u) => u.correo === 'estandar@demo.com')
    db.vinculosGoogle.set(ana?.id ?? '', {
      subject: 'sub-estandar@demo.com',
      vinculadoEn: new Date().toISOString(),
    })
    const { user } = renderApp('/ajustes/inicio-sesion', { como: 'estandar' })

    await user.click(await screen.findByRole('button', { name: 'Desvincular Google' }))
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: 'Desvincular' }),
    )

    expect(await screen.findByText('Google se desvinculó de tu cuenta.')).toBeInTheDocument()
    expect(db.vinculosGoogle.has(ana?.id ?? '')).toBe(false)
    expect(await screen.findByText('No vinculada')).toBeInTheDocument()
  })

  it('una cuenta que solo entra con Google no puede desvincularla y puede crear una contraseña', async () => {
    const usuario = {
      id: crypto.randomUUID(),
      nombre: 'Solo Google',
      correo: 'solo.google@example.com',
      password: null,
      rol: 'Estandar' as const,
      activo: true,
      intentosFallidos: 0,
      bloqueadoHasta: null,
      tokenActivacion: null,
      codigoRecuperacion: null,
      sesionesValidasDesde: 0,
    }
    db.usuarios.push(usuario)
    db.vinculosGoogle.set(usuario.id, {
      subject: 'sub-solo',
      vinculadoEn: new Date().toISOString(),
    })
    tokenStorage.guardar(emitirToken(usuario))
    const { user } = renderApp('/ajustes/inicio-sesion')

    expect(await screen.findByText('Sin contraseña')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Desvincular Google' })).toBeDisabled()
    expect(screen.getByText(/Google es tu única forma de entrar/)).toBeInTheDocument()

    // El aviso lleva a la sección donde se crea (antes mandaba a cerrar sesión).
    await user.click(screen.getByRole('link', { name: 'crea primero una contraseña' }))
    expect(
      await screen.findByRole('heading', { level: 2, name: 'Crear una contraseña' }),
    ).toBeInTheDocument()
    // No hay formulario para cambiar una contraseña que no existe.
    expect(screen.queryByLabelText('Contraseña actual')).not.toBeInTheDocument()

    // Se crea sin salir de la sesión: código al correo de la cuenta y la clave nueva.
    await user.click(screen.getByRole('button', { name: 'Enviarme un código' }))
    expect(
      await screen.findByText(/Te enviamos un código a solo.google@example.com/),
    ).toBeInTheDocument()
    await user.type(screen.getByLabelText('Código'), CODIGO_RECUPERACION_DEMO)
    await user.type(screen.getByLabelText('Nueva contraseña'), CLAVE_CREADA)
    await user.type(screen.getByLabelText('Confirmar contraseña'), CLAVE_CREADA)
    await user.click(screen.getByRole('button', { name: 'Crear contraseña' }))

    expect(await screen.findByText(/Contraseña creada/)).toBeInTheDocument()
    expect(usuario.password).toBe(CLAVE_CREADA)
  })

  it('una cuenta con contraseña sigue viendo el formulario de cambio de contraseña', async () => {
    renderApp('/ajustes/seguridad', { como: 'estandar' })

    expect(await screen.findByLabelText('Contraseña actual')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Crear una contraseña' })).not.toBeInTheDocument()
  })
})

describe('el token de la demostración', () => {
  it('lo arma el botón con el correo en minúsculas', () => {
    expect(armarTokenDemo('Ana@Demo.COM')).toContain('ana@demo.com')
  })
})
