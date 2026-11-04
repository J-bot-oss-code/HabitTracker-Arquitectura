import { infoActividad, type NivelActividad, type Sexo } from './types'

// Calculadora pública de calorías (especificación §5.1, punto 4). Funciona sin cuenta y no
// guarda nada. Es una estimación (ecuación de Mifflin-St Jeor por el factor de actividad),
// no una indicación médica: la interfaz lo dice junto al resultado.
export interface DatosCalorias {
  edad: number
  sexo: Sexo
  alturaCm: number
  pesoKg: number
  actividad: NivelActividad
}

// Metabolismo basal (kcal/día) según Mifflin-St Jeor.
export function metabolismoBasal({ edad, sexo, alturaCm, pesoKg }: DatosCalorias): number {
  const base = 10 * pesoKg + 6.25 * alturaCm - 5 * edad
  return sexo === 'hombre' ? base + 5 : base - 161
}

// Gasto diario estimado, redondeado a la decena: más precisión sugeriría una exactitud que la
// fórmula no tiene.
export function calcularKcal(datos: DatosCalorias): number {
  const total = metabolismoBasal(datos) * infoActividad[datos.actividad].factor
  return Math.round(total / 10) * 10
}
