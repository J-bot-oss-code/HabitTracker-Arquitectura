import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { db } from '@/mocks/db'
import { problema } from '@/mocks/http'
import { server } from '@/mocks/server'
import { iniciarSesionComo, renderApp } from '@/test/render'
import { miActividadACsv, nombreCsv } from '../csv'
import { detalleParaPersona } from '../detalle'
import { miActividadAJson, nombreJson, type ExportacionActividad } from '../exportarActividad'
import type { EventoAuditoriaDto, PaginaAuditoriaDto } from '../types'

const ANA = 'estandar@demo.com'

const evento = (extra: Partial<EventoAuditoriaDto> = {}): EventoAuditoriaDto => ({
  id: crypto.randomUUID(),
  fecha: new Date().toISOString(),
  usuarioId: '0b8e7a52-1c3d-4f6e-8a9b-1c2d3e4f5a01',
  actor: ANA,
  accion: 'Cambio de contraseña',
  entidad: 'Usuario',
  entidadId: 'abc',
  valorAnterior: '-',
  valorNuevo: '-',
  ...extra,
})

// Agrega `n` registros propios de la persona con sesión (más recientes primero).
function sembrarPropios(correo: string, n: number) {
  const usuario = db.usuarios.find((u) => u.correo === correo)!
  for (let i = 0; i < n; i++) {
    db.auditoria.push(
      evento({
        id: `propio-${correo}-${i}`,
        usuarioId: usuario.id,
        actor: usuario.correo,
        accion: 'Subida de documento',
        entidadId: `doc-${i}`,
        valorNuevo: `archivo-${i}.pdf`,
        fecha: new Date(Date.now() - 60_000 - i * 1000).toISOString(),
      }),
    )
  }
}

const url = (ruta: string) => `${window.location.origin}${ruta}`
const llamar = (ruta: string, token?: string) =>
  fetch(url(ruta), { headers: token ? { Authorization: `Bearer ${token}` } : {} })

// Intercepta la descarga: devuelve lo guardado (Blob + nombre) sin tocar el navegador.
function capturarDescarga() {
  const descarga: { blob: Blob | null; nombre: string } = { blob: null, nombre: '' }
  URL.createObjectURL = (b: Blob | MediaSource) => {
    descarga.blob = b as Blob
    return 'blob:prueba'
  }
  URL.revokeObjectURL = () => undefined
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
    this: HTMLAnchorElement,
  ) {
    descarga.nombre = this.download
  })
  return descarga
}

afterEach(() => vi.restoreAllMocks())

describe('«Mi actividad»: archivos', () => {
  it('el CSV lleva encabezado, un registro por fila y neutraliza las fórmulas', () => {
    const csv = miActividadACsv([
      evento({
        fecha: '2026-11-03T10:00:00.000Z',
        accion: 'Subida de documento',
        valorAnterior: 'No existía',
        valorNuevo: '=HIPERVINCULO("x")',
      }),
      evento({ fecha: '2026-11-02T10:00:00.000Z', valorNuevo: '+1,2' }),
      // El servidor manda "-" para "sin valor": en el archivo sale vacío, no como «'-».
      evento({ fecha: '2026-11-01T10:00:00.000Z', valorAnterior: '-', valorNuevo: '-' }),
    ])
    const filas = csv.split('\r\n')
    expect(filas[0]).toBe('Fecha (UTC),Acción,Antes,Después')
    expect(filas).toHaveLength(4)
    expect(filas[1]).toBe(
      `2026-11-03T10:00:00.000Z,Subida de documento,No existía,"'=HIPERVINCULO(""x"")"`,
    )
    expect(filas[2]).toContain(`"'+1,2"`)
    expect(filas[3]).toBe('2026-11-01T10:00:00.000Z,Cambio de contraseña,,')
    expect(csv).not.toContain("'-")
    // Ni el correo ni ningún identificador interno van en el archivo.
    expect(csv).not.toContain('@')
    expect(csv).not.toContain('abc')
  })

  it('el JSON lleva formato, periodo, total y los mismos cuatro campos que el CSV; los nombres llevan periodo y día UTC', () => {
    const e = evento({ fecha: '2026-11-03T10:00:00.000Z', valorAnterior: '-', valorNuevo: 'x' })
    const j = miActividadAJson([e], 30, new Date('2026-11-03T23:59:59Z'))
    expect(j).toEqual({
      formato: 'habittracker-mi-actividad',
      version: 1,
      generadoEn: '2026-11-03T23:59:59.000Z',
      dias: 30,
      total: 1,
      registros: [
        {
          fecha: '2026-11-03T10:00:00.000Z',
          accion: 'Cambio de contraseña',
          antes: '',
          despues: 'x',
        },
      ],
    })
    const ahora = new Date('2026-11-03T23:59:59Z')
    expect(nombreCsv('mi-actividad-7-dias', ahora)).toBe('mi-actividad-7-dias-2026-11-03.csv')
    expect(nombreJson('mi-actividad-30-dias', ahora)).toBe('mi-actividad-30-dias-2026-11-03.json')
  })
})

