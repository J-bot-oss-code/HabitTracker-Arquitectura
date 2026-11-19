import { useState } from 'react'
import { BotonEliminarArchivo } from '@/features/documentos/components/BotonEliminarArchivo'
import { SubirArchivo } from '@/features/documentos/components/SubirArchivo'
import { etiquetaTipo, formatearTamano } from '@/features/documentos/formato'
import {
  useAdjuntarArchivo,
  useAdjuntosDePlan,
  useDescargarDocumento,
  useDocumentos,
  useQuitarAdjunto,
} from '@/features/documentos/api'
import { mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Esqueleto } from '@/shared/ui/Spinner'
import { Select } from '@/shared/ui/Field'
import { useToast } from '@/shared/ui/Toast'
import { Link } from 'react-router'

// «Archivos del plan»: los de «Mis archivos» que la persona vincula a este plan (la rutina del entrenador, una indicación). Mientras
// el plan está en curso, quitar un archivo del plan pide el mismo permiso que eliminarlo: si no, sería la forma de esquivarlo.
export function ArchivosDelPlan({ planId, enCurso }: { planId: string; enCurso: boolean }) {
  const adjuntos = useAdjuntosDePlan(planId)
  const documentos = useDocumentos(enCurso)
  const adjuntar = useAdjuntarArchivo(planId)
  const quitar = useQuitarAdjunto(planId)
  const descargar = useDescargarDocumento()
  const { notificar } = useToast()
  const [elegido, setElegido] = useState('')

  const disponibles = (documentos.data ?? []).filter(
    (d) => !(adjuntos.data ?? []).some((a) => a.documentoId === d.id),
  )

  function vincular(id: string) {
    adjuntar.mutate(id, {
      onSuccess: () => {
        setElegido('')
        notificar('Archivo agregado al plan.')
      },
    })
  }

  return (
    <Card>
      <CardBody className="space-y-4">
        <h2 className="text-lg font-bold">Archivos del plan</h2>

        {adjuntos.isPending && <Esqueleto className="h-16" />}
        {adjuntos.isError && (
          <ErrorCarga
            error={adjuntos.error}
            porDefecto="No se pudieron cargar los archivos del plan."
            alReintentar={() => adjuntos.refetch()}
          />
        )}
        {adjuntos.data && adjuntos.data.length === 0 && (
          <p className="text-sm text-atenuado">
            Aún no hay archivos en este plan.
            {enCurso &&
              ' Agrega la rutina que te dieron o una indicación que quieras tener a mano.'}
          </p>
        )}
        {adjuntos.data && adjuntos.data.length > 0 && (
          <ul aria-label="Archivos del plan" className="divide-y divide-borde">
            {adjuntos.data.map((a) => (
              <li
                key={a.documentoId}
                className="flex flex-wrap items-center justify-between gap-2 py-3"
              >
                <div className="min-w-0">
                  <p className="break-words font-semibold">{a.nombre}</p>
                  <p className="text-sm text-atenuado">
                    {etiquetaTipo(a.tipo)} · {formatearTamano(a.tamanoBytes)}
                  </p>
                </div>
                <div className="flex flex-wrap items-center gap-1">
                  <Button
                    tamano="sm"
                    variante="secundario"
                    aria-label={`Descargar: ${a.nombre}`}
                    onClick={() =>
                      descargar.mutate(
                        { id: a.documentoId, nombre: a.nombre },
                        {
                          onError: (e) =>
                            notificar(
                              mensajeDeError(e, 'No se pudo descargar el archivo.'),
                              'peligro',
                            ),
                        },
                      )
                    }
                  >
                    Descargar
                  </Button>
                  <BotonEliminarArchivo
                    documentoId={a.documentoId}
                    nombre={a.nombre}
                    protegido={enCurso}
                    accion="Quitar del plan"
                    titulo="Quitar del plan"
                    mensaje={`«${a.nombre}» dejará de estar en este plan. Sigue en tus archivos.`}
                    ejecutar={() => quitar.mutateAsync(a.documentoId)}
                  />
                </div>
              </li>
            ))}
          </ul>
        )}

        {enCurso && (
          <div className="space-y-3 border-t border-borde pt-4">
            {disponibles.length > 0 && (
              <div className="flex flex-wrap items-end gap-2">
                <div className="min-w-56 flex-1">
                  <Select
                    etiqueta="Agregar un archivo tuyo"
                    value={elegido}
                    onChange={(e) => setElegido(e.target.value)}
                  >
                    <option value="">Elige uno…</option>
                    {disponibles.map((d) => (
                      <option key={d.id} value={d.id}>
                        {d.nombre}
                      </option>
                    ))}
                  </Select>
                </div>
                <Button
                  variante="secundario"
                  disabled={!elegido}
                  cargando={adjuntar.isPending}
                  onClick={() => vincular(elegido)}
                >
                  Agregar al plan
                </Button>
              </div>
            )}
            <SubirArchivo alSubir={vincular} />
            {documentos.data && documentos.data.length > 0 && (
              <p className="text-sm text-atenuado">
                Todos tus archivos están en{' '}
                <Link to="/documentos" className="font-semibold text-acento underline">
                  Mis archivos
                </Link>
                .
              </p>
            )}
            {adjuntar.isError && (
              <Alert tono="aviso">
                {mensajeDeError(adjuntar.error, 'No se pudo agregar el archivo.')} Tu archivo sigue
                en Mis archivos.
              </Alert>
            )}
          </div>
        )}
      </CardBody>
    </Card>
  )
}
