import { useState } from 'react'
import { useNavigate } from 'react-router'
import { mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { EmptyState, PageHeader } from '@/shared/ui/Card'
import { Estadistica } from '@/shared/ui/Estadistica'
import { Icono } from '@/shared/ui/Icono'
import { Modal } from '@/shared/ui/Modal'
import { Segmentado } from '@/shared/ui/Segmentado'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useToast } from '@/shared/ui/Toast'
import { useHabitos } from '../api'
import { CrearHabitoForm } from '../components/CrearHabitoForm'
import { HabitoCard } from '../components/HabitoCard'
import { rachaActual } from '../estados'
import { ESTADOS_HABITO, type EstadoHabito } from '../types'

type Filtro = 'Todos' | EstadoHabito

export function HabitsPage() {
  const habitos = useHabitos()
  const navigate = useNavigate()
  const { notificar } = useToast()
  const [filtro, setFiltro] = useState<Filtro>('Todos')
  const [creando, setCreando] = useState(false)

  const lista = habitos.data ?? []
  const visibles = filtro === 'Todos' ? lista : lista.filter((h) => h.estado === filtro)
  const conteo = (f: Filtro) =>
    f === 'Todos' ? lista.length : lista.filter((h) => h.estado === f).length

  const activos = lista.filter((h) => h.estado === 'Activo')
  const metasCumplidas = activos.filter((h) => h.cumpleMetaFinal).length
  const mejorRacha = Math.max(0, ...activos.map((h) => rachaActual(h)))

  return (
    <>
      <PageHeader
        rotulo="Tu rendimiento"
        titulo="Mis hábitos"
        descripcion="Planifica, activa y registra el avance de tus hábitos."
        acciones={
          <Button onClick={() => setCreando(true)}>
            <Icono nombre="mas" className="size-4" />
            Nuevo hábito
          </Button>
        }
      />

      {lista.length > 0 && (
        <section aria-label="Resumen de rendimiento" className="mb-6 grid grid-cols-3 gap-3">
          <Estadistica
            etiqueta="En marcha"
            icono="rayo"
            valor={activos.length}
            detalle="hábitos activos"
          />
          <Estadistica
            etiqueta="Metas hoy"
            icono="trofeo"
            valor={`${metasCumplidas}/${activos.length}`}
            detalle="del período"
            destacada={activos.length > 0 && metasCumplidas === activos.length}
          />
          <Estadistica
            etiqueta="Mejor racha"
            icono="llama"
            valor={mejorRacha}
            detalle="períodos seguidos"
          />
        </section>
      )}

      <div className="mb-6">
        <Segmentado
          etiqueta="Filtrar por estado"
          valor={filtro}
          alCambiar={setFiltro}
          opciones={(['Todos', ...ESTADOS_HABITO] as Filtro[]).map((f) => ({
            valor: f,
            etiqueta: f,
            contador: habitos.isSuccess ? conteo(f) : undefined,
          }))}
        />
      </div>

      {habitos.isPending ? (
        <CargandoPagina texto="Cargando hábitos…" />
      ) : habitos.isError ? (
        <Alert tono="peligro">
          {mensajeDeError(habitos.error, 'No se pudieron cargar los hábitos.')}
        </Alert>
      ) : visibles.length === 0 ? (
        <EmptyState
          icono="habitos"
          titulo={
            filtro === 'Todos' ? 'Aún no tienes hábitos' : `No hay hábitos en estado ${filtro}`
          }
          descripcion={
            filtro === 'Todos'
              ? 'Crea uno para empezar a registrar tu progreso.'
              : 'Prueba con otro filtro o revisa todos tus hábitos.'
          }
          accion={
            filtro === 'Todos' ? (
              <Button onClick={() => setCreando(true)}>Crear mi primer hábito</Button>
            ) : (
              <Button variante="secundario" onClick={() => setFiltro('Todos')}>
                Ver todos
              </Button>
            )
          }
        />
      ) : (
        <div className="grid gap-4 md:grid-cols-2 2xl:grid-cols-3">
          {visibles.map((h) => (
            <HabitoCard key={h.id} habito={h} />
          ))}
        </div>
      )}

      <Modal abierto={creando} titulo="Nuevo hábito" alCerrar={() => setCreando(false)}>
        <CrearHabitoForm
          alCrear={(id) => {
            setCreando(false)
            notificar('Hábito creado en estado Planificado.')
            navigate(`/habitos/${id}`)
          }}
        />
      </Modal>
    </>
  )
}
