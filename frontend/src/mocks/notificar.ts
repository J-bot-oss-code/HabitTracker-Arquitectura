import type { TipoNotificacion } from '@/features/notificaciones/types'
import { db, type UsuarioMock } from './db'

// Réplica de NotificacionService.NotificarAsync: una notificación interna por persona (siempre) y, si su
// preferencia de correo está activa (lo está por defecto), un correo Pendiente en la cola (RF-NOT-06 y 07).
export function notificar(
  destinatarios: UsuarioMock[],
  tipo: TipoNotificacion,
  titulo: string,
  mensaje: string,
) {
  const ahora = new Date().toISOString()
  for (const persona of new Map(destinatarios.map((u) => [u.id, u])).values()) {
    db.notificaciones.unshift({
      id: crypto.randomUUID(),
      usuarioId: persona.id,
      tipo,
      titulo,
      mensaje,
      creadaEnUtc: ahora,
      leida: false,
      leidaEnUtc: null,
    })
    if (db.preferenciasCorreo.get(persona.id) ?? true) {
      db.correos.unshift({
        id: crypto.randomUUID(),
        destinatario: persona.correo,
        asunto: titulo,
        estado: 'Pendiente',
        intentos: 0,
        fechaCreacion: ahora,
        fechaUltimoIntento: null,
        fechaEnvio: null,
        ultimoError: null,
      })
    }
  }
}

// IDirectorioUsuarios.ListarAdministradoresActivosAsync y ObtenerPorCorreoAsync (solo cuentas activas).
export const administradoresActivos = () =>
  db.usuarios.filter((u) => u.rol === 'Administrador' && u.activo)

export const activoPorCorreo = (correo: string) =>
  db.usuarios.find((u) => u.activo && u.correo.toLowerCase() === correo.toLowerCase())
