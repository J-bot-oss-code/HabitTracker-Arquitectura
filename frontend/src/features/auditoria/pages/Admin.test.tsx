import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { db, sembrarPlanDemo } from '@/mocks/db'
import { auditoriaHandlers } from '@/mocks/handlers/auditoria'
import { adminHandlers } from '@/mocks/handlers/propuestas/admin'
import { problema } from '@/mocks/http'
import { server } from '@/mocks/server'
import { iniciarSesionComo, renderApp } from '@/test/render'
import { auditoriaACsv, celdaCsv, nombreCsv } from '../csv'
import { metricasACsv, type MetricasDto } from '../metricas'
import type { EventoAuditoriaDto, PaginaAuditoriaDto } from '../types'

// Un evento completo (el servidor nunca manda null) con lo que cada prueba quiera cambiar.
const evento = (extra: Partial<EventoAuditoriaDto> = {}): EventoAuditoriaDto => ({
  id: crypto.randomUUID(),
  fecha: new Date().toISOString(),
  usuarioId: '0b8e7a52-1c3d-4f6e-8a9b-1c2d3e4f5a02',
  actor: 'admin@demo.com',
  accion: 'Cambio de rol',
  entidad: 'Usuario',
  entidadId: 'abc',
  valorAnterior: 'Estandar',
  valorNuevo: 'Administrador',
  ...extra,
})

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

const pagina = async (ruta: string, token: string): Promise<PaginaAuditoriaDto> =>
  (await llamar(ruta, token)).json()

afterEach(() => vi.restoreAllMocks())

describe('CSV seguro', () => {
  it('neutraliza las fórmulas que una hoja de cálculo ejecutaría', () => {
    expect(celdaCsv('=HIPERVINCULO("http://x")')).toBe(`"'=HIPERVINCULO(""http://x"")"`)
    expect(celdaCsv('+1')).toBe("'+1")
    expect(celdaCsv('-2')).toBe("'-2")
    expect(celdaCsv('@cmd')).toBe("'@cmd")
    // Los números y los textos normales no se tocan.
    expect(celdaCsv(-3)).toBe('-3')
    expect(celdaCsv('Cambio de rol')).toBe('Cambio de rol')
  })

  it('escapa comas, comillas y saltos de línea, y deja vacío lo nulo', () => {
    expect(celdaCsv('a,b')).toBe('"a,b"')
    expect(celdaCsv('dice "hola"')).toBe('"dice ""hola"""')
    expect(celdaCsv('una\nlínea')).toBe('"una\nlínea"')
    expect(celdaCsv(null)).toBe('')
  })

  it('la auditoría sale con encabezado, acción legible y un evento por fila', () => {
    const e = evento({
      fecha: '2026-11-03T10:00:00.000Z',
      valorAnterior: 'Administrador',
      valorNuevo: '=Estandar',
    })
    const filas = auditoriaACsv([e]).split('\r\n')
    expect(filas[0]).toBe(
      'Fecha (UTC),Usuario,Acción,Entidad,Id de la entidad,Valor anterior,Valor nuevo',
    )
    expect(filas[1]).toBe(
      "2026-11-03T10:00:00.000Z,admin@demo.com,Cambio de rol,Usuario,abc,Administrador,'=Estandar",
    )
  })

  it('el nombre lleva el día UTC y las métricas se exportan sin datos personales', () => {
    expect(nombreCsv('auditoria', new Date('2026-11-03T23:59:59Z'))).toBe(
      'auditoria-2026-11-03.csv',
    )
    const m: MetricasDto = {
      generadoEn: '2026-11-03T10:00:00.000Z',
      usuariosActivos30d: 2,
      registros7d: 1,
      retencion30d: null,
      eliminacionesEnCurso: 0,
      planesMasIniciados: [{ slug: 'x', cantidad: 3 }],
    }
    const csv = metricasACsv(m, (s) => `Plan ${s}`)
    expect(csv).toContain('Usuarios activos (30 días),2')
    expect(csv).toContain('Retención a 30 días (%),')
    expect(csv).toContain('Plan iniciado: Plan x,3')
    expect(csv).not.toContain('@')
  })
})

const CLAVE_NUEVA_CUENTA = 'Clave-Segura-2026'

