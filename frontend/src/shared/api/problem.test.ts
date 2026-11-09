import { describe, expect, it } from 'vitest'
import { ApiError, esNoDisponible, MENSAJE_CONSENTIMIENTO_SALUD, mensajeDeError } from './problem'

describe('mensajeDeError (RD-08)', () => {
  it('usa el primer error de validación del modelo', () => {
    const error = new ApiError(400, {
      title: 'Validación',
      errors: { Nombre: ['El nombre es obligatorio.'] },
    })
    expect(mensajeDeError(error)).toBe('El nombre es obligatorio.')
  })

  it('usa el detalle genérico que envía el backend', () => {
    const error = new ApiError(400, {
      title: 'La operación no pudo completarse.',
      detail: 'No permitida.',
    })
    expect(mensajeDeError(error)).toBe('No permitida.')
  })

  it('oculta los errores de conversión JSON de ASP.NET ($...)', () => {
    const error = new ApiError(400, {
      errors: { '$.cantidad': ['Path: $.cantidad | LineNumber: 0 | BytePositionInLine: 14.'] },
    })
    expect(mensajeDeError(error)).toBe(
      'Los datos enviados no son válidos o la operación no está permitida.',
    )
  })

  it('nunca muestra el contenido de un 500', () => {
    const error = new ApiError(500, {
      title: 'x',
      detail: 'at WebApi.Controllers.HabitsController',
    })
    expect(mensajeDeError(error, 'Algo falló.')).toBe('Algo falló.')
  })

  it('traduce un 403 sin cuerpo a un mensaje comprensible', () => {
    expect(mensajeDeError(new ApiError(403, null))).toBe(
      'No tienes permiso para realizar esta acción.',
    )
  })

  it('reconoce un fallo de red', () => {
    expect(mensajeDeError(new TypeError('Failed to fetch'))).toBe(
      'No se pudo contactar con el servidor.',
    )
  })
})

describe('mensajeDeError con datos de salud sin permiso', () => {
  it('el 409 consentimiento_salud_requerido dice dónde dar el permiso', () => {
    const error = new ApiError(409, {
      title: 'Falta el consentimiento.',
      detail: 'Falta el consentimiento para usar tus datos de salud.',
      codigo: 'consentimiento_salud_requerido',
    })
    expect(mensajeDeError(error)).toBe(MENSAJE_CONSENTIMIENTO_SALUD)
    expect(MENSAJE_CONSENTIMIENTO_SALUD).toMatch(/Configuración › Perfil y salud/)
  })
})

describe('esNoDisponible (contratos propuestos)', () => {
  it('un 404 sin cuerpo es un endpoint que el backend aún no tiene', () => {
    expect(esNoDisponible(new ApiError(404, null))).toBe(true)
  })

  it('un 405 también: la ruta existe con otro método', () => {
    expect(esNoDisponible(new ApiError(405, null))).toBe(true)
  })

  it('un 404 con ProblemDetails es un recurso inexistente, no una pieza pendiente', () => {
    expect(esNoDisponible(new ApiError(404, { title: 'No encontrado.' }))).toBe(false)
  })

  it('otros errores no cuentan', () => {
    expect(esNoDisponible(new ApiError(403, null))).toBe(false)
    expect(esNoDisponible(new TypeError('fetch failed'))).toBe(false)
  })
})
