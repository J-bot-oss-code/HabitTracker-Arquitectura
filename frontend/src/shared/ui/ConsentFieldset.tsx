import type { InputHTMLAttributes, Ref } from 'react'
import { Link } from 'react-router'
import { Casilla } from './Casilla'

type CampoCasilla = Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'children'> & {
  ref?: Ref<HTMLInputElement>
}

// ConsentFieldset de la especificación (§3 y §5.2): casillas separadas, ninguna marcada por
// defecto. Términos + Aviso de salud y Privacidad son obligatorias; los correos, opcionales.
// Los documentos se abren en otra pestaña para no perder lo escrito en el formulario.
export function ConsentFieldset({
  rutas,
  versionTerminos,
  versionPrivacidad,
  terminos,
  privacidad,
  marketing,
  errores = {},
}: {
  // Rutas de los documentos: las define quien conoce las páginas legales, no shared.
  rutas: { terminos: string; salud: string; privacidad: string }
  versionTerminos: string
  versionPrivacidad: string
  terminos: CampoCasilla
  privacidad: CampoCasilla
  marketing?: CampoCasilla
  errores?: { terminos?: string; privacidad?: string }
}) {
  const enlace = 'font-semibold text-acento underline underline-offset-2 hover:text-acento-fuerte'
  return (
    <fieldset className="space-y-1 rounded-xl border border-borde p-4">
      <legend className="px-1 text-sm font-semibold">Consentimientos</legend>
      <Casilla error={errores.terminos} {...terminos}>
        Acepto los{' '}
        <Link to={rutas.terminos} target="_blank" rel="noopener noreferrer" className={enlace}>
          Términos de uso ({versionTerminos})
        </Link>{' '}
        y el{' '}
        <Link to={rutas.salud} target="_blank" rel="noopener noreferrer" className={enlace}>
          Aviso de salud
        </Link>
        . <span className="text-atenuado">(obligatorio)</span>
      </Casilla>
      <Casilla error={errores.privacidad} {...privacidad}>
        Acepto la{' '}
        <Link to={rutas.privacidad} target="_blank" rel="noopener noreferrer" className={enlace}>
          Política de privacidad ({versionPrivacidad})
        </Link>{' '}
        y el tratamiento de mis datos de salud para generar mi plan.{' '}
        <span className="text-atenuado">(obligatorio)</span>
      </Casilla>
      {marketing && (
        <Casilla {...marketing}>
          Quiero recibir correos con consejos y novedades.{' '}
          <span className="text-atenuado">(opcional)</span>
        </Casilla>
      )}
    </fieldset>
  )
}