describe('contrato del panel de administración (auditoría real, métricas propuestas)', () => {
  it('métricas y auditoría son solo del Administrador', async () => {
    for (const ruta of ['/api/admin/metrics', '/api/audit']) {
      expect((await llamar(ruta)).status).toBe(401)
      expect((await llamar(ruta, iniciarSesionComo('estandar'))).status).toBe(403)
      expect((await llamar(ruta, iniciarSesionComo('admin'))).status).toBe(200)
    }
  })

  it('las métricas son agregadas y anónimas: ningún correo ni id de persona', async () => {
    sembrarPlanDemo()
    const r = await llamar('/api/admin/metrics', iniciarSesionComo('admin'))
    const m: MetricasDto = await r.json()
    expect(m).toMatchObject({
      usuariosActivos30d: 2,
      registros7d: 0,
      retencion30d: 50,
      eliminacionesEnCurso: 0,
      planesMasIniciados: [{ slug: 'mi-dia-oficina', cantidad: 1 }],
    })
    expect(JSON.stringify(m)).not.toMatch(/@|demo|-0000|[0-9a-f]{8}-[0-9a-f]{4}/)
  })

  it('la auditoría solo crece con las acciones reales; registro e inicio de sesión no se auditan', async () => {
    const antes = db.auditoria.length
    const actividadAntes = db.actividad.length
    await llamar('/api/auth/register', undefined, 'POST', {
      nombre: 'Persona Nueva',
      correo: 'nueva@demo.com',
      password: CLAVE_NUEVA_CUENTA,
      consentimientos: { terminosVersion: 'v2.1', privacidadVersion: 'v1.4', marketing: false },
    })
    const admin = iniciarSesionComo('admin')
    await llamar(`/api/admin/usuarios/${db.usuarios[2].id}/rol`, admin, 'PATCH', {
      nuevoRol: 'Administrador',
    })
    await llamar(`/api/admin/usuarios/${db.usuarios[2].id}/estado`, admin, 'PATCH', {
      nuevoEstado: false,
    })
    await llamar(`/api/permissions/${db.solicitudes[0].id}/reject`, admin, 'POST', {
      motivoRechazo: 'No procede.',
    })
    const nuevos = db.auditoria.slice(antes)
    expect(nuevos.map((e) => e.accion)).toEqual([
      'Cambio de rol',
      'Cambio de estado',
      'Rechazo de solicitud de permiso',
    ])
    expect(nuevos[0]).toMatchObject({
      actor: 'admin@demo.com',
      usuarioId: db.usuarios[1].id,
      entidad: 'Usuario',
      entidadId: db.usuarios[2].id,
      valorAnterior: 'Estandar',
      valorNuevo: 'Administrador',
    })
    expect(nuevos[2]).toMatchObject({ entidad: 'SolicitudPermiso', valorAnterior: 'Pendiente' })
    // Todos los campos van llenos: el servidor nunca manda null ni el campo "origen".
    for (const e of nuevos) {
      expect(Object.values(e).every((v) => typeof v === 'string' && v !== '')).toBe(true)
      expect(e).not.toHaveProperty('origen')
    }
    // El registro de la cuenta solo cuenta para las métricas propuestas.
    expect(db.actividad.length).toBe(actividadAntes + 1)
  })

  it('no existe ninguna ruta para modificar o borrar eventos (RF-AUD-06)', () => {
    expect(auditoriaHandlers.map((h) => String(h.info.method))).toEqual(['GET'])
    expect(adminHandlers.map((h) => String(h.info.method))).toEqual(['GET'])
  })

  it('un inicio de sesión no se audita (el backend real no lo hace), solo cuenta para las métricas', async () => {
    const antes = db.auditoria.length
    const actividadAntes = db.actividad.length
    await llamar('/api/auth/login', undefined, 'POST', {
      email: 'estandar@demo.com',
      password: 'Demo1234',
    })
    expect(db.auditoria).toHaveLength(antes)
    expect(db.actividad.slice(actividadAntes)).toMatchObject([
      { actor: 'estandar@demo.com', tipo: 'InicioSesion' },
    ])
  })

  it.each([
    ['tamano=0'],
    ['tamano=201'],
    ['tamano=abc'],
    ['pagina=0'],
    ['desde=ayer'],
    ['desde=2026-11-10&hasta=2026-11-01'],
    ['desde=2025-01-01&hasta=2026-01-02'],
    [`usuario=${'a'.repeat(255)}`],
    [`entidad=${'a'.repeat(101)}`],
    [`entidadId=${'a'.repeat(101)}`],
    [`accion=${'a'.repeat(101)}`],
  ])('rechaza el filtro %s', async (consulta) => {
    const r = await llamar(`/api/audit?${consulta}`, iniciarSesionComo('admin'))
    expect(r.status).toBe(400)
  })

  it('acepta el tamaño máximo, un rango de 366 días y una acción desconocida (texto libre)', async () => {
    const admin = iniciarSesionComo('admin')
    expect((await llamar('/api/audit?tamano=200', admin)).status).toBe(200)
    expect((await llamar('/api/audit?desde=2025-01-01&hasta=2026-01-01', admin)).status).toBe(200)
    const r = await pagina('/api/audit?accion=Hackeo', admin)
    expect(r).toMatchObject({ total: 0, registros: [] })
  })

  it('devuelve la página real y filtra por usuario, acción, entidad, entidadId y rango, de nuevo a viejo', async () => {
    const admin = iniciarSesionComo('admin')
    const todos = await pagina('/api/audit', admin)
    expect(todos).toMatchObject({ total: db.auditoria.length, pagina: 1, tamano: 50 })
    expect(todos.registros).toHaveLength(db.auditoria.length)
    const fechas = todos.registros.map((e) => Date.parse(e.fecha))
    expect(fechas).toEqual([...fechas].sort((a, b) => b - a))
    expect(todos.registros.every((e) => !('origen' in e))).toBe(true)

    const porUsuario = await pagina('/api/audit?usuario=ADMIN', admin)
    expect(porUsuario.registros.length).toBeGreaterThan(0)
    expect(porUsuario.registros.every((e) => e.actor === 'admin@demo.com')).toBe(true)
    // El correo CONTIENE el texto.
    expect((await pagina('/api/audit?usuario=@demo', admin)).total).toBe(todos.total)

    const porAccion = await pagina('/api/audit?accion=Cambio de rol', admin)
    expect(porAccion.registros.map((e) => e.accion)).toEqual(['Cambio de rol'])
    // La acción es exacta, no por parte del texto.
    expect((await pagina('/api/audit?accion=Cambio', admin)).total).toBe(0)

    const porEntidad = await pagina('/api/audit?entidad=Documento', admin)
    expect(porEntidad.registros.map((e) => e.accion)).toEqual(['Subida de documento'])
    const porEntidadId = await pagina(
      `/api/audit?entidadId=${porEntidad.registros[0].entidadId}`,
      admin,
    )
    expect(porEntidadId.total).toBe(1)

    const dia = todos.registros[0].fecha.slice(0, 10)
    const delDia = await pagina(`/api/audit?desde=${dia}&hasta=${dia}`, admin)
    expect(delDia.registros.length).toBeGreaterThan(0)
    expect(delDia.registros.every((e) => e.fecha.startsWith(dia))).toBe(true)
  })

  it('pagina: total cuenta todo lo que cumple los filtros y no solo la página', async () => {
    const admin = iniciarSesionComo('admin')
    const total = db.auditoria.length
    const primera = await pagina('/api/audit?tamano=2', admin)
    expect(primera).toMatchObject({ total, pagina: 1, tamano: 2 })
    expect(primera.registros).toHaveLength(2)
    const segunda = await pagina('/api/audit?tamano=2&pagina=2', admin)
    expect(segunda.pagina).toBe(2)
    expect(segunda.registros.map((e) => e.id)).not.toContain(primera.registros[0].id)
    // Más allá de la última página: lista vacía, el total se conserva.
    expect(await pagina('/api/audit?tamano=2&pagina=99', admin)).toMatchObject({
      total,
      registros: [],
    })
  })
})

