import { screen, waitFor, within } from '@testing-library/react'
import { delay, http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { db, sembrarPlanDemo, sembrarPoblacionDemo } from '@/mocks/db'
import type { CorreoColaDto } from '@/features/cola-correos/types'
import { auditoriaHandlers } from '@/mocks/handlers/auditoria'
import { metricasHandlers } from '@/mocks/handlers/metricas'
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
      cohorteMinima: 5,
      cuentasNuevas7d: 1,
      eliminacionesEnCurso: 0,
      sugerenciasNuevas: 4,
      registrosPorCorreo7d: 2,
      usuariosActivos30d: 12,
      retencion30d: null,
      planesMasIniciados: [{ slug: 'x', cantidad: 7 }],
    }
    const filas = metricasACsv(m, (s) => `Plan ${s}`).split('\r\n')
    expect(filas).toContain('Personas activas (30 días),12,')
    expect(filas).toContain('Cuentas nuevas (7 días),1,')
    expect(filas).toContain('Registros por correo (7 días),2,')
    expect(filas).toContain('Quejas y mejoras nuevas,4,')
    expect(filas).toContain('Plan iniciado: Plan x,7,')
    // Una cifra oculta sale vacía: no es lo mismo que cero.
    expect(filas).toContain('Retención a 30 días (%),,Oculta: menos de 5 personas')
    expect(filas.join('\n')).not.toContain('@')
  })

  it('en el CSV, las dos cifras ocultas salen vacías y sin planes', () => {
    const m: MetricasDto = {
      generadoEn: '2026-11-03T10:00:00.000Z',
      cohorteMinima: 5,
      cuentasNuevas7d: 0,
      eliminacionesEnCurso: 0,
      sugerenciasNuevas: 0,
      registrosPorCorreo7d: 0,
      usuariosActivos30d: null,
      retencion30d: null,
      planesMasIniciados: [],
    }
    const filas = metricasACsv(m, (s) => s).split('\r\n')
    expect(filas).toContain('Personas activas (30 días),,Oculta: menos de 5 personas')
    expect(filas).toContain('Retención a 30 días (%),,Oculta: menos de 5 personas')
    expect(filas).toContain('Cuentas nuevas (7 días),0,')
    expect(filas.some((f) => f.startsWith('Plan iniciado'))).toBe(false)
  })

  it('un correo no cabe en el CSV de las métricas: solo cifras y nombres de plan', () => {
    const m: MetricasDto = {
      generadoEn: '2026-11-03T10:00:00.000Z',
      cohorteMinima: 5,
      cuentasNuevas7d: 0,
      eliminacionesEnCurso: 0,
      sugerenciasNuevas: 0,
      registrosPorCorreo7d: 0,
      usuariosActivos30d: null,
      retencion30d: null,
      planesMasIniciados: [{ slug: 'x', cantidad: 5 }],
    }
    expect(metricasACsv(m, () => 'Plan, con "comas"')).toContain(
      '"Plan iniciado: Plan, con ""comas""",5',
    )
  })
})

const CLAVE_NUEVA_CUENTA = 'Clave-Segura-2026'

const DIA_MS = 86_400_000
const hace = (dias: number) => new Date(Date.now() - dias * DIA_MS).toISOString()
const metricasDe = async (token = iniciarSesionComo('admin')): Promise<MetricasDto> =>
  (await llamar('/api/admin/metrics', token)).json()

