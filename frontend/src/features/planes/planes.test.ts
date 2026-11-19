import { describe, expect, it } from 'vitest'
import {
  DESCRIPCION_NIVEL,
  ETIQUETA_ANCLA,
  ETIQUETA_AREA,
  ETIQUETA_INTENSIDAD,
  ETIQUETA_NIVEL,
  ETIQUETA_NIVEL_PLURAL,
  ETIQUETA_SEXO_PLAN,
  duracionPlan,
} from './etiquetas'
import {
  ANCLAS,
  AREAS_PLAN,
  INTENSIDADES_PLAN,
  NIVELES_PLAN,
  SEXOS_PLAN,
  SLUGS_MI_DIA,
  esMiDia,
  esSlugPlan,
} from './types'

describe('slugs de los planes', () => {
  it('acepta el formato del catálogo y descarta lo demás (la URL y el almacenamiento son editables)', () => {
    expect(esSlugPlan('primeros-5-km')).toBe(true)
    expect(esSlugPlan('mi-dia-oficina')).toBe(true)
    for (const malo of [
      '<script>',
      'Con Mayusculas',
      '-inicio',
      'fin-',
      'dos--guiones',
      'Mi-Dia',
      'MI-DIA-OFICINA',
      '',
      'a b',
    ]) {
      expect(esSlugPlan(malo)).toBe(false)
    }
    expect(esSlugPlan(null)).toBe(false)
    expect(esSlugPlan(undefined)).toBe(false)
    expect(esSlugPlan(42)).toBe(false)
    // Igual que el backend: 60 caracteres como máximo.
    expect(esSlugPlan('a'.repeat(60))).toBe(true)
    expect(esSlugPlan('a'.repeat(61))).toBe(false)
  })

  it('"Mi día de trabajo saludable" tiene una variante por jornada', () => {
    expect(SLUGS_MI_DIA).toEqual([
      'mi-dia-oficina',
      'mi-dia-turno-nocturno',
      'mi-dia-trabajo-fisico',
    ])
    for (const slug of SLUGS_MI_DIA) expect(esMiDia(slug)).toBe(true)
    expect(esMiDia('dormir-mejor')).toBe(false)
  })
})

describe('textos en español de cada enum', () => {
  it('no falta ninguno ni queda vacío (el contrato y las etiquetas van juntos)', () => {
    const tablas: [readonly string[], Record<string, string>][] = [
      [AREAS_PLAN, ETIQUETA_AREA],
      [NIVELES_PLAN, ETIQUETA_NIVEL],
      [NIVELES_PLAN, ETIQUETA_NIVEL_PLURAL],
      [NIVELES_PLAN, DESCRIPCION_NIVEL],
      [INTENSIDADES_PLAN, ETIQUETA_INTENSIDAD],
      [SEXOS_PLAN, ETIQUETA_SEXO_PLAN],
      [ANCLAS, ETIQUETA_ANCLA],
    ]
    for (const [claves, etiquetas] of tablas) {
      expect(Object.keys(etiquetas).sort()).toEqual([...claves].sort())
      for (const texto of Object.values(etiquetas)) expect(texto.trim()).not.toBe('')
    }
  })

  it('la duración se dice en semanas', () => {
    expect(duracionPlan(1)).toBe('1 semana')
    expect(duracionPlan(3)).toBe('3 semanas')
  })
})
