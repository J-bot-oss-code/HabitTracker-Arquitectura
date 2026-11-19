import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { claveEliminarArchivo } from '@/features/documentos/types'
import { PLAN_DEMO_ID, db, sembrarPlanDemo } from '@/mocks/db'
import { renderApp } from '@/test/render'

const CULPA = /fallaste|perdiste|culpa|castigo/i

// Archivo de Ana ya guardado (el borrado es lógico: `eliminado`).
function archivoDeAna(nombre = 'rutina.pdf') {
  const id = crypto.randomUUID()
  db.documentos.push({
    id,
    nombre,
    tipo: 'application/pdf',
    tamanoBytes: 2048,
    fechaSubidaUtc: new Date().toISOString(),
    usuarioId: db.usuarios.find((u) => u.correo === 'estandar@demo.com')!.id,
    correoUsuario: 'estandar@demo.com',
    eliminado: false,
    contenido: new TextEncoder().encode('hola').buffer as ArrayBuffer,
  })
  return id
}

// Deja el archivo en el plan de ejemplo (en curso, salvo que se diga otro estado).
function enElPlan(documentoId: string, estado: 'Activo' | 'Completado' | 'Cancelado' = 'Activo') {
  sembrarPlanDemo()
  db.planesUsuario.find((p) => p.id === PLAN_DEMO_ID)!.estado = estado
  db.adjuntos.push({
    planUsuarioId: PLAN_DEMO_ID,
    usuarioId: db.planesUsuario[0].usuarioId,
    documentoId,
    adjuntadoEnUtc: new Date().toISOString(),
  })
}

const permisoAplicadoDe = (documentoId: string) => {
  db.solicitudes.push({
    id: crypto.randomUUID(),
    solicitante: 'estandar@demo.com',
    permisoSolicitado: claveEliminarArchivo(documentoId),
    estado: 'Aplicada',
    aprobador: 'admin@demo.com',
    fechaCreacion: new Date().toISOString(),
    fechaResolucion: new Date().toISOString(),
    motivoRechazo: null,
  })
}

const entradaDeArchivo = () => document.querySelector<HTMLInputElement>('input[type=file]')!
// El filtro `accept` de user-event dejaría fuera el .exe antes de que lo vea la pantalla: se manda el cambio tal cual.
const elegir = (archivo: File) =>
  fireEvent.change(entradaDeArchivo(), { target: { files: [archivo] } })
const lista = () => within(screen.getByRole('list', { name: 'Tus archivos' }))

afterEach(() => vi.restoreAllMocks())