describe('Resumen del Administrador', () => {
  it('muestra los indicadores, los planes y la auditoría reciente', async () => {
    sembrarPlanDemo()
    renderApp('/admin', { como: 'admin' })
    expect(await screen.findByRole('heading', { level: 1, name: 'Resumen' })).toBeInTheDocument()
    expect(screen.getByText('Métricas agregadas y anónimas.')).toBeInTheDocument()

    const kpis = await screen.findByRole('region', { name: 'Indicadores' })
    expect(within(kpis).getByText('Usuarios activos')).toBeInTheDocument()
    expect(within(kpis).getByText('50 %')).toBeInTheDocument()
    // Sin identificadores internos ni avisos de proceso en la interfaz.
    expect(within(kpis).queryByText(/Google|RF-/)).not.toBeInTheDocument()

    const planes = await screen.findByRole('region', { name: 'Planes más iniciados' })
    expect(within(planes).getByText('Mi día de oficina saludable')).toBeInTheDocument()
    expect(within(planes).getByText('1 inicio')).toBeInTheDocument()

    const tabla = await screen.findByRole('table', { name: /Eventos de auditoría/ })
    expect(within(tabla).getAllByRole('row').length).toBeGreaterThan(1)
    expect(within(tabla).getAllByRole('row').length).toBeLessThanOrEqual(9)
    expect(screen.getByRole('link', { name: /Ver toda la auditoría/ })).toHaveAttribute(
      'href',
      '/admin/auditoria',
    )
    expect(screen.getByRole('heading', { name: 'Cola de correos' })).toBeInTheDocument()
    expect(screen.queryByText('Próximamente')).not.toBeInTheDocument()
  })

  it('el menú abre con Resumen marcado solo en /admin y ningún ítem dice "Pronto"', async () => {
    renderApp('/admin', { como: 'admin' })
    const nav = await screen.findByRole('navigation', { name: 'Principal' })
    expect(within(nav).getByRole('link', { name: 'Resumen' })).toHaveAttribute(
      'aria-current',
      'page',
    )
    const auditoria = within(nav).getByRole('link', { name: 'Auditoría' })
    expect(auditoria).not.toHaveAttribute('aria-current')
    expect(within(nav).queryByText('Pronto')).not.toBeInTheDocument()
  })

  it('en /admin/usuarios, Resumen no queda marcado', async () => {
    renderApp('/admin/usuarios', { como: 'admin' })
    const nav = await screen.findByRole('navigation', { name: 'Principal' })
    expect(within(nav).getByRole('link', { name: 'Resumen' })).not.toHaveAttribute('aria-current')
    expect(within(nav).getByRole('link', { name: 'Usuarios' })).toHaveAttribute(
      'aria-current',
      'page',
    )
  })

  it('el Estándar no accede', async () => {
    renderApp('/admin', { como: 'estandar' })
    expect(await screen.findByText('No tienes acceso a esta sección')).toBeInTheDocument()
  })

  it('con el backend real (sin las piezas) avisa en vez de fallar', async () => {
    server.use(
      http.get('/api/admin/metrics', () => new HttpResponse(null, { status: 404 })),
      http.get('/api/audit', () => new HttpResponse(null, { status: 404 })),
    )
    renderApp('/admin', { como: 'admin' })
    expect(await screen.findByText('Las métricas llegarán pronto')).toBeInTheDocument()
    expect(await screen.findByText('La auditoría llegará pronto.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Exportar CSV' })).toBeDisabled()
  })

  it('un error de carga de las métricas ofrece reintentar', async () => {
    let falla = true
    server.use(
      http.get('/api/admin/metrics', () =>
        falla
          ? problema(500, 'Error interno.')
          : HttpResponse.json({
              generadoEn: new Date().toISOString(),
              usuariosActivos30d: 0,
              registros7d: 0,
              retencion30d: null,
              eliminacionesEnCurso: 0,
              planesMasIniciados: [],
            }),
      ),
    )
    const { user } = renderApp('/admin', { como: 'admin' })
    await user.click(await screen.findByRole('button', { name: 'Reintentar' }))
    falla = false
    await user.click(await screen.findByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByText('Aún nadie ha elegido un plan.')).toBeInTheDocument()
    const kpis = screen.getByRole('region', { name: 'Indicadores' })
    expect(within(kpis).getByText('—')).toBeInTheDocument()
  })

  it('"Exportar CSV" baja las métricas sin datos personales', async () => {
    let blob: Blob | null = null
    URL.createObjectURL = (b: Blob | MediaSource) => {
      blob = b as Blob
      return 'blob:prueba'
    }
    URL.revokeObjectURL = () => undefined
    let nombre = ''
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      nombre = this.download
    })
    const { user } = renderApp('/admin', { como: 'admin' })
    await screen.findByRole('region', { name: 'Indicadores' })
    await user.click(screen.getByRole('button', { name: 'Exportar CSV' }))
    expect(nombre).toMatch(/^metricas-\d{4}-\d{2}-\d{2}\.csv$/)
    const texto = await (blob as unknown as Blob).text()
    expect(texto).toContain('Usuarios activos (30 días),2')
    expect(texto).not.toContain('@')
  })
})

