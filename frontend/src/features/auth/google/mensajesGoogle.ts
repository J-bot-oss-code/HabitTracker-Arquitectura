import { codigoDeError, esErrorHttp, mensajeDeError } from '@/shared/api/problem'

// Mensajes por el `codigo` estable que devuelve Control de Acceso (README raíz › códigos de error).
const POR_CODIGO: Record<string, string> = {
  token_externo_invalido: 'No pudimos verificar tu cuenta de Google. Inténtalo de nuevo.',
  proveedor_no_disponible:
    'El inicio de sesión con Google no está disponible en este momento. Usa tu correo y contraseña.',
  consentimiento_no_valido:
    'Las versiones de los documentos cambiaron. Recarga la página e inténtalo de nuevo.',
  cuenta_no_activa: 'Tu cuenta todavía no está activada. Usa el código que te enviamos por correo.',
  cuenta_desactivada: 'Tu cuenta fue desactivada por un administrador.',
  cuenta_bloqueada: 'Demasiados intentos fallidos. Tu cuenta está bloqueada 15 minutos.',
  proveedor_ya_vinculado:
    'Esa cuenta de Google ya está vinculada a otra cuenta, o la tuya ya tiene una.',
  ultimo_metodo_acceso:
    'Google es tu única forma de entrar. Crea primero una contraseña con «Recuperar contraseña».',
}

export function mensajeGoogle(error: unknown): string {
  const codigo = codigoDeError(error)
  if (codigo && POR_CODIGO[codigo]) return POR_CODIGO[codigo]
  // Contraseña incorrecta al vincular: el backend responde 401 genérico (no dice qué dato falló).
  if (esErrorHttp(error, 401))
    return 'La contraseña es incorrecta o la cuenta de Google no coincide.'
  return mensajeDeError(error, 'No se pudo completar el inicio con Google.')
}
