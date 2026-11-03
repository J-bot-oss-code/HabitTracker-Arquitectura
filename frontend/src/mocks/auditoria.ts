import type { AccionAuditoria } from '@/features/auditoria/types'
import { db } from './db'

// Registro de auditoría de la API simulada: solo se inserta, nunca se modifica ni se borra
// (RF-AUD-06). Cada handler de una acción sensible llama a `auditar` (docs/mejoras §6.1).
export function auditar(evento: {
  actor: string
  accion: AccionAuditoria
  entidad: string
  entidadId?: string | null
  valorAnterior?: string | null
  valorNuevo?: string | null
}) {
  db.auditoria.push({
    id: crypto.randomUUID(),
    fecha: new Date().toISOString(),
    entidadId: null,
    valorAnterior: null,
    valorNuevo: null,
    origen: 'Web',
    ...evento,
  })
}
