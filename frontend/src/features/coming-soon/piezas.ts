import type { Requisito } from '@/shared/ui/ComingSoon'

// Piezas del Core y del negocio que el backend aún no entrega (Requerimientos_Core_PIII.pdf).
// Cuando una se implemente, se crea su feature y se quita de aquí.
export interface PiezaPendiente {
  clave: string
  titulo: string
  descripcion: string
  entrega: string
  requisitos: Requisito[]
}

export const PIEZAS_PENDIENTES = {
  documentos: {
    clave: 'documentos',
    titulo: 'Documentos',
    descripcion:
      'Sube, lista, descarga y elimina (de forma lógica) documentos. Solo el dueño o un Administrador acceden a cada uno.',
    entrega: 'Pieza 3 · Manejador de documentos (Asignación 3)',
    requisitos: [
      {
        id: 'RF-DOC-01',
        texto: 'Subir un documento registrando nombre, tipo, tamaño, fecha y quién lo subió.',
      },
      { id: 'RF-DOC-02', texto: 'Listar los documentos que el usuario tiene permitido ver.' },
      { id: 'RF-DOC-03', texto: 'Descargar un documento (solo dueño o Administrador).' },
      { id: 'RF-DOC-04', texto: 'Eliminar un documento de forma lógica, no física.' },
      { id: 'RF-NEG-10', texto: 'Adjuntar documentos a un hábito o meta (opcional).' },
    ],
  },
  notificaciones: {
    clave: 'notificaciones',
    titulo: 'Notificaciones',
    descripcion:
      'Bandeja interna con notificaciones leídas y no leídas, entregadas también por correo, con opción de desactivar el canal de correo.',
    entrega: 'Pieza 4 · Notificaciones y cola de correos (Prácticas 4 y 5)',
    requisitos: [
      { id: 'RF-NOT-01', texto: 'Crear una solicitud de permiso notifica a los Administradores.' },
      {
        id: 'RF-NOT-02',
        texto: 'Resolver una solicitud notifica al solicitante, con el motivo si se rechaza.',
      },
      {
        id: 'RF-NOT-04',
        texto: 'Consultar las notificaciones propias, distinguiendo leídas de no leídas.',
      },
      { id: 'RF-NOT-05', texto: 'Marcar una notificación como leída.' },
      { id: 'RF-NOT-07', texto: 'Desactivar el canal de correo sin perder el canal interno.' },
      {
        id: 'RF-NEG-07',
        texto: 'Un evento del módulo de hábitos dispara una notificación propia.',
      },
    ],
  },
  colaCorreos: {
    clave: 'cola-correos',
    titulo: 'Cola de correos',
    descripcion:
      'Consulta del estado de cada correo encolado: pendiente, enviado o fallido, con intentos, fechas y último error.',
    entrega: 'Pieza 4 · Cola de correos (Práctica 5)',
    requisitos: [
      {
        id: 'RF-NOT-11',
        texto: 'Cada correo registra su estado, número de intentos, fechas y último error.',
      },
      { id: 'RF-NOT-10', texto: 'Un correo que falla se reintenta un número limitado de veces.' },
    ],
  },
  reportes: {
    clave: 'reportes',
    titulo: 'Reportes',
    descripcion:
      'Reportes con agregación real (agrupaciones, totales o promedios) filtrables por rango de fechas y respetando el rol de quien consulta.',
    entrega: 'Pieza 5 · Reportes con agregación (Práctica 5)',
    requisitos: [
      { id: 'RF-REP-01', texto: 'Solicitudes de permiso por estado y por período.' },
      {
        id: 'RF-REP-02',
        texto: 'Reporte del módulo de hábitos que responde una pregunta real del dominio.',
      },
      { id: 'RF-REP-04', texto: 'Un usuario Estándar no obtiene datos de otros usuarios.' },
      { id: 'RF-REP-05', texto: 'Filtro por rango de fechas.' },
      {
        id: 'RF-NEG-08',
        texto: 'El módulo de hábitos alimenta al menos un reporte con agregación.',
      },
    ],
  },
  auditoria: {
    clave: 'auditoria',
    titulo: 'Auditoría',
    descripcion:
      'Registro inmutable de quién hizo qué, sobre qué entidad, cuándo, y con qué valor anterior y nuevo. Solo para Administradores.',
    entrega: 'Pieza 6 · Auditoría (Asignación 4)',
    requisitos: [
      {
        id: 'RF-AUD-01',
        texto: 'Usuario, acción, entidad, identificador, fecha, valor anterior y nuevo.',
      },
      { id: 'RF-AUD-02', texto: 'Se audita toda resolución de solicitud de permiso.' },
      { id: 'RF-AUD-04', texto: 'Se audita todo cambio de rol.' },
      { id: 'RF-AUD-06', texto: 'El registro no se modifica ni se elimina desde la aplicación.' },
      { id: 'RF-AUD-07', texto: 'Consulta filtrable por usuario, entidad y rango de fechas.' },
    ],
  },
} satisfies Record<string, PiezaPendiente>
