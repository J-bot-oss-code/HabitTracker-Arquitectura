import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { db } from '@/mocks/db'
import { problema } from '@/mocks/http'
import { server } from '@/mocks/server'
import { iniciarSesionComo, renderApp } from '@/test/render'
import { armarExportacion, nombreArchivo } from '../exportar'

const ana = () => db.usuarios[0]

afterEach(() => vi.restoreAllMocks())

describe('exportación (pura)', () => {
  it('el nombre del archivo lleva el día UTC', () => {
    expect(nombreArchivo(new Date('2026-11-03T23:59:59Z'))).toBe(
      'habittracker-mis-datos-2026-11-03.json',
    )
  })

  it('arma el archivo con lo que entrega el servidor y le pone formato y versión', () => {
    const servidor = {
      generadoEn: '2026-11-03T10:00:00.000Z',
      cuenta: {
        id: '1',
        nombre: 'Ana',
        correo: 'a@b.c',
        rol: 'Estandar',
        activo: true,
        fechaCreacion: '2026-01-01T00:00:00.000Z',
        tienePassword: true,
        desactivadaPorUsuarioEn: null,
        bajaProgramadaPara: null,
      },
      consentimientos: [],
      vinculos: [],
      sesiones: [],
      modulos: { habitos: [], permisos: [], documentos: [] },
    }
    const e = armarExportacion(servidor)
    expect(e).toMatchObject({
      formato: 'habittracker-datos',
      version: 3,
      generadoEn: '2026-11-03T10:00:00.000Z',
      cuenta: { correo: 'a@b.c' },
      modulos: { habitos: [] },
    })
  })
})

