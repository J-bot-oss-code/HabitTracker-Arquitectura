import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { server } from '@/mocks/server'
import { iniciarSesionComo, renderApp } from '@/test/render'
import type { EstadoSugerencia, TipoSugerencia } from '../types'

const idDe = (correo: string) => db.usuarios.find((u) => u.correo === correo)!.id
const CULPA = /fallaste|perdiste|culpa|castigo/i

function sembrar(
  correo: string,
  texto: string,
  extra: { tipo?: TipoSugerencia; estado?: EstadoSugerencia; haceMinutos?: number } = {},
) {
  const sugerencia = {
    id: crypto.randomUUID(),
    usuarioId: idDe(correo),
    tipo: extra.tipo ?? 'Mejora',
    texto,
    creadaEn: new Date(Date.now() - (extra.haceMinutos ?? 0) * 60_000).toISOString(),
    estado: extra.estado ?? 'Nueva',
  }
  db.sugerencias.push(sugerencia)
  return sugerencia
}

const url = (ruta: string) => `${window.location.origin}${ruta}`
const llamar = (ruta: string, token?: string, metodo = 'GET', body?: unknown) =>
  fetch(url(ruta), {
    method: metodo,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  })

describe('Sugerencias del Estándar', () => {
  it('envía una sugerencia y la ve en «Lo que has enviado» como Recibida', async () => {
    const { user } = renderApp('/ajustes/sugerencias', { como: 'estandar' })
    expect(await screen.findByText('Todavía no has escrito nada')).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Tipo'), 'Queja')
    await user.click(screen.getByLabelText('Tu mensaje'))
    await user.paste('La lista de planes tarda en cargar.')
    await user.click(screen.getByRole('button', { name: 'Enviar' }))

    const lista = within(await screen.findByRole('list', { name: /Lo que has enviado/ }))
    const fila = lista.getByRole('listitem')
    expect(within(fila).getByText('Queja')).toBeInTheDocument()
    expect(within(fila).getByText('Recibida')).toBeInTheDocument()
    expect(within(fila).getByText('La lista de planes tarda en cargar.')).toBeInTheDocument()
    expect(fila.querySelector('time')).toHaveAttribute('datetime')
    // El formulario queda listo para otro mensaje, con el mismo tipo y el foco en el campo.
    await waitFor(() => expect(screen.getByLabelText('Tu mensaje')).toHaveFocus())
    expect(screen.getByLabelText('Tu mensaje')).toHaveValue('')
    expect(screen.getByLabelText('Tipo')).toHaveValue('Queja')
    // La confirmación vive junto a «Enviar», en una región fija.
    expect(screen.getByText('Enviado. Lo ves abajo, en «Lo que has enviado».')).toHaveAttribute(
      'role',
      'status',
    )
    expect(db.sugerencias).toHaveLength(1)
    expect(db.sugerencias[0]).toMatchObject({
      usuarioId: idDe('estandar@demo.com'),
      tipo: 'Queja',
      estado: 'Nueva',
    })
  })

  it('con menos de 10 caracteres (tras recortar) explica el límite y no envía nada', async () => {
    const { user } = renderApp('/ajustes/sugerencias', { como: 'estandar' })
    await screen.findByText('Todavía no has escrito nada')

    await user.type(screen.getByLabelText('Tu mensaje'), '   corto   ')
    await user.click(screen.getByRole('button', { name: 'Enviar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Cuéntanos con al menos 10 caracteres.',
    )
    expect(screen.getByLabelText('Tu mensaje')).toHaveAttribute('aria-invalid', 'true')
    expect(db.sugerencias).toHaveLength(0)
  })

  it('el contador cuenta lo escrito sobre 2000', async () => {
    const { user } = renderApp('/ajustes/sugerencias', { como: 'estandar' })
    expect(await screen.findByText('0 / 2000')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Tu mensaje'), 'hola')
    expect(screen.getByText('4 / 2000')).toBeInTheDocument()
    // Lo lee el lector de pantalla junto al campo, con la ayuda (y el aviso de privacidad).
    expect(screen.getByLabelText('Tu mensaje')).toHaveAccessibleDescription(/4 \/ 2000/)
    expect(screen.getByLabelText('Tu mensaje')).toHaveAccessibleDescription(
      /Entre 10 y 2000 caracteres\. No escribas contraseñas ni datos de salud\./,
    )
  })

  it('pasarse de 2000 lo dice con palabras («N de más»), no solo con el color, y la ayuda sigue a la vista', async () => {
    const { user } = renderApp('/ajustes/sugerencias', { como: 'estandar' })
    await screen.findByText('Todavía no has escrito nada')

    await user.click(screen.getByLabelText('Tu mensaje'))
    await user.paste('a'.repeat(2003))
    const contador = screen.getByText(/2003 \/ 2000/)
    expect(contador).toHaveTextContent('2003 / 2000 · 3 de más')
    expect(contador).toHaveClass('text-peligro')

    await user.click(screen.getByRole('button', { name: 'Enviar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('2000 caracteres como máximo')
    // El error no esconde la ayuda.
    expect(screen.getByText(/Entre 10 y 2000 caracteres\./)).toBeVisible()
    expect(db.sugerencias).toHaveLength(0)
  })

  it('con 5 envíos en las últimas 24 horas muestra el mensaje del servidor, sin culpa y sin enviar', async () => {
    for (let i = 0; i < 5; i++) {
      sembrar('estandar@demo.com', `Mensaje número ${i + 1} de prueba`, { haceMinutos: i + 1 })
    }
    const { user } = renderApp('/ajustes/sugerencias', { como: 'estandar' })
    await screen.findByText('Mensaje número 1 de prueba')

    await user.type(screen.getByLabelText('Tu mensaje'), 'Un sexto mensaje del día.')
    await user.click(screen.getByRole('button', { name: 'Enviar' }))
    expect(
      await screen.findByText(
        'Ya nos escribiste 5 veces hoy. Gracias: vuelve a escribirnos mañana.',
      ),
    ).toBeInTheDocument()
    expect(db.sugerencias).toHaveLength(5)
    // No se pierde lo que la persona escribió; «Enviar» queda deshabilitado y el aviso va justo encima del botón.
    expect(screen.getByLabelText('Tu mensaje')).toHaveValue('Un sexto mensaje del día.')
    const enviar = screen.getByRole('button', { name: 'Enviar' })
    expect(enviar).toBeDisabled()
    const aviso = screen.getByText(/Ya nos escribiste 5 veces hoy/)
    expect(aviso.compareDocumentPosition(enviar) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(
      screen.getByLabelText('Tu mensaje').compareDocumentPosition(aviso) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy()
    expect(document.body.textContent).not.toMatch(CULPA)
  })

  it('los mensajes de hace más de 24 horas no cuentan para el tope', async () => {
    for (let i = 0; i < 5; i++) {
      sembrar('estandar@demo.com', `Mensaje viejo número ${i + 1}`, {
        haceMinutos: 24 * 60 + 5 + i,
      })
    }
    const { user } = renderApp('/ajustes/sugerencias', { como: 'estandar' })
    await screen.findByText('Mensaje viejo número 1')

    await user.type(screen.getByLabelText('Tu mensaje'), 'Hoy sí puedo escribir otra vez.')
    await user.click(screen.getByRole('button', { name: 'Enviar' }))
    await waitFor(() => expect(db.sugerencias).toHaveLength(6))
    expect(await screen.findByText('Hoy sí puedo escribir otra vez.')).toBeInTheDocument()
  })

  it('lista solo lo propio, de lo más reciente a lo más antiguo, con los estados en español', async () => {
    sembrar('estandar@demo.com', 'Primero en llegar, ya resuelto.', {
      estado: 'Resuelta',
      haceMinutos: 30,
    })
    sembrar('estandar@demo.com', 'Segundo, ya leído por el equipo.', {
      estado: 'Leida',
      tipo: 'Queja',
      haceMinutos: 20,
    })
    sembrar('estandar@demo.com', 'Tercero, recién llegado a la bandeja.', { haceMinutos: 5 })
    sembrar('otro@demo.com', 'Mensaje de otra persona que no debe verse.')
    renderApp('/ajustes/sugerencias', { como: 'estandar' })

    const filas = within(
      await screen.findByRole('list', { name: /Lo que has enviado/ }),
    ).getAllByRole('listitem')
    expect(filas).toHaveLength(3)
    expect(within(filas[0]).getByText('Recibida')).toBeInTheDocument()
    expect(within(filas[0]).getByText('Propuesta de mejora')).toBeInTheDocument()
    expect(within(filas[1]).getByText('Leída')).toBeInTheDocument()
    expect(within(filas[1]).getByText('Queja')).toBeInTheDocument()
    expect(within(filas[2]).getByText('Resuelta')).toBeInTheDocument()
    expect(screen.queryByText(/otra persona/)).not.toBeInTheDocument()
    // Sin culpa, y una línea fija que dice qué pasa con lo enviado.
    const region = screen.getByRole('region', { name: 'Lo que has enviado' })
    expect(
      within(region).getByText(
        'No respondemos uno por uno, pero cada mensaje se lee y se usa para mejorar.',
      ),
    ).toBeInTheDocument()
    expect(region.textContent).not.toMatch(CULPA)
    // El formulario solo avisa de que no se escriban contraseñas ni datos de salud; no los pide.
    expect(screen.getByRole('form', { name: 'Enviar una sugerencia' }).textContent).not.toMatch(
      CULPA,
    )
  })

  it('un error al cargar se dice con un mensaje genérico y «Reintentar» recupera la lista', async () => {
    sembrar('estandar@demo.com', 'Un mensaje que ya había enviado.')
    let fallar = true
    server.use(
      http.get('/api/feedback', () => {
        if (fallar)
          return HttpResponse.json({ status: 500, title: 'Detalle interno' }, { status: 500 })
      }),
    )
    const { user } = renderApp('/ajustes/sugerencias', { como: 'estandar' })

    expect(await screen.findByText('No se pudieron cargar tus mensajes.')).toBeInTheDocument()
    expect(screen.queryByText(/Detalle interno/)).not.toBeInTheDocument()
    fallar = false
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByText('Un mensaje que ya había enviado.')).toBeInTheDocument()
  })

  it('el texto con HTML se muestra literal: no se interpreta ni se vuelve enlace', async () => {
    const peligroso = '<img src=x onerror=alert(1)> <b>negrita</b> https://ejemplo.com/x'
    sembrar('estandar@demo.com', peligroso)
    renderApp('/ajustes/sugerencias', { como: 'estandar' })

    const texto = await screen.findByText(peligroso)
    expect(texto).toHaveClass('whitespace-pre-wrap', 'break-words')
    expect(texto.children).toHaveLength(0)
    const principal = document.getElementById('contenido')!
    expect(principal.querySelector('img')).toBeNull()
    expect(principal.querySelector('b')).toBeNull()
    expect(within(principal).queryByRole('link', { name: /ejemplo/ })).toBeNull()
  })

  it('tiene un solo h1, etiquetas en los controles y está en Configuración', async () => {
    renderApp('/ajustes/sugerencias', { como: 'estandar' })
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Quejas y mejoras' }),
    ).toBeInTheDocument()
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
    expect(screen.queryByRole('heading', { name: 'Escríbenos' })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Tipo')).toBeInTheDocument()
    expect(screen.getByLabelText('Tu mensaje')).toBeInTheDocument()
    // El subtítulo es el de la ruta, sin repetirlo en la tarjeta.
    expect(screen.getAllByText(/Lo lee una persona/)).toHaveLength(1)
    const secciones = screen.getByRole('navigation', { name: 'Secciones de configuración' })
    expect(within(secciones).getByRole('link', { name: 'Quejas y mejoras' })).toHaveAttribute(
      'aria-current',
      'page',
    )
  })

  it('un Administrador no tiene la sección: la ruta le dice que no tiene acceso', async () => {
    renderApp('/ajustes/sugerencias', { como: 'admin' })
    expect(await screen.findByText('No tienes acceso a esta sección')).toBeInTheDocument()
    expect(screen.queryByLabelText('Tu mensaje')).not.toBeInTheDocument()
  })
})

describe('Quejas y mejoras del Administrador', () => {
  it('ve la bandeja con el tipo, la fecha, el estado, la persona y el texto, y el contador de nuevas', async () => {
    const ana = sembrar('estandar@demo.com', 'La lista de planes tarda en cargar.', {
      tipo: 'Queja',
      haceMinutos: 1,
    })
    sembrar('otro@demo.com', 'Me gustaría elegir la hora de cada recordatorio.', {
      estado: 'Leida',
      haceMinutos: 2,
    })
    renderApp('/admin/sugerencias', { como: 'admin' })

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Quejas y mejoras' }),
    ).toBeInTheDocument()
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
    const filas = within(
      await screen.findByRole('list', { name: /Quejas y mejoras, de la más reciente/ }),
    ).getAllByRole('listitem')
    expect(filas).toHaveLength(2)
    expect(within(filas[0]).getByText('Queja')).toBeInTheDocument()
    expect(within(filas[0]).getByText('Nueva')).toBeInTheDocument()
    const id = ana.usuarioId.slice(0, 8)
    expect(within(filas[0]).getByText(`ID ${id}`).tagName).toBe('CODE')
    expect(within(filas[0]).getByText(`Identificador de la persona ${id}`)).toBeInTheDocument()
    // La fila «Nueva» se distingue por la barra y el fondo de acento (como el ítem activo del menú) y el tipo en negrita.
    expect(filas[0]).toHaveClass('bg-acento-suave')
    expect(filas[0].className).toContain('shadow-[inset_3px_0_0_var(--c-acento)]')
    expect(within(filas[0]).getByText('Queja')).toHaveClass('font-bold')
    expect(filas[1]).not.toHaveClass('bg-acento-suave')
    expect(within(filas[1]).getByText('Propuesta de mejora')).not.toHaveClass('font-bold')
    expect(within(filas[0]).getByText('La lista de planes tarda en cargar.')).toBeInTheDocument()
    expect(filas[0].querySelector('time')).toHaveAttribute('datetime', ana.creadaEn)
    expect(within(filas[1]).getByText('Leída')).toBeInTheDocument()
    // Nada de nombres ni correos inventados.
    expect(
      within(document.getElementById('contenido')!).queryByText(/Ana|Luis|@demo\.com/),
    ).not.toBeInTheDocument()
    expect(screen.getByText('2 mensajes')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^Nuevas/ })).toHaveTextContent('Nuevas (1)')
    // La explicación del ID está en el encabezado y no hay nota de pie.
    expect(screen.getByText(/el ID sirve para agrupar los mensajes/)).toBeInTheDocument()
    expect(screen.queryByText(/Solo el equipo administrador ve esta bandeja/)).toBeNull()
  })

  it('cada fila ofrece solo lo que tiene efecto, según su estado', async () => {
    sembrar('estandar@demo.com', 'Una nueva, recién llegada.', { haceMinutos: 1 })
    sembrar('estandar@demo.com', 'Una leída por el equipo.', { estado: 'Leida', haceMinutos: 2 })
    sembrar('otro@demo.com', 'Una resuelta por completo.', { estado: 'Resuelta', haceMinutos: 3 })
    renderApp('/admin/sugerencias', { como: 'admin' })
    const botones = async (texto: string) =>
      within((await screen.findByText(texto)).closest('li')!)
        .getAllByRole('button')
        .map((b) => b.textContent)

    expect(await botones('Una nueva, recién llegada.')).toEqual([
      'Marcar como leída',
      'Marcar como resuelta',
    ])
    expect(await botones('Una leída por el equipo.')).toEqual(['Marcar como resuelta'])
    expect(await botones('Una resuelta por completo.')).toEqual(['Volver a leída'])
    // Nada de botones inertes.
    expect(
      within(screen.getByRole('list', { name: /Quejas y mejoras, de la más/ }))
        .getAllByRole('button')
        .some((b) => (b as HTMLButtonElement).disabled),
    ).toBe(false)
  })

  it('el filtro por estado cambia la lista y la deja sin tocar el contador de nuevas', async () => {
    sembrar('estandar@demo.com', 'Una nueva sin abrir todavía.', { haceMinutos: 1 })
    sembrar('estandar@demo.com', 'Una leída por el equipo.', { estado: 'Leida', haceMinutos: 2 })
    sembrar('otro@demo.com', 'Una ya resuelta por completo.', {
      estado: 'Resuelta',
      haceMinutos: 3,
    })
    const { user } = renderApp('/admin/sugerencias', { como: 'admin' })
    await screen.findByText('Una nueva sin abrir todavía.')

    await user.click(screen.getByRole('button', { name: /^Leídas/ }))
    expect(screen.getByRole('button', { name: /^Leídas/ })).toHaveAttribute('aria-pressed', 'true')
    expect(await screen.findByText('Una leída por el equipo.')).toBeInTheDocument()
    // El resumen dice el estado del filtro, no cuántas hay sin leer (eso va en «Nuevas (n)»).
    expect(await screen.findByText('1 mensaje leído')).toBeInTheDocument()
    await waitFor(() =>
      expect(screen.queryByText('Una nueva sin abrir todavía.')).not.toBeInTheDocument(),
    )
    expect(screen.queryByText('Una ya resuelta por completo.')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^Nuevas/ })).toHaveTextContent('Nuevas (1)')

    await user.click(screen.getByRole('button', { name: /^Resueltas/ }))
    expect(await screen.findByText('Una ya resuelta por completo.')).toBeInTheDocument()
    expect(await screen.findByText('1 mensaje resuelto')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /^Nuevas/ }))
    expect(await screen.findByText('1 mensaje nuevo')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /^Todas/ }))
    expect(await screen.findByText('3 mensajes')).toBeInTheDocument()
    expect(await screen.findByText('Una nueva sin abrir todavía.')).toBeInTheDocument()
    expect(screen.getByText('Una ya resuelta por completo.')).toBeInTheDocument()
  })

  it('un filtro sin mensajes lo dice y «Estás al día» cuando no queda nada nuevo', async () => {
    sembrar('estandar@demo.com', 'Un mensaje ya leído por el equipo.', { estado: 'Leida' })
    const { user } = renderApp('/admin/sugerencias', { como: 'admin' })
    await screen.findByText('Un mensaje ya leído por el equipo.')

    await user.click(screen.getByRole('button', { name: /^Nuevas/ }))
    expect(await screen.findByText('Estás al día')).toBeInTheDocument()
    expect(screen.getByText('No hay mensajes nuevos.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /^Resueltas/ }))
    expect(await screen.findByText('No hay mensajes resueltos')).toBeInTheDocument()
  })

  it('sin mensajes muestra el estado vacío', async () => {
    renderApp('/admin/sugerencias', { como: 'admin' })
    expect(await screen.findByText('Todavía no hay mensajes')).toBeInTheDocument()
  })

  it('«Marcar como leída» y «Marcar como resuelta» actualizan la fila, el contador y el menú', async () => {
    sembrar('estandar@demo.com', 'Un mensaje para marcar como leído.', { haceMinutos: 1 })
    const { user } = renderApp('/admin/sugerencias', { como: 'admin' })
    const nav = await screen.findByRole('navigation', { name: 'Principal' })
    const fila = (await screen.findByText('Un mensaje para marcar como leído.')).closest('li')!
    expect(
      await within(nav).findByRole('link', { name: /Quejas y mejoras.*1 nueva/ }),
    ).toBeInTheDocument()

    await user.click(within(fila).getByRole('button', { name: /^Marcar como leída/ }))
    await waitFor(() => expect(within(fila).getByText('Leída')).toBeInTheDocument())
    expect(db.sugerencias[0].estado).toBe('Leida')
    // El foco va a la fila (el botón pulsado desapareció) y el aviso es una región viva sin foco.
    await waitFor(() => expect(fila).toHaveFocus())
    const aviso = screen.getByText(/marcada como leída\./)
    expect(aviso).toHaveAttribute('role', 'status')
    expect(aviso).not.toHaveFocus()
    // Una leída solo ofrece resolverla, sin botones inertes. El contador y el globo del menú bajan a cero.
    expect(within(fila).queryByRole('button', { name: /^Marcar como leída/ })).toBeNull()
    expect(within(fila).getByRole('button', { name: /^Marcar como resuelta/ })).toBeEnabled()
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /^Nuevas/ })).toHaveTextContent('Nuevas (0)'),
    )
    expect(screen.getByText('1 mensaje')).toBeInTheDocument()
    expect(within(nav).queryByText(/\d+ nuevas?\)/)).not.toBeInTheDocument()

    await user.click(within(fila).getByRole('button', { name: /^Marcar como resuelta/ }))
    await waitFor(() => expect(within(fila).getByText('Resuelta')).toBeInTheDocument())
    expect(db.sugerencias[0].estado).toBe('Resuelta')
    await waitFor(() => expect(fila).toHaveFocus())
    // Una resuelta solo ofrece volver a «leída», pero nunca vuelve a «Nueva».
    expect(within(fila).queryByRole('button', { name: /^Marcar como resuelta/ })).toBeNull()
    expect(within(fila).getAllByRole('button')).toHaveLength(1)
    await user.click(within(fila).getByRole('button', { name: /^Volver a leída/ }))
    await waitFor(() => expect(within(fila).getByText('Leída')).toBeInTheDocument())
    expect(db.sugerencias[0].estado).toBe('Leida')
    expect(await screen.findByText(/: vuelta a leída\./)).toBeInTheDocument()
    expect(within(fila).queryByRole('button', { name: /^Volver a leída/ })).not.toBeInTheDocument()
  })

  it('el aviso nombra el mensaje por su tipo y su fecha, no por el ID de la persona', async () => {
    const s = sembrar('estandar@demo.com', 'Un mensaje para leer ahora.', { tipo: 'Queja' })
    const { user } = renderApp('/admin/sugerencias', { como: 'admin' })
    await user.click(await screen.findByRole('button', { name: /^Marcar como leída/ }))
    const aviso = await screen.findByText(/marcada como leída\./)
    expect(aviso).toHaveTextContent(/^«Queja del .+» marcada como leída\.$/)
    expect(aviso.textContent).not.toContain(s.usuarioId.slice(0, 8))
  })

  it('en el filtro «Nuevas», marcar una la saca de la lista y el foco pasa a la siguiente', async () => {
    sembrar('estandar@demo.com', 'Esta sale de la lista al leerla.', { haceMinutos: 1 })
    sembrar('otro@demo.com', 'Esta se queda en la lista.', { haceMinutos: 2 })
    const { user } = renderApp('/admin/sugerencias', { como: 'admin' })
    await screen.findByText('Esta sale de la lista al leerla.')
    await user.click(screen.getByRole('button', { name: /^Nuevas/ }))
    const primera = (await screen.findByText('Esta sale de la lista al leerla.')).closest('li')!
    await user.click(within(primera).getByRole('button', { name: /^Marcar como resuelta/ }))

    await waitFor(() =>
      expect(screen.queryByText('Esta sale de la lista al leerla.')).not.toBeInTheDocument(),
    )
    const siguiente = screen.getByText('Esta se queda en la lista.').closest('li')!
    await waitFor(() => expect(siguiente).toHaveFocus())
    expect(screen.getByText(/marcada como resuelta\./)).toHaveAttribute('role', 'status')
  })

  it('en el filtro «Nuevas», al marcar la última el foco va a la línea de estado de la lista', async () => {
    sembrar('estandar@demo.com', 'Esta sale de la lista al leerla.', { haceMinutos: 1 })
    const { user } = renderApp('/admin/sugerencias', { como: 'admin' })
    await screen.findByText('Esta sale de la lista al leerla.')
    await user.click(screen.getByRole('button', { name: /^Nuevas/ }))
    await user.click(await screen.findByRole('button', { name: /^Marcar como resuelta/ }))

    expect(await screen.findByText('Estás al día')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByText('0 mensajes nuevos')).toHaveFocus())
    expect(screen.getByText(/marcada como resuelta\./)).toBeInTheDocument()
  })

  it('una sugerencia que ya no existe (404) se dice arriba con el mensaje del servidor, aunque la fila desaparezca', async () => {
    sembrar('estandar@demo.com', 'Este mensaje desaparece antes de marcarlo.')
    const { user } = renderApp('/admin/sugerencias', { como: 'admin' })
    const fila = (await screen.findByText('Este mensaje desaparece antes de marcarlo.')).closest(
      'li',
    )!
    db.sugerencias = []

    await user.click(within(fila).getByRole('button', { name: /^Marcar como leída/ }))
    const alerta = await screen.findByRole('alert')
    expect(alerta).toHaveTextContent('La sugerencia no existe.')
    expect(alerta.parentElement).toHaveFocus()
    // La bandeja se vuelve a pedir: la fila ya no está y no queda un aviso de éxito.
    expect(await screen.findByText('Todavía no hay mensajes')).toBeInTheDocument()
    expect(screen.queryByText(/marcada como leída\./)).not.toBeInTheDocument()
    expect(screen.getByRole('alert')).toBeInTheDocument()
  })

  it('pagina de 20 en 20 con Anterior y Siguiente', async () => {
    for (let i = 0; i < 25; i++) {
      sembrar(
        'estandar@demo.com',
        `Mensaje de la bandeja número ${String(i + 1).padStart(2, '0')}`,
        {
          haceMinutos: i + 1,
        },
      )
    }
    const { user } = renderApp('/admin/sugerencias', { como: 'admin' })
    const lista = await screen.findByRole('list', { name: /Quejas y mejoras, de la más reciente/ })
    expect(within(lista).getAllByRole('listitem')).toHaveLength(20)
    expect(screen.getByText('Página 1 de 2')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled()

    await user.click(screen.getByRole('button', { name: 'Siguiente' }))
    expect(await screen.findByText('Página 2 de 2')).toBeInTheDocument()
    await waitFor(() =>
      expect(
        within(
          screen.getByRole('list', { name: /Quejas y mejoras, de la más reciente/ }),
        ).getAllByRole('listitem'),
      ).toHaveLength(5),
    )
    expect(screen.getByRole('button', { name: 'Siguiente' })).toBeDisabled()
    expect(screen.getByText('Mensaje de la bandeja número 25')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Anterior' }))
    expect(await screen.findByText('Página 1 de 2')).toBeInTheDocument()
  })

  it('el texto con HTML se muestra literal en la bandeja', async () => {
    const peligroso =
      '<script>alert(1)</script> <a href="http://malo.test">pulsa</a> http://malo.test'
    sembrar('estandar@demo.com', peligroso)
    renderApp('/admin/sugerencias', { como: 'admin' })

    const texto = await screen.findByText(peligroso)
    expect(texto).toHaveClass('whitespace-pre-wrap', 'break-words')
    expect(texto.children).toHaveLength(0)
    const principal = document.getElementById('contenido')!
    expect(principal.querySelector('script, a[href^="http"]')).toBeNull()
  })

  it('un error al cargar se dice con un mensaje genérico y «Reintentar» recupera la bandeja', async () => {
    sembrar('estandar@demo.com', 'Un mensaje que sí llega al reintentar.')
    let fallar = true
    server.use(
      http.get('/api/admin/feedback', ({ request }) => {
        // Solo la bandeja completa: el contador del menú (tamano=1) sigue su camino.
        if (fallar && new URL(request.url).searchParams.get('tamano') === '20') {
          return HttpResponse.json({ status: 500, title: 'Detalle interno' }, { status: 500 })
        }
      }),
    )
    const { user } = renderApp('/admin/sugerencias', { como: 'admin' })

    expect(await screen.findByText('No se pudieron cargar los mensajes.')).toBeInTheDocument()
    expect(screen.queryByText(/Detalle interno/)).not.toBeInTheDocument()
    fallar = false
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByText('Un mensaje que sí llega al reintentar.')).toBeInTheDocument()
  })

  it('un Estándar no ve la ruta: ni en el menú ni abriéndola', async () => {
    renderApp('/admin/sugerencias', { como: 'estandar' })
    expect(await screen.findByText('No tienes acceso a esta sección')).toBeInTheDocument()
    const nav = screen.getByRole('navigation', { name: 'Principal' })
    expect(within(nav).queryByRole('link', { name: /Quejas y mejoras/ })).toBeNull()
  })
})

