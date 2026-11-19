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
      // Como el dominio (Notificacion): texto recortado a 120 y 500 caracteres.
      titulo: titulo.trim().slice(0, 120),
      mensaje: mensaje.trim().slice(0, 500),
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

// NotificadorUsuarioAdapter.Texto: cada logro se felicita una vez, celebrando la conducta (sin cuerpo ni culpa).
export const TEXTO_LOGRO: Record<string, [string, string]> = {
  PrimerAvance: ['Primer avance', 'Registraste tu primer avance. Empezar es lo más difícil.'],
  PrimeraSemana: [
    'Una semana de constancia',
    'Completaste una semana seguida. Cada día cuenta, también la versión mínima.',
  ],
  VeinticincoAvances: ['25 avances', 'Ya van 25 avances registrados. Se nota tu constancia.'],
  Constancia21: ['21 días de constancia', 'Tres semanas de constancia. Ya es parte de tu rutina.'],
  Constancia30: ['30 días de constancia', 'Un mes entero de constancia. Gracias por quedarte.'],
  MetaCumplida: ['Meta cumplida', 'Cumpliste la meta de un hábito. Buen trabajo.'],
  Regreso: ['Volviste', 'Volver también cuenta. Qué bueno verte de nuevo por aquí.'],
  MitadDelPlan: ['Mitad del plan', 'Llegaste a la mitad de tu plan. Sigue a tu ritmo.'],
  PlanCompletado: ['Plan completado', 'Terminaste tu plan. Gracias por tu constancia.'],
}

// IDirectorioUsuarios.ListarAdministradoresActivosAsync y ObtenerPorCorreoAsync (solo cuentas activas).
export const administradoresActivos = () =>
  db.usuarios.filter((u) => u.rol === 'Administrador' && u.activo)

export const activoPorCorreo = (correo: string) =>
  db.usuarios.find((u) => u.activo && u.correo.toLowerCase() === correo.toLowerCase())