describe('Centro de privacidad', () => {
  it('muestra descarga, consentimientos y baja, y está en el menú del Estándar', async () => {
    renderApp('/ajustes/privacidad', { como: 'estandar' })
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Privacidad y datos' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Descargar mis datos' })).toBeInTheDocument()
    expect(await screen.findByRole('heading', { name: 'Consentimientos' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Retirarme de la plataforma' })).toBeInTheDocument()
    // Vive en Configuración: el menú principal marca Configuración y el de secciones, Privacidad.
    const menu = screen.getByRole('navigation', { name: 'Principal' })
    expect(within(menu).getByRole('link', { name: 'Configuración' })).toHaveAttribute(
      'aria-current',
      'page',
    )
    const secciones = screen.getByRole('navigation', { name: 'Secciones de configuración' })
    expect(within(secciones).getByRole('link', { name: 'Privacidad y datos' })).toHaveAttribute(
      'aria-current',
      'page',
    )
  })

  it('el Administrador no accede', async () => {
    renderApp('/ajustes/privacidad', { como: 'admin' })
    expect(await screen.findByText('No tienes acceso a esta sección')).toBeInTheDocument()
  })

  describe('descargar mis datos', () => {
    function capturarDescarga() {
      const captura = { blob: null as Blob | null, nombre: '' }
      URL.createObjectURL = (b: Blob | MediaSource) => {
        captura.blob = b as Blob
        return 'blob:prueba'
      }
      URL.revokeObjectURL = () => undefined
      vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
        this: HTMLAnchorElement,
      ) {
        captura.nombre = this.download
      })
      return captura
    }

    it('baja el JSON que arma el servidor y la auditoría lo anota', async () => {
      const captura = capturarDescarga()
      const eventosAntes = db.auditoria.length

      const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      await user.click(await screen.findByRole('button', { name: /Descargar mis datos/ }))
      expect(await screen.findByText('Descarga lista.')).toBeInTheDocument()

      expect(captura.nombre).toMatch(/^habittracker-mis-datos-\d{4}-\d{2}-\d{2}\.json$/)
      const contenido = JSON.parse(await (captura.blob as unknown as Blob).text())
      expect(contenido).toMatchObject({
        formato: 'habittracker-datos',
        version: 3,
        cuenta: { correo: 'estandar@demo.com', rol: 'Estandar', tienePassword: true },
        modulos: { perfilYSalud: { perfil: { metaSlug: 'primeros-5-km' } } },
      })
      expect(contenido.consentimientos).toHaveLength(1)
      expect(contenido.consentimientos[0]).toMatchObject({
        terminosVersion: 'v2.1',
        datosSalud: true,
      })
      expect(contenido.modulos.habitos).toHaveLength(3)
      expect(Array.isArray(contenido.modulos.permisos)).toBe(true)
      expect(contenido.modulos.perfilYSalud.pesos).toHaveLength(10)
      expect(contenido.modulos.perfilYSalud.mediciones).toHaveLength(8)
      // No viaja nada de otras cuentas ni credenciales.
      expect(JSON.stringify(contenido)).not.toMatch(/otro@demo|Demo1234|"password"|passwordHash/i)
      expect(db.auditoria).toHaveLength(eventosAntes + 1)
      expect(db.auditoria.at(-1)).toMatchObject({
        actor: 'estandar@demo.com',
        accion: 'DescargaDatos',
      })
    })

    it('el archivo es el del servidor: no pide perfil ni progreso por su cuenta', async () => {
      const captura = capturarDescarga()
      // Si el cliente aún pidiera el progreso aparte, este 500 rompería la descarga.
      server.use(http.get('/api/progress/weights', () => problema(500, 'Error interno.')))
      const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      await user.click(await screen.findByRole('button', { name: /Descargar mis datos/ }))
      expect(await screen.findByText('Descarga lista.')).toBeInTheDocument()

      const contenido = JSON.parse(await (captura.blob as unknown as Blob).text())
      expect(contenido.modulos.perfilYSalud.pesos).toHaveLength(10)
      expect(contenido.modulos.habitos).toHaveLength(3)
    })

    it('si falla la descarga del servidor avisa en vez de entregar un archivo incompleto', async () => {
      const descargas = vi
        .spyOn(HTMLAnchorElement.prototype, 'click')
        .mockImplementation(() => undefined)
      server.use(http.get('/api/privacy/export', () => problema(500, 'Error interno.')))
      const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      await user.click(await screen.findByRole('button', { name: /Descargar mis datos/ }))
      expect(await screen.findByText('No se pudo preparar la descarga.')).toBeInTheDocument()
      expect(descargas).not.toHaveBeenCalled()
    })

    it('no sale del navegador nada más que la petición de la descarga', async () => {
      capturarDescarga()
      const eventosAntes = db.auditoria.length
      const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      await user.click(await screen.findByRole('button', { name: /Descargar mis datos/ }))
      expect(await screen.findByText('Descarga lista.')).toBeInTheDocument()
      // El servidor ya deja la constancia al servir el archivo: no hay un aviso aparte (export-request).
      expect(db.auditoria.slice(eventosAntes).map((e) => e.accion)).toEqual(['DescargaDatos'])
    })
  })

  describe('consentimientos', () => {
    it('cada interruptor es un switch con su estado y se guarda al cambiarlo', async () => {
      const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      const correos = await screen.findByRole('switch', {
        name: 'Correos con consejos y novedades',
      })
      expect(correos).toHaveAttribute('aria-checked', 'false')
      expect(within(correos).getByText('Desactivado')).toBeInTheDocument()

      await user.click(correos)
      await waitFor(() => expect(correos).toHaveAttribute('aria-checked', 'true'))
      expect(within(correos).getByText('Activado')).toBeInTheDocument()
      expect(db.consentimientos.get(ana().id)?.marketing).toBe(true)
      // La fecha y las versiones aceptadas no cambian por un simple interruptor.
      expect(db.consentimientos.get(ana().id)).toMatchObject({
        terminosVersion: 'v2.1',
        privacidadVersion: 'v1.4',
      })
    })

    it('retirar los datos de salud pide confirmación y cancelar no cambia nada', async () => {
      const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      const salud = await screen.findByRole('switch', {
        name: 'Usar mis datos de salud para generar el plan',
      })
      await user.click(salud)
      const dialogo = await screen.findByRole('dialog', { name: /datos de salud/ })
      await user.click(within(dialogo).getByRole('button', { name: 'Cancelar' }))
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
      expect(db.consentimientos.get(ana().id)?.datosSalud).toBe(true)

      await user.click(salud)
      await user.click(
        within(await screen.findByRole('dialog')).getByRole('button', {
          name: 'Retirar consentimiento',
        }),
      )
      await waitFor(() => expect(salud).toHaveAttribute('aria-checked', 'false'))
      expect(db.consentimientos.get(ana().id)?.datosSalud).toBe(false)
    })

    it('dice qué versiones aceptó y cuándo, con enlaces a los documentos', async () => {
      renderApp('/ajustes/privacidad', { como: 'estandar' })
      expect(await screen.findByRole('link', { name: 'Términos v2.1' })).toHaveAttribute(
        'href',
        '/legal/terminos',
      )
      expect(screen.getByRole('link', { name: 'Política de privacidad v1.4' })).toHaveAttribute(
        'href',
        '/legal/privacidad',
      )
    })

    it('con el backend real (sin la pieza) avisa en vez de fallar', async () => {
      server.use(http.get('/api/privacy/consents', () => new HttpResponse(null, { status: 404 })))
      renderApp('/ajustes/privacidad', { como: 'estandar' })
      expect(await screen.findByText('Los consentimientos llegarán pronto')).toBeInTheDocument()
    })

    it('sin consentimientos registrados invita a completar el perfil', async () => {
      db.consentimientos.delete(ana().id)
      renderApp('/ajustes/privacidad', { como: 'estandar' })
      expect(await screen.findByText('Aún no has aceptado los documentos')).toBeInTheDocument()
      expect(screen.getByRole('link', { name: 'Completar mi perfil' })).toHaveAttribute(
        'href',
        '/bienvenida',
      )
    })
  })

  describe('retirarme de la plataforma', () => {
    it('exige la contraseña y, si es errónea, avisa sin cerrar la sesión', async () => {
      const { user, router } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      await user.click(await screen.findByRole('button', { name: 'Eliminar mi cuenta' }))
      const dialogo = await screen.findByRole('dialog', { name: 'Eliminar mi cuenta' })
      expect(within(dialogo).getByText(/30 días/)).toBeInTheDocument()

      await user.click(within(dialogo).getByRole('button', { name: 'Eliminar mi cuenta' }))
      expect(
        await within(dialogo).findByText('Escribe tu contraseña para confirmar.'),
      ).toBeInTheDocument()

      await user.type(within(dialogo).getByLabelText('Tu contraseña'), 'incorrecta1')
      await user.click(within(dialogo).getByRole('button', { name: 'Eliminar mi cuenta' }))
      expect(await within(dialogo).findByText('La contraseña no es correcta.')).toBeInTheDocument()
      expect(router.state.location.pathname).toBe('/ajustes/privacidad')
      expect(ana().activo).toBe(true)
      expect(db.bajas.size).toBe(0)
    })

    it('desactivar cierra la sesión y deja la cuenta inactiva por su dueño', async () => {
      const { user, router } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      await user.click(await screen.findByRole('button', { name: 'Desactivar' }))
      const dialogo = await screen.findByRole('dialog', { name: 'Desactivar mi cuenta' })
      await user.type(within(dialogo).getByLabelText('Tu contraseña'), 'Demo1234')
      await user.click(within(dialogo).getByRole('button', { name: 'Desactivar cuenta' }))

      expect(await screen.findByText(/Tu cuenta quedó desactivada/)).toBeInTheDocument()
      expect(router.state.location.pathname).toBe('/login')
      expect(ana()).toMatchObject({ activo: false, desactivadaPorUsuario: true })
      expect(db.bajas.size).toBe(0)
      expect(db.auditoria.at(-1)).toMatchObject({
        accion: 'SolicitudBaja',
        valorNuevo: 'desactivar',
      })
    })

    it('una cuenta desactivada por su dueño se reactiva al iniciar sesión de nuevo', async () => {
      const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      await user.click(await screen.findByRole('button', { name: 'Desactivar' }))
      const dialogo = await screen.findByRole('dialog', { name: 'Desactivar mi cuenta' })
      await user.type(within(dialogo).getByLabelText('Tu contraseña'), 'Demo1234')
      await user.click(within(dialogo).getByRole('button', { name: 'Desactivar cuenta' }))
      await screen.findByRole('heading', { name: 'Iniciar sesión' })

      await user.type(screen.getByLabelText('Correo'), 'estandar@demo.com')
      await user.type(screen.getByLabelText('Contraseña'), 'Demo1234')
      await user.click(screen.getByRole('button', { name: 'Entrar' }))

      expect(
        await screen.findByRole('heading', {
          level: 1,
          name: /^(Buenos días|Buenas tardes|Buenas noches)/,
        }),
      ).toBeInTheDocument()
      expect(ana()).toMatchObject({ activo: true, desactivadaPorUsuario: false })
    })

    it('eliminar fija el borrado a 30 días (UTC), cierra la sesión y dice la fecha', async () => {
      const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      await user.click(await screen.findByRole('button', { name: 'Eliminar mi cuenta' }))
      const dialogo = await screen.findByRole('dialog', { name: 'Eliminar mi cuenta' })
      await user.type(within(dialogo).getByLabelText('Tu contraseña'), 'Demo1234')
      await user.click(within(dialogo).getByRole('button', { name: 'Eliminar mi cuenta' }))

      expect(await screen.findByText(/se borrará el .* Si cambias de opinión/)).toBeInTheDocument()
      const baja = db.bajas.get(ana().id)
      expect(baja?.borradoEn).toMatch(/Z$/)
      const dias =
        (Date.parse(baja?.borradoEn ?? '') - Date.parse(baja?.solicitadaEn ?? '')) / 86_400_000
      expect(dias).toBeCloseTo(30, 3)
      // Durante la gracia la cuenta sigue activa para poder cancelar.
      expect(ana().activo).toBe(true)
    })

    it('con la eliminación programada se puede cancelar desde el aviso de toda la aplicación', async () => {
      const ahora = Date.now()
      db.bajas.set(ana().id, {
        solicitadaEn: new Date(ahora).toISOString(),
        borradoEn: new Date(ahora + 29 * 86_400_000).toISOString(),
      })
      const { user } = renderApp('/hoy', { como: 'estandar' })

      const aviso = await screen.findByText('Tu cuenta se eliminará')
      expect(aviso).toBeInTheDocument()
      await user.click(screen.getByRole('button', { name: 'Cancelar eliminación' }))

      await waitFor(() =>
        expect(screen.queryByText('Tu cuenta se eliminará')).not.toBeInTheDocument(),
      )
      expect(db.bajas.size).toBe(0)
      expect(ana().activo).toBe(true)
    })

    it('en el centro de privacidad avisa de la fecha, oculta «Eliminar» y permite cancelar', async () => {
      db.bajas.set(ana().id, {
        solicitadaEn: new Date().toISOString(),
        borradoEn: new Date(Date.now() + 10 * 86_400_000).toISOString(),
      })
      const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })

      expect(await screen.findByText('Eliminación programada')).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Eliminar mi cuenta' })).not.toBeInTheDocument()
      await user.click(screen.getAllByRole('button', { name: 'Cancelar eliminación' })[0])

      expect(await screen.findByRole('button', { name: 'Eliminar mi cuenta' })).toBeInTheDocument()
      expect(db.bajas.size).toBe(0)
    })

    it('una cuenta sin contraseña confirma con Google en lugar de con la contraseña', async () => {
      db.vinculosGoogle.set(ana().id, {
        subject: 'sub-estandar@demo.com',
        vinculadoEn: new Date().toISOString(),
      })
      ana().password = null
      vi.spyOn(window, 'prompt').mockReturnValue('estandar@demo.com')
      const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      await user.click(await screen.findByRole('button', { name: 'Eliminar mi cuenta' }))
      const dialogo = await screen.findByRole('dialog', { name: 'Eliminar mi cuenta' })

      expect(within(dialogo).queryByLabelText('Tu contraseña')).not.toBeInTheDocument()
      await user.click(await within(dialogo).findByRole('button', { name: /Continuar con Google/ }))

      expect(await screen.findByText(/se borrará el/)).toBeInTheDocument()
      expect(db.bajas.has(ana().id)).toBe(true)
    })

    it('un token de Google de otra cuenta no sirve para darla de baja (400, sin cerrar la sesión)', async () => {
      db.vinculosGoogle.set(ana().id, {
        subject: 'sub-estandar@demo.com',
        vinculadoEn: new Date().toISOString(),
      })
      ana().password = null
      vi.spyOn(window, 'prompt').mockReturnValue('otra.persona@example.com')
      const { user, router } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      await user.click(await screen.findByRole('button', { name: 'Desactivar' }))
      const dialogo = await screen.findByRole('dialog', { name: 'Desactivar mi cuenta' })
      await user.click(await within(dialogo).findByRole('button', { name: /Continuar con Google/ }))

      expect(
        await within(dialogo).findByText(/No pudimos confirmarlo con Google/),
      ).toBeInTheDocument()
      expect(router.state.location.pathname).toBe('/ajustes/privacidad')
      expect(ana().activo).toBe(true)
    })

    it('tras 5 contraseñas erróneas la cuenta queda bloqueada y lo dice', async () => {
      const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      await user.click(await screen.findByRole('button', { name: 'Eliminar mi cuenta' }))
      const dialogo = await screen.findByRole('dialog', { name: 'Eliminar mi cuenta' })
      for (let i = 0; i < 5; i++) {
        await user.clear(within(dialogo).getByLabelText('Tu contraseña'))
        await user.type(within(dialogo).getByLabelText('Tu contraseña'), 'incorrecta-12345')
        await user.click(within(dialogo).getByRole('button', { name: 'Eliminar mi cuenta' }))
        await within(dialogo).findByRole('alert')
      }
      expect(await within(dialogo).findByText(/bloqueada 15 minutos/)).toBeInTheDocument()
      expect(db.bajas.size).toBe(0)
    })
  })

  describe('contrato simulado de la baja y la descarga', () => {
    const llamar = (ruta: string, token: string, metodo = 'POST', cuerpo?: unknown) =>
      fetch(`${window.location.origin}${ruta}`, {
        method: metodo,
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
        body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
      })

    it('rechaza modos y contraseñas inválidos y el rol Administrador', async () => {
      const token = iniciarSesionComo('estandar')
      expect(
        (
          await llamar('/api/privacy/deletion-request', token, 'POST', {
            modo: 'borrar',
            password: 'x',
          })
        ).status,
      ).toBe(400)
      expect(
        (await llamar('/api/privacy/deletion-request', token, 'POST', { password: 'x' })).status,
      ).toBe(400)
      const admin = iniciarSesionComo('admin')
      expect(
        (
          await llamar('/api/privacy/deletion-request', admin, 'POST', {
            modo: 'eliminar',
            password: 'x',
          })
        ).status,
      ).toBe(403)
      expect((await llamar('/api/privacy/export', admin, 'GET')).status).toBe(403)
      expect((await llamar('/api/privacy/deletion-request', admin, 'GET')).status).toBe(403)
      expect(db.bajas.size).toBe(0)
    })

    it('una contraseña errónea es un 400 clave_actual_incorrecta que cuenta para el bloqueo (un 401 cerraría la sesión)', async () => {
      const token = iniciarSesionComo('estandar')
      const r = await llamar('/api/privacy/deletion-request', token, 'POST', {
        modo: 'desactivar',
        password: 'mala1234',
      })
      expect(r.status).toBe(400)
      expect((await r.json()).codigo).toBe('clave_actual_incorrecta')
      expect(ana().intentosFallidos).toBe(1)
      // Sin contraseña no cuenta como intento (no hay nada que adivinar).
      await llamar('/api/privacy/deletion-request', token, 'POST', { modo: 'desactivar' })
      expect(ana().intentosFallidos).toBe(1)
    })

    it('pedir la eliminación otra vez conserva la fecha original y revoca las sesiones', async () => {
      const token = iniciarSesionComo('estandar')
      const primera = await (
        await llamar('/api/privacy/deletion-request', token, 'POST', {
          modo: 'eliminar',
          password: 'Demo1234',
        })
      ).json()
      const segunda = await (
        await llamar('/api/privacy/deletion-request', iniciarSesionComo('estandar'), 'POST', {
          modo: 'ELIMINAR',
          password: 'Demo1234',
        })
      ).json()

      expect(primera.modo).toBe('eliminar')
      expect(segunda.borradoEn).toBe(primera.borradoEn)
      expect((await llamar('/api/auth/me', token, 'GET')).status).toBe(401)
    })

    it('GET y DELETE de la baja: fecha, cancelación y 404 sin baja', async () => {
      const token = iniciarSesionComo('estandar')
      expect((await llamar('/api/privacy/deletion-request', token, 'GET')).status).toBe(404)
      expect((await llamar('/api/privacy/deletion-request', token, 'DELETE')).status).toBe(404)

      await llamar('/api/privacy/deletion-request', token, 'POST', {
        modo: 'eliminar',
        password: 'Demo1234',
      })
      const nuevo = iniciarSesionComo('estandar')
      const consulta = await (await llamar('/api/privacy/deletion-request', nuevo, 'GET')).json()
      expect(consulta.borradoEn).toMatch(/Z$/)
      expect((await llamar('/api/privacy/deletion-request', nuevo, 'DELETE')).status).toBe(204)
      expect((await llamar('/api/privacy/deletion-request', nuevo, 'GET')).status).toBe(404)
    })

    it('el login de una cuenta con baja programada trae bajaProgramadaPara', async () => {
      await llamar('/api/privacy/deletion-request', iniciarSesionComo('estandar'), 'POST', {
        modo: 'eliminar',
        password: 'Demo1234',
      })
      const login = await (
        await fetch(`${window.location.origin}/api/auth/login`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ email: 'estandar@demo.com', password: 'Demo1234' }),
        })
      ).json()
      expect(login.bajaProgramadaPara).toMatch(/Z$/)
    })
  })
})

