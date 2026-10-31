import { env } from '@/shared/config/env'

export function AvisoModoDemo() {
  if (!env.useMocks) return null
  return (
    <div className="bg-superficie-2 px-4 py-2 text-center text-xs text-texto">
      <span className="mr-1.5 rounded bg-acento px-1.5 py-0.5 font-semibold uppercase tracking-wider text-acento-texto">
        Demo
      </span>
      Datos simulados en el navegador. Usuarios: <code>estandar@demo.com</code> y{' '}
      <code>admin@demo.com</code> (contraseña <code>Demo1234</code>).
    </div>
  )
}