describe('Auditoría', () => {
  it('lista los eventos y avisa que el registro es de solo lectura', async () => {
    renderApp('/admin/auditoria', { como: 'admin' })
    expect(await screen.findByRole('heading', { level: 1, name: 'Auditoría' })).toBeInTheDocument()
    expect(screen.getByText(/solo lectura/)).toBeInTheDocument()
    const tabla = await screen.findByRole('table', { name: /Eventos de auditoría/ })
    expect(within(tabla).getAllByRole('row')).toHaveLength(db.auditoria.length + 1)
    // La acción llega legible del servidor y se muestra tal cual.
    expect(within(tabla).getByText('Subida de documento')).toBeInTheDocument()
    // Cambio de rol con valor anterior y nuevo (RF-AUD-01).
    expect(within(tabla).getByText('Administrador → Estandar')).toBeInTheDocument()
    expect(
      screen.getByText(`${db.auditoria.length} eventos, del más reciente al más antiguo.`),
    ).toBeInTheDocument()
  })

  it('filtra por usuario y por acción, y limpia los filtros', async () => {
    const { user } = renderApp('/admin/auditoria', { como: 'admin' })
    await screen.findByRole('table', { name: /Eventos de auditoría/ })

    await user.type(screen.getByLabelText('Usuario'), 'admin@')
    await user.selectOptions(screen.getByLabelText('Acción'), 'Cambio de rol')
    await user.click(screen.getByRole('button', { name: 'Filtrar' }))
    const tabla = await screen.findByRole('table', { name: /Eventos de auditoría/ })
    await waitFor(() => expect(within(tabla).getAllByRole('row')).toHaveLength(2))
    expect(within(tabla).getByText('Cambio de rol')).toBeInTheDocument()
    expect(screen.getByText(/con los filtros aplicados/)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Limpiar' }))
    await waitFor(() =>
      expect(within(screen.getByRole('table')).getAllByRole('row')).toHaveLength(
        db.auditoria.length + 1,
      ),
    )
  })

  it('sin resultados invita a limpiar los filtros', async () => {
    const { user } = renderApp('/admin/auditoria', { como: 'admin' })
    await screen.findByRole('table', { name: /Eventos de auditoría/ })
    await user.type(screen.getByLabelText('Usuario'), 'nadie@')
    await user.click(screen.getByRole('button', { name: 'Filtrar' }))
    expect(await screen.findByText('Sin eventos con esos filtros')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Exportar CSV' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Limpiar filtros' }))
    expect(await screen.findByRole('table', { name: /Eventos de auditoría/ })).toBeInTheDocument()
  })

  it('valida el rango de fechas antes de consultar', async () => {
    const { user } = renderApp('/admin/auditoria', { como: 'admin' })
    await screen.findByRole('table', { name: /Eventos de auditoría/ })
    await user.type(screen.getByLabelText('Desde'), '2026-11-10')
    await user.type(screen.getByLabelText('Hasta'), '2026-11-01')
    await user.click(screen.getByRole('button', { name: 'Filtrar' }))
    expect(await screen.findByText('"Desde" no puede ser posterior a "Hasta".')).toBeInTheDocument()
  })

  it('"Exportar CSV" baja exactamente lo que se ve, con BOM y sin fórmulas activas', async () => {
    db.auditoria.push(evento({ valorNuevo: '=1+1' }))
    let blob: Blob | null = null
    URL.createObjectURL = (b: Blob | MediaSource) => {
      blob = b as Blob
      return 'blob:prueba'
    }
    URL.revokeObjectURL = () => undefined
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
    const { user } = renderApp('/admin/auditoria', { como: 'admin' })
    await screen.findByRole('table', { name: /Eventos de auditoría/ })
    await user.click(screen.getByRole('button', { name: 'Exportar CSV' }))
    const archivo = blob as unknown as Blob
    // Blob.text() descarta el BOM: se comprueba en los bytes (EF BB BF), que Excel necesita.
    const bytes = new Uint8Array(await archivo.arrayBuffer())
    expect([...bytes.slice(0, 3)]).toEqual([0xef, 0xbb, 0xbf])
    const texto = await archivo.text()
    expect(texto.startsWith('Fecha (UTC),Usuario,')).toBe(true)
    expect(texto.split('\r\n')).toHaveLength(db.auditoria.length + 1)
    expect(texto).toContain("'=1+1")
    expect(texto).not.toMatch(/(^|,)=1\+1/m)
  })

  it('los datos del evento se muestran como texto, nunca como HTML', async () => {
    db.auditoria.push(
      evento({
        actor: '<img src=x onerror=alert(1)>@demo.com',
        entidad: '<b>Sesión</b>',
      }),
    )
    renderApp('/admin/auditoria', { como: 'admin' })
    const tabla = await screen.findByRole('table', { name: /Eventos de auditoría/ })
    expect(within(tabla).getByText('<b>Sesión</b>')).toBeInTheDocument()
    expect(tabla.querySelector('img, b')).toBeNull()
  })

  it('el Estándar no accede', async () => {
    renderApp('/admin/auditoria', { como: 'estandar' })
    expect(await screen.findByText('No tienes acceso a esta sección')).toBeInTheDocument()
  })

  it('con el backend real (sin la pieza) avisa en vez de fallar', async () => {
    server.use(http.get('/api/audit', () => new HttpResponse(null, { status: 404 })))
    renderApp('/admin/auditoria', { como: 'admin' })
    expect(await screen.findByText('La auditoría llegará pronto')).toBeInTheDocument()
  })

  it('un error del servidor ofrece reintentar y recupera la tabla', async () => {
    let falla = true
    server.use(
      http.get('/api/audit', () =>
        falla
          ? problema(500, 'Error interno.')
          : HttpResponse.json({ total: 1, pagina: 1, tamano: 200, registros: [evento()] }),
      ),
    )
    const { user } = renderApp('/admin/auditoria', { como: 'admin' })
    const reintentar = await screen.findByRole('button', { name: 'Reintentar' })
    falla = false
    await user.click(reintentar)
    expect(await screen.findByRole('table', { name: /Eventos de auditoría/ })).toBeInTheDocument()
  })

  it('un filtro que el servidor rechaza (400) se muestra como error, sin romper la pantalla', async () => {
    server.use(http.get('/api/audit', () => problema(400, 'Solicitud inválida.')))
    renderApp('/admin/auditoria', { como: 'admin' })
    expect(await screen.findByRole('heading', { level: 1, name: 'Auditoría' })).toBeInTheDocument()
    expect(await screen.findByRole('button', { name: 'Reintentar' })).toBeInTheDocument()
  })

  it('el filtro de usuario viaja como "usuario" y el tamaño como "tamano"', async () => {
    const consultas: string[] = []
    server.use(
      http.get('/api/audit', ({ request }) => {
        consultas.push(new URL(request.url).search)
        return HttpResponse.json({ total: 0, pagina: 1, tamano: 200, registros: [] })
      }),
    )
    const { user } = renderApp('/admin/auditoria', { como: 'admin' })
    await screen.findByText('Aún no hay eventos registrados')
    await user.type(screen.getByLabelText('Usuario'), 'ana@')
    await user.selectOptions(screen.getByLabelText('Acción'), 'Subida de documento')
    await user.click(screen.getByRole('button', { name: 'Filtrar' }))
    await waitFor(() => expect(consultas.length).toBeGreaterThan(1))
    const params = new URLSearchParams(consultas.at(-1))
    expect(params.get('usuario')).toBe('ana@')
    expect(params.get('accion')).toBe('Subida de documento')
    expect(params.get('tamano')).toBe('200')
    expect(params.has('actor')).toBe(false)
    expect(params.has('limite')).toBe(false)
  })

  it('valida en el formulario un rango de más de 366 días', async () => {
    const { user } = renderApp('/admin/auditoria', { como: 'admin' })
    await screen.findByRole('table', { name: /Eventos de auditoría/ })
    await user.type(screen.getByLabelText('Desde'), '2025-01-01')
    await user.type(screen.getByLabelText('Hasta'), '2026-01-02')
    await user.click(screen.getByRole('button', { name: 'Filtrar' }))
    expect(await screen.findByText('El rango no puede pasar de 366 días.')).toBeInTheDocument()
  })
})

