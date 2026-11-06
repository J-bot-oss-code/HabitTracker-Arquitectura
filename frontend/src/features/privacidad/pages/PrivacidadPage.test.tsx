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

  it('arma el archivo con versión, instante UTC y las piezas ausentes como null', () => {
    const e = armarExportacion(
      {
        cuenta: {
          id: '1',
          nombre: 'Ana',
          correo: 'a@b.c',
          rol: 'Estandar',
          requiereAceptarDocumentos: false,
        },
        habitos: [],
        permisos: [],
        perfil: null,
        pesos: null,
        mediciones: null,
        consentimientos: null,
      },
      new Date('2026-11-03T10:00:00Z'),
    )
    expect(e).toMatchObject({
      formato: 'habittracker-datos',
      version: 1,
      generadoEn: '2026-11-03T10:00:00.000Z',
      perfil: null,
      progreso: { pesos: null, mediciones: null },
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
    const menu = screen.getByRole('navigation', { name: 'Principal' })
    expect(within(menu).getByRole('link', { name: 'Privacidad' })).toHaveAttribute(
      'aria-current',
      'page',
    )
    expect(screen.getByRole('link', { name: /Volver a Hoy/ })).toHaveAttribute('href', '/hoy')
  })

  it('el Administrador no accede', async () => {
    renderApp('/ajustes/privacidad', { como: 'admin' })
    expect(await screen.findByText('No tienes acceso a esta sección')).toBeInTheDocument()
  })

  describe('descargar mis datos', () => {
    it('baja un JSON con los datos del usuario y deja constancia en la auditoría', async () => {
      let blobSubido: Blob | null = null
      URL.createObjectURL = (b: Blob | MediaSource) => {
        blobSubido = b as Blob
        return 'blob:prueba'
      }
      URL.revokeObjectURL = () => undefined
      let nombre = ''
      vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
        this: HTMLAnchorElement,
      ) {
        nombre = this.download
      })

      const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      await user.click(await screen.findByRole('button', { name: /Descargar mis datos/ }))
      expect(await screen.findByText('Descarga lista.')).toBeInTheDocument()

      expect(nombre).toMatch(/^habittracker-mis-datos-\d{4}-\d{2}-\d{2}\.json$/)
      const contenido = JSON.parse(await (blobSubido as unknown as Blob).text())
      expect(contenido).toMatchObject({
        formato: 'habittracker-datos',
        cuenta: { correo: 'estandar@demo.com', rol: 'Estandar' },
        perfil: { metaSlug: 'primeros-5-km' },
        consentimientos: { terminosVersion: 'v2.1', datosSalud: true },
      })
      expect(contenido.habitos).toHaveLength(3)
      expect(contenido.progreso.pesos.registros).toHaveLength(10)
      expect(contenido.progreso.mediciones).toHaveLength(8)
      // No viaja nada de otras cuentas ni credenciales.
      expect(JSON.stringify(contenido)).not.toMatch(/otro@demo|Demo1234|password/i)
      const ultimo = db.auditoria.at(-1)
      expect(ultimo).toMatchObject({ actor: 'estandar@demo.com', accion: 'DescargaDatos' })
    })

    it('si falta una pieza en el servidor la descarga sigue con esa parte en null', async () => {
      URL.createObjectURL = () => 'blob:prueba'
      URL.revokeObjectURL = () => undefined
      vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
      server.use(
        http.get('/api/progress/weights', () => new HttpResponse(null, { status: 404 })),
        http.post('/api/privacy/export-request', () => new HttpResponse(null, { status: 404 })),
      )
      const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      await user.click(await screen.findByRole('button', { name: /Descargar mis datos/ }))
      expect(await screen.findByText('Descarga lista.')).toBeInTheDocument()
    })

    it('si falla una pieza esencial avisa en vez de entregar un archivo incompleto', async () => {
      const descargas = vi
        .spyOn(HTMLAnchorElement.prototype, 'click')
        .mockImplementation(() => undefined)
      server.use(http.get('/api/habits', () => problema(500, 'Error interno.')))
      const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      await user.click(await screen.findByRole('button', { name: /Descargar mis datos/ }))
      expect(await screen.findByText('No se pudo preparar la descarga.')).toBeInTheDocument()
      expect(descargas).not.toHaveBeenCalled()
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
      expect(db.solicitudesBaja).toHaveLength(0)
    })

    it('desactivar cierra la sesión y deja la cuenta inactiva', async () => {
      const { user, router } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      await user.click(await screen.findByRole('button', { name: 'Desactivar' }))
      const dialogo = await screen.findByRole('dialog', { name: 'Desactivar mi cuenta' })
      await user.type(within(dialogo).getByLabelText('Tu contraseña'), 'Demo1234')
      await user.click(within(dialogo).getByRole('button', { name: 'Desactivar cuenta' }))

      expect(await screen.findByText('Tu cuenta quedó desactivada.')).toBeInTheDocument()
      expect(router.state.location.pathname).toBe('/login')
      expect(ana().activo).toBe(false)
      expect(db.solicitudesBaja[0]).toMatchObject({ tipo: 'desactivar', borradoEn: null })
      expect(db.auditoria.at(-1)).toMatchObject({
        accion: 'SolicitudBaja',
        valorNuevo: 'desactivar',
      })
    })

    it('eliminar fija el borrado a 30 días (UTC) y cierra la sesión', async () => {
      const { user } = renderApp('/ajustes/privacidad', { como: 'estandar' })
      await user.click(await screen.findByRole('button', { name: 'Eliminar mi cuenta' }))
      const dialogo = await screen.findByRole('dialog', { name: 'Eliminar mi cuenta' })
      await user.type(within(dialogo).getByLabelText('Tu contraseña'), 'Demo1234')
      await user.click(within(dialogo).getByRole('button', { name: 'Eliminar mi cuenta' }))

      expect(await screen.findByText(/Tus datos se borrarán en 30 días/)).toBeInTheDocument()
      const { solicitadaEn, borradoEn } = db.solicitudesBaja[0]
      expect(borradoEn).toMatch(/Z$/)
      const dias = (Date.parse(borradoEn ?? '') - Date.parse(solicitadaEn)) / 86_400_000
      expect(dias).toBeCloseTo(30, 3)
    })
  })

  describe('contrato propuesto', () => {
    const post = (ruta: string, token: string, cuerpo?: unknown) =>
      fetch(`${window.location.origin}${ruta}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
        body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
      })

    it('rechaza modos y contraseñas inválidos y el rol Administrador', async () => {
      const token = iniciarSesionComo('estandar')
      expect(
        (await post('/api/privacy/deletion-request', token, { modo: 'borrar', password: 'x' }))
          .status,
      ).toBe(400)
      expect(
        (await post('/api/privacy/deletion-request', token, { modo: 'eliminar', password: '' }))
          .status,
      ).toBe(400)
      expect(
        (
          await post('/api/privacy/deletion-request', iniciarSesionComo('admin'), {
            modo: 'eliminar',
            password: 'x',
          })
        ).status,
      ).toBe(403)
      expect((await post('/api/privacy/export-request', iniciarSesionComo('admin'))).status).toBe(
        403,
      )
      expect(db.solicitudesBaja).toHaveLength(0)
    })

    it('una contraseña errónea es un 400 (un 401 cerraría la sesión en el cliente)', async () => {
      const token = iniciarSesionComo('estandar')
      const r = await post('/api/privacy/deletion-request', token, {
        modo: 'desactivar',
        password: 'mala1234',
      })
      expect(r.status).toBe(400)
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
