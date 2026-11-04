import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { usePerfil } from '@/features/perfil/api'
import { buscarMeta } from '@/features/publico/api'
import { fechaLargaUtc } from '@/shared/lib/fechas'
import { Button } from '@/shared/ui/Button'
import { claseBoton } from '@/shared/ui/claseBoton'
import { Card, CardBody, EmptyState } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Esqueleto } from '@/shared/ui/Spinner'
import { Estadistica } from '@/shared/ui/Estadistica'
import { Icono } from '@/shared/ui/Icono'
import { InsigniaRacha } from '@/shared/ui/InsigniaRacha'
import { Modal } from '@/shared/ui/Modal'
import { TiraSemana, type CeldaSemana } from '@/shared/ui/TiraSemana'
import { useHabitos } from '../api'
import { CrearHabitoForm } from '../components/CrearHabitoForm'
import { FilaTarea } from '../components/FilaTarea'
import { RegistrarAvanceForm } from '../components/RegistrarAvanceForm'
import {
  cumplimientoSemanal,
  rachaDeHoy,
  rachaMasLarga,
  resumenDeHoy,
  saludoSegunHora,
  semanaDelPlan,
  semanaEnCurso,
  tareasDeHoy,
} from '../hoy'

const DIAS = [
  ['L', 'lunes'],
  ['M', 'martes'],
  ['M', 'miércoles'],
  ['J', 'jueves'],
  ['V', 'viernes'],
  ['S', 'sábado'],
  ['D', 'domingo'],
] as const

