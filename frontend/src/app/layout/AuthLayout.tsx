import { Outlet } from 'react-router'
import { AvisoModoDemo } from './AvisoModoDemo'
import { Marca } from './Marca'

export function AuthLayout() {
  return (
    <div className="flex min-h-screen flex-col">
      <AvisoModoDemo />
      <main className="flex flex-1 flex-col items-center justify-center gap-8 px-4 py-10">
        <Marca />
        <Outlet />
      </main>
    </div>
  )
}
