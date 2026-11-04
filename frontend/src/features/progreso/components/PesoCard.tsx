import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { esNoDisponible } from '@/shared/api/problem'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody, EmptyState } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { GraficaLinea } from '@/shared/ui/GraficaLinea'
import { Icono } from '@/shared/ui/Icono'
import { Modal } from '@/shared/ui/Modal'
import { Esqueleto } from '@/shared/ui/Spinner'
import { usePesos } from '../api'
import {
  lineaDelPlan,
  pesosDelPeriodo,
  puntosReales,
  resumenDePeso,
  textoDuracion,
} from '../metricas'
import type { Periodo } from '../types'
import { RegistrarPesoForm } from './RegistrarPesoForm'

const kg = new Intl.NumberFormat('es-DO', { minimumFractionDigits: 1, maximumFractionDigits: 1 })
const diaCorto = new Intl.DateTimeFormat('es-DO', {
  day: 'numeric',
  month: 'short',
  timeZone: 'UTC',
})

// Peso (§5.4): resumen "X kg → Y kg en N semanas", gráfica Real frente al Plan y el registro.
export function PesoCard({
  periodo,
  finDelPlan,
}: {
  periodo: Periodo
  // Instante UTC en que termina el plan de la meta; null si no hay plan activo.
  finDelPlan: number | null
}) {
  const pesos = usePesos()
  const [params, setParams] = useSearchParams()
  // "Registrar peso" en Hoy llega con ?registrar=peso y abre el formulario directamente.
  const [registrando, setRegistrando] = useState(params.get('registrar') === 'peso')
  const cerrar = () => {
    setRegistrando(false)
    if (params.has('registrar')) setParams({}, { replace: true })
  }

  const todos = pesos.data?.registros ?? []
  const visibles = pesosDelPeriodo(todos, periodo)
  const reales = puntosReales(visibles)
  // El plan se ancla al primer peso de TODO el historial: el periodo solo recorta el eje.
  const plan = lineaDelPlan(
    puntosReales(pesosDelPeriodo(todos, 'todo')),
    pesos.data?.objetivoKg ?? null,
    finDelPlan,
    reales[0]?.t,
  )
  const resumen = resumenDePeso(visibles)
  // El servidor no garantiza el orden: el último peso es el de la fecha más reciente.
  const ordenados = pesosDelPeriodo(todos, 'todo')
  const ultimo = ordenados.length > 0 ? ordenados[ordenados.length - 1].pesoKg : undefined
  // Sin el contrato en el servidor (404 sin cuerpo) registrar no puede funcionar.
  const noDisponible = pesos.isError && esNoDisponible(pesos.error)
  const duracion = resumen ? textoDuracion(resumen.dias, resumen.semanas) : ''

  let conclusion = ''
  if (resumen) {
    conclusion =
      visibles.length === 1
        ? `Peso: ${kg.format(resumen.actual)} kg, un solo registro.`
        : `Peso: de ${kg.format(resumen.inicial)} kg a ${kg.format(resumen.actual)} kg en ${duracion}.`
    if (plan) {
      // La línea discontinua también se dice en texto (WCAG 1.1.1).
      const fin = plan[plan.length - 1]
      conclusion += ` Plan: ${kg.format(fin.v)} kg el ${diaCorto.format(new Date(fin.t))}.`
    }
  }

  return (
    <Card>
      <CardBody>
        <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="text-lg font-bold">Peso</h2>
            {resumen && (
              <p className="mt-1 text-sm text-atenuado">
                <span className="cifra text-2xl text-texto">{kg.format(resumen.actual)} kg</span>
                {visibles.length > 1 && (
                  <>
                    {' '}
                    · de {kg.format(resumen.inicial)} kg en {duracion} ·{' '}
                    {/* Neutro: bajar de peso no es "bueno" por sí mismo (mejoras §4.6). */}
                    <span className="font-semibold text-texto">
                      {resumen.cambio > 0 ? '+' : resumen.cambio < 0 ? '−' : ''}
                      {kg.format(Math.abs(resumen.cambio))} kg
                    </span>
                  </>
                )}
              </p>
            )}
            <p className="mt-1 flex items-center gap-1.5 text-xs text-atenuado">
              <Icono nombre="candado" className="size-3.5" />
              Solo tú ves tu peso.
            </p>
          </div>
          <Button
            data-no-imprimir
            variante="secundario"
            disabled={noDisponible}
            onClick={() => setRegistrando(true)}
          >
            <Icono nombre="mas" className="size-4" />
            Registrar peso
          </Button>
        </div>

        {pesos.isPending ? (
          <Esqueleto className="h-56" />
        ) : pesos.isError ? (
          esNoDisponible(pesos.error) ? (
            <EmptyState
              icono="calendario"
              titulo="El registro de peso llegará pronto"
              descripcion="Esta parte aún no está disponible en el servidor."
            />
          ) : (
            <ErrorCarga
              error={pesos.error}
              porDefecto="No se pudo cargar tu peso."
              alReintentar={() => pesos.refetch()}
            />
          )
        ) : todos.length === 0 ? (
          <EmptyState
            icono="reportes"
            titulo="Aún no registras tu peso"
            descripcion="Anota tu primer peso y verás aquí cómo evoluciona."
            accion={<Button onClick={() => setRegistrando(true)}>Registrar peso</Button>}
          />
        ) : visibles.length === 0 ? (
          <EmptyState
            icono="calendario"
            titulo="Sin registros en este periodo"
            descripcion="Elige un periodo más largo para ver tus pesos anteriores."
          />
        ) : (
          <GraficaLinea
            real={reales}
            plan={plan}
            conclusion={conclusion}
            unidad="kg"
            formatoValor={(v) => kg.format(v)}
            formatoFecha={(t) => diaCorto.format(new Date(t))}
          />
        )}
      </CardBody>

      <Modal abierto={registrando} titulo="Registrar peso" alCerrar={cerrar}>
        <RegistrarPesoForm
          pesoSugerido={ultimo}
          objetivoActual={pesos.data?.objetivoKg ?? null}
          alRegistrar={cerrar}
        />
      </Modal>
    </Card>
  )
}
