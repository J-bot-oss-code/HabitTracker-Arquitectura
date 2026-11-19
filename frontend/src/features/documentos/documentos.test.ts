import { beforeEach, describe, expect, it } from 'vitest'
import { nombreDeContentDisposition } from '@/shared/api/http'
import { permissionsApi } from '@/features/permissions/api'
import { db, PLAN_DEMO_ID, sembrarPlanDemo } from '@/mocks/db'
import { iniciarSesionComo } from '@/test/render'
import { adjuntosApi, claveEliminarArchivo, documentosApi, requierePermisoDeArchivo } from './api'
import { TAMANO_MAXIMO_BYTES } from './types'

// El mock de /api/documents y de los adjuntos de plan debe responder como DocumentsController y
// AdjuntosPlanController. Estas pruebas fijan las reglas que las pantallas dan por supuestas.
const archivo = (nombre: string, contenido: BlobPart = 'hola', type = '') =>
  new File([contenido], nombre, { type })

const GUID_INEXISTENTE = '11111111-2222-4333-8444-555555555555'

// Un rechazo de la API: estado y, si lo hay, el código estable del ProblemDetails.
const rechazo = (promesa: Promise<unknown>, status: number, codigo?: string) =>
  expect(promesa).rejects.toMatchObject({
    status,
    ...(codigo ? { problem: { codigo } } : {}),
  })

async function subirComo(quien: 'estandar' | 'admin' | 'otro', nombre = 'plan.pdf') {
  iniciarSesionComo(quien)
  return documentosApi.subir(archivo(nombre))
}

// Ana tiene el plan de ejemplo en curso; deja un archivo suyo vinculado a él.
async function archivoVinculado() {
  const documento = await subirComo('estandar')
  await adjuntosApi.adjuntar(PLAN_DEMO_ID, documento.id)
  return documento
}

// El Administrador aprueba el permiso de eliminar ese archivo, pedido por Ana.
async function concederPermiso(documentoId: string) {
  iniciarSesionComo('estandar')
  const { id } = await permissionsApi.crear({
    permisoSolicitado: claveEliminarArchivo(documentoId),
  })
  iniciarSesionComo('admin')
  await permissionsApi.aprobar(id)
  iniciarSesionComo('estandar')
}

beforeEach(() => {
  sembrarPlanDemo()
  iniciarSesionComo('estandar')
})

describe('claveEliminarArchivo', () => {
  it('es el prefijo y el id del documento sin guiones (PermisosDocumentos.EliminarVinculado)', () => {
    expect(claveEliminarArchivo('0B8E7A52-1C3D-4F6E-8A9B-1C2D3E4F5A01')).toBe(
      'Documents.DeleteLinked.0b8e7a521c3d4f6e8a9b1c2d3e4f5a01',
    )
  })
})

describe('nombreDeContentDisposition', () => {
  it('prefiere filename* (RFC 5987) y recurre a filename', () => {
    expect(
      nombreDeContentDisposition(
        `attachment; filename="a_b.txt"; filename*=UTF-8''a%C3%B1o%20b.txt`,
      ),
    ).toBe('año b.txt')
    expect(nombreDeContentDisposition('attachment; filename=informe.pdf')).toBe('informe.pdf')
    expect(nombreDeContentDisposition('attachment; filename="con espacio.pdf"')).toBe(
      'con espacio.pdf',
    )
  })

  it('sin cabecera o sin nombre devuelve null', () => {
    expect(nombreDeContentDisposition(null)).toBeNull()
    expect(nombreDeContentDisposition('attachment')).toBeNull()
  })
})

