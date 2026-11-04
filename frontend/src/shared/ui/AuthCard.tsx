import type { ReactNode } from 'react'
import { Card, CardBody } from './Card'

// Tarjeta de las pantallas de cuenta y del alta (registro, verificación, onboarding). Su título
// admite foco (tabIndex -1) para anunciar el cambio de paso en un asistente.

export function AuthCard({
  titulo,
  descripcion,
  children,
  pie,
  paso,
  total = 5,
}: {
  titulo: string
  // Posición en el alta (§5.2): 1 registro, 2 verificación, 3–5 onboarding.
  paso?: number
  total?: number
  descripcion?: ReactNode
  children: ReactNode
  pie?: ReactNode
}) {
  return (
    <Card className="anim-aparecer w-full max-w-md">
      <CardBody className="space-y-6 p-6 sm:p-8">
        <div>
          {paso !== undefined && (
            <p className="rotulo mb-2">
              Paso {paso} de {total}
            </p>
          )}
          <h1 tabIndex={-1} className="text-[1.75rem] font-extrabold outline-none">
            {titulo}
          </h1>
          {descripcion && <p className="mt-1.5 text-sm text-atenuado">{descripcion}</p>}
        </div>
        {children}
        {pie && <div className="border-t border-borde pt-4 text-sm">{pie}</div>}
      </CardBody>
    </Card>
  )
}

export const claseEnlace =
  'inline-flex min-h-11 items-center font-semibold text-acento underline-offset-4 hover:underline'
