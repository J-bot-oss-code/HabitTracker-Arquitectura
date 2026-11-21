import { useEffect, useRef, useState } from 'react'
import { cn } from '@/shared/lib/cn'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { EmptyState, PageHeader } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Segmentado } from '@/shared/ui/Segmentado'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useBandejaSugerencias } from '../api'
import { FilaSugerenciaAdmin } from '../components/FilaSugerenciaAdmin'
import type { EstadoSugerencia } from '../types'

// Mensajes por página (el servidor admite de 1 a 100).
export const TAMANO_PAGINA_BANDEJA = 20

type Filtro = 'todas' | EstadoSugerencia

const VACIO: Record<Filtro, { icono: 'info' | 'check'; titulo: string; descripcion?: string }> = {
  todas: {
    icono: 'info',
    titulo: 'Todavía no hay mensajes',
    descripcion: 'Aquí aparecerán las quejas y propuestas de mejora de las personas.',
  },
  Nueva: { icono: 'check', titulo: 'Estás al día', descripcion: 'No hay mensajes nuevos.' },
  Leida: { icono: 'info', titulo: 'No hay mensajes leídos' },
  Resuelta: { icono: 'info', titulo: 'No hay mensajes resueltos' },
}

// «3 mensajes nuevos», «1 mensaje leído»: el estado del filtro, en singular y en plural.
const CALIFICATIVO: Record<Filtro, [string, string]> = {
  todas: ['', ''],
  Nueva: [' nuevo', ' nuevos'],
  Leida: [' leído', ' leídos'],
  Resuelta: [' resuelto', ' resueltos'],
}

// A dónde va el foco cuando termina un cambio: la fila (o, si ya no está en la lista, la de al lado) o el mensaje de error.
interface FocoPendiente {
  error: boolean
  fila?: string
  alternativa?: string
}

