import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { mensajeDeError } from '@/shared/api/problem'
import { fechaInputAUtcIso, hoyUtcInput } from '@/shared/lib/fechas'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Field'
import { useToast } from '@/shared/ui/Toast'
import { useRegistrarAvance } from '../api'
import { registrarAvanceSchema, type RegistrarAvanceForm as Datos } from '../schemas'
import type { HabitoDto } from '../types'

export function RegistrarAvanceForm({ habito }: { habito: HabitoDto }) {
  const registrar = useRegistrarAvance(habito.id)
  const { notificar } = useToast()
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<Datos>({
    resolver: zodResolver(registrarAvanceSchema),
    defaultValues: { fecha: hoyUtcInput() },
  })

  if (habito.estado !== 'Activo') {
    return (
      <p className="text-sm text-slate-500">
        Solo se registra avance cuando el hábito está Activo.
      </p>
    )
  }

  const enviar = handleSubmit((d) =>
    registrar.mutate(
      { cantidad: d.cantidad, fechaUtc: fechaInputAUtcIso(d.fecha) },
      {
        onSuccess: () => {
          notificar('Avance registrado.')
          reset({ fecha: d.fecha })
        },
      },
    ),
  )

  return (
    <form onSubmit={enviar} noValidate className="space-y-4">
      {registrar.isError && (
        <Alert tono="peligro">
          {mensajeDeError(registrar.error, 'No se pudo registrar el avance.')}
        </Alert>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Input
          etiqueta={`Cantidad (${habito.unidad})`}
          type="number"
          inputMode="decimal"
          step="any"
          min="0.01"
          error={errors.cantidad?.message}
          {...register('cantidad', { valueAsNumber: true })}
        />
        <Input
          etiqueta="Fecha (UTC)"
          type="date"
          max={hoyUtcInput()}
          error={errors.fecha?.message}
          {...register('fecha')}
        />
      </div>
      <Button type="submit" cargando={registrar.isPending}>
        Registrar avance
      </Button>
    </form>
  )
}
