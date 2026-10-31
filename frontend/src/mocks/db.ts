// Base de datos en memoria de la API simulada. Solo existe en el navegador (modo demo)
// o en las pruebas; nunca sustituye la persistencia real (RD-09 lo cumple el backend).
import type { Periodicidad, EstadoHabito } from '@/features/habits/types'
import type { SolicitudPermisoDto } from '@/features/permissions/types'
import type { Rol } from '@/shared/auth/roles'

export const PASSWORD_DEMO = 'Demo1234'
export const META_DEMO_ID = '3f2b8c1e-6a4d-4e9b-9c7a-1d2e3f4a5b6c'
// Códigos fijos para poder probar los flujos de correo sin servidor SMTP.
export const TOKEN_ACTIVACION_DEMO = 'DEMO-ACTIVACION'
export const CODIGO_RECUPERACION_DEMO = 'DEMO-123456'

export interface UsuarioMock {
  id: string
  nombre: string
  correo: string
  password: string | null
  rol: Rol
  activo: boolean
  intentosFallidos: number
  bloqueadoHasta: number | null
  tokenActivacion: string | null
  codigoRecuperacion: string | null
  // Tokens emitidos antes de este instante (ms) quedan invalidados (RF-CA-12, RF-CA-21).
  sesionesValidasDesde: number
}

export interface RegistroMock {
  id: string
  cantidad: number
  fecha: string
  unidad: string
}

export interface HabitoMock {
  id: string
  usuarioId: string
  nombre: string
  periodicidad: Periodicidad
  cantidadObjetivo: number
  unidad: string
  estado: EstadoHabito
  metaId: string
  fechaInicioCiclo: string
  registros: RegistroMock[]
}

interface Db {
  usuarios: UsuarioMock[]
  habitos: HabitoMock[]
  solicitudes: SolicitudPermisoDto[]
  metas: Set<string>
  tokensRevocados: Set<string>
}

export const db: Db = {
  usuarios: [],
  habitos: [],
  solicitudes: [],
  metas: new Set(),
  tokensRevocados: new Set(),
}

const DIA = 24 * 60 * 60 * 1000

function usuario(
  datos: Partial<UsuarioMock> & Pick<UsuarioMock, 'id' | 'nombre' | 'correo' | 'rol'>,
): UsuarioMock {
  return {
    password: PASSWORD_DEMO,
    activo: true,
    intentosFallidos: 0,
    bloqueadoHasta: null,
    tokenActivacion: null,
    codigoRecuperacion: null,
    sesionesValidasDesde: 0,
    ...datos,
  }
}

function inicioDelDiaUtc(desplazamientoDias: number, ahora = Date.now()): string {
  const fecha = new Date(ahora + desplazamientoDias * DIA)
  return new Date(
    Date.UTC(fecha.getUTCFullYear(), fecha.getUTCMonth(), fecha.getUTCDate()),
  ).toISOString()
}

export function reiniciarDb() {
  const ahora = Date.now()
  db.usuarios = [
    usuario({
      id: '0b8e7a52-1c3d-4f6e-8a9b-1c2d3e4f5a01',
      nombre: 'Ana Estándar',
      correo: 'estandar@demo.com',
      rol: 'Estandar',
    }),
    usuario({
      id: '0b8e7a52-1c3d-4f6e-8a9b-1c2d3e4f5a02',
      nombre: 'Admin Demo',
      correo: 'admin@demo.com',
      rol: 'Administrador',
    }),
    usuario({
      id: '0b8e7a52-1c3d-4f6e-8a9b-1c2d3e4f5a03',
      nombre: 'Luis Otro',
      correo: 'otro@demo.com',
      rol: 'Estandar',
    }),
    usuario({
      id: '0b8e7a52-1c3d-4f6e-8a9b-1c2d3e4f5a04',
      nombre: 'Cuenta Sin Activar',
      correo: 'inactiva@demo.com',
      rol: 'Estandar',
      activo: false,
      tokenActivacion: TOKEN_ACTIVACION_DEMO,
    }),
  ]

  const estandar = db.usuarios[0].id
  const inicioCiclo = new Date(ahora - 10 * DIA).toISOString()
  db.habitos = [
    {
      id: '7c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e01',
      usuarioId: estandar,
      nombre: 'Leer',
      periodicidad: 'Diaria',
      cantidadObjetivo: 20,
      unidad: 'páginas',
      estado: 'Activo',
      metaId: META_DEMO_ID,
      fechaInicioCiclo: inicioCiclo,
      registros: [
        {
          id: '9e8d7c6b-5a49-4382-9100-000000000001',
          cantidad: 25,
          fecha: inicioDelDiaUtc(-2, ahora),
          unidad: 'páginas',
        },
        {
          id: '9e8d7c6b-5a49-4382-9100-000000000002',
          cantidad: 20,
          fecha: inicioDelDiaUtc(-1, ahora),
          unidad: 'páginas',
        },
        {
          id: '9e8d7c6b-5a49-4382-9100-000000000005',
          cantidad: 22,
          fecha: inicioDelDiaUtc(-3, ahora),
          unidad: 'páginas',
        },
        {
          id: '9e8d7c6b-5a49-4382-9100-000000000006',
          cantidad: 18,
          fecha: inicioDelDiaUtc(-4, ahora),
          unidad: 'páginas',
        },
        {
          id: '9e8d7c6b-5a49-4382-9100-000000000003',
          cantidad: 8,
          fecha: inicioDelDiaUtc(0, ahora),
          unidad: 'páginas',
        },
      ],
    },
    {
      id: '7c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e02',
      usuarioId: estandar,
      nombre: 'Correr',
      periodicidad: 'Semanal',
      cantidadObjetivo: 15,
      unidad: 'km',
      estado: 'Planificado',
      metaId: META_DEMO_ID,
      fechaInicioCiclo: inicioCiclo,
      registros: [],
    },
    {
      id: '7c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e03',
      usuarioId: estandar,
      nombre: 'Meditar',
      periodicidad: 'Diaria',
      cantidadObjetivo: 10,
      unidad: 'minutos',
      estado: 'Pausado',
      metaId: META_DEMO_ID,
      fechaInicioCiclo: inicioCiclo,
      registros: [
        {
          id: '9e8d7c6b-5a49-4382-9100-000000000004',
          cantidad: 10,
          fecha: inicioDelDiaUtc(-3, ahora),
          unidad: 'minutos',
        },
      ],
    },
  ]

  db.solicitudes = [
    {
      id: '5a4b3c2d-1e0f-4a9b-8c7d-6e5f4a3b2c01',
      solicitante: 'otro@demo.com',
      permisoSolicitado: 'Habits.AdvancedReconfig',
      estado: 'Pendiente',
      aprobador: null,
      fechaCreacion: new Date(ahora - 2 * DIA).toISOString(),
      fechaResolucion: null,
      motivoRechazo: null,
    },
  ]

  db.metas = new Set([META_DEMO_ID])
  db.tokensRevocados = new Set()
}

reiniciarDb()
