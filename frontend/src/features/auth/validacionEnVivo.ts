import type { ZodType } from 'zod'
import { passwordNuevaSchema } from './schemas'

// Señales de "vas bien" mientras se escribe. Los errores los da el formulario (modo onTouched:
// al salir del campo y, desde ahí, en cada tecla); esto solo confirma lo que ya está correcto.
export function cumple(esquema: ZodType, valor: unknown): boolean {
  return esquema.safeParse(valor).success
}

export function estadoConfirmacion(
  clave: string | undefined,
  confirmacion: string | undefined,
): { valido: boolean; ayuda?: string } {
  if (!confirmacion) return { valido: false }
  if (confirmacion !== clave) return { valido: false, ayuda: 'Aún no coincide con la contraseña.' }
  return { valido: cumple(passwordNuevaSchema, clave), ayuda: 'Las contraseñas coinciden.' }
}
