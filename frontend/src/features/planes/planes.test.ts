import { describe, expect, it } from 'vitest'
import { solicitudDePerfil } from '@/features/perfil/api'
import type { PerfilDto } from '@/features/perfil/types'
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

describe('solicitudDePerfil', () => {
  const perfil: PerfilDto = {
    nombre: 'Ana Pérez',
    perfilLaboral: 'OficinaDiurna',
    zonaHoraria: 'America/Santo_Domingo',
    avisoSaludAceptado: true,
    horaEntradaTrabajo: '08:00:00',
    horaSalidaTrabajo: '17:00:00',
    horaDespertar: null,
    horaDormir: null,
    edad: 29,
    sexo: 'Mujer',
    alturaCm: 165,
    pesoKg: 68.4,
    actividad: 'Ligera',
    diasPorSemana: 3,
    minutosPorSesion: 30,
    metaSlug: 'primeros-5-km',
    inicioPlan: '2026-10-20T00:00:00Z',
    actualizadoEn: '2026-10-20T10:00:00Z',
  }

  it('reenvía el perfil tal cual, sin lo que fija el servidor, y cambia solo lo pedido', () => {
    const solicitud = solicitudDePerfil(perfil, { metaSlug: 'dormir-mejor' })

    expect(solicitud).toEqual({
      perfilLaboral: 'OficinaDiurna',
      zonaHoraria: 'America/Santo_Domingo',
      avisoSaludAceptado: true,
      horaEntradaTrabajo: '08:00:00',
      horaSalidaTrabajo: '17:00:00',
      horaDespertar: null,
      horaDormir: null,
      edad: 29,
      sexo: 'Mujer',
      alturaCm: 165,
      pesoKg: 68.4,
      actividad: 'Ligera',
      diasPorSemana: 3,
      minutosPorSesion: 30,
      metaSlug: 'dormir-mejor',
    })
    for (const campo of ['nombre', 'inicioPlan', 'actualizadoEn'])
      expect(solicitud).not.toHaveProperty(campo)
  })

  it('no modifica el perfil original', () => {
    solicitudDePerfil(perfil, { metaSlug: null })
    expect(perfil.metaSlug).toBe('primeros-5-km')
    expect(perfil.nombre).toBe('Ana Pérez')
  })
})