// Quejas y mejoras (Administrador): la bandeja de lo que las personas escriben sobre la web, filtrable por estado y
// paginada. Solo el Administrador la ve (el servidor responde 403 a un Estándar). El texto de cada mensaje es de una
// persona cualquiera: se muestra siempre como texto, nunca como HTML.
export function SugerenciasAdminPage() {
  const [filtro, setFiltro] = useState<Filtro>('todas')
  const [pagina, setPagina] = useState(1)
  // Un objeto nuevo en cada aviso: el efecto del foco se repite aunque el texto sea el mismo.
  const [aviso, setAviso] = useState<{ texto: string; error: boolean } | null>(null)
  const avisoRef = useRef<HTMLDivElement>(null)
  const estado = useRef<HTMLDivElement>(null)
  const listaRef = useRef<HTMLUListElement>(null)
  const foco = useRef<FocoPendiente | null>(null)

  const bandeja = useBandejaSugerencias({
    estado: filtro === 'todas' ? undefined : filtro,
    pagina,
    tamano: TAMANO_PAGINA_BANDEJA,
  })

  const total = bandeja.data?.total ?? 0
  const nuevas = bandeja.data?.nuevas
  const paginas = Math.max(1, Math.ceil(total / TAMANO_PAGINA_BANDEJA))
  const sugerencias = bandeja.data?.sugerencias ?? []

  // Si la última página se vacía (se resolvieron los últimos mensajes de un filtro), se vuelve a la última que queda.
  if (bandeja.isSuccess && !bandeja.isPlaceholderData && total > 0 && pagina > paginas) {
    setPagina(paginas)
  }

  // El botón que se pulsó desaparece (y en «Nuevas» la fila entera): cuando la lista ya se actualizó, el foco va a la fila,
  // a la de al lado si esa ya no está, o a la lista; un error se queda con el foco en su alerta. El aviso de éxito es solo
  // una región viva (no recibe el foco).
  useEffect(() => {
    const pendiente = foco.current
    if (!pendiente || bandeja.isFetching) return
    foco.current = null
    if (pendiente.error) {
      avisoRef.current?.focus()
      return
    }
    const filas = [...(listaRef.current?.children ?? [])] as HTMLElement[]
    const porId = (id?: string) => filas.find((f) => f.dataset.id === id)
    const destino = porId(pendiente.fila) ?? porId(pendiente.alternativa) ?? listaRef.current
    ;(destino ?? estado.current)?.focus()
  }, [aviso, bandeja.isFetching])

  const elegirFiltro = (nuevo: Filtro) => {
    setFiltro(nuevo)
    setPagina(1)
    setAviso(null)
    foco.current = null
  }
  const irAPagina = (nueva: number) => {
    setPagina(nueva)
    setAviso(null)
    foco.current = null
    estado.current?.scrollIntoView?.({ block: 'start' })
    estado.current?.focus()
  }
  const alCambiar = (id: string, texto: string) => {
    const i = sugerencias.findIndex((s) => s.id === id)
    foco.current = {
      error: false,
      fila: id,
      alternativa: (sugerencias[i + 1] ?? sugerencias[i - 1])?.id,
    }
    setAviso({ texto, error: false })
  }
  const alFallar = (mensaje: string) => {
    foco.current = { error: true }
    setAviso({ texto: mensaje, error: true })
  }

  let resumen = null
  if (bandeja.isSuccess) {
    resumen = (
      <>
        {total} {total === 1 ? 'mensaje' : 'mensajes'}
        {CALIFICATIVO[filtro][total === 1 ? 0 : 1]}
      </>
    )
  }

  let contenido
  if (bandeja.isPending) {
    contenido = <CargandoPagina texto="Cargando los mensajes…" />
  } else if (bandeja.isError) {
    contenido = (
      <ErrorCarga
        error={bandeja.error}
        porDefecto="No se pudieron cargar los mensajes."
        alReintentar={() => bandeja.refetch()}
      />
    )
  } else if (sugerencias.length === 0) {
    const v = VACIO[filtro]
    contenido = <EmptyState icono={v.icono} titulo={v.titulo} descripcion={v.descripcion} />
  } else {
    contenido = (
      <>
        <ul
          ref={listaRef}
          tabIndex={-1}
          aria-label="Quejas y mejoras, de la más reciente a la más antigua"
          aria-busy={bandeja.isFetching}
          className={cn(
            'divide-y divide-borde overflow-hidden rounded-2xl border border-borde bg-superficie outline-none focus-visible:outline-2 focus-visible:-outline-offset-2',
            // Solo se atenúa al cambiar de página o de filtro (se ve la lista anterior), no en cada refresco.
            bandeja.isPlaceholderData && 'opacity-60',
          )}
        >
          {sugerencias.map((s) => (
            <FilaSugerenciaAdmin
              key={s.id}
              sugerencia={s}
              alCambiar={alCambiar}
              alFallar={alFallar}
            />
          ))}
        </ul>
        {paginas > 1 && (
          <nav
            aria-label="Páginas de quejas y mejoras"
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
    <>
      <PageHeader
        titulo="Quejas y mejoras"
        descripcion="Lo que las personas escriben sobre la web, de lo más reciente a lo más antiguo. No hay nombre ni correo: el ID sirve para agrupar los mensajes de una misma persona. Al marcar un mensaje, quien lo escribió ve si ya lo leíste o lo resolviste."
      />
      <div className="space-y-4">
        <Segmentado<Filtro>
          etiqueta="Filtrar por estado"
          valor={filtro}
          alCambiar={elegirFiltro}
          opciones={[
            { valor: 'todas', etiqueta: 'Todas' },
            { valor: 'Nueva', etiqueta: 'Nuevas', contador: nuevas },
            { valor: 'Leida', etiqueta: 'Leídas' },
            { valor: 'Resuelta', etiqueta: 'Resueltas' },
          ]}
        />
        <div>
          <div
            ref={estado}
            role="status"
            tabIndex={-1}
            className="min-h-5 scroll-mt-24 text-sm font-semibold tabular-nums text-texto-2 outline-none focus-visible:outline-2 focus-visible:outline-offset-2"
          >
            {resumen}
          </div>
          {/* Región viva siempre montada: el aviso de éxito se anuncia sin mover el foco. */}
          <div role="status" className="text-sm text-atenuado">
            {aviso && !aviso.error ? aviso.texto : null}
          </div>
        </div>
        {aviso?.error && (
          <div
            ref={avisoRef}
            tabIndex={-1}
            className="rounded-lg outline-none focus-visible:outline-2 focus-visible:outline-offset-2"
          >
            <Alert tono="peligro">{aviso.texto}</Alert>
          </div>
        )}
        {contenido}
      </div>
    </>
  )
}