describe('«Mi actividad»: cómo se lee el detalle', () => {
  const detalle = (valorAnterior: string, valorNuevo: string) =>
    detalleParaPersona(evento({ valorAnterior, valorNuevo }))

  it('con valor anterior y nuevo dice «Antes: X. Ahora: Y» y escribe Estándar con tilde', () => {
    expect(detalle('Administrador', 'Estandar')).toBe('Antes: Administrador. Ahora: Estándar')
  })

  it('sin valor anterior dice solo «Ahora: Y»; «No existía» y «-» no son un valor anterior', () => {
    expect(detalle('-', 'desactivar')).toBe('Ahora: desactivar')
    expect(detalle('', 'desactivar')).toBe('Ahora: desactivar')
    expect(detalle('No existía', 'informe.pdf')).toBe('Ahora: informe.pdf')
  })

  it('sin valor nuevo no hay detalle, y lo que «no se guarda» tampoco se muestra', () => {
    expect(detalle('-', '-')).toBeNull()
    expect(detalle('', '')).toBeNull()
    expect(detalle('-', 'La contraseña no se guarda')).toBeNull()
    expect(detalle('No se guarda el valor', 'otro')).toBeNull()
  })

  it('convierte los bytes a KB o MB y no deja flechas', () => {
    expect(detalle('No existía', 'analisis.pdf (20480 bytes)')).toBe('Ahora: analisis.pdf (20 KB)')
    expect(detalle('-', 'foto.png (500 bytes)')).toBe('Ahora: foto.png (menos de 1 KB)')
    expect(detalle('-', 'video.mp4 (1572864 bytes)')).toMatch(/^Ahora: video\.mp4 \(1[.,]5 MB\)$/)
    expect(detalle('Pendiente', 'Aplicada')).not.toContain('→')
  })
})

describe('GET /api/me/activity simulado', () => {
  it('exige sesión y solo acepta 7 o 30 días', async () => {
    expect((await llamar('/api/me/activity')).status).toBe(401)
    const ana = iniciarSesionComo('estandar')
    for (const consulta of ['dias=5', 'dias=0', 'dias=abc', 'pagina=0', 'tamano=0', 'tamano=201']) {
      expect((await llamar(`/api/me/activity?${consulta}`, ana)).status, consulta).toBe(400)
    }
    // Sin parámetros: 7 días, página 1, 50 por página.
    const r: PaginaAuditoriaDto = await (await llamar('/api/me/activity', ana)).json()
    expect(r).toMatchObject({ pagina: 1, tamano: 50 })
  })

  it('devuelve solo lo de la sesión, dentro de la ventana y de lo nuevo a lo viejo', async () => {
    const ana = iniciarSesionComo('estandar')
    const siete: PaginaAuditoriaDto = await (await llamar('/api/me/activity?dias=7', ana)).json()
    expect(siete.registros.map((e) => e.accion)).toEqual(['Descarga de datos personales'])
    const treinta: PaginaAuditoriaDto = await (await llamar('/api/me/activity?dias=30', ana)).json()
    expect(treinta.total).toBe(2)
    expect(treinta.registros.every((e) => e.actor === ANA)).toBe(true)
    expect(treinta.registros.map((e) => e.accion)).toEqual([
      'Descarga de datos personales',
      'Subida de documento',
    ])
  })
})

