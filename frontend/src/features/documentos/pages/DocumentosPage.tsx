import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { EmptyState, PageHeader } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Esqueleto } from '@/shared/ui/Spinner'
import { useToast } from '@/shared/ui/Toast'
import { mensajeDeError } from '@/shared/api/problem'
import { formatearFecha } from '@/shared/lib/fechas'
import { useDescargarDocumento, useDocumentos, useEliminarDocumento, useVinculos } from '../api'
import { BotonEliminarArchivo } from '../components/BotonEliminarArchivo'
import { SubirArchivo } from '../components/SubirArchivo'
import { etiquetaTipo, formatearTamano } from '../formato'
import type { DocumentoDto } from '../types'

// «Mis archivos»: los archivos de la persona. Los puede eliminar cuando quiera; solo los que están en un plan en curso
// piden un permiso (uno por archivo). La lista es lo que ve el servidor para esta persona (RD-06).
export function DocumentosPage() {
  const documentos = useDocumentos()
  const vinculos = useVinculos()

  return (
    <>
      <PageHeader
        titulo="Mis archivos"
        descripcion="Guarda aquí tus archivos. Solo tú y un Administrador, para dar soporte, pueden abrirlos."
      />
      <div className="space-y-6">
        <SubirArchivo />

        {documentos.isPending && <Esqueleto className="h-40" />}
        {documentos.isError && (
          <ErrorCarga
            error={documentos.error}
            porDefecto="No se pudieron cargar tus archivos."
            alReintentar={() => documentos.refetch()}
          />
        )}
        {vinculos.isError && (
          <ErrorCarga
            error={vinculos.error}
            porDefecto="No pudimos comprobar si algún archivo está en un plan."
            alReintentar={() => vinculos.refetch()}
          />
        )}
        {documentos.data &&
          (documentos.data.length === 0 ? (
            <EmptyState
              icono="documentos"
              titulo="Aún no tienes archivos"
              descripcion="Sube uno para guardarlo y, si quieres, adjuntarlo a tu plan."
            />
          ) : (
            <ul aria-label="Tus archivos" className="border-b border-borde">
              {documentos.data.map((d) => (
                <FilaArchivo
                  key={d.id}
                  documento={d}
                  plan={vinculos.data?.find((v) => v.documentoId === d.id)}
                  listo={vinculos.isSuccess}
                />
              ))}
            </ul>
          ))}
      </div>
    </>
  )
}

function FilaArchivo({
  documento,
  plan,
  listo,
}: {
  documento: DocumentoDto
  plan?: { planNombre: string; enCurso: boolean }
  listo: boolean
}) {
  const descargar = useDescargarDocumento()
  const eliminar = useEliminarDocumento()
  const { notificar } = useToast()

  return (
    <li className="flex flex-wrap items-center justify-between gap-x-6 gap-y-2 border-t border-borde py-4">
      <div className="min-w-0">
        <p className="break-words font-semibold">{documento.nombre}</p>
        <p className="text-sm text-atenuado">
          {etiquetaTipo(documento.tipo)} · {formatearTamano(documento.tamanoBytes)} ·{' '}
          {formatearFecha(documento.fechaSubidaUtc)}
        </p>
        {plan && (
          <p className="mt-1">
            <Badge tono={plan.enCurso ? 'info' : 'neutro'}>
              {plan.enCurso ? `En tu plan «${plan.planNombre}»` : `Estuvo en «${plan.planNombre}»`}
            </Badge>
          </p>
        )}
        {plan?.enCurso && (
          <p className="mt-1 text-sm text-atenuado">
            Mientras el plan siga en curso, eliminarlo necesita permiso.
          </p>
        )}
      </div>
      <div className="flex flex-wrap items-center gap-1">
        <Button
          tamano="sm"
          variante="secundario"
          cargando={descargar.isPending}
          aria-label={`Descargar: ${documento.nombre}`}
          onClick={() =>
            descargar.mutate(
              { id: documento.id, nombre: documento.nombre },
              {
                onError: (e) =>
                  notificar(mensajeDeError(e, 'No se pudo descargar el archivo.'), 'peligro'),
              },
            )
          }
        >
          Descargar
        </Button>
        <BotonEliminarArchivo
          documentoId={documento.id}
          nombre={documento.nombre}
          protegido={plan?.enCurso === true}
          listo={listo}
          accion="Eliminar"
          titulo="Eliminar este archivo"
          mensaje={`«${documento.nombre}» dejará de aparecer en tus archivos.`}
          ejecutar={() => eliminar.mutateAsync(documento.id)}
        />
      </div>
    </li>
  )
}
