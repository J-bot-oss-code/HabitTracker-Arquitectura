import { isRouteErrorResponse, Link, useRouteError } from 'react-router'
import { EmptyState } from '@/shared/ui/Card'

// Límite de errores de todas las rutas. El de React Router por defecto pinta el mensaje y la
// pila (también en producción), así que se reemplaza por uno genérico (RD-08). Cubre un error
// de render inesperado y el fallo al descargar una pantalla perezosa tras un despliegue.
export function ErrorRuta() {
  const error = useRouteError()
  const noEncontrada = isRouteErrorResponse(error) && error.status === 404

  return (
    <main id="contenido" className="mx-auto flex min-h-screen max-w-lg items-center px-4">
      <div className="w-full">
        <EmptyState
          nivelTitulo={1}
          icono="alerta"
          titulo={noEncontrada ? 'Página no encontrada' : 'Algo salió mal'}
          descripcion={
            noEncontrada
              ? 'La dirección no existe.'
              : 'No pudimos mostrar esta página. Recárgala o vuelve al inicio.'
          }
          accion={
            <div className="flex flex-wrap justify-center gap-3">
              {!noEncontrada && (
                <button
                  type="button"
                  onClick={() => window.location.reload()}
                  className="inline-flex min-h-11 items-center rounded-lg px-3 font-semibold text-acento underline"
                >
                  Recargar
                </button>
              )}
              <Link
                to="/"
                className="inline-flex min-h-11 items-center rounded-lg px-3 font-semibold text-acento underline"
              >
                Ir al inicio
              </Link>
            </div>
          }
        />
      </div>
    </main>
  )
}