describe('Mis archivos', () => {
  it('sin archivos muestra el estado vacío y el límite de tamaño', async () => {
    renderApp('/documentos', { como: 'estandar' })

    expect(await screen.findByText('Aún no tienes archivos')).toBeInTheDocument()
    expect(screen.getByText(/hasta 10 MB/)).toBeInTheDocument()
  })

  it('sube un PDF válido y lo lista con su tipo y tamaño', async () => {
    renderApp('/documentos', { como: 'estandar' })
    await screen.findByText('Aún no tienes archivos')

    elegir(new File([new Uint8Array(2048)], 'rutina.pdf', { type: 'application/pdf' }))

    expect(await screen.findByText('Archivo guardado.')).toBeInTheDocument()
    const fila = (await lista().findByText('rutina.pdf')).closest('li')!
    expect(within(fila).getByText(/PDF · 2 KB/)).toBeInTheDocument()
    expect(db.documentos).toHaveLength(1)
  })

  it('un .exe o un archivo de más de 10 MB se rechaza en español y no llega a la API', async () => {
    renderApp('/documentos', { como: 'estandar' })
    await screen.findByText('Aún no tienes archivos')

    elegir(new File(['MZ'], 'virus.exe'))
    expect(await screen.findByText(/Este tipo de archivo no se puede guardar/)).toBeInTheDocument()

    elegir(new File([new Uint8Array(10 * 1024 * 1024 + 1)], 'grande.pdf'))
    expect(await screen.findByText(/pesa más de 10 MB/)).toBeInTheDocument()

    expect(db.documentos).toHaveLength(0)
    expect(screen.queryByText('Archivo guardado.')).not.toBeInTheDocument()
  })

  it('eliminar un archivo libre pide confirmación; cancelar lo conserva y confirmar lo quita', async () => {
    archivoDeAna()
    const { user } = renderApp('/documentos', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Eliminar: rutina.pdf' }))

    const dialogo = await screen.findByRole('dialog', { name: 'Eliminar este archivo' })
    await user.click(within(dialogo).getByRole('button', { name: 'Cancelar' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(db.documentos[0].eliminado).toBe(false)
    expect(screen.getByText('rutina.pdf')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Eliminar: rutina.pdf' }))
    const otro = await screen.findByRole('dialog', { name: 'Eliminar este archivo' })
    await user.click(within(otro).getByRole('button', { name: 'Eliminar' }))

    expect(await screen.findByText('Aún no tienes archivos')).toBeInTheDocument()
    expect(db.documentos[0].eliminado).toBe(true)
  })

  it('en un plan en curso pide un permiso por archivo y queda en revisión', async () => {
    const id = archivoDeAna()
    enElPlan(id)
    const { user } = renderApp('/documentos', { como: 'estandar' })

    expect(await screen.findByText('En tu plan «Mi día de oficina saludable»')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Eliminar: rutina.pdf' })).not.toBeInTheDocument()

    await user.click(
      await screen.findByRole('button', { name: 'Pedir permiso para eliminar: rutina.pdf' }),
    )

    expect(await screen.findByText('Pedido en revisión')).toBeInTheDocument()
    expect(
      db.solicitudes.find((s) => s.permisoSolicitado === claveEliminarArchivo(id)),
    ).toMatchObject({ solicitante: 'estandar@demo.com', estado: 'Pendiente' })
    expect(db.documentos[0].eliminado).toBe(false)
  })

  it('con el permiso aplicado, eliminar un archivo del plan en curso es libre', async () => {
    const id = archivoDeAna()
    enElPlan(id)
    permisoAplicadoDe(id)
    const { user } = renderApp('/documentos', { como: 'estandar' })

    await user.click(await screen.findByRole('button', { name: 'Eliminar: rutina.pdf' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Eliminar este archivo' })
    await user.click(within(dialogo).getByRole('button', { name: 'Eliminar' }))

    expect(await screen.findByText('Aún no tienes archivos')).toBeInTheDocument()
    expect(db.documentos[0].eliminado).toBe(true)
  })

  it.each(['Cancelado', 'Completado'] as const)(
    'con el plan %s el archivo ya es libre de eliminar',
    async (estado) => {
      enElPlan(archivoDeAna(), estado)
      renderApp('/documentos', { como: 'estandar' })

      expect(await screen.findByText('Estuvo en «Mi día de oficina saludable»')).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Eliminar: rutina.pdf' })).toBeInTheDocument()
      expect(screen.queryByText(/Pedir permiso/)).not.toBeInTheDocument()
    },
  )

  it('descargar baja el archivo con su nombre y sin error', async () => {
    archivoDeAna()
    const crearUrl = vi.fn(() => 'blob:prueba')
    URL.createObjectURL = crearUrl
    URL.revokeObjectURL = vi.fn()
    const clic = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
    const { user } = renderApp('/documentos', { como: 'estandar' })

    await user.click(await screen.findByRole('button', { name: 'Descargar: rutina.pdf' }))

    await waitFor(() => expect(clic).toHaveBeenCalled())
    expect(crearUrl).toHaveBeenCalledWith(expect.any(Blob))
    expect(screen.queryByText(/No se pudo descargar/)).not.toBeInTheDocument()
  })

  it('los textos de la pantalla, incluidos el pedido de permiso y el error, no culpan', async () => {
    enElPlan(archivoDeAna())
    const { user } = renderApp('/documentos', { como: 'estandar' })
    await user.click(
      await screen.findByRole('button', { name: 'Pedir permiso para eliminar: rutina.pdf' }),
    )
    await screen.findByText('Pedido en revisión')
    elegir(new File(['MZ'], 'virus.exe'))
    await screen.findByText(/Este tipo de archivo no se puede guardar/)

    expect(document.body.textContent).not.toMatch(CULPA)
  })
})

describe('Mis archivos: acceso por rol', () => {
  it('el Estándar ve «Mis archivos» en el menú, con enlace a /documentos', async () => {
    renderApp('/hoy', { como: 'estandar' })

    const menu = await screen.findByRole('navigation', { name: 'Principal' })
    expect(within(menu).getByRole('link', { name: 'Mis archivos' })).toHaveAttribute(
      'href',
      '/documentos',
    )
  })

  it('el Administrador no ve el enlace y /documentos le dice que no tiene acceso', async () => {
    renderApp('/documentos', { como: 'admin' })

    expect(await screen.findByText('No tienes acceso a esta sección')).toBeInTheDocument()
    const menu = screen.getByRole('navigation', { name: 'Principal' })
    expect(within(menu).queryByRole('link', { name: 'Mis archivos' })).not.toBeInTheDocument()
  })
})
