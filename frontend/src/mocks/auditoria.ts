import { db } from './db'

// Registro de auditoría de la API simulada: solo se inserta, nunca se modifica ni se borra
// (RF-AUD-06). Cada handler de una acción que el backend real audita llama a `auditar`, con el
// texto exacto de ACCIONES_AUDITORIA y todos los campos llenos (el servidor nunca manda null).
export function auditar(evento: {
  actor: { id: string; correo: string }
  accion: string
  entidad: string
  entidadId: string
  valorAnterior: string
  valorNuevo: string
}) {
  const { actor, ...resto } = evento
  db.auditoria.push({
    id: crypto.randomUUID(),
    fecha: new Date().toISOString(),
    usuarioId: actor.id,
    actor: actor.correo,
    ...resto,
  })
}

// El backend real NO audita los registros de cuenta ni los inicios de sesión: aquí solo alimentan las
// «cuentas nuevas» de /api/admin/metrics y no aparecen en GET /api/audit.
export function registrarActividad(tipo: 'Registro' | 'InicioSesion', correo: string) {
  db.actividad.push({ fecha: new Date().toISOString(), actor: correo, tipo })
}
