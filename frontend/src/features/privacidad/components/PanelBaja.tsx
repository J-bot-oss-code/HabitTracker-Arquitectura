import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { mensajeDeError } from '@/shared/api/problem'
import { useAuth } from '@/shared/auth/AuthContext'
import { Alert } from '@/shared/ui/Alert'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody } from '@/shared/ui/Card'
import { Input } from '@/shared/ui/Field'
import { Modal } from '@/shared/ui/Modal'
import { useSolicitarBaja } from '../api'
import { DIAS_DE_GRACIA, type ModoBaja } from '../types'

const esquema = z.object({
  password: z.string().min(1, 'Escribe tu contraseña para confirmar.').max(1024),
})

const TEXTOS: Record<
  ModoBaja,
  { titulo: string; boton: string; explicacion: string; aviso: string }
> = {
  desactivar: {
    titulo: 'Desactivar mi cuenta',
    boton: 'Desactivar cuenta',
    explicacion:
      'Tu cuenta deja de estar activa y se cierran tus sesiones. No se borra nada, pero no podrás volver a entrar hasta que un administrador la reactive.',
    aviso: 'Tu cuenta quedó desactivada.',
  },
  eliminar: {
    titulo: 'Eliminar mi cuenta',
    boton: 'Eliminar mi cuenta',
    explicacion: `Se cierra tu sesión hoy y, pasados ${DIAS_DE_GRACIA} días, se borran para siempre tu cuenta, tus hábitos, tu peso, tus medidas y tus consentimientos. Durante ese plazo un administrador puede cancelar la eliminación si lo pides. La auditoría se conserva sin datos que te identifiquen.`,
    aviso: `Pediste eliminar tu cuenta. Tus datos se borrarán en ${DIAS_DE_GRACIA} días.`,
  },
}

// Retirarme de la plataforma (§5.5): desactivar o eliminar, con la contraseña como confirmación
// y el periodo de gracia dicho antes de confirmar. Al terminar se cierra la sesión.
export function PanelBaja() {
  const [modo, setModo] = useState<ModoBaja | null>(null)
  return (
    <Card className="border-peligro-borde">
      <CardBody>
        <h2 className="text-lg font-bold text-peligro">Retirarme de la plataforma</h2>
        <ul className="mt-2 space-y-1 text-sm text-texto-2">
          <li>
            <strong>Desactivar:</strong> no podrás entrar hasta que un administrador reactive tu
            cuenta. No se borra nada.
          </li>
          <li>
            <strong>Eliminar:</strong> pasados {DIAS_DE_GRACIA} días se borra todo y no se puede
            deshacer. Antes, descarga tus datos si quieres conservarlos.
          </li>
        </ul>
        <p className="mt-2 text-sm text-atenuado">Las dos opciones cierran tu sesión.</p>
        <div className="mt-4 flex flex-col gap-4 sm:flex-row">
          <Button variante="secundario" onClick={() => setModo('desactivar')}>
            Desactivar
          </Button>
          <Button variante="peligro" onClick={() => setModo('eliminar')}>
            Eliminar mi cuenta
          </Button>
        </div>
      </CardBody>
      {modo && <ModalBaja key={modo} modo={modo} alCerrar={() => setModo(null)} />}
    </Card>
  )
}

function ModalBaja({ modo, alCerrar }: { modo: ModoBaja; alCerrar: () => void }) {
  const solicitar = useSolicitarBaja()
  const { cerrarSesion } = useAuth()
  const textos = TEXTOS[modo]
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<z.infer<typeof esquema>>({ resolver: zodResolver(esquema) })

  const enviar = handleSubmit((d) =>
    solicitar.mutate(
      { modo, password: d.password },
      { onSuccess: () => cerrarSesion({ voluntario: true, aviso: textos.aviso }) },
    ),
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
        {solicitar.isError && (
          <Alert tono="peligro">
            {mensajeDeError(solicitar.error, 'No se pudo completar la solicitud.')}
          </Alert>
        )}
        <Input
          etiqueta="Tu contraseña"
          type="password"
          autoComplete="current-password"
          error={errors.password?.message}
          {...register('password')}
        />
        <div className="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <Button variante="secundario" disabled={solicitar.isPending} onClick={alCerrar}>
            Cancelar
          </Button>
          <Button
            type="submit"
            variante={modo === 'eliminar' ? 'peligro' : 'primario'}
            cargando={solicitar.isPending}
          >
            {textos.boton}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
