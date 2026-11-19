import { cleanup, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { TipoNotificacion } from '@/features/notificaciones/types'
import { db } from '@/mocks/db'
import { problema, respuestas } from '@/mocks/http'
import { server } from '@/mocks/server'
import { renderApp } from '@/test/render'

const idDe = (correo: string) => db.usuarios.find((u) => u.correo === correo)!.id
const CULPA = /fallaste|perdiste|culpa|castigo/i

// db.notificaciones va de la más reciente a la más antigua: se siembra en ese orden.
function sembrar(
  correo: string,
  tipo: TipoNotificacion,
  titulo: string,
  leida = false,
  haceMinutos = 0,
) {
  const creada = new Date(Date.now() - haceMinutos * 60_000).toISOString()
  db.notificaciones.push({
    id: crypto.randomUUID(),
    usuarioId: idDe(correo),
    tipo,
    titulo,
    mensaje: `Detalle de ${titulo}`,
    creadaEnUtc: creada,
    leida,
    leidaEnUtc: leida ? creada : null,
  })
}

describe('Notificaciones', () => {
  it('sin avisos muestra el estado vacío con qué esperar', async () => {
    renderApp('/notificaciones', { como: 'estandar' })
    expect(await screen.findByText('Todavía no tienes avisos')).toBeInTheDocument()
    expect(
      screen.getByText('Aquí aparecerán los avisos de tus solicitudes y tus logros.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Avisos por correo' })).toHaveAttribute(
      'href',
      '/ajustes/notificaciones',
    )
  })

  it('lista solo los propios, distingue nuevos de leídos y presenta el logro sin culpa', async () => {
    sembrar('estandar@demo.com', 'LogroObtenido', 'Primera semana completa', false, 1)
    sembrar('estandar@demo.com', 'SolicitudResuelta', 'Tu solicitud fue aprobada', true, 5)
    sembrar('otro@demo.com', 'SolicitudCreada', 'Aviso ajeno', false, 2)
    renderApp('/notificaciones', { como: 'estandar' })

    const lista = within(await screen.findByRole('list', { name: 'Tus notificaciones' }))
    const filas = lista.getAllByRole('listitem')
    expect(filas).toHaveLength(2)
    expect(within(filas[0]).getByText('Primera semana completa')).toBeInTheDocument()
    expect(within(filas[0]).getByText('Logro')).toBeInTheDocument()
    expect(within(filas[0]).getByText('Nueva')).toBeInTheDocument()
    expect(within(filas[1]).getByText(/· Leída/)).toBeInTheDocument()
    expect(within(filas[1]).queryByRole('button')).not.toBeInTheDocument()
    expect(screen.queryByText('Aviso ajeno')).not.toBeInTheDocument()
    expect(document.body.textContent).not.toMatch(CULPA)
  })

  it('el filtro «Sin leer» oculta los leídos y marcar uno lo anuncia', async () => {
    sembrar('estandar@demo.com', 'SolicitudCreada', 'Pediste un permiso', false, 1)
    sembrar('estandar@demo.com', 'SolicitudResuelta', 'Ya resuelta', true, 2)
    const { user } = renderApp('/notificaciones', { como: 'estandar' })

    await screen.findByText('Ya resuelta')
    await user.click(screen.getByRole('button', { name: /^Sin leer/ }))
    expect(screen.getByRole('button', { name: /^Sin leer/ })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
    await screen.findByText('Pediste un permiso')
    expect(screen.queryByText('Ya resuelta')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Marcar como leída: Pediste un permiso' }))
    expect(await screen.findByText('Estás al día')).toBeInTheDocument()
    const aviso = screen.getByText('«Pediste un permiso» marcada como leída.')
    expect(aviso).toHaveFocus()
    expect(db.notificaciones.find((n) => n.titulo === 'Pediste un permiso')?.leida).toBe(true)
  })

  it('con el filtro «Todas» el foco también pasa al aviso al marcar', async () => {
    sembrar('estandar@demo.com', 'LogroObtenido', 'Primera semana completa', false)
    const { user } = renderApp('/notificaciones', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: /^Marcar como leída/ }))
    expect(await screen.findByText('«Primera semana completa» marcada como leída.')).toHaveFocus()
    expect(screen.queryByRole('button', { name: /^Marcar como leída/ })).not.toBeInTheDocument()
  })

  it('un fallo del servidor se muestra en la fila y el botón sigue disponible', async () => {
    sembrar('estandar@demo.com', 'SolicitudCreada', 'Pediste un permiso', false)
    server.use(
      http.post('/api/notifications/:id/read', () =>
        HttpResponse.json({ status: 500, title: 'Error del servidor.' }, { status: 500 }),
      ),
    )
    const { user } = renderApp('/notificaciones', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: /^Marcar como leída/ }))
    expect(await screen.findByRole('alert')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^Marcar como leída/ })).toBeEnabled()
  })

  it('el menú muestra el conteo de sin leer con texto para el lector de pantalla', async () => {
    sembrar('admin@demo.com', 'SolicitudCreada', 'Nueva solicitud', false, 1)
    sembrar('admin@demo.com', 'SolicitudCreada', 'Otra solicitud', false, 2)
    sembrar('admin@demo.com', 'SolicitudCreada', 'Vieja', true, 3)
    renderApp('/admin', { como: 'admin' })
    const nav = await screen.findByRole('navigation', { name: 'Principal' })
    expect(
      await within(nav).findByRole('link', { name: /Notificaciones.*2 sin leer/ }),
    ).toHaveAttribute('href', '/notificaciones')
  })
})

