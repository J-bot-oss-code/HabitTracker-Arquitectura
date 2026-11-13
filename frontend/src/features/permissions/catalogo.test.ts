import { describe, expect, it } from 'vitest'
import { PERMISO_RECONFIGURACION } from '@/features/habits/types'
import { claveEliminarArchivo } from '@/features/documentos/types'
import { documentoDeClave, esClaveEliminarArchivo, nombrePermiso } from './catalogo'
import { crearSolicitudSchema } from './schemas'

const ID = '0b8e7a52-1c3d-4f6e-8a9b-1c2d3e4f5a01'

describe('nombrePermiso', () => {
  it('nombra el permiso de reconfiguración y no lo rompe el de los archivos', () => {
    expect(nombrePermiso(PERMISO_RECONFIGURACION)).toBe('Reconfiguración avanzada de hábitos')
  })

  it('nombra Documents.DeleteLinked.{id} sea cual sea el documento', () => {
    expect(nombrePermiso(claveEliminarArchivo(ID))).toBe('Eliminar un archivo de un plan en curso')
    expect(nombrePermiso('Documents.DeleteLinked.FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF')).toBe(
      'Eliminar un archivo de un plan en curso',
    )
  })

  it('una clave desconocida se muestra tal cual', () => {
    expect(nombrePermiso('Otra.Cosa')).toBe('Otra.Cosa')
    expect(nombrePermiso('Documents.DeleteLinked.corta')).toBe('Documents.DeleteLinked.corta')
  })
})

describe('documentoDeClave y esClaveEliminarArchivo', () => {
  it('recuperan el id del documento con guiones', () => {
    const clave = claveEliminarArchivo(ID)
    expect(esClaveEliminarArchivo(clave)).toBe(true)
    expect(documentoDeClave(clave)).toBe(ID)
  })

  it('devuelven el id en minúsculas aunque la clave venga en mayúsculas', () => {
    expect(documentoDeClave(`Documents.DeleteLinked.${ID.replaceAll('-', '').toUpperCase()}`)).toBe(
      ID,
    )
  })

  it.each([
    PERMISO_RECONFIGURACION,
    'Documents.DeleteLinked.',
    'Documents.DeleteLinked.xyz',
    `Documents.DeleteLinked.${ID}`,
    `Documents.DeleteLinked.${ID.replaceAll('-', '')}0`,
    `Otro.Prefijo.${ID.replaceAll('-', '')}`,
  ])('no son de un archivo: %s', (clave) => {
    expect(esClaveEliminarArchivo(clave)).toBe(false)
    expect(documentoDeClave(clave)).toBeNull()
  })
})

describe('crearSolicitudSchema (misma regla que CrearPermisoRequest)', () => {
  const error = (permisoSolicitado: string) => {
    const r = crearSolicitudSchema.safeParse({ permisoSolicitado })
    return r.success ? null : r.error.issues[0].message
  }

  it('acepta un nombre con puntos', () => {
    expect(error('Habits.AdvancedReconfig')).toBeNull()
    expect(error(claveEliminarArchivo(ID))).toBeNull()
  })

  it.each(['', 'Habits', 'Habits..X', '1abc.def', 'Habits.Re config', `a.${'b'.repeat(255)}`])(
    'rechaza %j',
    (clave) => {
      expect(error(clave)).not.toBeNull()
    },
  )
})
