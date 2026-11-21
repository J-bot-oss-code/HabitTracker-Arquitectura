import { Card, CardBody } from '@/shared/ui/Card'
import { FormularioSugerencia } from '../components/FormularioSugerencia'
import { MisSugerencias } from '../components/MisSugerencias'

// Configuración › Quejas y mejoras (solo Estándar): una queja o una propuesta de mejora sobre la web. El h1 y el subtítulo
// son los de Configuración (AjustesLayout, desde el `handle` de la ruta); aquí solo van secciones h2. Un Administrador recibe
// 403 en api/feedback.
export function SugerenciasPage() {
  return (
    <div className="space-y-6">
      <Card>
        <CardBody>
          <FormularioSugerencia />
        </CardBody>
      </Card>
      <MisSugerencias />
    </div>
  )
}
