import { SelectorTemaTarjetas } from '@/shared/theme/SelectorTema'
import { Card, CardBody } from '@/shared/ui/Card'

// Configuración › Apariencia. El tema se guarda en este navegador (shared/theme).
export function AparienciaPage() {
  return (
    <Card>
      <CardBody>
        <h2 className="text-lg font-bold">Tema</h2>
        <p className="mb-4 mt-1 text-sm text-atenuado">
          Se guarda en este navegador. «Sistema» sigue el modo claro u oscuro de tu dispositivo.
        </p>
        <SelectorTemaTarjetas />
      </CardBody>
    </Card>
  )
}
