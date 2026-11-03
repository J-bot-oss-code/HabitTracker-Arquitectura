import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { LIMITES_CORPORALES } from '@/features/publico/types'
import { mensajeDeError } from '@/shared/api/problem'
import { fechaInputAUtcIso, hoyUtcInput } from '@/shared/lib/fechas'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Input } from '@/shared/ui/Field'
import { useToast } from '@/shared/ui/Toast'
import { useRegistrarPeso } from '../api'
import { pesoSchema, type PesoForm } from '../schemas'

// Registra un peso (y, si se escribe, el peso objetivo). Es un dato de salud: solo lo ve su
// dueño y el servidor exige el consentimiento (409 si falta).
export function RegistrarPesoForm({
  pesoSugerido,
  objetivoActual,
  alRegistrar,
}: {
  pesoSugerido?: number
  objetivoActual: number | null
  alRegistrar: () => void
}) {
  const registrar = useRegistrarPeso()
  const { notificar } = useToast()
  const { pesoKg } = LIMITES_CORPORALES
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<PesoForm>({
    resolver: zodResolver(pesoSchema),
    defaultValues: {
      fecha: hoyUtcInput(),
      pesoKg: pesoSugerido,
      objetivoKg: objetivoActual ?? undefined,
    },
  })

  const enviar = handleSubmit((d) =>
    registrar.mutate(
      {
        fecha: fechaInputAUtcIso(d.fecha),
        pesoKg: d.pesoKg,
        // Vacío (NaN) deja el objetivo como estaba.
        ...(d.objetivoKg !== undefined && !Number.isNaN(d.objetivoKg)
          ? { objetivoKg: d.objetivoKg }
          : {}),
      },
      {
        onSuccess: () => {
          notificar('Peso registrado.')
          alRegistrar()
        },
      },
    ),
  )

  return (
    <form onSubmit={enviar} noValidate className="space-y-4">
      <p className="text-sm text-atenuado">Solo tú ves tu peso.</p>
      {registrar.isError && (
        <Alert tono="peligro">
          {mensajeDeError(registrar.error, 'No se pudo registrar el peso.')}
        </Alert>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Input
          etiqueta="Peso (kg)"
          type="number"
          inputMode="decimal"
          step="0.1"
          min={pesoKg.min}
          max={pesoKg.max}
          error={errors.pesoKg?.message}
          {...register('pesoKg', { valueAsNumber: true })}
        />
        <Input
          etiqueta="Fecha"
          type="date"
          max={hoyUtcInput()}
          error={errors.fecha?.message}
          {...register('fecha')}
        />
      </div>
      <Input
        etiqueta="Peso objetivo (kg), opcional"
        type="number"
        inputMode="decimal"
        step="0.1"
        min={pesoKg.min}
        max={pesoKg.max}
        ayuda="Dibuja la línea del plan en la gráfica."
        error={errors.objetivoKg?.message}
        {...register('objetivoKg', { valueAsNumber: true })}
      />
      <Button type="submit" tamano="lg" className="w-full" cargando={registrar.isPending}>
        Guardar peso
      </Button>
    </form>
  )
}