// Panel "Hoy" (especificación §5.3): lo primero que ve el usuario al entrar. Las métricas son
// de presentación (habits/hoy.ts); el servidor decide qué avances se aceptan.
export function HoyPage() {
  const habitos = useHabitos()
  const perfil = usePerfil()
  const navigate = useNavigate()
  const [creando, setCreando] = useState(false)
  // Solo el id: el hábito se busca en la lista vigente al dibujar, así el diálogo no muestra
  // una copia vieja si el listado se refresca.
  const [registrandoId, setRegistrandoId] = useState<string | null>(null)

  if (habitos.isPending) return <CargandoHoy />
  if (habitos.isError) {
    return (
      <ErrorCarga
        error={habitos.error}
        porDefecto="No se pudo cargar tu día."
        alReintentar={() => habitos.refetch()}
      />
    )
  }

  const lista = habitos.data
  const tareas = tareasDeHoy(lista)
  const { hechas, total, porcentaje } = resumenDeHoy(tareas)
  const racha = rachaDeHoy(lista)
  const cumplimiento = cumplimientoSemanal(lista)
  const pendiente = tareas.find((t) => !t.hecha)
  const registrando = lista.find((h) => h.id === registrandoId)

  // El perfil es una pieza propuesta: sin él (o sin meta) el panel funciona igual.
  const meta = buscarMeta(perfil.data?.metaSlug)
  const semanaPlan =
    meta && perfil.data?.inicioPlan ? semanaDelPlan(perfil.data.inicioPlan, meta.semanas) : null
  // Sin perfil no se saluda por el correo (dato personal): solo se omite el nombre.
  const nombre = (perfil.data?.nombre ?? '').split(' ')[0]

  const semana: CeldaSemana[] = semanaEnCurso(lista).map((d, i) => ({
    corta: DIAS[i][0],
    larga: DIAS[i][1],
    estado: d.estado,
    hoy: d.hoy,
  }))

  return (
    <>
      <header className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <p className="text-sm font-medium text-atenuado">{fechaLargaUtc()}</p>
          <h1 className="mt-1 text-3xl font-extrabold sm:text-[2rem]">
            {saludoSegunHora(new Date().getHours())}
            {nombre && `, ${nombre}`}
          </h1>
        </div>
        <div className="flex flex-wrap gap-2">
          <Link to="/progreso?registrar=peso" className={claseBoton({ variante: 'secundario' })}>
            Registrar peso
          </Link>
          <Button onClick={() => setCreando(true)}>
            <Icono nombre="mas" className="size-4" />
            Añadir hábito
          </Button>
        </div>
      </header>

      <section aria-label="Resumen del día" className="mb-6 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Estadistica
          etiqueta="Tareas de hoy"
          icono="check"
          valor={`${hechas}/${total}`}
          progreso={porcentaje}
          destacada={total > 0 && hechas === total}
        />
        <Estadistica
          etiqueta="Racha actual"
          icono="llama"
          valor={racha}
          detalle={`${racha === 1 ? 'día' : 'días'} seguidos · récord ${rachaMasLarga(lista)}`}
        />
        <Estadistica
          etiqueta="Cumplimiento semanal"
          icono="trofeo"
          valor={cumplimiento === null ? '—' : `${Math.round(cumplimiento)} %`}
          detalle="hábitos diarios, últimos 7 días"
        />
        <Estadistica
          etiqueta="Plan · semana"
          icono="calendario"
          valor={semanaPlan && meta ? `${semanaPlan} de ${meta.semanas}` : 'Sin plan'}
          detalle={
            meta ? (
              meta.nombre
            ) : (
              <Link
                to="/metas"
                className="inline-flex min-h-11 items-center font-semibold text-acento underline"
              >
                Elige una meta
              </Link>
            )
          }
        />
      </section>

      <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <section aria-labelledby="tareas-titulo">
          <Card>
            <CardBody>
              <div className="mb-1 flex items-center justify-between gap-3">
                <h2 id="tareas-titulo" className="text-lg font-bold">
                  Tus tareas de hoy
                </h2>
                {racha > 0 && <InsigniaRacha dias={racha} />}
              </div>
              {lista.length === 0 ? (
                <EmptyState
                  icono="rayo"
                  titulo="Aún no tienes un plan — elige una meta"
                  descripcion="Explora las metas, o crea tu primer hábito para empezar a registrar tu día."
                  accion={
                    <div className="flex flex-wrap justify-center gap-3">
                      <Link
                        to="/metas"
                        className="inline-flex min-h-11 items-center rounded-lg px-3 font-semibold text-acento underline"
                      >
                        Ver metas
                      </Link>
                      <Button variante="secundario" onClick={() => setCreando(true)}>
                        Crear mi primer hábito
                      </Button>
                    </div>
                  }
                />
              ) : tareas.length === 0 ? (
                <EmptyState
                  icono="pausa"
                  titulo="Hoy no tienes hábitos activos"
                  descripcion="Activa un hábito planificado o reanuda uno pausado para verlo aquí."
                  accion={
                    <Link
                      to="/habitos"
                      className="inline-flex min-h-11 items-center rounded-lg px-3 font-semibold text-acento underline"
                    >
                      Ir a mis hábitos
                    </Link>
                  }
                />
              ) : (
                <ul className="divide-y divide-borde">
                  {tareas.map((t) => (
                    <FilaTarea
                      key={t.habito.id}
                      tarea={t}
                      alRegistrarCantidad={() => setRegistrandoId(t.habito.id)}
                    />
                  ))}
                </ul>
              )}
            </CardBody>
          </Card>
        </section>

        <aside aria-label="Siguiente paso y semana" className="space-y-4">
          <div className="panel-tinta rounded-2xl p-5">
            <p className="text-sm font-semibold text-tinta-atenuado">
              {pendiente ? 'Próxima tarea' : 'Tu día'}
            </p>
            {pendiente ? (
              <>
                <p className="mt-2 text-lg font-bold">{pendiente.habito.nombre}</p>
                <p className="mt-1 text-sm text-tinta-atenuado">
                  Te faltan {Math.round(pendiente.restante * 100) / 100} {pendiente.habito.unidad}.
                </p>
                <Link
                  to={`/habitos/${pendiente.habito.id}`}
                  className="mt-4 inline-flex min-h-11 items-center gap-1.5 rounded-lg font-semibold text-acento-claro underline underline-offset-4"
                >
                  Ver hábito
                  <Icono nombre="flecha" className="size-4" />
                </Link>
              </>
            ) : (
              <p className="mt-2 text-lg font-bold">
                {total > 0 ? '¡Todo hecho por hoy!' : 'Sin tareas pendientes'}
              </p>
            )}
          </div>

          <Card>
            <CardBody>
              <h2 className="mb-3 text-lg font-bold">Esta semana</h2>
              <TiraSemana dias={semana} etiqueta="Cumplimiento de la semana" />
            </CardBody>
          </Card>
        </aside>
      </div>

      <Modal abierto={creando} titulo="Nuevo hábito" alCerrar={() => setCreando(false)}>
        <CrearHabitoForm
          alCrear={(id) => {
            setCreando(false)
            // Nace Planificado: se activa desde su detalle para que cuente en "Hoy".
            navigate(`/habitos/${id}`)
          }}
        />
      </Modal>
      <Modal
        abierto={registrando !== undefined}
        titulo={registrando ? `Registrar avance: ${registrando.nombre}` : 'Registrar avance'}
        alCerrar={() => setRegistrandoId(null)}
      >
        {registrando && (
          <RegistrarAvanceForm habito={registrando} alRegistrar={() => setRegistrandoId(null)} />
        )}
      </Modal>
    </>
  )
}

function CargandoHoy() {
  return (
    <div role="status" aria-live="polite" aria-label="Cargando tu día" className="space-y-4">
      <Esqueleto className="h-14 w-72" />
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {[0, 1, 2, 3].map((i) => (
          <Esqueleto key={i} className="h-28" />
        ))}
      </div>
      <Esqueleto className="h-64" />
    </div>
  )
}
