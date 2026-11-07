import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { useVinculos } from '@/features/auth/api'
import { BotonGoogle } from '@/features/auth/google/BotonGoogle'
import { codigoDeError, mensajeDeError } from '@/shared/api/problem'
import { useAuth } from '@/shared/auth/AuthContext'
import { formatearFecha } from '@/shared/lib/fechas'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody } from '@/shared/ui/Card'
import { PasswordInput } from '@/shared/ui/Field'
import { Modal } from '@/shared/ui/Modal'
import { useToast } from '@/shared/ui/Toast'
import { useBajaProgramada, useCancelarBaja, useSolicitarBaja } from '../api'
import { DIAS_DE_GRACIA, type ModoBaja, type SolicitudBajaResponse } from '../types'

const esquema = z.object({
  password: z.string().min(1, 'Escribe tu contraseña para confirmar.').max(1024),
})

const TEXTOS: Record<ModoBaja, { titulo: string; boton: string; explicacion: string }> = {
  desactivar: {
    titulo: 'Desactivar mi cuenta',
    boton: 'Desactivar cuenta',
    explicacion:
      'Tu cuenta queda inactiva y se cierran todas tus sesiones. No se borra nada: volverá a activarse cuando inicies sesión de nuevo con tu contraseña o con Google.',
  },
  eliminar: {
    titulo: 'Eliminar mi cuenta',
    boton: 'Eliminar mi cuenta',
    explicacion: `Se cierran todas tus sesiones hoy y, pasados ${DIAS_DE_GRACIA} días, se borran para siempre tu cuenta, tus hábitos y avances, tus solicitudes de permiso y tus documentos (también los archivos). Durante ese plazo puedes cancelar la eliminación: inicia sesión y pulsa «Cancelar eliminación». La auditoría se conserva sin datos que te identifiquen.`,
  },
}

function avisoDeCierre(respuesta: SolicitudBajaResponse): string {
  return respuesta.modo === 'eliminar' && respuesta.borradoEn
    ? `Pediste eliminar tu cuenta: se borrará el ${formatearFecha(respuesta.borradoEn)}. Si cambias de opinión, inicia sesión antes y cancélala.`
    : 'Tu cuenta quedó desactivada. Vuelve a activarse cuando inicies sesión.'
}

function mensajeBaja(error: unknown): string {
  switch (codigoDeError(error)) {
    case 'clave_actual_incorrecta':
      return 'La contraseña no es correcta.'
    case 'cuenta_bloqueada':
      return 'Demasiados intentos fallidos. Tu cuenta está bloqueada 15 minutos.'
    case 'confirmacion_incorrecta':
      return 'No pudimos confirmarlo con Google. Usa la cuenta de Google vinculada a esta cuenta.'
    default:
      return mensajeDeError(error, 'No se pudo completar la solicitud.')
  }
}