describe('contrato del panel de administración (auditoría y métricas reales)', () => {
  it('métricas y auditoría son solo del Administrador', async () => {
    for (const ruta of ['/api/admin/metrics', '/api/audit']) {
      expect((await llamar(ruta)).status).toBe(401)
      expect((await llamar(ruta, iniciarSesionComo('estandar'))).status).toBe(403)
      expect((await llamar(ruta, iniciarSesionComo('admin'))).status).toBe(200)
    }
  })

  it('con pocas personas, lo que cuenta personas llega null y no hay planes; lo demás sí se cuenta', async () => {
    sembrarPlanDemo()
    const m = await metricasDe()
    expect(m).toMatchObject({
      cohorteMinima: 5,
      cuentasNuevas7d: 0,
      eliminacionesEnCurso: 0,
      sugerenciasNuevas: 0,
      registrosPorCorreo7d: 0,
      usuariosActivos30d: null,
      retencion30d: null,
      planesMasIniciados: [],
    })
  })

  it('con el piloto de ejemplo se ven las cifras, los planes de más a menos y la retención', async () => {
    sembrarPlanDemo()
    sembrarPoblacionDemo()
    const m = await metricasDe()
    expect(m.usuariosActivos30d).toBeGreaterThanOrEqual(5)
    // De 5 personas con avances hace 30 a 60 días, 3 siguen.
    expect(m.retencion30d).toBe(60)
    expect(m.planesMasIniciados).toEqual([
      { slug: 'mi-dia-oficina', cantidad: 6 },
      { slug: 'caminar-para-empezar', cantidad: 5 },
    ])
    expect(m.cuentasNuevas7d).toBe(2)
    expect(m.registrosPorCorreo7d).toBe(2)
    // Agregadas y anónimas: ningún correo ni id de persona.
    expect(JSON.stringify(m)).not.toMatch(/@|demo|piloto|-0000|[0-9a-f]{8}-[0-9a-f]{4}/)
  })

  it('la cohorte mínima es exacta: con 4 personas se oculta y con 5 se muestra (también un plan)', async () => {
    // Solo Ana tiene avances recientes en las cuentas de ejemplo: 1 + 3 anónimas = 4 (oculto) y 1 + 4 = 5 (visible).
    db.poblacionSimulada.registros.push(
      { persona: 'a', fecha: hace(1) },
      { persona: 'b', fecha: hace(2) },
      { persona: 'c', fecha: hace(3) },
    )
    db.poblacionSimulada.planesIniciados.push('x', 'x', 'x', 'x')
    let m = await metricasDe()
    expect(m.usuariosActivos30d).toBeNull()
    expect(m.planesMasIniciados).toEqual([])

    db.poblacionSimulada.registros.push({ persona: 'd', fecha: hace(4) })
    db.poblacionSimulada.planesIniciados.push('x')
    m = await metricasDe()
    expect(m.usuariosActivos30d).toBe(5)
    expect(m.planesMasIniciados).toEqual([{ slug: 'x', cantidad: 5 }])
  })

  it('las bajas en curso, las quejas nuevas y las cuentas nuevas se cuentan aunque sean pocas', async () => {
    db.bajas.set(db.usuarios[2].id, { solicitadaEn: hace(1), borradoEn: hace(-29) })
    // Una baja cuyo periodo de gracia ya terminó no está «en curso».
    db.bajas.set(db.usuarios[1].id, { solicitadaEn: hace(40), borradoEn: hace(10) })
    const texto = 'Un mensaje de al menos diez caracteres.'
    db.sugerencias.push(
      {
        id: 's1',
        usuarioId: db.usuarios[0].id,
        tipo: 'Queja',
        texto,
        creadaEn: hace(1),
        estado: 'Nueva',
      },
      {
        id: 's2',
        usuarioId: db.usuarios[0].id,
        tipo: 'Mejora',
        texto,
        creadaEn: hace(2),
        estado: 'Leida',
      },
    )
    db.registrosPorCorreo.push(hace(1), hace(8))
    db.actividad.push({ fecha: hace(2), actor: 'nueva@demo.com', tipo: 'Registro' })
    const m = await metricasDe()
    expect(m).toMatchObject({
      eliminacionesEnCurso: 1,
      sugerenciasNuevas: 1,
      registrosPorCorreo7d: 1,
      cuentasNuevas7d: 1,
    })
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
    expect(metricasHandlers.map((h) => String(h.info.method))).toEqual(['GET'])
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

// El bloque de una cifra (Estadistica): la fila de la etiqueta, el número grande y la explicación cuelgan del mismo contenedor.
const tarjeta = (etiqueta: string) =>
  screen.getByText(etiqueta).closest('div')!.parentElement as HTMLElement

const AVISO_PRIVACIDAD = /Oculta hasta que haya al menos 5 personas, para proteger su anonimato\./

const correoDeEjemplo: CorreoColaDto = {
  id: 'c0',
  destinatario: 'persona@demo.com',
  asunto: 'Tu resumen de la semana',
  estado: 'Pendiente',
  intentos: 0,
  fechaCreacion: new Date().toISOString(),
  fechaUltimoIntento: null,
  fechaEnvio: null,
  ultimoError: null,
}

const metricasBase = (extra: Partial<MetricasDto> = {}): MetricasDto => ({
  generadoEn: new Date().toISOString(),
  cohorteMinima: 5,
  cuentasNuevas7d: 0,
  eliminacionesEnCurso: 0,
  sugerenciasNuevas: 0,
  registrosPorCorreo7d: 0,
  usuariosActivos30d: null,
  retencion30d: null,
  planesMasIniciados: [],
  ...extra,
})

describe('Resumen del Administrador', () => {
  it('muestra las cifras agrupadas, los planes con su nombre y la auditoría reciente', async () => {
    sembrarPlanDemo()
    sembrarPoblacionDemo()
    renderApp('/admin', { como: 'admin' })
    expect(await screen.findByRole('heading', { level: 1, name: 'Resumen' })).toBeInTheDocument()
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
    expect(screen.getByText('Métricas agregadas y anónimas.')).toBeInTheDocument()

    const personas = await screen.findByRole('region', { name: 'Personas' })
    expect(personas).toBeInTheDocument()
    expect(within(tarjeta('Cuentas nuevas')).getByText('2')).toBeInTheDocument()
    expect(within(tarjeta('Retención a 30 días')).getByText('60 %')).toBeInTheDocument()
    // Con cifra no se explica la privacidad, se explica qué cuenta.
    expect(tarjeta('Personas activas')).not.toHaveTextContent(/privacidad/)
    expect(tarjeta('Personas activas')).not.toHaveTextContent('—')

    const uso = screen.getByRole('region', { name: 'Uso' })
    expect(within(tarjeta('Registros por correo')).getByText('2')).toBeInTheDocument()
    const lista = within(uso).getByRole('list')
    const filas = within(lista).getAllByRole('listitem')
    expect(filas).toHaveLength(2)
    // Con el nombre del plan (no el slug) y la cantidad, de más a menos.
    expect(within(filas[0]).getByText('Mi día de oficina saludable')).toBeInTheDocument()
    expect(within(filas[0]).getByText('6 personas')).toBeInTheDocument()
    expect(within(filas[1]).getByText('5 personas')).toBeInTheDocument()
    expect(within(lista).queryByText(/mi-dia-oficina|caminar-para-empezar/)).not.toBeInTheDocument()
    // Sin identificadores internos ni avisos de proceso en la interfaz.
    expect(screen.queryByText(/Google|RF-/)).not.toBeInTheDocument()

    expect(within(personas).getByText('Bajas en curso')).toBeInTheDocument()

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

  it('con la cohorte pequeña muestra un guion y explica por qué, nunca un cero ni un error', async () => {
    // Sin el piloto de ejemplo solo hay 3 cuentas: por debajo de la cohorte mínima.
    renderApp('/admin', { como: 'admin' })
    await screen.findByRole('region', { name: 'Personas' })
    for (const etiqueta of ['Personas activas', 'Retención a 30 días']) {
      const bloque = tarjeta(etiqueta)
      expect(bloque).toHaveTextContent('—')
      expect(bloque).not.toHaveTextContent(/\b0\b/)
      expect(bloque).not.toHaveTextContent('%')
    }
    expect(tarjeta('Personas activas')).toHaveTextContent(AVISO_PRIVACIDAD)
    // La retención dice cuál es la cohorte que falta, no solo que está oculta.
    expect(tarjeta('Retención a 30 días')).toHaveTextContent(
      'Hay menos de 5 personas que registraron algo hace 30 a 60 días.',
    )
    // El guion va atenuado (no es un dato) y no se lee como «raya».
    expect(within(tarjeta('Personas activas')).getByText('—')).toHaveClass('text-atenuado')
    expect(within(tarjeta('Personas activas')).getByText('Sin cifra')).toHaveClass('sr-only')
    // Sin planes visibles, la razón en la lista de planes.
    const uso = screen.getByRole('region', { name: 'Uso' })
    expect(within(uso).queryByRole('list')).not.toBeInTheDocument()
    expect(
      within(uso).getByText(
        'Ningún plan llega todavía a 5 personas, así que no se muestra ninguno.',
      ),
    ).toBeInTheDocument()
    expect(
      within(uso).getByRole('heading', { name: 'Planes más iniciados (desde el inicio)' }),
    ).toBeInTheDocument()
    // Nada se presenta como un fallo.
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reintentar' })).not.toBeInTheDocument()
    // Lo demás sí se muestra aunque sea 0.
    expect(within(tarjeta('Cuentas nuevas')).getByText('0')).toBeInTheDocument()
    expect(within(tarjeta('Bajas en curso')).getByText('0')).toBeInTheDocument()
    expect(within(tarjeta('Quejas y mejoras nuevas')).getByText('0')).toBeInTheDocument()
    expect(within(tarjeta('Registros por correo')).getByText('0')).toBeInTheDocument()
  })

  it('el orden de los grupos es: Para atender, Personas y Uso', async () => {
    renderApp('/admin', { como: 'admin' })
    await screen.findByRole('region', { name: 'Personas' })
    const titulos = screen
      .getAllByRole('heading', { level: 2 })
      .map((h) => h.textContent)
      .filter((t) => ['Para atender', 'Personas', 'Uso'].includes(t ?? ''))
    expect(titulos).toEqual(['Para atender', 'Personas', 'Uso'])
  })

  it('«Para atender» trae las quejas nuevas y los correos fallidos, cada uno con su enlace', async () => {
    db.correos.push(
      { ...correoDeEjemplo, id: 'c1', estado: 'Fallido' },
      { ...correoDeEjemplo, id: 'c2', estado: 'Fallido' },
      { ...correoDeEjemplo, id: 'c3', estado: 'Pendiente' },
    )
    renderApp('/admin', { como: 'admin' })
    const atender = await screen.findByRole('region', { name: 'Para atender' })
    await waitFor(() =>
      expect(within(tarjeta('Correos fallidos')).getByText('2')).toBeInTheDocument(),
    )
    expect(
      within(atender).getByRole('link', { name: 'Revisar los correos fallidos' }),
    ).toHaveAttribute('href', '/admin/cola-correos')
    // La bajas no son accionables: viven en «Personas».
    expect(within(atender).queryByText('Bajas en curso')).not.toBeInTheDocument()
    expect(
      within(screen.getByRole('region', { name: 'Personas' })).getByText('Bajas en curso'),
    ).toBeInTheDocument()
  })

  it('sin correos fallidos la cifra es 0; si la cola no responde, queda sin cifra y lo dice', async () => {
    server.use(http.get('/api/admin/email-queue', () => problema(500, 'Error interno.')))
    renderApp('/admin', { como: 'admin' })
    await screen.findByRole('region', { name: 'Para atender' })
    await waitFor(() =>
      expect(tarjeta('Correos fallidos')).toHaveTextContent(
        'No se pudo consultar la cola de correos.',
      ),
    )
    expect(within(tarjeta('Correos fallidos')).getByText('Sin cifra')).toBeInTheDocument()
    expect(
      within(tarjeta('Correos fallidos')).getByRole('link', {
        name: 'Revisar los correos fallidos',
      }),
    ).toBeInTheDocument()
  })

  it('la barra de cada plan es decorativa: no se anuncia como progreso', async () => {
    sembrarPlanDemo()
    sembrarPoblacionDemo()
    renderApp('/admin', { como: 'admin' })
    const uso = await screen.findByRole('region', { name: 'Uso' })
    await within(uso).findByText('Mi día de oficina saludable')
    expect(within(uso).queryByRole('progressbar')).not.toBeInTheDocument()
    expect(uso.querySelectorAll('[aria-hidden="true"].bg-borde')).toHaveLength(2)
  })

  it('el aviso de privacidad usa la cohorte mínima que manda el servidor', async () => {
    server.use(
      http.get('/api/admin/metrics', () => HttpResponse.json(metricasBase({ cohorteMinima: 8 }))),
    )
    renderApp('/admin', { como: 'admin' })
    await screen.findByRole('region', { name: 'Personas' })
    expect(tarjeta('Personas activas')).toHaveTextContent(
      'Oculta hasta que haya al menos 8 personas, para proteger su anonimato.',
    )
  })

  it('un plan que ya no está en el catálogo se muestra por su slug, sin romper la lista', async () => {
    server.use(
      http.get('/api/admin/metrics', () =>
        HttpResponse.json(
          metricasBase({
            usuariosActivos30d: 9,
            planesMasIniciados: [{ slug: 'plan-retirado', cantidad: 5 }],
          }),
        ),
      ),
    )
    renderApp('/admin', { como: 'admin' })
    const uso = await screen.findByRole('region', { name: 'Uso' })
    expect(await within(uso).findByText('plan-retirado')).toBeInTheDocument()
    expect(within(tarjeta('Personas activas')).getByText('9')).toBeInTheDocument()
  })

  it('las quejas nuevas se cuentan y llevan a la bandeja de /admin/sugerencias', async () => {
    db.sugerencias.push({
      id: 'nueva-1',
      usuarioId: db.usuarios[0].id,
      tipo: 'Mejora',
      texto: 'Me gustaría elegir la hora de cada aviso.',
      creadaEn: new Date().toISOString(),
      estado: 'Nueva',
    })
    const { user } = renderApp('/admin', { como: 'admin' })
    await screen.findByRole('region', { name: 'Para atender' })
    const bloque = tarjeta('Quejas y mejoras nuevas')
    expect(within(bloque).getByText('1')).toBeInTheDocument()
    const enlace = within(bloque).getByRole('link', { name: 'Abrir las quejas y mejoras' })
    expect(enlace).toHaveAttribute('href', '/admin/sugerencias')
    await user.click(enlace)
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Quejas y mejoras' }),
    ).toBeInTheDocument()
  })

  it('las bajas en curso se muestran con su explicación', async () => {
    db.bajas.set(db.usuarios[2].id, {
      solicitadaEn: new Date().toISOString(),
      borradoEn: new Date(Date.now() + 29 * 86_400_000).toISOString(),
    })
    renderApp('/admin', { como: 'admin' })
    await screen.findByRole('region', { name: 'Personas' })
    const bloque = tarjeta('Bajas en curso')
    expect(within(bloque).getByText('1')).toBeInTheDocument()
    expect(bloque).toHaveTextContent('periodo de gracia')
  })

  it('dice cuándo se generaron las cifras, en la hora de quien mira', async () => {
    const generadoEn = '2026-11-21T15:30:00.000Z'
    server.use(
      http.get('/api/admin/metrics', () => HttpResponse.json(metricasBase({ generadoEn }))),
    )
    renderApp('/admin', { como: 'admin' })
    const esperado = new Intl.DateTimeFormat('es-DO', {
      dateStyle: 'medium',
      timeStyle: 'short',
    })
      .format(new Date(generadoEn))
      // El formato usa un espacio fino antes de «a. m.»: el DOM lo normaliza a un espacio normal.
      .replace(/\s/g, ' ')
    expect(await screen.findByText(`Generado el ${esperado}`)).toBeInTheDocument()
  })

  it('mientras llegan las cifras muestra un estado de carga accesible', async () => {
    server.use(
      http.get('/api/admin/metrics', async () => {
        await delay(300)
        return HttpResponse.json(metricasBase())
      }),
    )
    renderApp('/admin', { como: 'admin' })
    expect(await screen.findByRole('status')).toHaveTextContent('Cargando las métricas…')
    expect(await screen.findByRole('region', { name: 'Personas' })).toBeInTheDocument()
    expect(screen.queryByText('Cargando las métricas…')).not.toBeInTheDocument()
  })

  it('una métrica que el servidor rechaza (403) es un error normal con reintento, sin «llegarán pronto»', async () => {
    server.use(http.get('/api/admin/metrics', () => problema(403, 'Acceso denegado.')))
    renderApp('/admin', { como: 'admin' })
    expect(await screen.findByRole('button', { name: 'Reintentar' })).toBeInTheDocument()
    expect(screen.queryByText(/llegarán pronto/)).not.toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Personas' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Exportar CSV' })).toBeDisabled()
    // El resto del Resumen sigue en pie.
    expect(await screen.findByRole('heading', { name: 'Cola de correos' })).toBeInTheDocument()
  })

  it('un 404 sin cuerpo de las métricas tampoco se toma por «llegarán pronto»: es un error con reintento', async () => {
    server.use(http.get('/api/admin/metrics', () => new HttpResponse(null, { status: 404 })))
    renderApp('/admin', { como: 'admin' })
    expect(await screen.findByRole('button', { name: 'Reintentar' })).toBeInTheDocument()
    expect(screen.queryByText(/llegarán pronto/)).not.toBeInTheDocument()
  })

  it('un error de carga de las métricas ofrece reintentar y se recupera', async () => {
    let falla = true
    server.use(
      http.get('/api/admin/metrics', () =>
        falla ? problema(500, 'Error interno.') : HttpResponse.json(metricasBase()),
      ),
    )
    const { user } = renderApp('/admin', { como: 'admin' })
    await user.click(await screen.findByRole('button', { name: 'Reintentar' }))
    falla = false
    await user.click(await screen.findByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByRole('region', { name: 'Personas' })).toBeInTheDocument()
    expect(tarjeta('Personas activas')).toHaveTextContent('—')
    expect(screen.queryByRole('button', { name: 'Reintentar' })).not.toBeInTheDocument()
  })

  it('"Exportar CSV" deja vacías las cifras ocultas y no lleva datos personales', async () => {
    let blob: Blob | null = null
    URL.createObjectURL = (b: Blob | MediaSource) => {
      blob = b as Blob
      return 'blob:prueba'
    }
    URL.revokeObjectURL = () => undefined
    const { user } = renderApp('/admin', { como: 'admin' })
    await screen.findByRole('region', { name: 'Personas' })
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
    await user.click(screen.getByRole('button', { name: 'Exportar CSV' }))
    const filas = (await (blob as unknown as Blob).text()).split('\r\n')
    expect(filas).toContain('Personas activas (30 días),,Oculta: menos de 5 personas')
    expect(filas).toContain('Retención a 30 días (%),,Oculta: menos de 5 personas')
    expect(filas).toContain('Cuentas nuevas (7 días),0,')
    expect(filas.join('\n')).not.toContain('@')
  })

  it('"Exportar CSV" baja las métricas con los nombres de los planes', async () => {
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
    sembrarPlanDemo()
    sembrarPoblacionDemo()
    const { user } = renderApp('/admin', { como: 'admin' })
    await screen.findByRole('region', { name: 'Personas' })
    // El nombre del plan llega del catálogo: se espera a que la lista lo muestre.
    await screen.findByText('Mi día de oficina saludable')
    await user.click(screen.getByRole('button', { name: 'Exportar CSV' }))
    expect(nombre).toMatch(/^metricas-\d{4}-\d{2}-\d{2}\.csv$/)
    const filas = (await (blob as unknown as Blob).text()).split('\r\n')
    expect(filas).toContain('Retención a 30 días (%),60,')
    expect(filas).toContain('Plan iniciado: Mi día de oficina saludable,6,')
    expect(filas.join('\n')).not.toContain('@')
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