describe('«Mi actividad»: pantalla', () => {
  it('el Estándar ve el título, la introducción y solo sus acciones de los últimos 7 días', async () => {
    renderApp('/ajustes/actividad', { como: 'estandar' })
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Mi actividad' }),
    ).toBeInTheDocument()
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
    expect(
      screen.getByText('Lo que hiciste en tu cuenta. Puedes guardarlo en tu equipo.'),
    ).toBeInTheDocument()
    const lista = await screen.findByRole('list', { name: /Tu actividad/ })
    const filas = within(lista).getAllByRole('listitem')
    expect(filas).toHaveLength(1)
    expect(within(lista).getByText('Descarga de datos personales')).toBeInTheDocument()
    // Nada de otras personas ni acciones fuera de la ventana.
    expect(screen.queryByText('Cambio de rol')).not.toBeInTheDocument()
    expect(screen.queryByText('Subida de documento')).not.toBeInTheDocument()
    expect(screen.getByText(/1 acción en los últimos 7 días/)).toBeInTheDocument()
    expect(screen.getByText(/Solo tú ves esta lista/)).toBeInTheDocument()
    expect(screen.getByText(/en los archivos van en UTC/)).toBeInTheDocument()
    expect(screen.getByText(/equipo administrador puede consultar/)).toBeInTheDocument()
    // Un único contenedor de estado, siempre montado, con el recuento.
    expect(screen.getByRole('status')).toHaveTextContent('1 acción en los últimos 7 días.')
    // Lista apilada, sin tabla: nunca desborda en 360 px.
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(within(lista).getByText(/\d{2}:\d{2}/).tagName).toBe('TIME')
  })

  it('el Administrador también la ve, con sus propias acciones', async () => {
    renderApp('/ajustes/actividad', { como: 'admin' })
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Mi actividad' }),
    ).toBeInTheDocument()
    const lista = await screen.findByRole('list', { name: /Tu actividad/ })
    expect(within(lista).getByText('Cambio de rol')).toBeInTheDocument()
    expect(within(lista).getByText('Antes: Administrador. Ahora: Estándar')).toBeInTheDocument()
    expect(within(lista).queryByText('Descarga de datos personales')).not.toBeInTheDocument()
  })

  it('está en el menú de Configuración y se alcanza desde ahí', async () => {
    const { user, router } = renderApp('/ajustes/cuenta', { como: 'estandar' })
    await screen.findByRole('heading', { level: 1, name: 'Cuenta' })
    const menu = screen.getByRole('navigation', { name: 'Secciones de configuración' })
    await user.click(within(menu).getByRole('link', { name: 'Mi actividad' }))
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Mi actividad' }),
    ).toBeInTheDocument()
    await waitFor(() => expect(router.state.location.pathname).toBe('/ajustes/actividad'))
    expect(await screen.findByRole('list', { name: /Tu actividad/ })).toBeInTheDocument()
  })

  it('al pasar de 7 a 30 días vuelve a pedir con dias=30 y muestra el periodo completo', async () => {
    const consultas: URLSearchParams[] = []
    server.events.on('request:start', ({ request }) => {
      const u = new URL(request.url)
      if (u.pathname === '/api/me/activity') consultas.push(u.searchParams)
    })
    const { user } = renderApp('/ajustes/actividad', { como: 'estandar' })
    const siete = await screen.findByRole('button', { name: 'Últimos 7 días' })
    expect(siete).toHaveAttribute('aria-pressed', 'true')
    await screen.findByRole('list', { name: /Tu actividad/ })
    expect(consultas.at(-1)?.get('dias')).toBe('7')

    await user.click(screen.getByRole('button', { name: 'Últimos 30 días' }))
    expect(screen.getByRole('button', { name: 'Últimos 30 días' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
    expect(await screen.findByText(/2 acciones en los últimos 30 días/)).toBeInTheDocument()
    expect(screen.getByText('Subida de documento')).toBeInTheDocument()
    expect(screen.getByText('Ahora: analisis.pdf (20 KB)')).toBeInTheDocument()
    expect(consultas.some((c) => c.get('dias') === '30' && c.get('pagina') === '1')).toBe(true)
    server.events.removeAllListeners()
  })

  it('sin actividad en el periodo lo dice y no deja descargar', async () => {
    db.auditoria.length = 0
    const { user } = renderApp('/ajustes/actividad', { como: 'estandar' })
    expect(await screen.findByText('No hay actividad en este periodo.')).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Descargar para hoja de cálculo (CSV)' }),
    ).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Descargar copia completa (JSON)' })).toBeDisabled()
    // La salida natural desde 7 días es mirar los 30.
    await user.click(screen.getByRole('button', { name: 'Ver los últimos 30 días' }))
    expect(screen.getByRole('button', { name: 'Últimos 30 días' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
  })

  it('un error ofrece reintentar y luego muestra la lista, sin textos internos', async () => {
    let falla = true
    server.use(
      http.get('/api/me/activity', () =>
        falla
          ? problema(500, 'Error interno.', 'System.NullReferenceException en Servicio.cs:42')
          : HttpResponse.json({ total: 1, pagina: 1, tamano: 50, registros: [evento()] }),
      ),
    )
    const { user } = renderApp('/ajustes/actividad', { como: 'estandar' })
    expect(await screen.findByText('No se pudo cargar tu actividad.')).toBeInTheDocument()
    expect(screen.queryByText(/NullReference|Servicio\.cs/)).not.toBeInTheDocument()
    falla = false
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByText('Cambio de contraseña')).toBeInTheDocument()
  })

  it('pagina de 50 en 50 y avisa en qué página va', async () => {
    sembrarPropios(ANA, 120)
    const paginas: string[] = []
    server.events.on('request:start', ({ request }) => {
      const u = new URL(request.url)
      if (u.pathname === '/api/me/activity') paginas.push(`${u.searchParams.get('pagina')}`)
    })
    const { user } = renderApp('/ajustes/actividad', { como: 'estandar' })
    expect(await screen.findByText('Página 1 de 3')).toBeInTheDocument()
    expect(
      within(await screen.findByRole('list', { name: /Tu actividad/ })).getAllByRole('listitem'),
    ).toHaveLength(50)
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Siguiente' }))
    expect(await screen.findByText('Página 2 de 3')).toBeInTheDocument()
    expect(paginas).toContain('2')
    await user.click(screen.getByRole('button', { name: 'Siguiente' }))
    expect(await screen.findByText('Página 3 de 3')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Siguiente' })).toBeDisabled()
    server.events.removeAllListeners()
  })

  it('con pocos registros no muestra paginación', async () => {
    renderApp('/ajustes/actividad', { como: 'estandar' })
    await screen.findByRole('list', { name: /Tu actividad/ })
    expect(screen.queryByRole('navigation', { name: 'Páginas de tu actividad' })).toBeNull()
  })

  it('los datos del evento se muestran como texto, nunca como HTML', async () => {
    server.use(
      http.get('/api/me/activity', () =>
        HttpResponse.json({
          total: 1,
          pagina: 1,
          tamano: 50,
          registros: [
            evento({ accion: '<b>Hecho</b>', valorNuevo: '<img src=x onerror=alert(1)>' }),
          ],
        }),
      ),
    )
    renderApp('/ajustes/actividad', { como: 'estandar' })
    const lista = await screen.findByRole('list', { name: /Tu actividad/ })
    expect(within(lista).getByText('<b>Hecho</b>')).toBeInTheDocument()
    expect(lista.querySelector('img, b')).toBeNull()
  })
})

describe('«Mi actividad»: guardar', () => {
  it('«Descargar para hoja de cálculo (CSV)» baja TODO el periodo (varias páginas de 200), con BOM, sin repetidos y sin fórmulas activas', async () => {
    sembrarPropios(ANA, 250)
    db.auditoria.push(
      evento({
        id: 'formula',
        usuarioId: db.usuarios.find((u) => u.correo === ANA)!.id,
        valorNuevo: '=1+1',
        fecha: new Date(Date.now() - 1000).toISOString(),
      }),
    )
    const pedidas: string[] = []
    server.events.on('request:start', ({ request }) => {
      const u = new URL(request.url)
      if (u.pathname === '/api/me/activity')
        pedidas.push(
          `${u.searchParams.get('dias')}/${u.searchParams.get('pagina')}/${u.searchParams.get('tamano')}`,
        )
    })
    const descarga = capturarDescarga()
    const { user } = renderApp('/ajustes/actividad', { como: 'estandar' })
    await screen.findByRole('list', { name: /Tu actividad/ })
    await user.click(screen.getByRole('button', { name: 'Descargar para hoja de cálculo (CSV)' }))
    await waitFor(() => expect(descarga.blob).not.toBeNull())

    expect(descarga.nombre).toMatch(/^mi-actividad-7-dias-\d{4}-\d{2}-\d{2}\.csv$/)
    expect(pedidas).toContain('7/1/200')
    expect(pedidas).toContain('7/2/200')
    const bytes = new Uint8Array(await descarga.blob!.arrayBuffer())
    expect([...bytes.slice(0, 3)]).toEqual([0xef, 0xbb, 0xbf])
    const filas = (await descarga.blob!.text()).split('\r\n')
    // Encabezado + 250 sembrados + la fórmula + la descarga de datos que ya traía el ejemplo.
    expect(filas).toHaveLength(1 + 250 + 1 + 1)
    expect(filas[0]).toBe('Fecha (UTC),Acción,Antes,Después')
    expect(new Set(filas).size).toBe(filas.length)
    expect(filas.join('\n')).toContain("'=1+1")
    expect(filas.join('\n')).not.toMatch(/(^|,)=1\+1/m)
    expect(await screen.findByText('Se descargó tu archivo.')).toBeInTheDocument()
    server.events.removeAllListeners()
  })

  it('«Descargar copia completa (JSON)» baja el periodo elegido con su formato y los registros', async () => {
    const descarga = capturarDescarga()
    const { user } = renderApp('/ajustes/actividad', { como: 'estandar' })
    await screen.findByRole('list', { name: /Tu actividad/ })
    await user.click(screen.getByRole('button', { name: 'Últimos 30 días' }))
    await screen.findByText(/2 acciones en los últimos 30 días/)
    await user.click(screen.getByRole('button', { name: 'Descargar copia completa (JSON)' }))
    await waitFor(() => expect(descarga.blob).not.toBeNull())

    expect(descarga.nombre).toMatch(/^mi-actividad-30-dias-\d{4}-\d{2}-\d{2}\.json$/)
    const contenido = JSON.parse(await descarga.blob!.text()) as ExportacionActividad
    expect(contenido).toMatchObject({
      formato: 'habittracker-mi-actividad',
      version: 1,
      dias: 30,
      total: 2,
    })
    expect(contenido.registros).toHaveLength(2)
    for (const r of contenido.registros) {
      expect(Object.keys(r).sort()).toEqual(['accion', 'antes', 'despues', 'fecha'])
    }
    expect(JSON.stringify(contenido)).not.toContain('@')
    // Sin valor anterior sale vacío, no «-».
    expect(contenido.registros.some((r) => r.antes === '-' || r.despues === '-')).toBe(false)
  })

  it('si falla al preparar el archivo lo dice con un mensaje genérico y no descarga nada', async () => {
    const descarga = capturarDescarga()
    const { user } = renderApp('/ajustes/actividad', { como: 'estandar' })
    await screen.findByRole('list', { name: /Tu actividad/ })
    server.use(
      http.get('/api/me/activity', () =>
        problema(500, 'Error interno.', 'System.InvalidOperationException'),
      ),
    )
    await user.click(screen.getByRole('button', { name: 'Descargar para hoja de cálculo (CSV)' }))
    expect(
      await screen.findByText('No se pudo preparar el archivo. Inténtalo de nuevo.'),
    ).toBeInTheDocument()
    expect(descarga.blob).toBeNull()
    expect(screen.queryByText(/InvalidOperation/)).not.toBeInTheDocument()
  })

  it('los botones y los periodos se alcanzan con el teclado', async () => {
    const { user } = renderApp('/ajustes/actividad', { como: 'estandar' })
    await screen.findByRole('list', { name: /Tu actividad/ })
    const treinta = screen.getByRole('button', { name: 'Últimos 30 días' })
    treinta.focus()
    expect(treinta).toHaveFocus()
    await user.keyboard('{Enter}')
    expect(treinta).toHaveAttribute('aria-pressed', 'true')
    await screen.findByText(/2 acciones en los últimos 30 días/)
    await user.tab()
    expect(
      screen.getByRole('button', { name: 'Descargar para hoja de cálculo (CSV)' }),
    ).toHaveFocus()
  })
})

describe('«Mi actividad»: paginación y avisos', () => {
  it('al cambiar de página el foco pasa a la región de estado y la página no se anuncia aparte', async () => {
    sembrarPropios(ANA, 120)
    const { user } = renderApp('/ajustes/actividad', { como: 'estandar' })
    await screen.findByText('Página 1 de 3')
    expect(screen.getByText('Página 1 de 3')).not.toHaveAttribute('aria-live')
    await user.click(screen.getByRole('button', { name: 'Siguiente' }))
    await screen.findByText('Página 2 de 3')
    expect(screen.getByRole('status')).toHaveFocus()
    expect(screen.getAllByRole('status')).toHaveLength(1)
  })

  it('si la ventana se encoge y la página ya no existe, vuelve a la última', async () => {
    let encogida = false
    const registros = (n: number) =>
      Array.from({ length: n }, (_, i) => evento({ id: `r-${i}`, accion: `Acción ${i}` }))
    server.use(
      http.get('/api/me/activity', ({ request }) => {
        const pagina = Number(new URL(request.url).searchParams.get('pagina'))
        if (pagina === 2) encogida = true
        return encogida
          ? HttpResponse.json({
              total: 10,
              pagina,
              tamano: 50,
              registros: pagina === 1 ? registros(10) : [],
            })
          : HttpResponse.json({ total: 120, pagina, tamano: 50, registros: registros(50) })
      }),
    )
    const { user } = renderApp('/ajustes/actividad', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Siguiente' }))
    expect(await screen.findByText(/10 acciones en los últimos 7 días/)).toBeInTheDocument()
    expect(await screen.findByText('Acción 9')).toBeInTheDocument()
    expect(screen.queryByRole('navigation', { name: 'Páginas de tu actividad' })).toBeNull()
    expect(screen.queryByText('No hay actividad en este periodo.')).toBeNull()
  })

  it('el aviso de error al guardar desaparece al cambiar de periodo', async () => {
    capturarDescarga()
    const { user } = renderApp('/ajustes/actividad', { como: 'estandar' })
    await screen.findByRole('list', { name: /Tu actividad/ })
    server.use(http.get('/api/me/activity', () => problema(500, 'Error interno.')))
    await user.click(screen.getByRole('button', { name: 'Descargar para hoja de cálculo (CSV)' }))
    expect(await screen.findByText(/Inténtalo de nuevo/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Últimos 30 días' }))
    await waitFor(() => expect(screen.queryByText(/Inténtalo de nuevo/)).toBeNull())
  })
})