describe('API simulada de quejas y mejoras (réplica del servidor)', () => {
  it('sin sesión es 401 y con el rol equivocado, 403', async () => {
    const ana = iniciarSesionComo('estandar')
    const admin = iniciarSesionComo('admin')
    const cuerpo = { tipo: 'Queja', texto: 'Un mensaje válido de prueba.' }

    expect((await llamar('/api/feedback')).status).toBe(401)
    expect((await llamar('/api/feedback', undefined, 'POST', cuerpo)).status).toBe(401)
    expect((await llamar('/api/admin/feedback')).status).toBe(401)
    expect((await llamar('/api/feedback', admin)).status).toBe(403)
    expect((await llamar('/api/feedback', admin, 'POST', cuerpo)).status).toBe(403)
    expect((await llamar('/api/admin/feedback', ana)).status).toBe(403)
    const propia = sembrar('estandar@demo.com', 'Un mensaje de Ana para probar.')
    expect(
      (await llamar(`/api/admin/feedback/${propia.id}/status`, ana, 'POST', { estado: 'Leida' }))
        .status,
    ).toBe(403)
  })

  it('valida el tipo y el texto: recorta antes de medir y 2000 es el tope', async () => {
    const ana = iniciarSesionComo('estandar')
    const enviar = (tipo: unknown, texto: unknown) =>
      llamar('/api/feedback', ana, 'POST', { tipo, texto })

    expect((await enviar('Queja', 'demasiado')).status).toBe(400)
    expect((await enviar('Queja', '   nueve 9   ')).status).toBe(400)
    expect((await enviar('Otra', 'Un texto suficientemente largo.')).status).toBe(400)
    expect((await enviar(undefined, 'Un texto suficientemente largo.')).status).toBe(400)
    expect((await enviar('Queja', 12345678901234)).status).toBe(400)
    expect((await enviar('Queja', 'a'.repeat(2001))).status).toBe(400)
    expect(db.sugerencias).toHaveLength(0)

    const diez = await enviar('Queja', '  1234567890  ')
    expect(diez.status).toBe(201)
    expect((await diez.json()).texto).toBe('1234567890')
    expect((await enviar('Mejora', 'a'.repeat(2000))).status).toBe(201)
  })

  it('al sexto envío del día responde 400 con el código limite_sugerencias', async () => {
    const ana = iniciarSesionComo('estandar')
    for (let i = 0; i < 5; i++) {
      const r = await llamar('/api/feedback', ana, 'POST', {
        tipo: 'Mejora',
        texto: `Envío número ${i + 1} del día`,
      })
      expect(r.status).toBe(201)
    }
    const sexto = await llamar('/api/feedback', ana, 'POST', {
      tipo: 'Mejora',
      texto: 'Un sexto envío del día',
    })
    expect(sexto.status).toBe(400)
    expect(await sexto.json()).toMatchObject({ codigo: 'limite_sugerencias' })
    // El tope es por persona: otra Estándar sigue pudiendo escribir.
    db.sugerencias.forEach((s) => (s.usuarioId = idDe('otro@demo.com')))
    const otra = await llamar('/api/feedback', ana, 'POST', {
      tipo: 'Mejora',
      texto: 'Ana vuelve a poder escribir',
    })
    expect(otra.status).toBe(201)
  })

  it('la bandeja filtra por estado, pagina y valida sus parámetros', async () => {
    const admin = iniciarSesionComo('admin')
    sembrar('estandar@demo.com', 'Una nueva de prueba aquí.', { haceMinutos: 1 })
    sembrar('estandar@demo.com', 'Una leída de prueba aquí.', { estado: 'Leida', haceMinutos: 2 })
    sembrar('otro@demo.com', 'Una resuelta de prueba aquí.', { estado: 'Resuelta', haceMinutos: 3 })

    const leidas = await (await llamar('/api/admin/feedback?estado=Leida', admin)).json()
    expect(leidas).toMatchObject({ total: 1, pagina: 1, tamano: 20, nuevas: 1 })
    expect(leidas.sugerencias).toHaveLength(1)
    const dosPorPagina = await (await llamar('/api/admin/feedback?tamano=2&pagina=2', admin)).json()
    expect(dosPorPagina).toMatchObject({ total: 3, pagina: 2, tamano: 2, nuevas: 1 })
    expect(dosPorPagina.sugerencias).toHaveLength(1)
    // Una página que no existe no es un error: viene vacía.
    expect(
      (await (await llamar('/api/admin/feedback?pagina=9', admin)).json()).sugerencias,
    ).toEqual([])

    for (const mala of ['pagina=0', 'tamano=0', 'tamano=101', 'pagina=abc', 'estado=Otra']) {
      expect((await llamar(`/api/admin/feedback?${mala}`, admin)).status, mala).toBe(400)
    }
    expect((await llamar('/api/admin/feedback?tamano=100', admin)).status).toBe(200)
  })

  it('el cambio de estado: 404 si no existe, nada vuelve a «Nueva» y repetir es inofensivo', async () => {
    const admin = iniciarSesionComo('admin')
    const s = sembrar('estandar@demo.com', 'Un mensaje para cambiar de estado.')
    const cambiar = (id: string, estado: unknown) =>
      llamar(`/api/admin/feedback/${id}/status`, admin, 'POST', { estado })

    const inexistente = await cambiar(crypto.randomUUID(), 'Leida')
    expect(inexistente.status).toBe(404)
    expect(await inexistente.json()).toMatchObject({ codigo: 'sugerencia_no_encontrada' })
    expect((await cambiar('no-es-un-guid', 'Leida')).status).toBe(404)
    expect((await cambiar(s.id, 'Otra')).status).toBe(400)
    expect((await cambiar(s.id, undefined)).status).toBe(400)

    // Se puede resolver sin pasar por leída; repetir el mismo estado no cambia nada.
    expect((await (await cambiar(s.id, 'Resuelta')).json()).estado).toBe('Resuelta')
    expect((await cambiar(s.id, 'Resuelta')).status).toBe(200)
    expect((await (await cambiar(s.id, 'Leida')).json()).estado).toBe('Leida')
    expect((await cambiar(s.id, 'Nueva')).status).toBe(400)
    expect(s.estado).toBe('Leida')
  })

  it('la descarga de datos incluye la sección sugerencias solo con lo propio', async () => {
    const ana = iniciarSesionComo('estandar')
    sembrar('estandar@demo.com', 'Lo que escribió Ana para exportar.')
    sembrar('otro@demo.com', 'Lo que escribió otra persona.')
    const datos = await (await llamar('/api/privacy/export', ana)).json()
    expect(datos.modulos.sugerencias).toHaveLength(1)
    expect(datos.modulos.sugerencias[0].texto).toBe('Lo que escribió Ana para exportar.')
    expect(Object.keys(datos.modulos.sugerencias[0]).sort()).toEqual([
      'creadaEn',
      'estado',
      'id',
      'texto',
      'tipo',
    ])
  })
})
