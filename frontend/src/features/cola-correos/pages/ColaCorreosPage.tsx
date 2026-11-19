import { useState } from 'react'
import { cn } from '@/shared/lib/cn'
import { esErrorHttp, mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { EmptyState, PageHeader } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Segmentado } from '@/shared/ui/Segmentado'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useColaCorreos, useProcesarCola } from '../api'
import { TablaCorreos } from '../components/TablaCorreos'
import {
  ESTADOS_CORREO,
  MAX_INTENTOS_CORREO,
  type EstadoCorreo,
  type ResultadoColaDto,
} from '../types'

type Filtro = 'Todos' | EstadoCorreo

const OPCIONES = (['Todos', ...ESTADOS_CORREO] as Filtro[]).map((valor) => ({
  valor,
  etiqueta: valor,
}))

const plural = (n: number, uno: string, varios: string) => `${n} ${n === 1 ? uno : varios}`

// Solo se nombran las cifras que no son cero. «No había nada que procesar» no afirma que la cola esté vacía: un correo
// con fallo puede estar esperando su reintento.
function describirResultado(r: ResultadoColaDto): string {
  const partes = [
    r.enviados > 0 && plural(r.enviados, 'enviado', 'enviados'),
    r.fallidos > 0 && `${r.fallidos} con fallo`,
    r.recuperados > 0 &&
      `${r.recuperados} que se ${r.recuperados === 1 ? 'había quedado' : 'habían quedado'} a medias y ${r.recuperados === 1 ? 'vuelve' : 'vuelven'} a la cola`,
  ].filter((p): p is string => p !== false)
  if (partes.length === 0) {
    return 'No había nada que procesar en este momento. Un correo con fallo espera unos minutos antes de reintentarse.'
  }
  const ultima = partes.pop()
  return `${partes.length > 0 ? `${partes.join(', ')} y ` : ''}${ultima}.`
}

function vacio(filtro: Filtro) {
  if (filtro === 'Todos') return { titulo: 'No hay correos en la cola' }
  if (filtro === 'Fallido')
    return { titulo: 'No hay correos fallidos', descripcion: 'Todo va bien.' }
  return { titulo: `No hay correos con estado ${filtro}` }
}

// Cola de correos (RF-NOT-10 y 11, solo Administrador): el estado de cada correo, sin su cuerpo, que puede llevar códigos
// de activación. La autorización es del servidor (RD-06): ocultar la ruta a un Estándar solo orienta.
export function ColaCorreosPage() {
  const [filtro, setFiltro] = useState<Filtro>('Todos')
  const correos = useColaCorreos(filtro === 'Todos' ? undefined : filtro)
  const procesar = useProcesarCola()

  return (
    <>
      <PageHeader
        titulo="Cola de correos"
        descripcion="Los correos que la aplicación envía y cómo va cada uno. El contenido de los correos no se muestra. «Procesar ahora» envía ya los correos pendientes, sin esperar al servicio en segundo plano."
        acciones={
          <Button
            variante="secundario"
            cargando={procesar.isPending}
            onClick={() => procesar.mutate()}
          >
            Procesar ahora
          </Button>
        }
      />
      <div className="space-y-4">
        {procesar.isSuccess && (
          <Alert tono={procesar.data.fallidos > 0 ? 'aviso' : 'exito'} titulo="Cola procesada">
            {describirResultado(procesar.data)}
          </Alert>
        )}
        {procesar.isError && (
          <Alert tono="peligro">
            {mensajeDeError(procesar.error, 'No se pudo procesar la cola.')}
          </Alert>
        )}

        <Segmentado<Filtro>
          etiqueta="Filtrar por estado"
          valor={filtro}
          alCambiar={setFiltro}
          opciones={OPCIONES}
        />
        <p className="text-sm text-atenuado">
          Del más reciente al más antiguo, hasta 200 correos.{' '}
          {`Un correo con fallo se reintenta hasta ${MAX_INTENTOS_CORREO} veces.`}
        </p>

        {correos.isPending ? (
          <CargandoPagina texto="Cargando la cola de correos…" />
        ) : correos.isError ? (
          esErrorHttp(correos.error, 403) ? (
            <Alert tono="aviso" titulo="Solo para Administradores">
              La cola de correos solo la puede ver un Administrador.
            </Alert>
          ) : (
            <ErrorCarga
              error={correos.error}
              porDefecto="No se pudo cargar la cola de correos."
              alReintentar={() => correos.refetch()}
            />
          )
        ) : correos.data.length === 0 ? (
          <EmptyState
            icono="correo"
            descripcion="Un correo aparece aquí cuando un aviso se envía a alguien que tiene el correo activado."
            {...vacio(filtro)}
          />
        ) : (
          <div aria-busy={correos.isFetching} className={cn(correos.isFetching && 'opacity-60')}>
            <TablaCorreos correos={correos.data} />
          </div>
        )}
      </div>
    </>
  )
}
