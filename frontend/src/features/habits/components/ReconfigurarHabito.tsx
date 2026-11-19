import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router'
import { useCrearSolicitud, usePermisoEfectivo, useSolicitudes } from '@/features/permissions/api'
import { MENSAJE_PEDIDO_ENVIADO, nombrePermiso } from '@/features/permissions/catalogo'
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
  const solicitudes = useSolicitudes(true)
  const pedir = useCrearSolicitud()
  const { notificar } = useToast()

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
    // Una solicitud pendiente ya se pidió: se dice y no se ofrece pedirla otra vez (el servidor tampoco la admitiría).
    // Solo una Pendiente: tras un rechazo se puede volver a pedir. (`useSolicitudes(true)` ya trae solo las pendientes.)
    const pendiente = solicitudes.data?.some(
      (s) =>
        s.estado === 'Pendiente' &&
        s.permisoSolicitado.toLowerCase() === PERMISO_RECONFIGURACION.toLowerCase(),
    )
    return (
      <Alert tono="aviso" titulo="Necesitas un permiso">
        <p>
          Reconfigurar requiere el permiso «{nombrePermiso(PERMISO_RECONFIGURACION)}» aprobado por
          un Administrador.
        </p>
        {pendiente || pedir.isSuccess ? (
          <p className="mt-2 font-medium">
            Ya lo pediste: un Administrador lo está revisando.{' '}
            <Link to="/ajustes/solicitudes" className="underline">
              Ver mis solicitudes
            </Link>
          </p>
        ) : (
          <div className="mt-3 space-y-2">
            <Button
              tamano="sm"
              variante="secundario"
              cargando={pedir.isPending}
              disabled={solicitudes.isPending}
              onClick={() =>
                pedir.mutate(
                  { permisoSolicitado: PERMISO_RECONFIGURACION },
                  { onSuccess: () => notificar(MENSAJE_PEDIDO_ENVIADO) },
                )
              }
            >
              Pedir permiso
            </Button>
            {pedir.isError && (
              <Alert tono="peligro">
                {mensajeDeError(pedir.error, 'No se pudo pedir el permiso.')}
              </Alert>
            )}
          </div>
        )}
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
