// Forma de los errores del backend (GlobalExceptionMiddleware → ProblemDetails, RFC 7807).
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  // Solo en errores de validación del modelo ([ApiController] → 400).
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | null

  constructor(status: number, problem: ProblemDetails | null) {
    super(problem?.title ?? `Error HTTP ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

const mensajesPorEstado: Record<number, string> = {
  400: 'Los datos enviados no son válidos o la operación no está permitida.',
  401: 'Tu sesión no es válida. Inicia sesión de nuevo.',
  403: 'No tienes permiso para realizar esta acción.',
  404: 'El recurso solicitado no existe.',
  409: 'La operación entra en conflicto con el estado actual.',
  429: 'Demasiadas solicitudes. Espera un momento e inténtalo de nuevo.',
}

// RD-08: al usuario solo le llegan mensajes comprensibles, nunca detalles internos.
// El backend ya envía textos genéricos en title/detail; si no hay, se usa uno por estado.
export function mensajeDeError(
  error: unknown,
  porDefecto = 'Ocurrió un error inesperado.',
): string {
  if (error instanceof ApiError) {
    const validacion = error.problem?.errors
    if (validacion) {
      // Las claves "$..." son errores de conversión JSON de ASP.NET con detalles internos
      // (ruta, línea, posición): no se muestran.
      const primero = Object.entries(validacion)
        .filter(([clave]) => !clave.startsWith('$'))
        .flatMap(([, mensajes]) => mensajes)[0]
      return primero ?? mensajesPorEstado[400]
    }
    if (error.status >= 500) return porDefecto
    return (
      error.problem?.detail || error.problem?.title || mensajesPorEstado[error.status] || porDefecto
    )
  }
  if (error instanceof TypeError) return 'No se pudo contactar con el servidor.'
  return porDefecto
}

export function esErrorHttp(error: unknown, status: number): boolean {
  return error instanceof ApiError && error.status === status
}