// Retirarme de la plataforma (§5.5): desactivar o eliminar, confirmando con la contraseña (o con Google si la cuenta
// no tiene) y diciendo el periodo de gracia antes de confirmar. Al terminar se cierra la sesión (el servidor ya
// revocó todas). Si la eliminación ya está programada, se puede cancelar mientras dure el plazo.
export function PanelBaja() {
  const [modo, setModo] = useState<ModoBaja | null>(null)
  const baja = useBajaProgramada()
  const cancelar = useCancelarBaja()
  const { notificar } = useToast()
  const borradoEn = baja.data

  return (
    <Card className="border-peligro-borde">
      <CardBody>
        <h2 className="text-lg font-bold text-peligro">Retirarme de la plataforma</h2>

        {borradoEn && (
          <div className="mt-3 space-y-3">
            <Alert tono="aviso" titulo="Eliminación programada">
              Tu cuenta y tus datos se borrarán para siempre el {formatearFecha(borradoEn)}. Hasta
              entonces puedes cancelarla.
            </Alert>
            {cancelar.isError && (
              <Alert tono="peligro">{mensajeDeError(cancelar.error, 'No se pudo cancelar.')}</Alert>
            )}
            <Button
              cargando={cancelar.isPending}
              onClick={() =>
                cancelar.mutate(undefined, {
                  onSuccess: () => notificar('Eliminación cancelada: tu cuenta sigue activa.'),
                })
              }
            >
              Cancelar eliminación
            </Button>
          </div>
        )}

        <ul className="mt-2 space-y-1 text-sm text-texto-2">
          <li>
            <strong>Desactivar:</strong> tu cuenta queda inactiva y se reactiva sola cuando inicies
            sesión de nuevo. No se borra nada.
          </li>
          <li>
            <strong>Eliminar:</strong> pasados {DIAS_DE_GRACIA} días se borra todo y no se puede
            deshacer. Antes, descarga tus datos si quieres conservarlos.
          </li>
        </ul>
        <p className="mt-2 text-sm text-atenuado">Las dos opciones cierran todas tus sesiones.</p>
        <div className="mt-4 flex flex-col gap-4 sm:flex-row">
          <Button variante="secundario" onClick={() => setModo('desactivar')}>
            Desactivar
          </Button>
          {!borradoEn && (
            <Button variante="peligro" onClick={() => setModo('eliminar')}>
              Eliminar mi cuenta
            </Button>
          )}
        </div>
      </CardBody>
      {modo && <ModalBaja key={modo} modo={modo} alCerrar={() => setModo(null)} />}
    </Card>
  )
}

function ModalBaja({ modo, alCerrar }: { modo: ModoBaja; alCerrar: () => void }) {
  const solicitar = useSolicitarBaja()
  const vinculos = useVinculos()
  const { cerrarSesion } = useAuth()
  const textos = TEXTOS[modo]
  // Una cuenta que solo entra con Google no tiene contraseña que confirmar.
  const confirmaConGoogle = vinculos.data?.tienePassword === false
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<z.infer<typeof esquema>>({ resolver: zodResolver(esquema) })

  const alTerminar = (respuesta: SolicitudBajaResponse) =>
    cerrarSesion({ voluntario: true, aviso: avisoDeCierre(respuesta) })

  const enviar = handleSubmit((d) =>
    solicitar.mutate({ modo, password: d.password }, { onSuccess: alTerminar }),
  )

  return (
    <Modal
      abierto
      titulo={textos.titulo}
      // Mientras se envía no se cierra: la sesión se cerraría sin que se vea el resultado.
      alCerrar={() => {
        if (!solicitar.isPending) alCerrar()
      }}
    >
      <form onSubmit={enviar} noValidate className="space-y-4">
        <p className="text-sm">{textos.explicacion}</p>
        {solicitar.isError && <Alert tono="peligro">{mensajeBaja(solicitar.error)}</Alert>}

        {confirmaConGoogle ? (
          <div className="space-y-2">
            <p className="text-sm text-atenuado">
              Tu cuenta no tiene contraseña: confírmalo con la cuenta de Google que tienes
              vinculada.
            </p>
            <BotonGoogle
              texto="continue_with"
              deshabilitado={solicitar.isPending}
              alCredencial={(idToken) =>
                solicitar.mutate({ modo, idTokenGoogle: idToken }, { onSuccess: alTerminar })
              }
            />
          </div>
        ) : (
          <PasswordInput
            etiqueta="Tu contraseña"
            autoComplete="current-password"
            error={errors.password?.message}
            {...register('password')}
          />
        )}

        <div className="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <Button variante="secundario" disabled={solicitar.isPending} onClick={alCerrar}>
            Cancelar
          </Button>
          {!confirmaConGoogle && (
            <Button
              type="submit"
              variante={modo === 'eliminar' ? 'peligro' : 'primario'}
              // Hasta saber si la cuenta tiene contraseña no se sabe qué confirmación pedir.
              disabled={vinculos.isPending}
              cargando={solicitar.isPending}
            >
              {textos.boton}
            </Button>
          )}
        </div>
      </form>
    </Modal>
  )
}
