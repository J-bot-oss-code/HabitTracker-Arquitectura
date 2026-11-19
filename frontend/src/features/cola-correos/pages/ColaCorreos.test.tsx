import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi, afterEach } from 'vitest'
import type { CorreoColaDto, EstadoCorreo } from '@/features/cola-correos/types'
import { db } from '@/mocks/db'
import { DOMINIO_QUE_FALLA } from '@/mocks/handlers/colaCorreos'
import { server } from '@/mocks/server'
import { renderApp } from '@/test/render'

function correo(destinatario: string, estado: EstadoCorreo, extra: Partial<CorreoColaDto> = {}) {
  db.correos.push({
    id: crypto.randomUUID(),
    destinatario,
    asunto: `Asunto para ${destinatario}`,
    estado,
    intentos: 0,
    fechaCreacion: new Date().toISOString(),
    fechaUltimoIntento: null,
    fechaEnvio: null,
    ultimoError: null,
    ...extra,
  })
}

afterEach(() => vi.restoreAllMocks())

const tabla = () => screen.findByRole('table', { name: 'Correos en la cola' })

describe('Cola de correos', () => {
  it('vacía, explica cuándo aparece un correo', async () => {
    renderApp('/admin/cola-correos', { como: 'admin' })
    expect(await screen.findByText('No hay correos en la cola')).toBeInTheDocument()
  })

  it('muestra el estado con texto, los intentos, el error y la nota de que no hay cuerpo', async () => {
    const ahora = new Date().toISOString()
    correo('ana@demo.com', 'Enviado', {
      intentos: 1,
      fechaEnvio: ahora,
      fechaUltimoIntento: ahora,
    })
    correo('x@fallo.test', 'Fallido', {
      intentos: 3,
      fechaUltimoIntento: ahora,
      ultimoError: 'No se pudo entregar el correo.',
    })
    renderApp('/admin/cola-correos', { como: 'admin' })

    const filas = within(await tabla())
      .getAllByRole('row')
      .slice(1)
    expect(filas).toHaveLength(2)
    const fallido = filas.find((f) => within(f).queryByText('x@fallo.test'))!
    expect(within(fallido).getByText('Fallido')).toBeInTheDocument()
    expect(within(fallido).getByText(/Intentos: 3 de 3 · ya no se reintenta/)).toBeInTheDocument()
    expect(within(fallido).getByText('No se pudo entregar el correo.')).toBeInTheDocument()
    expect(within(fallido).getByText('Sin enviar')).toBeInTheDocument()
    expect(screen.getByText(/El contenido de los correos no se muestra/)).toBeInTheDocument()
    // Con espacio entre el texto y la cifra.
    expect(screen.getByText(/se reintenta hasta 3 veces./)).toBeInTheDocument()
  })

  it('filtra por estado', async () => {
    correo('ana@demo.com', 'Enviado')
    correo('luis@demo.com', 'Pendiente')
    const { user } = renderApp('/admin/cola-correos', { como: 'admin' })
    await tabla()

    await user.click(screen.getByRole('button', { name: 'Pendiente' }))
    expect(await screen.findByText('luis@demo.com')).toBeInTheDocument()
    expect(screen.queryByText('ana@demo.com')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Fallido' }))
    expect(await screen.findByText('No hay correos fallidos')).toBeInTheDocument()
    expect(screen.getByText('Todo va bien.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Procesando' }))
    expect(await screen.findByText('No hay correos con estado Procesando')).toBeInTheDocument()
  })

  it('«Procesar ahora» cuenta enviados y fallos; si no hay nada, no afirma que no haya pendientes', async () => {
    correo('ana@demo.com', 'Pendiente')
    correo(`b@${DOMINIO_QUE_FALLA}`, 'Pendiente')
    const { user } = renderApp('/admin/cola-correos', { como: 'admin' })
    await tabla()

    await user.click(screen.getByRole('button', { name: 'Procesar ahora' }))
    expect(await screen.findByText('1 enviado y 1 con fallo.')).toBeInTheDocument()

    // El fallido espera su reintento: ahora no hay nada listo.
    await user.click(screen.getByRole('button', { name: 'Procesar ahora' }))
    expect(await screen.findByText(/No había nada que procesar/)).toBeInTheDocument()
    expect(document.body.textContent).not.toMatch(/no hay (correos )?pendientes/i)
  })

  it('un correo que estaba a medias se explica sin jerga', async () => {
    correo('ana@demo.com', 'Procesando')
    const { user } = renderApp('/admin/cola-correos', { como: 'admin' })
    await tabla()
    // El mock nunca deja correos en Procesando, así que no hay nada que recuperar: el resultado es el de siempre.
    await user.click(screen.getByRole('button', { name: 'Procesar ahora' }))
    expect(await screen.findByText(/No había nada que procesar/)).toBeInTheDocument()
    expect(document.body.textContent).not.toMatch(/recuperados?/i)
  })

  it('el resumen del Administrador muestra las cifras de la cola', async () => {
    correo('x@fallo.test', 'Fallido', { intentos: 1 })
    correo('a@demo.com', 'Pendiente')
    renderApp('/admin', { como: 'admin' })
    expect(await screen.findByText('1 fallido · 1 pendiente')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Abrir la cola de correos' })).toHaveAttribute(
      'href',
      '/admin/cola-correos',
    )
  })

  it('un Estándar no entra: la ruta es del Administrador', async () => {
    renderApp('/admin/cola-correos', { como: 'estandar' })
    expect(await screen.findByText('No tienes acceso a esta sección')).toBeInTheDocument()
  })

  it('el menú del Estándar no ofrece la cola pero sí Notificaciones y Reportes', async () => {
    renderApp('/notificaciones', { como: 'estandar' })
    const nav = await screen.findByRole('navigation', { name: 'Principal' })
    expect(within(nav).queryByRole('link', { name: 'Cola de correos' })).not.toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: /Notificaciones/ })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: 'Reportes' })).toBeInTheDocument()
  })
})