describe('Notificaciones: filtros, errores y globo', () => {
  it('cambiar de filtro retira el aviso anterior y el foco sigue yendo al nuevo aviso', async () => {
    sembrar('estandar@demo.com', 'SolicitudCreada', 'Primero', false, 1)
    sembrar('estandar@demo.com', 'SolicitudCreada', 'Segundo', false, 2)
    const { user } = renderApp('/notificaciones', { como: 'estandar' })

    await user.click(await screen.findByRole('button', { name: 'Marcar como leída: Primero' }))
    expect(await screen.findByText('«Primero» marcada como leída.')).toHaveFocus()

    await user.click(screen.getByRole('button', { name: /^Sin leer/ }))
    expect(screen.queryByText('«Primero» marcada como leída.')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^Todas/ })).toHaveAttribute('aria-pressed', 'false')

    await user.click(await screen.findByRole('button', { name: 'Marcar como leída: Segundo' }))
    const aviso = await screen.findByText('«Segundo» marcada como leída.')
    expect(aviso).toHaveAttribute('role', 'status')
    expect(aviso).toHaveFocus()
    expect(await screen.findByText('Estás al día')).toBeInTheDocument()
  })

  it.each([
    [
      '403: la notificación es de otra persona',
      () => respuestas.ajeno(),
      'No se pudo autorizar la solicitud.',
    ],
    [
      '404: la notificación ya no existe',
      () =>
        problema(404, 'No encontrado.', 'La notificación no existe.', 'notificacion_no_encontrada'),
      'No encontramos esa notificación.',
    ],
  ])(
    'el rechazo del servidor (%s) se dice en la fila y no marca nada',
    async (_caso, responder, texto) => {
      sembrar('estandar@demo.com', 'SolicitudCreada', 'Pediste un permiso', false)
      server.use(http.post('/api/notifications/:id/read', () => responder()))
      const { user } = renderApp('/notificaciones', { como: 'estandar' })

      const fila = (await screen.findByText('Pediste un permiso')).closest('li')!
      await user.click(within(fila).getByRole('button', { name: /^Marcar como leída/ }))
      expect(await within(fila).findByRole('alert')).toHaveTextContent(texto)
      // La fila conserva su botón, sin aviso de éxito y con el aviso sin leer en el servidor.
      expect(within(fila).getByRole('button', { name: /^Marcar como leída/ })).toBeEnabled()
      expect(screen.queryByText(/marcada como leída\./)).not.toBeInTheDocument()
      expect(db.notificaciones[0].leida).toBe(false)
    },
  )

  it('un error al cargar se dice con un mensaje genérico y «Reintentar» recupera la lista', async () => {
    sembrar('estandar@demo.com', 'LogroObtenido', 'Primera semana completa', false)
    let fallar = true
    server.use(
      http.get('/api/notifications', ({ request }) => {
        // Solo la lista completa: el contador del menú (soloNoLeidas=true) sigue su camino.
        if (fallar && new URL(request.url).searchParams.get('soloNoLeidas') === 'false') {
          return HttpResponse.json({ status: 500, title: 'Detalle interno' }, { status: 500 })
        }
      }),
    )
    const { user } = renderApp('/notificaciones', { como: 'estandar' })

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'No se pudieron cargar tus notificaciones.',
    )
    expect(screen.queryByText(/Detalle interno/)).not.toBeInTheDocument()
    fallar = false
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByText('Primera semana completa')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('el globo dice «N sin leer» con palabras y desaparece cuando ya no queda ninguno', async () => {
    sembrar('estandar@demo.com', 'SolicitudCreada', 'Pediste un permiso', false)
    const { user } = renderApp('/notificaciones', { como: 'estandar' })
    const nav = await screen.findByRole('navigation', { name: 'Principal' })

    const enlace = await within(nav).findByRole('link', { name: /Notificaciones.*1 sin leer/ })
    // El número es visible y el texto para el lector lo repite con palabras.
    expect(within(enlace).getByText('1')).toBeInTheDocument()
    expect(within(enlace).getByText('(1 sin leer)')).toHaveClass('sr-only')

    await user.click(await screen.findByRole('button', { name: /^Marcar como leída/ }))
    await waitFor(() =>
      expect(within(nav).getByRole('link', { name: 'Notificaciones' })).toBeInTheDocument(),
    )
    expect(within(nav).queryByText(/sin leer/)).not.toBeInTheDocument()
  })

  it('sin avisos sin leer, el menú no pinta globo; solo cuenta los propios', async () => {
    sembrar('estandar@demo.com', 'SolicitudResuelta', 'Ya leída', true)
    sembrar('otro@demo.com', 'SolicitudCreada', 'Ajena sin leer', false)
    renderApp('/notificaciones', { como: 'estandar' })
    const nav = await screen.findByRole('navigation', { name: 'Principal' })
    await screen.findByText('Ya leída')
    // La cifra del filtro y el globo salen de la misma consulta: con la cifra en 0 el globo ya decidió.
    expect(await screen.findByRole('button', { name: 'Sin leer (0)' })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: 'Notificaciones' })).toBeInTheDocument()
    expect(within(nav).queryByText(/sin leer/)).not.toBeInTheDocument()
  })
})

