// Contrato de ProgressController (/api/progress/weights y /measurements, solo Estándar). Son datos de
// salud: el servidor no los guarda sin el consentimiento datosSalud (409 consentimiento_salud_requerido).
// Un peso y una medida (por zona) por día UTC: el nuevo reemplaza al anterior.

export interface PesoDto {
  id: string
  // Instante UTC (RD-11) en que se registró el peso.
  fecha: string
  pesoKg: number
}

export interface PesosDto {
  // Peso al que quiere llegar; null si no lo definió. Dibuja la línea del plan.
  objetivoKg: number | null
  registros: PesoDto[]
}

export interface RegistrarPesoRequest {
  fecha: string
  pesoKg: number
  // Si se envía, reemplaza el objetivo; null lo quita; sin la propiedad lo deja como estaba.
  objetivoKg?: number | null
}

export const ZONAS = ['Cintura', 'Pecho', 'Brazo', 'Grasa'] as const
export type Zona = (typeof ZONAS)[number]

export const infoZona: Record<Zona, { etiqueta: string; unidad: string }> = {
  Cintura: { etiqueta: 'Cintura', unidad: 'cm' },
  Pecho: { etiqueta: 'Pecho', unidad: 'cm' },
  Brazo: { etiqueta: 'Brazo', unidad: 'cm' },
  Grasa: { etiqueta: '% grasa', unidad: '%' },
}

export interface MedicionDto {
  id: string
  fecha: string
  zona: Zona
  valor: number
}

export interface RegistrarMedicionRequest {
  fecha: string
  zona: Zona
  valor: number
}

export const PERIODOS = ['4semanas', '3meses', 'todo'] as const
export type Periodo = (typeof PERIODOS)[number]
