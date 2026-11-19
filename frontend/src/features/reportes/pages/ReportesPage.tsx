import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useAuth } from '@/shared/auth/AuthContext'
import { formatearFecha } from '@/shared/lib/fechas'
import { cn } from '@/shared/lib/cn'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody, PageHeader } from '@/shared/ui/Card'
import { Input } from '@/shared/ui/Field'
import { BloqueConstancia } from '../components/BloqueConstancia'
import { BloquePermisos } from '../components/BloquePermisos'
import { rangoUltimosDias } from '../rango'
import { diasDelRango, rangoReporteSchema, type RangoReporteForm } from '../schemas'
import type { RangoReporte } from '../types'

// Reportes (RF-REP-01, 02 y 05): dos preguntas sobre un rango de fechas. Se abre con los últimos 7 días; un rango
// inválido se corrige en el formulario y no llega al servidor (que también lo valida, RD-07). El alcance (lo propio o
// todo) lo decide el servidor según el rol, no esta pantalla (RF-REP-04).
export function ReportesPage() {
  const { tieneRol } = useAuth()
  const [rango, setRango] = useState<RangoReporte>(() => rangoUltimosDias(7))
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<RangoReporteForm>({
    resolver: zodResolver(rangoReporteSchema),
    defaultValues: rango,
  })

  const totalDias = diasDelRango(rango.desde, rango.hasta)

  const ultimosDias = (dias: number) => {
    const nuevo = rangoUltimosDias(dias)
    reset(nuevo)
    setRango(nuevo)
  }

  return (
    <>
      <PageHeader
        titulo="Reportes"
        descripcion={
          tieneRol('Administrador')
            ? 'Elige un período y mira las solicitudes y la constancia por plan de todas las personas.'
            : 'Elige un período y mira cómo van tus solicitudes y tu constancia en cada plan.'
        }
      />
      <div className="space-y-6">
        <Card>
          <CardBody>
            <form
              noValidate
              aria-label="Período del reporte"
              onSubmit={handleSubmit((valores) => setRango(valores))}
              className="space-y-4"
            >
              <div className="grid max-w-xl gap-4 sm:grid-cols-2">
                <Input
                  etiqueta="Desde"
                  type="date"
                  error={errors.desde?.message}
                  {...register('desde')}
                />
                <Input
                  etiqueta="Hasta"
                  type="date"
                  error={errors.hasta?.message}
                  ayuda="Fechas en UTC. Máximo 366 días."
                  {...register('hasta')}
                />
              </div>
              <div className="flex flex-wrap gap-2">
                <Button type="submit">Ver reporte</Button>
                <div role="group" aria-label="Atajos de período" className="flex flex-wrap gap-2">
                  {[7, 30].map((dias) => {
                    const atajo = rangoUltimosDias(dias)
                    const vigente = atajo.desde === rango.desde && atajo.hasta === rango.hasta
                    return (
                      <Button
                        key={dias}
                        variante="secundario"
                        aria-label={`Últimos ${dias} días`}
                        aria-pressed={vigente}
                        className={cn(
                          vigente && 'border-acento bg-acento-suave text-acento-fuerte',
                        )}
                        onClick={() => ultimosDias(dias)}
                      >
                        {dias} días
                      </Button>
                    )
                  })}
                </div>
              </div>
            </form>
            {/* Se anuncia al cambiar el período que se muestra. */}
            <p role="status" className="mt-4 text-sm text-atenuado">
              Mostrando del {formatearFecha(rango.desde)} al {formatearFecha(rango.hasta)} (
              {totalDias} {totalDias === 1 ? 'día' : 'días'}).
            </p>
          </CardBody>
        </Card>

        <div className="grid items-start gap-6 lg:grid-cols-2">
          <BloquePermisos rango={rango} />
          <BloqueConstancia rango={rango} />
        </div>
      </div>
    </>
  )
}
