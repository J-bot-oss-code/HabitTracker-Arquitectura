import { zodResolver } from '@hookform/resolvers/zod'
import { useForm, useWatch } from 'react-hook-form'
import { mensajeDeError } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Select } from '@/shared/ui/Field'
import { useToast } from '@/shared/ui/Toast'
import { useCrearSolicitud } from '../api'
import { CATALOGO_PERMISOS } from '../catalogo'
import { crearSolicitudSchema, type CrearSolicitudForm } from '../schemas'

export function SolicitarPermisoForm({ permisoInicial }: { permisoInicial?: string }) {
  const crear = useCrearSolicitud()
  const { notificar } = useToast()
  const inicial = CATALOGO_PERMISOS.some((p) => p.clave === permisoInicial)
    ? permisoInicial
    : CATALOGO_PERMISOS[0].clave

  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<CrearSolicitudForm>({
    resolver: zodResolver(crearSolicitudSchema),
    defaultValues: { permisoSolicitado: inicial },
  })

  const clave = useWatch({ control, name: 'permisoSolicitado' })
  const seleccionado = CATALOGO_PERMISOS.find((p) => p.clave === clave)

  return (
    <form
      onSubmit={handleSubmit((d) =>
        crear.mutate(d, {
          onSuccess: () => notificar('Solicitud enviada. Un Administrador la revisará.'),
        }),
      )}
      noValidate
      className="space-y-4"
    >
      {crear.isError && (
        <Alert tono="peligro">
          {mensajeDeError(crear.error, 'No se pudo crear la solicitud.')}
        </Alert>
      )}
      <Select
        etiqueta="Permiso"
        error={errors.permisoSolicitado?.message}
        {...register('permisoSolicitado')}
      >
        {CATALOGO_PERMISOS.map((p) => (
          <option key={p.clave} value={p.clave}>
            {p.nombre}
          </option>
        ))}
      </Select>
      {seleccionado && <p className="text-sm text-slate-500">{seleccionado.descripcion}</p>}
      <Button type="submit" cargando={crear.isPending}>
        Enviar solicitud
      </Button>
    </form>
  )
}
