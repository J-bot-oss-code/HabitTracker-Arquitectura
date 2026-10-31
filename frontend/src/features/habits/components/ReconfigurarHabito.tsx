import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router'
import { usePermisoEfectivo } from '@/features/permissions/api'
import { nombrePermiso } from '@/features/permissions/catalogo'
import { mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Input, Select } from '@/shared/ui/Field'
import { Spinner } from '@/shared/ui/Spinner'
import { useToast } from '@/shared/ui/Toast'
import { useReconfigurarHabito } from '../api'
import { reconfigurarSchema, type ReconfigurarForm } from '../schemas'
import { PERIODICIDADES, PERMISO_RECONFIGURACION, type HabitoDto } from '../types'

// RF-GP-05: la acción solo está disponible con el permiso aplicado. La interfaz lo consulta
// para orientar al usuario, pero quien lo exige es el servidor (RD-06).
export function ReconfigurarHabito({ habito }: { habito: HabitoDto }) {
  const permiso = usePermisoEfectivo(PERMISO_RECONFIGURACION)

  if (habito.estado === 'Completado') {
    return <p className="text-sm text-atenuado">Un hábito completado no se puede reconfigurar.</p>
  }

  if (permiso.isPending) {
    return (
      <div className="flex items-center gap-2 text-sm text-atenuado">
        <Spinner className="size-4" /> Comprobando permiso…
      </div>
    )
  }

  if (permiso.isError) {
    return <Alert tono="peligro">{mensajeDeError(permiso.error)}</Alert>
  }

  if (!permiso.data) {
    return (
      <Alert tono="aviso" titulo="Necesitas un permiso">
        <p>
          Reconfigurar requiere el permiso «{nombrePermiso(PERMISO_RECONFIGURACION)}» aprobado por
          un Administrador.
        </p>
        <Link
          to={`/permisos?solicitar=${encodeURIComponent(PERMISO_RECONFIGURACION)}`}
          className="mt-2 inline-block font-medium underline"
        >
          Solicitar permiso
        </Link>
      </Alert>
    )
  }

  return <FormularioReconfiguracion habito={habito} />
}

function FormularioReconfiguracion({ habito }: { habito: HabitoDto }) {
  const reconfigurar = useReconfigurarHabito(habito.id)
  const { notificar } = useToast()
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ReconfigurarForm>({
    resolver: zodResolver(reconfigurarSchema),
    defaultValues: {
      periodicidad: habito.periodicidad,
      cantidadObjetivo: habito.cantidadObjetivo,
      unidad: habito.unidad,
    },
  })

  return (
    <form
      onSubmit={handleSubmit((d) =>
        reconfigurar.mutate(d, {
          onSuccess: () => notificar('Hábito reconfigurado. Comienza un ciclo nuevo.'),
        }),
      )}
      noValidate
      className="space-y-4"
    >
      <p className="text-sm text-atenuado">
        Al guardar se inicia un ciclo nuevo; el historial de avances se conserva.
      </p>
      {reconfigurar.isError && (
        <Alert tono="peligro">
          {mensajeDeError(reconfigurar.error, 'No se pudo reconfigurar el hábito.')}
        </Alert>
      )}
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
        <Input etiqueta="Unidad" error={errors.unidad?.message} {...register('unidad')} />
      </div>
      <Button type="submit" variante="secundario" cargando={reconfigurar.isPending}>
        Guardar reconfiguración
      </Button>
    </form>
  )
}