describe('ajustes de la revisión del Centro de privacidad', () => {
  it('se puede entrar sin haber completado el perfil (derechos sin condiciones)', async () => {
    db.perfiles.delete(db.usuarios[0].id)
    const { router } = renderApp('/ajustes/privacidad', { como: 'estandar' })
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Privacidad y datos' }),
    ).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/ajustes/privacidad')
    expect(screen.getByRole('button', { name: /Descargar mis datos/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Eliminar mi cuenta' })).toBeInTheDocument()
  })

  it('con documentos aceptados en una versión anterior los interruptores siguen funcionando', async () => {
    const actual = db.consentimientos.get(db.usuarios[0].id)
    if (actual) db.consentimientos.set(db.usuarios[0].id, { ...actual, terminosVersion: 'v2.0' })
    const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })
    const correos = await screen.findByRole('switch', { name: 'Correos con consejos y novedades' })
    await user.click(correos)
    await waitFor(() => expect(correos).toHaveAttribute('aria-checked', 'true'))
    // No se "acepta" en silencio una versión que la persona no vio.
    expect(db.consentimientos.get(db.usuarios[0].id)).toMatchObject({
      terminosVersion: 'v2.0',
      marketing: true,
    })
  })

  it('el interruptor conserva el foco mientras guarda y anuncia el resultado', async () => {
    const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })
    const correos = await screen.findByRole('switch', { name: 'Correos con consejos y novedades' })
    await user.click(correos)
    await waitFor(() => expect(correos).toHaveAttribute('aria-checked', 'true'))
    expect(correos).toHaveFocus()
    expect(await screen.findByText('Cambio guardado.')).toBeInTheDocument()
  })

  it('avisa que el archivo descargado no está cifrado', async () => {
    renderApp('/ajustes/privacidad', { como: 'estandar' })
    expect(await screen.findByText(/no está cifrado y incluye datos de salud/)).toBeInTheDocument()
  })
})
