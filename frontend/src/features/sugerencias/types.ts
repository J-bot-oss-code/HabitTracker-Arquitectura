// Contrato de SugerenciasController (api/feedback y api/admin/feedback). Ver Sugerencias en HabitTracker.Application y
// la regla del texto en HabitTracker.Domain/Sugerencias/Sugerencia.cs. Los enums viajan como texto.
export const TIPOS_SUGERENCIA = ['Queja', 'Mejora'] as const
export type TipoSugerencia = (typeof TIPOS_SUGERENCIA)[number]

export const ESTADOS_SUGERENCIA = ['Nueva', 'Leida', 'Resuelta'] as const
export type EstadoSugerencia = (typeof ESTADOS_SUGERENCIA)[number]

// Sugerencia.LongitudMinima / LongitudMaxima (el texto se recorta antes de medirlo).
export const LONGITUD_MINIMA = 10
export const LONGITUD_MAXIMA = 2000
// SugerenciasService.MaximoPorDia (en 24 horas) y TamanoMaximo de la página del Administrador.
export const MAXIMO_POR_DIA = 5
export const TAMANO_MAXIMO = 100
export const CODIGO_LIMITE = 'limite_sugerencias'

// Lo que ve la persona que escribió.
export interface SugerenciaDto {
  id: string
  tipo: TipoSugerencia
  texto: string
  // UTC con «Z».
  creadaEn: string
  estado: EstadoSugerencia
}

// Lo que ve el Administrador: lo mismo, más el identificador de quien la envió (no hay correo ni nombre).
export interface SugerenciaAdminDto extends SugerenciaDto {
  usuarioId: string
}

// `nuevas` cuenta las que siguen sin leer con cualquier filtro: es el número del aviso del menú.
export interface PaginaSugerenciasDto {
  total: number
  pagina: number
  tamano: number
  nuevas: number
  sugerencias: SugerenciaAdminDto[]
}

export interface EnviarSugerenciaRequest {
  tipo: TipoSugerencia
  texto: string
}

// El Administrador solo puede pedir «Leida» o «Resuelta»: nada vuelve a «Nueva».
export type EstadoDestino = Exclude<EstadoSugerencia, 'Nueva'>

export interface CambiarEstadoSugerenciaRequest {
  estado: EstadoDestino
}

export interface FiltroBandeja {
  estado?: EstadoSugerencia
  pagina: number
  tamano: number
}

export const ROTULO_TIPO: Record<TipoSugerencia, string> = {
  Queja: 'Queja',
  Mejora: 'Propuesta de mejora',
}

// Como lo lee quien escribió: «Nueva» es «Recibida» (ya llegó; todavía nadie la abrió).
export const ESTADO_PARA_PERSONA: Record<EstadoSugerencia, string> = {
  Nueva: 'Recibida',
  Leida: 'Leída',
  Resuelta: 'Resuelta',
}

export const ESTADO_PARA_ADMIN: Record<EstadoSugerencia, string> = {
  Nueva: 'Nueva',
  Leida: 'Leída',
  Resuelta: 'Resuelta',
}
