import { zodResolver } from '@hookform/resolvers/zod'
import { useForm, useWatch } from 'react-hook-form'
import { mensajeDeError } from '@/shared/api/problem'
import { fechaInputAUtcIso, hoyUtcInput } from '@/shared/lib/fechas'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Input, Select } from '@/shared/ui/Field'
import { useToast } from '@/shared/ui/Toast'
import { useRegistrarMedicion } from '../api'
import { LIMITES_MEDIDA, medicionSchema, type MedicionForm } from '../schemas'
import { infoZona, ZONAS } from '../types'

export function RegistrarMedicionForm({ alRegistrar }: { alRegistrar: () => void }) {
  const registrar = useRegistrarMedicion()
  const { notificar } = useToast()
  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<MedicionForm>({
    resolver: zodResolver(medicionSchema),
    defaultValues: { fecha: hoyUtcInput(), zona: 'Cintura' },
  })
  const zona = useWatch({ control, name: 'zona' })
  const { unidad } = infoZona[zona]
  const limites = LIMITES_MEDIDA[zona]

  const enviar = handleSubmit((d) =>
    registrar.mutate(
      { fecha: fechaInputAUtcIso(d.fecha), zona: d.zona, valor: d.valor },
      {
        onSuccess: () => {
          notificar('Medición registrada.')
          alRegistrar()
        },
      },
    ),
  )

  return (
    <form onSubmit={enviar} noValidate className="space-y-4">
      <p className="text-sm text-atenuado">Solo tú ves tus medidas.</p>
      {registrar.isError && (
        <Alert tono="peligro">
          {mensajeDeError(registrar.error, 'No se pudo registrar la medición.')}
        </Alert>
      )}
      <Select etiqueta="Zona" error={errors.zona?.message} {...register('zona')}>
        {ZONAS.map((z) => (
          <option key={z} value={z}>
            {infoZona[z].etiqueta}
          </option>
        ))}
      </Select>
      <div className="grid gap-4 sm:grid-cols-2">
        <Input
          etiqueta={`Medida (${unidad})`}
          type="number"
          inputMode="decimal"
          step="0.1"
          min={limites.min}
          max={limites.max}
          ayuda={`Entre ${limites.min} y ${limites.max} ${unidad}.`}
          error={errors.valor?.message}
          {...register('valor', { valueAsNumber: true })}
        />
        <Input
          etiqueta="Fecha"
          type="date"
          max={hoyUtcInput()}
          error={errors.fecha?.message}
          {...register('fecha')}
        />
      </div>
      <Button type="submit" tamano="lg" className="w-full" cargando={registrar.isPending}>
        Guardar medición
      </Button>
    </form>
  )
}
