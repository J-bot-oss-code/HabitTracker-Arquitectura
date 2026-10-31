import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { mensajeDeError } from '@/shared/api/problem'
import { env } from '@/shared/config/env'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Input, Select } from '@/shared/ui/Field'
import { useCrearHabito } from '../api'
import { crearHabitoSchema, type CrearHabitoForm as Datos } from '../schemas'
import { PERIODICIDADES } from '../types'

export function CrearHabitoForm({ alCrear }: { alCrear: (id: string) => void }) {
  const crear = useCrearHabito()
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<Datos>({
    resolver: zodResolver(crearHabitoSchema),
    defaultValues: { periodicidad: 'Diaria', metaId: env.metaIdPorDefecto },
  })

  const enviar = handleSubmit((datos) =>
    crear.mutate(datos, { onSuccess: (respuesta) => alCrear(respuesta.id) }),
  )

  return (
    <form onSubmit={enviar} noValidate className="space-y-4">
      {crear.isError && (
        <Alert tono="peligro">{mensajeDeError(crear.error, 'No se pudo crear el hábito.')}</Alert>
      )}
      <Input
        etiqueta="Nombre"
        placeholder="Ej. Leer"
        error={errors.nombre?.message}
        {...register('nombre')}
      />
      <div className="grid gap-4 sm:grid-cols-3">
        <Select
          etiqueta="Periodicidad"
          error={errors.periodicidad?.message}
          {...register('periodicidad')}
        >
          {PERIODICIDADES.map((p) => (
            <option key={p} value={p}>
              {p}
            </option>
          ))}
        </Select>
        <Input
          etiqueta="Objetivo"
          type="number"
          inputMode="decimal"
          step="any"
          min="0.01"
          error={errors.cantidadObjetivo?.message}
          {...register('cantidadObjetivo', { valueAsNumber: true })}
        />
        <Input
          etiqueta="Unidad"
          placeholder="páginas"
          error={errors.unidad?.message}
          {...register('unidad')}
        />
      </div>
      <Input
        etiqueta="Meta"
        placeholder="Identificador de la meta"
        ayuda="Pega el identificador de una meta existente."
        error={errors.metaId?.message}
        {...register('metaId')}
      />
      <div className="flex justify-end">
        <Button type="submit" cargando={crear.isPending}>
          Crear hábito
        </Button>
      </div>
    </form>
  )
}