describe('Cola de correos: estados, resultado y errores', () => {
  it('un Fallido que aún se reintentará es un aviso; el que ya se rindió es un problema', async () => {
    correo('a@fallo.test', 'Fallido', {
      intentos: 1,
      ultimoError: 'No se pudo entregar el correo.',
    })
    correo('b@fallo.test', 'Fallido', {
      intentos: 3,
      ultimoError: 'No se pudo entregar el correo.',
    })
    renderApp('/admin/cola-correos', { como: 'admin' })
    const filas = within(await tabla())

    const reintentable = filas.getByText('a@fallo.test').closest('tr')!
    expect(within(reintentable).getByText('Fallido')).toHaveClass('text-nota-texto')
    expect(within(reintentable).getByText('Intentos: 1 de 3')).toBeInTheDocument()
    expect(within(reintentable).queryByText(/ya no se reintenta/)).not.toBeInTheDocument()

    const rendido = filas.getByText('b@fallo.test').closest('tr')!
    expect(within(rendido).getByText('Fallido')).toHaveClass('text-peligro')
    expect(within(rendido).getByText(/Intentos: 3 de 3 · ya no se reintenta/)).toBeInTheDocument()
  })

  it('«Procesar ahora» con solo envíos lo cuenta en plural y lo da por bueno', async () => {
    correo('ana@demo.com', 'Pendiente')
    correo('luis@demo.com', 'Pendiente')
    const { user } = renderApp('/admin/cola-correos', { como: 'admin' })
    await tabla()

    await user.click(screen.getByRole('button', { name: 'Procesar ahora' }))
    const texto = await screen.findByText('2 enviados.')
    expect(texto.closest('[role="status"]')).toHaveClass('border-exito/50')
    expect(await screen.findAllByText('Enviado', { selector: 'span' })).toHaveLength(2)
  })

  it('«Procesar ahora» con un fallo avisa en tono de aviso, no de éxito', async () => {
    correo(`b@${DOMINIO_QUE_FALLA}`, 'Pendiente')
    const { user } = renderApp('/admin/cola-correos', { como: 'admin' })
    await tabla()

    await user.click(screen.getByRole('button', { name: 'Procesar ahora' }))
    const texto = await screen.findByText('1 con fallo.')
    expect(texto.closest('[role="status"]')).toHaveClass('border-aviso/50')
    expect(await screen.findByText('Intentos: 1 de 3')).toBeInTheDocument()
  })

  it.each([
    [
      { enviados: 1, fallidos: 0, recuperados: 2 },
      '1 enviado y 2 que se habían quedado a medias y vuelven a la cola.',
    ],
    [
      { enviados: 0, fallidos: 0, recuperados: 1 },
      '1 que se había quedado a medias y vuelve a la cola.',
    ],
    [
      { enviados: 2, fallidos: 1, recuperados: 1 },
      '2 enviados, 1 con fallo y 1 que se había quedado a medias y vuelve a la cola.',
    ],
  ])('«Procesar ahora» con las cifras %j dice «%s»', async (resultado, esperado) => {
    server.use(http.post('/api/admin/email-queue/process', () => HttpResponse.json(resultado)))
    const { user } = renderApp('/admin/cola-correos', { como: 'admin' })
    await screen.findByText('No hay correos en la cola')

    await user.click(screen.getByRole('button', { name: 'Procesar ahora' }))
    expect(await screen.findByText(esperado)).toBeInTheDocument()
  })

  it.each([
    [500, 'No se pudo procesar la cola.'],
    [403, 'No tienes permiso para realizar esta acción.'],
  ])(
    'si «Procesar ahora» falla con %i lo dice y el botón queda disponible',
    async (status, texto) => {
      correo('ana@demo.com', 'Pendiente')
      server.use(
        http.post('/api/admin/email-queue/process', () =>
          status === 500
            ? HttpResponse.json({ status, title: 'Detalle interno' }, { status })
            : new HttpResponse(null, { status }),
        ),
      )
      const { user } = renderApp('/admin/cola-correos', { como: 'admin' })
      await tabla()

      await user.click(screen.getByRole('button', { name: 'Procesar ahora' }))
      expect(await screen.findByRole('alert')).toHaveTextContent(texto)
      expect(screen.queryByText(/Detalle interno/)).not.toBeInTheDocument()
      expect(screen.queryByText('Cola procesada')).not.toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Procesar ahora' })).toBeEnabled()
      // El correo sigue Pendiente: el fallo no lo toca.
      expect(db.correos[0].estado).toBe('Pendiente')
    },
  )

  it('un error al cargar la lista se dice con un mensaje genérico y se puede reintentar', async () => {
    correo('ana@demo.com', 'Enviado')
    let fallar = true
    server.use(
      http.get('/api/admin/email-queue', () => {
        if (fallar)
          return HttpResponse.json({ status: 500, title: 'Detalle interno' }, { status: 500 })
      }),
    )
    const { user } = renderApp('/admin/cola-correos', { como: 'admin' })
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'No se pudo cargar la cola de correos.',
    )
    expect(screen.queryByText(/Detalle interno/)).not.toBeInTheDocument()

    fallar = false
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByText('ana@demo.com')).toBeInTheDocument()
  })

  it('si el servidor rechaza la lista con 403 lo dice, sin tabla ni reintento', async () => {
    renderApp('/admin/cola-correos', { como: 'admin' })
    // El token ya se emitió como Administrador; el servidor decide con el rol que tiene ahora (RD-06).
    db.usuarios.find((u) => u.correo === 'admin@demo.com')!.rol = 'Estandar'

    expect(await screen.findByText('Solo para Administradores')).toBeInTheDocument()
    expect(
      screen.getByText('La cola de correos solo la puede ver un Administrador.'),
    ).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reintentar' })).not.toBeInTheDocument()
  })
})

