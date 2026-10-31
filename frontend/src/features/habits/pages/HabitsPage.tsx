import { useState } from 'react'
import { useNavigate } from 'react-router'
import { mensajeDeError } from '@/shared/api/problem'
import { cn } from '@/shared/lib/cn'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { EmptyState, PageHeader } from '@/shared/ui/Card'
import { Modal } from '@/shared/ui/Modal'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useToast } from '@/shared/ui/Toast'
import { useHabitos } from '../api'
import { CrearHabitoForm } from '../components/CrearHabitoForm'
import { HabitoCard } from '../components/HabitoCard'
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

  return (
    <>
      <PageHeader
        titulo="Mis hábitos"
        descripcion="Planifica, activa y registra el avance de tus hábitos."
        acciones={<Button onClick={() => setCreando(true)}>Nuevo hábito</Button>}
      />

      <div role="tablist" aria-label="Filtrar por estado" className="mb-6 flex flex-wrap gap-2">
        {(['Todos', ...ESTADOS_HABITO] as Filtro[]).map((f) => (
          <button
            key={f}
            role="tab"
            type="button"
            aria-selected={filtro === f}
            onClick={() => setFiltro(f)}
            className={cn(
              'rounded-full px-3 py-1 text-sm font-medium transition-colors',
              filtro === f
                ? 'bg-brand-600 text-white'
                : 'bg-white text-slate-700 ring-1 ring-slate-200 hover:bg-slate-100 dark:bg-slate-900 dark:text-slate-300 dark:ring-slate-800',
            )}
          >
            {f} <span className="opacity-70">({conteo(f)})</span>
          </button>
        ))}
      </div>

      {habitos.isPending ? (
        <CargandoPagina texto="Cargando hábitos…" />
      ) : habitos.isError ? (
        <Alert tono="peligro">
          {mensajeDeError(habitos.error, 'No se pudieron cargar los hábitos.')}
        </Alert>
      ) : visibles.length === 0 ? (
        <EmptyState
          titulo={
            filtro === 'Todos' ? 'Aún no tienes hábitos' : `No hay hábitos en estado ${filtro}`
          }
          descripcion="Crea uno para empezar a registrar tu progreso."
          accion={
            filtro === 'Todos' && (
              <Button onClick={() => setCreando(true)}>Crear mi primer hábito</Button>
            )
          }
        />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
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
