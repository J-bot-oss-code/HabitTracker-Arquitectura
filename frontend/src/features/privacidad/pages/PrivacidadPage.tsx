import { PanelBaja } from '../components/PanelBaja'
import { PanelConsentimientos } from '../components/PanelConsentimientos'
import { PanelDescarga } from '../components/PanelDescarga'

// Centro de privacidad (especificación §5.5): descargar, decidir y retirarse. Vive en
// Configuración › Privacidad y datos (el título y el menú los pone app/ajustes/AjustesLayout).
// Es de primera clase porque los datos de salud son lo más sensible que guarda la aplicación.
export function PrivacidadPage() {
  return (
    <div className="space-y-6">
      <PanelDescarga />
      <PanelConsentimientos />
      <PanelBaja />
    </div>
  )
}