describe('Cola de correos: acceso por rol', () => {
  it('un Estándar es rechazado por la ruta sin que se pida la cola al servidor', async () => {
    const espia = vi.spyOn(globalThis, 'fetch')
    renderApp('/admin/cola-correos', { como: 'estandar' })
    expect(await screen.findByText('No tienes acceso a esta sección')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Cola de correos' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Procesar ahora' })).not.toBeInTheDocument()
    const pedidas = espia.mock.calls.filter(([entrada]) =>
      String(entrada instanceof Request ? entrada.url : entrada).includes('/api/admin/email-queue'),
    )
    expect(pedidas).toHaveLength(0)
  })

  it('el menú del Administrador la ofrece y lleva a ella', async () => {
    const { user, router } = renderApp('/admin', { como: 'admin' })
    const nav = await screen.findByRole('navigation', { name: 'Principal' })
    await user.click(within(nav).getByRole('link', { name: 'Cola de correos' }))
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Cola de correos' }),
    ).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/admin/cola-correos')
  })
})

describe('Resumen del Administrador: tarjeta «Cola de correos»', () => {
  it('sin fallidos dice «Todo en orden», aunque haya pendientes', async () => {
    correo('a@demo.com', 'Pendiente')
    correo('b@demo.com', 'Enviado')
    renderApp('/admin', { como: 'admin' })
    expect(await screen.findByText('Todo en orden')).toBeInTheDocument()
    expect(screen.queryByText(/fallidos?\s·/)).not.toBeInTheDocument()
  })

  it('con la cola vacía también dice «Todo en orden»', async () => {
    renderApp('/admin', { como: 'admin' })
    expect(await screen.findByText('Todo en orden')).toBeInTheDocument()
  })

  it('con varios fallidos usa el plural y cuenta los pendientes', async () => {
    correo('x@fallo.test', 'Fallido', { intentos: 1 })
    correo('y@fallo.test', 'Fallido', { intentos: 3 })
    renderApp('/admin', { como: 'admin' })
    expect(await screen.findByText('2 fallidos · 0 pendientes')).toBeInTheDocument()
    expect(screen.queryByText('Todo en orden')).not.toBeInTheDocument()
  })

  it('si la consulta de la cola falla, la tarjeta se queda solo con el enlace', async () => {
    let atendida = false
    server.use(
      http.get('/api/admin/email-queue', () => {
        atendida = true
        return HttpResponse.json({ status: 500, title: 'Detalle interno' }, { status: 500 })
      }),
    )
    const { user } = renderApp('/admin', { como: 'admin' })
    const enlace = await screen.findByRole('link', { name: 'Abrir la cola de correos' })
    await waitFor(() => expect(atendida).toBe(true))
    // Un turno de eventos más (user-event) para que la respuesta fallida llegue a la pantalla.
    await user.tab()

    expect(enlace).toHaveAttribute('href', '/admin/cola-correos')
    expect(screen.getByRole('heading', { name: 'Cola de correos', level: 2 })).toBeInTheDocument()
    expect(screen.queryByText('Todo en orden')).not.toBeInTheDocument()
    expect(screen.queryByText(/fallidos?\s·|pendientes?$/)).not.toBeInTheDocument()
    expect(screen.queryByText(/Detalle interno/)).not.toBeInTheDocument()
  })
})
