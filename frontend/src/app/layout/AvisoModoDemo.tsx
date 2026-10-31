import { env } from '@/shared/config/env'

export function AvisoModoDemo() {
  if (!env.useMocks) return null
  return (
    <div className="bg-amber-100 px-4 py-1.5 text-center text-xs text-amber-900 dark:bg-amber-950 dark:text-amber-200">
      Modo demostración: datos simulados en el navegador. Usuarios: <code>estandar@demo.com</code> y{' '}
      <code>admin@demo.com</code> (contraseña <code>Demo1234</code>).
    </div>
  )
}
