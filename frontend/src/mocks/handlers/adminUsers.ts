import { auditar } from '../auditoria'
import { http, HttpResponse } from 'msw'
import { esRol } from '@/shared/auth/roles'
import { CODIGO_RECUPERACION_DEMO, db } from '../db'
import { autenticar, latencia, leerJson, respuestas } from '../http'
import { aDto } from './auth'

const ROLES = ['Administrador' as const]

function usuarioObjetivo(id: string) {
  return db.usuarios.find((u) => u.id === id)
}

export const adminUsersHandlers = [
  http.get('/api/admin/usuarios', async ({ request }) => {
    await latencia()
    const { error } = autenticar(request, ROLES)
    if (error) return error
    // ObtenerTodosAsync arma el DTO solo con id, nombre, correo, rol y estado: el resto va con sus valores por defecto.
    return HttpResponse.json(
      db.usuarios.map((u) => ({
        ...aDto(u),
        requiereAceptarDocumentos: false,
        bajaProgramadaPara: null,
      })),
    )
  }),

  http.patch<{ id: string }>('/api/admin/usuarios/:id/rol', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const body = await leerJson<{ nuevoRol: string }>(request)
    const usuario = usuarioObjetivo(params.id)
    if (!body || !esRol(body.nuevoRol) || !usuario) return respuestas.operacion()
    auditar({
      actor: sesion.usuario,
      accion: 'Cambio de rol',
      entidad: 'Usuario',
      entidadId: usuario.id,
      valorAnterior: usuario.rol,
      valorNuevo: body.nuevoRol,
    })
    usuario.rol = body.nuevoRol
    // RF-CA-21: el cambio de rol revoca las sesiones del usuario.
    usuario.sesionesValidasDesde = Date.now()
    return respuestas.mensaje('Rol actualizado.')
  }),

  http.patch<{ id: string }>('/api/admin/usuarios/:id/estado', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const body = await leerJson<{ nuevoEstado: boolean }>(request)
    const usuario = usuarioObjetivo(params.id)
    if (!body || typeof body.nuevoEstado !== 'boolean' || !usuario) return respuestas.operacion()
    auditar({
      actor: sesion.usuario,
      accion: 'Cambio de estado',
      entidad: 'Usuario',
      entidadId: usuario.id,
      valorAnterior: usuario.activo ? 'Activo' : 'Inactivo',
      valorNuevo: body.nuevoEstado ? 'Activo' : 'Inactivo',
    })
    usuario.activo = body.nuevoEstado
    if (!usuario.activo) usuario.sesionesValidasDesde = Date.now()
    return respuestas.mensaje('Estado actualizado.')
  }),

  http.post<{ id: string }>(
    '/api/admin/usuarios/:id/forzar-restablecimiento',
    async ({ request, params }) => {
      await latencia()
      const { error } = autenticar(request, ROLES)
      if (error) return error
      const usuario = usuarioObjetivo(params.id)
      if (!usuario) return respuestas.operacion()
      // RF-CA-13: la contraseña anterior deja de servir de inmediato.
      usuario.password = null
      usuario.codigoRecuperacion = CODIGO_RECUPERACION_DEMO
      usuario.sesionesValidasDesde = Date.now()
      return respuestas.mensaje('Restablecimiento forzado.')
    },
  ),
]