describe('ajustes de la revisión del Paso 7', () => {
  it('avisa cuando la lista está truncada a los 200 más recientes', async () => {
    for (let i = 0; i < 205; i++) {
      db.auditoria.push(
        evento({ id: `masivo-${i}`, fecha: new Date(Date.now() - i * 1000).toISOString() }),
      )
    }
    renderApp('/admin/auditoria', { como: 'admin' })
    expect(await screen.findByText(/Se muestran solo los 200 más recientes/)).toBeInTheDocument()
    const tabla = await screen.findByRole('table', { name: /Eventos de auditoría/ })
    expect(within(tabla).getAllByRole('row')).toHaveLength(201)
    // El total es el de todo lo que cumple los filtros, no el de la página.
    expect(screen.getByText(new RegExp(`^${db.auditoria.length} eventos`))).toBeInTheDocument()
  })

  it('la tabla es una región enfocable para desplazarla con el teclado', async () => {
    renderApp('/admin/auditoria', { como: 'admin' })
    const region = await screen.findByRole('region', { name: 'Tabla de eventos de auditoría' })
    expect(region).toHaveAttribute('tabindex', '0')
  })

  it('los filtros de fecha dicen que son días UTC', async () => {
    renderApp('/admin/auditoria', { como: 'admin' })
    await screen.findByRole('table', { name: /Eventos de auditoría/ })
    expect(screen.getAllByText('Día UTC')).toHaveLength(2)
  })

  it('rechaza un día que no existe en el calendario, en el formulario y en el servidor', async () => {
    const admin = iniciarSesionComo('admin')
    expect((await llamar('/api/audit?desde=2026-13-45', admin)).status).toBe(400)
    expect((await llamar('/api/audit?hasta=2026-02-30', admin)).status).toBe(400)
    expect((await llamar('/api/audit?desde=2026-02-28', admin)).status).toBe(200)
  })

  it('el resumen enlaza con la cola de correos', async () => {
    renderApp('/admin', { como: 'admin' })
    expect(await screen.findByRole('link', { name: 'Abrir la cola de correos' })).toHaveAttribute(
      'href',
      '/admin/cola-correos',
    )
  })
})

