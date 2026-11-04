import { Link, useParams } from 'react-router'
import { formatearFecha } from '@/shared/lib/fechas'
import { EmptyState } from '@/shared/ui/Card'
import { esDocumentoLegal, INFO_LEGAL } from '../legal'

// Términos, privacidad, cookies y aviso de salud, con su versión visible (docs/mejoras §3).
export function LegalPage() {
  const { documento } = useParams()

  if (!esDocumentoLegal(documento)) {
    return (
      <div className="py-12">
        <EmptyState
          nivelTitulo={1}
          icono="alerta"
          titulo="Documento no encontrado"
          descripcion="El documento legal que buscas no existe."
          accion={
            <Link
              to="/"
              className="inline-flex min-h-11 items-center rounded-lg px-3 font-semibold text-acento underline"
            >
              Ir al inicio
            </Link>
          }
        />
      </div>
    )
  }

  const info = INFO_LEGAL[documento]
  return (
    <article className="mx-auto max-w-2xl py-10 sm:py-14">
      <p className="rotulo">
        Versión {info.version} · vigente desde el {formatearFecha(info.vigenteDesde)}
      </p>
      <h1 className="mt-2 text-3xl font-extrabold sm:text-[2rem]">{info.titulo}</h1>
      <div className="mt-8 space-y-8">
        {info.secciones.map((s) => (
          <section key={s.titulo}>
            <h2 className="text-lg font-bold">{s.titulo}</h2>
            {s.parrafos.map((p) => (
              <p key={p} className="mt-2 text-texto-2">
                {p}
              </p>
            ))}
          </section>
        ))}
      </div>
      <p className="mt-10 rounded-xl bg-superficie-2 px-4 py-3 text-sm text-atenuado">
        Texto de demostración. La versión definitiva la redacta el titular del servicio.
      </p>
    </article>
  )
}
