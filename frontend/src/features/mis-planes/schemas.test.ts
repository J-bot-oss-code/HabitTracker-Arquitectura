import { describe, expect, it } from 'vitest'
import {
  aInscribirseRequest,
  aPausarRequest,
  aRecordatorioRequest,
  horaParaApi,
  inscribirseSchema,
  pausarPlanSchema,
  recordatorioSchema,
} from './schemas'
import { MOTIVOS_PAUSA } from './types'

describe('inscribirseSchema', () => {
  it('basta con el slug: el momento del día y la hora son opcionales', () => {
    const resultado = inscribirseSchema.safeParse({ slug: 'mi-dia-oficina' })
    expect(resultado.success).toBe(true)
    expect(resultado.data).toEqual({ slug: 'mi-dia-oficina' })
  })

  it('acepta un ancla del catálogo y una hora HH:mm', () => {
    const resultado = inscribirseSchema.safeParse({
      slug: 'dormir-mejor',
      ancla: 'AntesDeDormir',
      hora: '21:30',
    })
    expect(resultado.success).toBe(true)
  })

  it('un campo vacío del formulario cuenta como no elegido', () => {
    const resultado = inscribirseSchema.safeParse({ slug: 'dormir-mejor', ancla: '', hora: '' })
    expect(resultado.success).toBe(true)
    expect(resultado.data).toEqual({ slug: 'dormir-mejor', ancla: undefined, hora: undefined })
  })

  it.each([
    ['slug vacío', { slug: '' }],
    ['slug con mayúsculas o espacios', { slug: 'Mi Dia' }],
    ['slug de más de 60 caracteres', { slug: 'a'.repeat(61) }],
    ['ancla desconocida', { slug: 'dormir-mejor', ancla: 'AlAtardecer' }],
    ['hora fuera de rango', { slug: 'dormir-mejor', hora: '25:00' }],
    ['hora sin el cero inicial', { slug: 'dormir-mejor', hora: '7:30' }],
    ['hora con segundos', { slug: 'dormir-mejor', hora: '07:30:00' }],
  ])('rechaza %s', (_caso, datos) => {
    expect(inscribirseSchema.safeParse(datos).success).toBe(false)
  })

  it('la petición lleva la hora como "HH:mm:ss" y omite lo que no se eligió', () => {
    expect(
      aInscribirseRequest({ slug: 'dormir-mejor', ancla: undefined, hora: undefined }),
    ).toEqual({ slug: 'dormir-mejor' })
    expect(
      aInscribirseRequest({ slug: 'dormir-mejor', ancla: 'AntesDeDormir', hora: '21:30' }),
    ).toEqual({ slug: 'dormir-mejor', ancla: 'AntesDeDormir', hora: '21:30:00' })
    expect(horaParaApi('06:05')).toBe('06:05:00')
  })
})

describe('pausarPlanSchema', () => {
  it.each(MOTIVOS_PAUSA)('acepta el motivo %s', (motivoPausa) => {
    expect(pausarPlanSchema.safeParse({ motivoPausa }).success).toBe(true)
  })

  it('el motivo es obligatorio y debe ser uno de los del backend', () => {
    const sinMotivo = pausarPlanSchema.safeParse({})
    expect(sinMotivo.success).toBe(false)
    expect(sinMotivo.error?.issues[0].message).toBe('Elige el motivo de la pausa.')
    expect(pausarPlanSchema.safeParse({ motivoPausa: '' }).success).toBe(false)
    expect(pausarPlanSchema.safeParse({ motivoPausa: 'Pereza' }).success).toBe(false)
  })

  it('la petición es una transición a Pausado con su motivo', () => {
    expect(aPausarRequest({ motivoPausa: 'Viaje' })).toEqual({
      nuevoEstado: 'Pausado',
      motivoPausa: 'Viaje',
    })
  })
})

describe('recordatorioSchema', () => {
  it('activar el recordatorio exige una hora', () => {
    const sinHora = recordatorioSchema.safeParse({ recordatoriosActivos: true, hora: '' })
    expect(sinHora.success).toBe(false)
    expect(sinHora.error?.issues[0]).toMatchObject({
      path: ['hora'],
      message: 'Elige a qué hora quieres el recordatorio.',
    })
    expect(
      recordatorioSchema.safeParse({ recordatoriosActivos: true, hora: '07:15' }).success,
    ).toBe(true)
  })

  it('apagado no necesita hora, pero si viene debe ser HH:mm', () => {
    expect(recordatorioSchema.safeParse({ recordatoriosActivos: false, hora: '' }).success).toBe(
      true,
    )
    expect(
      recordatorioSchema.safeParse({ recordatoriosActivos: false, hora: '24:00' }).success,
    ).toBe(false)
    expect(recordatorioSchema.safeParse({ hora: '07:15' }).success).toBe(false)
  })

  it('la petición lleva la hora como "HH:mm:ss", o null para conservar la del plan', () => {
    expect(aRecordatorioRequest({ recordatoriosActivos: true, hora: '07:15' })).toEqual({
      hora: '07:15:00',
      recordatoriosActivos: true,
    })
    expect(aRecordatorioRequest({ recordatoriosActivos: false, hora: undefined })).toEqual({
      hora: null,
      recordatoriosActivos: false,
    })
  })
})
