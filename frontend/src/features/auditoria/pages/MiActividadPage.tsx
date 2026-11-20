import { useRef, useState } from 'react'
import { mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody, EmptyState } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Icono } from '@/shared/ui/Icono'
import { Segmentado } from '@/shared/ui/Segmentado'
import { Esqueleto } from '@/shared/ui/Spinner'
import { useToast } from '@/shared/ui/Toast'
import { useExportarMiActividad, useMiActividad, type FormatoExportacion } from '../api'
import { ListaActividad } from '../components/ListaActividad'
import type { DiasActividad } from '../types'

// Registros por página en pantalla (el servidor admite hasta 200); lo que se guarda en un archivo es TODO el periodo.
export const TAMANO_PAGINA_ACTIVIDAD = 50

// Segmentado trabaja con textos: el valor es el número de días como texto.
const PERIODOS = [
  { valor: '7', etiqueta: 'Últimos 7 días' },
  { valor: '30', etiqueta: 'Últimos 30 días' },
]

// Configuración › Mi actividad (RF-AUD-06, RF-AUD-07): lo que la persona hizo en su cuenta durante la semana o
// el mes, de solo lectura, con la opción de guardarlo (CSV o JSON). Solo ve lo suyo: el servidor filtra por la
// sesión. Nada se borra; esto es una ventana corta sobre el historial completo.
export function MiActividadPage() {
  const [dias, setDias] = useState<DiasActividad>(7)
  const [pagina, setPagina] = useState(1)
  const actividad = useMiActividad(dias, pagina, TAMANO_PAGINA_ACTIVIDAD)
  const exportar = useExportarMiActividad()
  const { notificar } = useToast()
  // Región de estado (siempre montada): anuncia la carga, el recuento o la ausencia de actividad, y recibe
  // el foco al cambiar de página para que el teclado y el lector no se queden al pie de la lista anterior.
  const estado = useRef<HTMLDivElement>(null)

  const eventos = actividad.data?.registros ?? []
  const total = actividad.data?.total ?? 0
  const paginas = Math.max(1, Math.ceil(total / TAMANO_PAGINA_ACTIVIDAD))

  // Si la ventana se encoge (pasa el tiempo) y la página elegida ya no existe, se vuelve a la última.
  if (actividad.isSuccess && !actividad.isPlaceholderData && total > 0 && pagina > paginas) {
    setPagina(paginas)
  }

  function elegirPeriodo(nuevo: DiasActividad) {
    setDias(nuevo)
    setPagina(1)
    exportar.reset()
  }

  function irAPagina(nueva: number) {
    setPagina(nueva)
    exportar.reset()
    estado.current?.scrollIntoView?.({ block: 'start' })
    estado.current?.focus()
  }

  function guardar(formato: FormatoExportacion) {
    exportar.mutate({ dias, formato }, { onSuccess: () => notificar('Se descargó tu archivo.') })
  }
  const guardando = (formato: FormatoExportacion) =>
    exportar.isPending && exportar.variables?.formato === formato
  const sinDatos = !actividad.isSuccess || total === 0

  let textoEstado = null
  if (actividad.isPending) {
    textoEstado = <span className="sr-only">Cargando tu actividad…</span>
  } else if (actividad.isSuccess && total === 0) {
    textoEstado = <span className="sr-only">Sin actividad en los últimos {dias} días.</span>
  } else if (actividad.isSuccess) {
    textoEstado = (
      <>
        {total} {total === 1 ? 'acción' : 'acciones'} en los últimos {dias} días.
      </>
    )
  }

  let contenido
  if (actividad.isPending) {
    contenido = (
      <div className="space-y-3">
        <Esqueleto className="h-14" />
        <Esqueleto className="h-14" />
        <Esqueleto className="h-14" />
      </div>
    )
  } else if (actividad.isError) {
    contenido = (
      <ErrorCarga
        error={actividad.error}
        porDefecto="No se pudo cargar tu actividad."
        alReintentar={() => {
          exportar.reset()
          void actividad.refetch()
        }}
      />
    )
  } else if (total === 0) {
    contenido = (
      <EmptyState
        icono="calendario"
        titulo="No hay actividad en este periodo."
        descripcion={
          dias === 7
            ? 'Prueba con los últimos 30 días.'
            : 'Cuando hagas algo en tu cuenta, aparecerá aquí.'
        }
        accion={
          dias === 7 ? (
            <Button variante="secundario" onClick={() => elegirPeriodo(30)}>
              Ver los últimos 30 días
            </Button>
          ) : undefined
        }
      />
    )
  } else {
    contenido = (
      <>
        <div aria-busy={actividad.isPlaceholderData}>
          <ListaActividad eventos={eventos} />
        </div>
        {paginas > 1 && (
          <nav
            aria-label="Páginas de tu actividad"
            className="mt-4 flex flex-wrap items-center justify-between gap-3"
          >
            <Button
              variante="secundario"
              disabled={pagina <= 1}
              onClick={() => irAPagina(Math.max(1, pagina - 1))}
            >
              Anterior
            </Button>
            <p className="text-sm tabular-nums text-texto-2">
              Página {pagina} de {paginas}
            </p>
            <Button
              variante="secundario"
              disabled={pagina >= paginas}
              onClick={() => irAPagina(Math.min(paginas, pagina + 1))}
            >
              Siguiente
            </Button>
          </nav>
        )}
      </>
    )
  }

  return (
    <div className="space-y-5">
      <Card>
        <CardBody className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <Segmentado
            etiqueta="Periodo"
            opciones={PERIODOS}
            valor={String(dias)}
            alCambiar={(v) => elegirPeriodo(v === '30' ? 30 : 7)}
          />
          <div className="flex flex-wrap gap-2">
            <Button
              variante="secundario"
              disabled={sinDatos || exportar.isPending}
              cargando={guardando('csv')}
              onClick={() => guardar('csv')}
            >
              <Icono nombre="documentos" className="size-4" />
              Descargar para hoja de cálculo (CSV)
            </Button>
            <Button
              variante="secundario"
              disabled={sinDatos || exportar.isPending}
              cargando={guardando('json')}
              onClick={() => guardar('json')}
            >
              <Icono nombre="documentos" className="size-4" />
              Descargar copia completa (JSON)
            </Button>
          </div>
        </CardBody>
      </Card>

      {exportar.isError && (
        <Alert tono="peligro">
          {mensajeDeError(exportar.error, 'No se pudo preparar el archivo.')} Inténtalo de nuevo.
        </Alert>
      )}

      <div
        ref={estado}
        role="status"
        tabIndex={-1}
        className="min-h-5 text-sm font-semibold text-texto-2"
      >
        {textoEstado}
      </div>
      {contenido}

      <p className="flex items-start gap-2 text-sm text-atenuado">
        <Icono nombre="candado" className="mt-0.5 size-4 shrink-0" />
        <span>
          Solo tú ves esta lista. Las horas son las de tu dispositivo; en los archivos van en UTC.
          El equipo administrador puede consultar el historial de auditoría de la cuenta.
        </span>
      </p>
    </div>
  )
}