describe('subir y listar (DocumentsController)', () => {
  it('201 con el registro: tipo deducido del servidor, dueño del token y fecha UTC con Z', async () => {
    // El tipo que declara el cliente no cuenta: un .txt «text/html» se guarda como text/plain.
    const documento = await documentosApi.subir(archivo('notas.txt', 'abc', 'text/html'))
    expect(documento).toMatchObject({
      nombre: 'notas.txt',
      tipo: 'text/plain',
      tamanoBytes: 3,
      correoUsuario: 'estandar@demo.com',
    })
    expect(documento.id).toMatch(/^[0-9a-f-]{36}$/)
    expect(documento.usuarioId).toBe(db.usuarios[0].id)
    expect(documento.fechaSubidaUtc).toMatch(/Z$/)
    // Los detalles internos del mock no viajan.
    expect(documento).not.toHaveProperty('contenido')
    expect(documento).not.toHaveProperty('eliminado')
  })

  it('acepta justo 10 MB', async () => {
    const documento = await documentosApi.subir(
      archivo('a.pdf', new Uint8Array(TAMANO_MAXIMO_BYTES)),
    )
    expect(documento.tamanoBytes).toBe(TAMANO_MAXIMO_BYTES)
  })

  it('rechaza con 400 lo que pasa de 10 MB, lo vacío y las extensiones fuera de la lista', async () => {
    await rechazo(
      documentosApi.subir(archivo('a.pdf', new Uint8Array(TAMANO_MAXIMO_BYTES + 1))),
      400,
    )
    await rechazo(documentosApi.subir(archivo('a.pdf', '')), 400)
    await rechazo(documentosApi.subir(archivo('virus.exe')), 400)
    await rechazo(documentosApi.subir(archivo('sin-extension')), 400)
    // El mensaje es el del servidor y se puede mostrar tal cual (ArchivoInvalidoException).
    await expect(documentosApi.subir(archivo('virus.exe'))).rejects.toMatchObject({
      problem: { title: 'Archivo no válido.', detail: 'El tipo de archivo no está permitido.' },
    })
    expect(db.documentos).toHaveLength(0)
  })

  it('un cuerpo que supera el límite de Kestrel (11 MB) responde 413', async () => {
    await rechazo(documentosApi.subir(archivo('a.pdf', new Uint8Array(11 * 1024 * 1024 + 1))), 413)
  })

  it('sin el campo "archivo" responde 400', async () => {
    const formulario = new FormData()
    formulario.append('otro', archivo('a.pdf'))
    const respuesta = await fetch(`${window.location.origin}/api/documents`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${iniciarSesionComo('estandar')}` },
      body: formulario,
    })
    expect(respuesta.status).toBe(400)
  })

  it('sin token responde 401', async () => {
    const respuesta = await fetch(`${window.location.origin}/api/documents`)
    expect(respuesta.status).toBe(401)
  })

  it('el Estándar solo lista los suyos; el Administrador, todos; los eliminados no salen', async () => {
    const mio = await subirComo('estandar', 'mio.pdf')
    const eliminado = await subirComo('estandar', 'viejo.pdf')
    await documentosApi.eliminar(eliminado.id)
    await subirComo('otro', 'ajeno.pdf')

    iniciarSesionComo('estandar')
    expect((await documentosApi.listar()).map((d) => d.id)).toEqual([mio.id])
    iniciarSesionComo('admin')
    expect((await documentosApi.listar()).map((d) => d.nombre).sort()).toEqual([
      'ajeno.pdf',
      'mio.pdf',
    ])
  })
})

describe('consultar, descargar y eliminar', () => {
  it('uno ajeno es 403 con ProblemDetails; el Administrador sí lo ve', async () => {
    const documento = await subirComo('otro')
    iniciarSesionComo('estandar')
    await expect(documentosApi.obtener(documento.id)).rejects.toMatchObject({
      status: 403,
      problem: { title: 'No autorizado.' },
    })
    await rechazo(documentosApi.descargar(documento.id), 403)
    await rechazo(documentosApi.eliminar(documento.id), 403)
    iniciarSesionComo('admin')
    expect((await documentosApi.obtener(documento.id)).id).toBe(documento.id)
  })

  it('uno que no existe es 404 con cuerpo; un id que no es GUID, 404 sin cuerpo', async () => {
    await expect(documentosApi.obtener(GUID_INEXISTENTE)).rejects.toMatchObject({
      status: 404,
      problem: { title: 'No encontrado.' },
    })
    await expect(documentosApi.obtener('no-es-guid')).rejects.toMatchObject({
      status: 404,
      problem: null,
    })
  })

  it('descarga los bytes con el nombre del servidor', async () => {
    const documento = await documentosApi.subir(archivo('informe ñ.txt', 'contenido'))
    const descarga = await documentosApi.descargar(documento.id)
    expect(await descarga.blob.text()).toBe('contenido')
    expect(descarga.nombre).toBe('informe ñ.txt')
  })

  it('eliminar es lógico: 204, deja de listarse y después es 404', async () => {
    const documento = await subirComo('estandar')
    await documentosApi.eliminar(documento.id)
    expect(await documentosApi.listar()).toEqual([])
    expect(db.documentos).toHaveLength(1)
    await rechazo(documentosApi.obtener(documento.id), 404)
    await rechazo(documentosApi.descargar(documento.id), 404)
    await rechazo(documentosApi.eliminar(documento.id), 404)
  })
})

describe('archivos de un plan (AdjuntosPlanController)', () => {
  it('adjuntar es 204 y el archivo aparece en el plan y en los vínculos', async () => {
    const documento = await subirComo('estandar')
    await adjuntosApi.adjuntar(PLAN_DEMO_ID, documento.id)

    const delPlan = await adjuntosApi.deUnPlan(PLAN_DEMO_ID)
    expect(delPlan).toHaveLength(1)
    expect(delPlan[0]).toMatchObject({ documentoId: documento.id, nombre: 'plan.pdf' })
    expect(delPlan[0].adjuntadoEnUtc).toMatch(/Z$/)
    expect(await adjuntosApi.vinculos()).toEqual([
      {
        documentoId: documento.id,
        planId: PLAN_DEMO_ID,
        planNombre: db.planesUsuario[0].nombre,
        enCurso: true,
      },
    ])
  })

  it('solo el rol Estándar: el Administrador recibe 403', async () => {
    iniciarSesionComo('admin')
    await rechazo(adjuntosApi.vinculos(), 403)
    await rechazo(adjuntosApi.deUnPlan(PLAN_DEMO_ID), 403)
  })

  it('un plan ajeno o inexistente es 404 plan_usuario_no_encontrado', async () => {
    const documento = await subirComo('otro')
    iniciarSesionComo('otro')
    await rechazo(adjuntosApi.deUnPlan(PLAN_DEMO_ID), 404, 'plan_usuario_no_encontrado')
    await rechazo(
      adjuntosApi.adjuntar(PLAN_DEMO_ID, documento.id),
      404,
      'plan_usuario_no_encontrado',
    )
    await rechazo(adjuntosApi.quitar(PLAN_DEMO_ID, documento.id), 404, 'plan_usuario_no_encontrado')
    iniciarSesionComo('estandar')
    await rechazo(adjuntosApi.deUnPlan(GUID_INEXISTENTE), 404, 'plan_usuario_no_encontrado')
  })

  it('un archivo ajeno, eliminado o inexistente es 404 documento_no_encontrado', async () => {
    const ajeno = await subirComo('otro')
    iniciarSesionComo('estandar')
    await rechazo(adjuntosApi.adjuntar(PLAN_DEMO_ID, ajeno.id), 404, 'documento_no_encontrado')
    await rechazo(
      adjuntosApi.adjuntar(PLAN_DEMO_ID, GUID_INEXISTENTE),
      404,
      'documento_no_encontrado',
    )
    const mio = await documentosApi.subir(archivo('mio.pdf'))
    await documentosApi.eliminar(mio.id)
    await rechazo(adjuntosApi.adjuntar(PLAN_DEMO_ID, mio.id), 404, 'documento_no_encontrado')
    // Quitar uno que no está en el plan también es 404.
    await rechazo(
      adjuntosApi.quitar(PLAN_DEMO_ID, GUID_INEXISTENTE),
      404,
      'documento_no_encontrado',
    )
  })

  it('el mismo archivo dos veces es 400 adjunto_duplicado', async () => {
    const documento = await archivoVinculado()
    await rechazo(adjuntosApi.adjuntar(PLAN_DEMO_ID, documento.id), 400, 'adjunto_duplicado')
  })

  it('un plan admite 10 archivos: el 11.º es 400 limite_adjuntos', async () => {
    for (let i = 0; i < 10; i++) {
      const documento = await documentosApi.subir(archivo(`a${i}.pdf`))
      await adjuntosApi.adjuntar(PLAN_DEMO_ID, documento.id)
    }
    const extra = await documentosApi.subir(archivo('extra.pdf'))
    await rechazo(adjuntosApi.adjuntar(PLAN_DEMO_ID, extra.id), 400, 'limite_adjuntos')
  })

  it('un archivo eliminado deja de contar para los 10 y de verse en el plan', async () => {
    const documentos = []
    for (let i = 0; i < 10; i++) {
      const documento = await documentosApi.subir(archivo(`a${i}.pdf`))
      await adjuntosApi.adjuntar(PLAN_DEMO_ID, documento.id)
      documentos.push(documento)
    }
    // Eliminarlo pide permiso mientras el plan está en curso; con el plan terminado queda libre.
    db.planesUsuario[0].estado = 'Completado'
    await documentosApi.eliminar(documentos[0].id)
    expect(await adjuntosApi.deUnPlan(PLAN_DEMO_ID)).toHaveLength(9)
    db.planesUsuario[0].estado = 'Activo'
    const nuevo = await documentosApi.subir(archivo('nuevo.pdf'))
    await adjuntosApi.adjuntar(PLAN_DEMO_ID, nuevo.id)
    expect(await adjuntosApi.deUnPlan(PLAN_DEMO_ID)).toHaveLength(10)
  })

  it('solo se adjunta a un plan en curso: En pausa sí; Completado o Cancelado, 400 plan_no_en_curso', async () => {
    const documento = await subirComo('estandar')
    db.planesUsuario[0].estado = 'Pausado'
    await adjuntosApi.adjuntar(PLAN_DEMO_ID, documento.id)
    expect(await adjuntosApi.deUnPlan(PLAN_DEMO_ID)).toHaveLength(1)

    const otro = await documentosApi.subir(archivo('otro.pdf'))
    for (const estado of ['Completado', 'Cancelado'] as const) {
      db.planesUsuario[0].estado = estado
      await rechazo(adjuntosApi.adjuntar(PLAN_DEMO_ID, otro.id), 400, 'plan_no_en_curso')
    }
  })
})

describe('eliminar un archivo vinculado a un plan en curso (documento_requiere_permiso)', () => {
  it('el dueño recibe 409 documento_requiere_permiso y el archivo sigue ahí', async () => {
    const documento = await archivoVinculado()
    const error = await documentosApi.eliminar(documento.id).catch((e: unknown) => e)
    expect(requierePermisoDeArchivo(error)).toBe(true)
    expect(error).toMatchObject({ status: 409, problem: { title: 'Se necesita un permiso.' } })
    expect(await documentosApi.listar()).toHaveLength(1)
  })

  it('con el plan En pausa también hace falta el permiso', async () => {
    const documento = await archivoVinculado()
    db.planesUsuario[0].estado = 'Pausado'
    await rechazo(documentosApi.eliminar(documento.id), 409, 'documento_requiere_permiso')
  })

  it('una solicitud Pendiente no basta; con la solicitud Aplicada el 204 llega', async () => {
    const documento = await archivoVinculado()
    await permissionsApi.crear({ permisoSolicitado: claveEliminarArchivo(documento.id) })
    await rechazo(documentosApi.eliminar(documento.id), 409, 'documento_requiere_permiso')

    iniciarSesionComo('admin')
    const solicitud = (await permissionsApi.listar(true)).find(
      (s) => s.permisoSolicitado === claveEliminarArchivo(documento.id),
    )
    await permissionsApi.aprobar(solicitud!.id)
    iniciarSesionComo('estandar')
    await documentosApi.eliminar(documento.id)
    expect(await documentosApi.listar()).toEqual([])
  })

  it('el permiso es de ese archivo: el de otro no sirve', async () => {
    const [a, b] = [await archivoVinculado(), await subirComo('estandar', 'b.pdf')]
    await adjuntosApi.adjuntar(PLAN_DEMO_ID, b.id)
    await concederPermiso(a.id)
    await rechazo(documentosApi.eliminar(b.id), 409, 'documento_requiere_permiso')
    await documentosApi.eliminar(a.id)
  })

  it.each(['Completado', 'Cancelado'] as const)(
    'con el plan %s el archivo se elimina libremente',
    async (estado) => {
      const documento = await archivoVinculado()
      db.planesUsuario[0].estado = estado
      await documentosApi.eliminar(documento.id)
      expect(await documentosApi.listar()).toEqual([])
    },
  )

  it('un archivo sin vínculo se elimina sin permiso', async () => {
    const documento = await subirComo('estandar')
    await documentosApi.eliminar(documento.id)
  })

  it('el Administrador elimina el archivo de otra persona sin pasar por la guarda', async () => {
    const documento = await archivoVinculado()
    iniciarSesionComo('admin')
    await documentosApi.eliminar(documento.id)
    iniciarSesionComo('estandar')
    expect(await documentosApi.listar()).toEqual([])
  })
})

describe('quitar un archivo de un plan en curso', () => {
  it('con el plan en curso y sin permiso es 409 documento_requiere_permiso (no se esquiva quitando el vínculo)', async () => {
    const documento = await archivoVinculado()
    await rechazo(adjuntosApi.quitar(PLAN_DEMO_ID, documento.id), 409, 'documento_requiere_permiso')
    expect(await adjuntosApi.deUnPlan(PLAN_DEMO_ID)).toHaveLength(1)
  })

  it('con el permiso aplicado es 204: el vínculo se va y el archivo sigue en «Mis archivos»', async () => {
    const documento = await archivoVinculado()
    await concederPermiso(documento.id)
    await adjuntosApi.quitar(PLAN_DEMO_ID, documento.id)
    expect(await adjuntosApi.deUnPlan(PLAN_DEMO_ID)).toEqual([])
    expect(await adjuntosApi.vinculos()).toEqual([])
    expect((await documentosApi.listar()).map((d) => d.id)).toEqual([documento.id])
  })

  it('con el plan terminado es libre y los vínculos lo marcan como no en curso', async () => {
    const documento = await archivoVinculado()
    db.planesUsuario[0].estado = 'Completado'
    expect((await adjuntosApi.vinculos())[0].enCurso).toBe(false)
    await adjuntosApi.quitar(PLAN_DEMO_ID, documento.id)
    expect(await adjuntosApi.vinculos()).toEqual([])
  })
})
