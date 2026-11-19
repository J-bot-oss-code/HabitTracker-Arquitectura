import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { esNoDisponible } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody, EmptyState, PageHeader } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Input, Select } from '@/shared/ui/Field'
import { Icono } from '@/shared/ui/Icono'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useAuditoria } from '../api'
import { TablaAuditoria } from '../components/TablaAuditoria'
import { auditoriaACsv, descargarCsv, nombreCsv } from '../csv'
import { filtroAuditoriaSchema, type FiltroAuditoriaForm } from '../schemas'
import { ACCIONES_AUDITORIA, etiquetaAccion, type FiltroAuditoria } from '../types'

// El servidor entrega como máximo este número de eventos por consulta.
export const LIMITE_EVENTOS = 200

const VACIO: FiltroAuditoriaForm = { actor: '', accion: '', desde: '', hasta: '' }

// Los campos vacíos no viajan: la consulta solo lleva lo que el administrador eligió.
function aFiltro(f: FiltroAuditoriaForm): FiltroAuditoria {
  return {
    ...(f.actor && { actor: f.actor }),
    ...(f.accion && { accion: f.accion }),
    ...(f.desde && { desde: f.desde }),
    ...(f.hasta && { hasta: f.hasta }),
  }
}

// Auditoría (RF-AUD-07, §5.6): consulta filtrable por usuario, acción y rango de fechas, de
// solo lectura (RF-AUD-06), con exportación a CSV. Solo la ve el Administrador.
export function AuditoriaPage() {
  const [filtro, setFiltro] = useState<FiltroAuditoria>({})
  const auditoria = useAuditoria({ ...filtro, limite: LIMITE_EVENTOS })
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<FiltroAuditoriaForm>({
    resolver: zodResolver(filtroAuditoriaSchema),
    defaultValues: VACIO,
  })

  const eventos = auditoria.data ?? []
  const hayFiltro = Object.keys(filtro).length > 0
  const truncada = eventos.length >= LIMITE_EVENTOS

  let contenido
  if (auditoria.isPending) {
    contenido = <CargandoPagina texto="Cargando la auditoría…" />
  } else if (auditoria.isError) {
    contenido = esNoDisponible(auditoria.error) ? (
      <EmptyState
        icono="calendario"
        titulo="La auditoría llegará pronto"
        descripcion="Esta parte aún no está disponible en el servidor."
      />
    ) : (
      <ErrorCarga
        error={auditoria.error}
        porDefecto="No se pudo cargar la auditoría."
        alReintentar={() => auditoria.refetch()}
      />
    )
  } else if (eventos.length === 0) {
    contenido = (
      <EmptyState
        icono="buscar"
        titulo={hayFiltro ? 'Sin eventos con esos filtros' : 'Aún no hay eventos registrados'}
        descripcion={hayFiltro ? 'Prueba con otro usuario, acción o rango de fechas.' : undefined}
        accion={
          hayFiltro ? (
            <Button
              variante="secundario"
              onClick={() => {
                reset(VACIO)
                setFiltro({})
              }}
            >
              Limpiar filtros
            </Button>
          ) : undefined
        }
      />
    )
  } else {
    contenido = <TablaAuditoria eventos={eventos} />
  }

  return (
    <>
      <PageHeader
        titulo="Auditoría"
        descripcion="Quién hizo qué y cuándo. El registro es de solo lectura: no se modifica ni se elimina desde la aplicación."
        acciones={
          <Button
            variante="secundario"
            disabled={eventos.length === 0}
            onClick={() => descargarCsv(nombreCsv('auditoria'), auditoriaACsv(eventos))}
          >
            <Icono nombre="documentos" className="size-4" />
            Exportar CSV
          </Button>
        }
      />

      <Card className="mb-6">
        <CardBody>
          <form
            onSubmit={handleSubmit((f) => setFiltro(aFiltro(f)))}
            noValidate
            aria-label="Filtros de auditoría"
            className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4"
          >
            <Input
              etiqueta="Usuario"
              type="search"
              ayuda="Correo completo o parte de él"
              placeholder="correo"
              autoComplete="off"
              error={errors.actor?.message}
              {...register('actor')}
            />
            <Select etiqueta="Acción" error={errors.accion?.message} {...register('accion')}>
              <option value="">Todas</option>
              {ACCIONES_AUDITORIA.map((a) => (
                <option key={a} value={a}>
                  {etiquetaAccion[a]}
                </option>
              ))}
            </Select>
            <div className="grid grid-cols-2 gap-4 sm:col-span-2 lg:col-span-2">
              <Input
                etiqueta="Desde"
                type="date"
                ayuda="Día UTC"
                error={errors.desde?.message}
                {...register('desde')}
              />
              <Input
                etiqueta="Hasta"
                type="date"
                ayuda="Día UTC"
                error={errors.hasta?.message}
                {...register('hasta')}
              />
            </div>
            <div className="flex flex-wrap gap-3 sm:col-span-2 lg:col-span-4">
              <Button type="submit">Filtrar</Button>
              <Button
                variante="secundario"
                onClick={() => {
                  reset(VACIO)
                  setFiltro({})
                }}
              >
                Limpiar
              </Button>
            </div>
          </form>
        </CardBody>
      </Card>

      {auditoria.isSuccess && eventos.length > 0 && (
        <Alert tono="info" className="mb-4">
          {eventos.length} {eventos.length === 1 ? 'evento' : 'eventos'}
          {hayFiltro ? ' con los filtros aplicados' : ''}, del más reciente al más antiguo.
          {truncada &&
            ` Se muestran solo los ${LIMITE_EVENTOS} más recientes y el CSV incluye únicamente esos: afina los filtros para ver el resto.`}
        </Alert>
      )}
      {contenido}
    </>
  )
}
