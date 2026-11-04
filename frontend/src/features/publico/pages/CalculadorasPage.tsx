import { CalculadoraCalorias } from '../components/CalculadoraCalorias'

// Calculadoras públicas (§4: /calculadoras). Por ahora, la de calorías; IMC, ritmo de carrera
// y agua quedan como evolución (docs/mejoras §3).
export function CalculadorasPage() {
  return <CalculadoraCalorias nivelTitulo={1} />
}