describe('ajustes de la ronda 3 de crítica', () => {
  it('la tabla muestra primero la acción y la fecha en 24 horas', async () => {
    renderApp('/admin/auditoria', { como: 'admin' })
    const tabla = await screen.findByRole('table', { name: /Eventos de auditoría/ })
    const cabecera = within(tabla)
      .getAllByRole('columnheader')
      .map((c) => c.textContent)
    expect(cabecera).toEqual(['Acción', 'Quién', 'Cuándo (UTC)', 'Detalle'])
    // Sin "UTC" repetido por fila ni a. m./p. m.
    expect(within(tabla).queryByText(/UTC$|a\. m\.|p\. m\./)).toBeNull()
  })

  it('el resumen no repite la columna Detalle', async () => {
    renderApp('/admin', { como: 'admin' })
    const tabla = await screen.findByRole('table', { name: /Eventos de auditoría/ })
    expect(within(tabla).queryByRole('columnheader', { name: 'Detalle' })).toBeNull()
  })

  it('sin eventos ni filtros no dice "con esos filtros"', async () => {
    db.auditoria.length = 0
    renderApp('/admin/auditoria', { como: 'admin' })
    expect(await screen.findByText('Aún no hay eventos registrados')).toBeInTheDocument()
    expect(screen.queryByText('Sin eventos con esos filtros')).not.toBeInTheDocument()
  })

  it('el campo Usuario explica qué escribir con una ayuda visible', async () => {
    renderApp('/admin/auditoria', { como: 'admin' })
    expect(await screen.findByText('Correo completo o parte de él')).toBeInTheDocument()
  })
})
