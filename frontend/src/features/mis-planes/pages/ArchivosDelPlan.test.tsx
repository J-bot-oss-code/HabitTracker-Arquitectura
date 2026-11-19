import { screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { claveEliminarArchivo } from '@/features/documentos/types'
import { PLAN_DEMO_ID, db, sembrarPlanDemo } from '@/mocks/db'
import { renderApp } from '@/test/render'

const CULPA = /fallaste|perdiste|culpa|castigo/i
const RUTA = `/mis-planes/${PLAN_DEMO_ID}`

beforeEach(() => sembrarPlanDemo())

// Archivo de Ana ya guardado.
function archivoDeAna(nombre: string) {
  const id = crypto.randomUUID()
  db.documentos.push({
    id,
    nombre,
    tipo: 'application/pdf',
    tamanoBytes: 2048,
    fechaSubidaUtc: new Date().toISOString(),
    usuarioId: db.planesUsuario[0].usuarioId,
    correoUsuario: 'estandar@demo.com',
    eliminado: false,
    contenido: new TextEncoder().encode('hola').buffer as ArrayBuffer,
  })
  return id
}

const vincular = (documentoId: string) =>
  db.adjuntos.push({
    planUsuarioId: PLAN_DEMO_ID,
    usuarioId: db.planesUsuario[0].usuarioId,
    documentoId,
    adjuntadoEnUtc: new Date().toISOString(),
  })

const terminar = (estado: 'Completado' | 'Cancelado') => {
  db.planesUsuario.find((p) => p.id === PLAN_DEMO_ID)!.estado = estado
}

describe('Mis planes: archivos del plan', () => {
  it('lista los archivos del plan con su tipo y tamaño', async () => {
    vincular(archivoDeAna('rutina.pdf'))
    archivoDeAna('otro-suelto.pdf')
    renderApp(RUTA, { como: 'estandar' })

    const lista = await screen.findByRole('list', { name: 'Archivos del plan' })
    const fila = within(lista).getByText('rutina.pdf').closest('li')!
    expect(within(fila).getByText(/PDF · 2 KB/)).toBeInTheDocument()
    expect(within(lista).queryByText('otro-suelto.pdf')).not.toBeInTheDocument()
  })

  it('sin archivos lo dice con calma y ofrece agregar uno de «Mis archivos»', async () => {
    const id = archivoDeAna('rutina.pdf')
    const { user } = renderApp(RUTA, { como: 'estandar' })
    await screen.findByRole('heading', { level: 2, name: 'Archivos del plan' })
    expect(await screen.findByText(/Aún no hay archivos en este plan/)).toBeInTheDocument()

    await user.selectOptions(await screen.findByLabelText('Agregar un archivo tuyo'), id)
    await user.click(screen.getByRole('button', { name: 'Agregar al plan' }))

    expect(await screen.findByText('Archivo agregado al plan.')).toBeInTheDocument()
    const lista = await screen.findByRole('list', { name: 'Archivos del plan' })
    expect(within(lista).getByText('rutina.pdf')).toBeInTheDocument()
    expect(db.adjuntos).toHaveLength(1)
    // Ya está en el plan: deja de ofrecerse.
    await waitFor(() =>
      expect(screen.queryByLabelText('Agregar un archivo tuyo')).not.toBeInTheDocument(),
    )
  })

  it('si el archivo ya estaba en el plan, lo avisa y no lo duplica', async () => {
    const id = archivoDeAna('rutina.pdf')
    const { user } = renderApp(RUTA, { como: 'estandar' })
    const selector = await screen.findByLabelText('Agregar un archivo tuyo')
    // Se agregó desde otra pestaña mientras esta seguía abierta.
    vincular(id)

    await user.selectOptions(selector, id)
    await user.click(screen.getByRole('button', { name: 'Agregar al plan' }))

    expect(await screen.findByText(/Ese archivo ya está en este plan/)).toBeInTheDocument()
    expect(db.adjuntos).toHaveLength(1)
  })

  it('quitar un archivo con el plan en curso pide un permiso y no lo quita', async () => {
    const id = archivoDeAna('rutina.pdf')
    vincular(id)
    const { user } = renderApp(RUTA, { como: 'estandar' })
    expect(screen.queryByRole('button', { name: /^Quitar del plan:/ })).not.toBeInTheDocument()

    await user.click(
      await screen.findByRole('button', { name: 'Pedir permiso para quitar del plan: rutina.pdf' }),
    )

    expect(await screen.findByText('Pedido en revisión')).toBeInTheDocument()
    expect(
      db.solicitudes.find((s) => s.permisoSolicitado === claveEliminarArchivo(id)),
    ).toMatchObject({ solicitante: 'estandar@demo.com', estado: 'Pendiente' })
    expect(db.adjuntos).toHaveLength(1)
  })

  it.each(['Completado', 'Cancelado'] as const)(
    'con el plan %s no se ofrece agregar y quitar es libre',
    async (estado) => {
      vincular(archivoDeAna('rutina.pdf'))
      terminar(estado)
      const { user } = renderApp(RUTA, { como: 'estandar' })

      await user.click(await screen.findByRole('button', { name: 'Quitar del plan: rutina.pdf' }))
      expect(screen.queryByLabelText('Agregar un archivo tuyo')).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Subir archivo' })).not.toBeInTheDocument()

      const dialogo = await screen.findByRole('dialog', { name: 'Quitar del plan' })
      await user.click(within(dialogo).getByRole('button', { name: 'Quitar del plan' }))

      await waitFor(() => expect(db.adjuntos).toHaveLength(0))
      expect(await screen.findByText(/Aún no hay archivos en este plan/)).toBeInTheDocument()
      // Quitarlo del plan no elimina el archivo.
      expect(db.documentos[0].eliminado).toBe(false)
    },
  )

  it('los textos de la sección, con el pedido de permiso, no culpan', async () => {
    vincular(archivoDeAna('rutina.pdf'))
    const { user } = renderApp(RUTA, { como: 'estandar' })
    await user.click(
      await screen.findByRole('button', { name: 'Pedir permiso para quitar del plan: rutina.pdf' }),
    )
    await screen.findByText('Pedido en revisión')

    expect(document.body.textContent).not.toMatch(CULPA)
  })
})
