import { useState } from 'react'
import { esNoDisponible } from '@/shared/api/problem'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody, EmptyState } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Icono } from '@/shared/ui/Icono'
import { Modal } from '@/shared/ui/Modal'
import { Esqueleto } from '@/shared/ui/Spinner'
import { useMediciones } from '../api'
import { filasDeMedidas } from '../metricas'
import { infoZona } from '../types'
import { RegistrarMedicionForm } from './RegistrarMedicionForm'

const numero = new Intl.NumberFormat('es-DO', { maximumFractionDigits: 1 })

// Medidas (§5.4): tabla Zona · Inicio · Hoy con el cambio y "+ Nueva medición".
export function MedidasCard() {
  const mediciones = useMediciones()
  const [registrando, setRegistrando] = useState(false)
  const filas = filasDeMedidas(mediciones.data ?? [])

  return (
    <Card>
      <CardBody>
        <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="text-lg font-bold">Medidas</h2>
            <p className="mt-1 flex items-center gap-1.5 text-xs text-atenuado">
              <Icono nombre="candado" className="size-3.5" />
              Solo tú ves tus medidas.
            </p>
          </div>
          <Button
            data-no-imprimir
            variante="secundario"
            disabled={mediciones.isError && esNoDisponible(mediciones.error)}
            onClick={() => setRegistrando(true)}
          >
            <Icono nombre="mas" className="size-4" />
            Nueva medición
          </Button>
        </div>

        {mediciones.isPending ? (
          <Esqueleto className="h-40" />
        ) : mediciones.isError ? (
          esNoDisponible(mediciones.error) ? (
            <EmptyState
              icono="calendario"
              titulo="Las medidas llegarán pronto"
              descripcion="Esta parte aún no está disponible en el servidor."
            />
          ) : (
            <ErrorCarga
              error={mediciones.error}
              porDefecto="No se pudieron cargar tus medidas."
              alReintentar={() => mediciones.refetch()}
            />
          )
        ) : filas.length === 0 ? (
          <EmptyState
            icono="reportes"
            titulo="Aún no registras medidas"
            descripcion="Anota cintura, pecho, brazo o % de grasa para ver tu cambio."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <caption className="sr-only">Medidas al inicio y hoy, con su cambio</caption>
              <thead className="rotulo">
                <tr>
                  <th scope="col" className="py-2 pr-4">
                    Zona
                  </th>
                  <th scope="col" className="py-2 pr-4 text-right">
                    Inicio
                  </th>
                  <th scope="col" className="py-2 pr-4 text-right">
                    Hoy
                  </th>
                  <th scope="col" className="py-2 text-right">
                    Cambio
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-borde">
                {filas.map((f) => {
                  const { etiqueta, unidad } = infoZona[f.zona]
                  return (
                    <tr key={f.zona}>
                      <th scope="row" className="py-3 pr-4 font-semibold">
                        {etiqueta}
                      </th>
                      <td className="py-3 pr-4 text-right tabular-nums">
                        {numero.format(f.inicio)} {unidad}
                      </td>
                      <td className="py-3 pr-4 text-right font-semibold tabular-nums">
                        {numero.format(f.hoy)} {unidad}
                      </td>
                      <td className="py-3 text-right tabular-nums text-atenuado">
                        {f.cambio === 0
                          ? 'Sin cambio'
                          : `${f.cambio > 0 ? '+' : '−'}${numero.format(Math.abs(f.cambio))} ${unidad}`}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </CardBody>

      <Modal abierto={registrando} titulo="Nueva medición" alCerrar={() => setRegistrando(false)}>
        <RegistrarMedicionForm alRegistrar={() => setRegistrando(false)} />
      </Modal>
    </Card>
  )
}