describe('Configuración › Notificaciones', () => {
  it('guarda la preferencia de correo y confirma sin perder la bandeja', async () => {
    const { user } = renderApp('/ajustes/notificaciones', { como: 'estandar' })
    const interruptor = await screen.findByRole('switch', { name: 'Recibir también por correo' })
    expect(interruptor).toHaveAttribute('aria-checked', 'true')
    const explicacion = screen.getByText(/Tus avisos siempre se guardan en/)
    expect(within(explicacion).getByRole('link', { name: 'Notificaciones' })).toHaveAttribute(
      'href',
      '/notificaciones',
    )

    await user.click(interruptor)
    expect(await screen.findByText('Dejarás de recibir los avisos por correo.')).toBeInTheDocument()
    expect(db.preferenciasCorreo.get(idDe('estandar@demo.com'))).toBe(false)
    expect(screen.getByRole('switch', { name: 'Recibir también por correo' })).toHaveAttribute(
      'aria-checked',
      'false',
    )
  })

  it('se puede volver a activar el correo y la preferencia se conserva', async () => {
    db.preferenciasCorreo.set(idDe('estandar@demo.com'), false)
    const { user } = renderApp('/ajustes/notificaciones', { como: 'estandar' })
    const interruptor = await screen.findByRole('switch', { name: 'Recibir también por correo' })
    expect(interruptor).toHaveAttribute('aria-checked', 'false')

    await user.click(interruptor)
    expect(await screen.findByText('Recibirás los avisos también por correo.')).toBeInTheDocument()
    expect(db.preferenciasCorreo.get(idDe('estandar@demo.com'))).toBe(true)
    expect(screen.getByRole('switch', { name: 'Recibir también por correo' })).toHaveAttribute(
      'aria-checked',
      'true',
    )
  })

  it('si no se pudo guardar lo dice y el interruptor queda como estaba', async () => {
    server.use(
      http.put('/api/notifications/preferences', () =>
        HttpResponse.json({ status: 500, title: 'Detalle interno' }, { status: 500 }),
      ),
    )
    const { user } = renderApp('/ajustes/notificaciones', { como: 'estandar' })
    await user.click(await screen.findByRole('switch', { name: 'Recibir también por correo' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo guardar tu preferencia.')
    expect(screen.queryByText(/Detalle interno/)).not.toBeInTheDocument()
    expect(screen.getByRole('switch', { name: 'Recibir también por correo' })).toHaveAttribute(
      'aria-checked',
      'true',
    )
    expect(db.preferenciasCorreo.has(idDe('estandar@demo.com'))).toBe(false)
  })

  it('un error al cargar la preferencia ofrece reintentar', async () => {
    let fallar = true
    server.use(
      http.get('/api/notifications/preferences', () => {
        if (fallar) return HttpResponse.json({ status: 500, title: 'x' }, { status: 500 })
      }),
    )
    const { user } = renderApp('/ajustes/notificaciones', { como: 'estandar' })
    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo cargar tu preferencia.')
    expect(screen.queryByRole('switch')).not.toBeInTheDocument()

    fallar = false
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(
      await screen.findByRole('switch', { name: 'Recibir también por correo' }),
    ).toHaveAttribute('aria-checked', 'true')
  })

  it('el Administrador también la tiene en el menú de Configuración', async () => {
    renderApp('/ajustes/cuenta', { como: 'admin' })
    const menu = await screen.findByRole('navigation', { name: 'Secciones de configuración' })
    expect(within(menu).getByRole('link', { name: 'Notificaciones' })).toHaveAttribute(
      'href',
      '/ajustes/notificaciones',
    )
  })
})

describe('Avisos de punta a punta (API simulada)', () => {
  // Cada persona abre su propia sesión: se desmonta la anterior antes de entrar con otra.
  async function solicitarComoEstandar() {
    const { user } = renderApp('/permisos', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Enviar solicitud' }))
    expect(await screen.findByText('Pendiente')).toBeInTheDocument()
    cleanup()
  }

  async function rechazarComoAdmin(motivo: string) {
    const { user } = renderApp('/admin/permisos', { como: 'admin' })
    const fila = (await screen.findByText('estandar@demo.com', { selector: 'td' })).closest('tr')!
    await user.click(within(fila).getByRole('button', { name: /^Rechazar/ }))
    const dialogo = await screen.findByRole('dialog', { name: 'Rechazar solicitud' })
    await user.type(within(dialogo).getByLabelText('Motivo del rechazo'), motivo)
    await user.click(within(dialogo).getByRole('button', { name: 'Rechazar' }))
    expect(await screen.findByText('Solicitud rechazada.')).toBeInTheDocument()
    cleanup()
  }

  it('el Estándar pide, el Administrador la ve en su bandeja y, al rechazarla, el Estándar lee el motivo', async () => {
    await solicitarComoEstandar()

    // El Administrador la ve en su bandeja y en el globo del menú.
    const admin = renderApp('/notificaciones', { como: 'admin' })
    const lista = within(await screen.findByRole('list', { name: 'Tus notificaciones' }))
    const fila = lista.getByText('Nueva solicitud de permiso').closest('li')!
    expect(within(fila).getByText(/estandar@demo\.com pidió el permiso/)).toBeInTheDocument()
    expect(within(fila).getByText('Nueva')).toBeInTheDocument()
    const nav = screen.getByRole('navigation', { name: 'Principal' })
    expect(
      await within(nav).findByRole('link', { name: /Notificaciones.*1 sin leer/ }),
    ).toBeInTheDocument()
    await admin.user.click(within(fila).getByRole('button', { name: /^Marcar como leída/ }))
    expect(
      await screen.findByText('«Nueva solicitud de permiso» marcada como leída.'),
    ).toHaveFocus()
    cleanup()

    await rechazarComoAdmin('Sin justificación suficiente')

    renderApp('/notificaciones', { como: 'estandar' })
    const suyas = within(await screen.findByRole('list', { name: 'Tus notificaciones' }))
    const rechazo = suyas.getByText('Solicitud rechazada').closest('li')!
    expect(within(rechazo).getByText(/Motivo: Sin justificación suficiente/)).toBeInTheDocument()
    expect(within(rechazo).getByText('Nueva')).toBeInTheDocument()
    // Nada del aviso del Administrador llega al Estándar.
    expect(suyas.queryByText('Nueva solicitud de permiso')).not.toBeInTheDocument()
    expect(suyas.getAllByRole('listitem')).toHaveLength(1)
    expect(document.body.textContent).not.toMatch(CULPA)
    cleanup()

    // Con el correo activo (por defecto) los dos avisos quedan en la cola del Administrador, sin cuerpo.
    renderApp('/admin/cola-correos', { como: 'admin' })
    const filas = within(await screen.findByRole('table', { name: 'Correos en la cola' }))
      .getAllByRole('row')
      .slice(1)
    expect(filas).toHaveLength(2)
    expect(within(filas[0]).getByText('estandar@demo.com')).toBeInTheDocument()
    expect(within(filas[0]).getByText('Solicitud rechazada')).toBeInTheDocument()
    expect(within(filas[1]).getByText('admin@demo.com')).toBeInTheDocument()
    expect(within(filas[1]).getByText('Nueva solicitud de permiso')).toBeInTheDocument()
    expect(screen.queryByText(/Motivo: Sin justificación suficiente/)).not.toBeInTheDocument()
  })

  it('con el correo desactivado el aviso llega a la bandeja, pero no se encola ningún correo', async () => {
    // El Estándar apaga el correo desde Configuración.
    const ajustes = renderApp('/ajustes/notificaciones', { como: 'estandar' })
    await ajustes.user.click(
      await screen.findByRole('switch', { name: 'Recibir también por correo' }),
    )
    expect(await screen.findByText('Dejarás de recibir los avisos por correo.')).toBeInTheDocument()
    cleanup()

    await solicitarComoEstandar()
    await rechazarComoAdmin('Todavía no hace falta')

    // La bandeja se conserva (RF-NOT-07)...
    renderApp('/notificaciones', { como: 'estandar' })
    const rechazo = (await screen.findByText('Solicitud rechazada')).closest('li')!
    expect(within(rechazo).getByText(/Motivo: Todavía no hace falta/)).toBeInTheDocument()
    cleanup()

    // ...pero en la cola del Administrador solo está el correo que él sí recibe.
    renderApp('/admin/cola-correos', { como: 'admin' })
    const tabla = within(await screen.findByRole('table', { name: 'Correos en la cola' }))
    const filas = tabla.getAllByRole('row').slice(1)
    expect(filas).toHaveLength(1)
    expect(within(filas[0]).getByText('admin@demo.com')).toBeInTheDocument()
    expect(tabla.queryByText('estandar@demo.com')).not.toBeInTheDocument()
    expect(tabla.queryByText('Solicitud rechazada')).not.toBeInTheDocument()
  })
})
