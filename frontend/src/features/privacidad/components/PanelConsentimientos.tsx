import { useState } from 'react'
import { Link } from 'react-router'
import { useConsentimientos, useGuardarConsentimientos } from '@/features/perfil/api'
import type { ConsentimientosDto } from '@/features/perfil/types'
import { INFO_LEGAL } from '@/features/publico/api'
import { esErrorHttp, esNoDisponible, mensajeDeError } from '@/shared/api/problem'
import { formatearFecha } from '@/shared/lib/fechas'
import { Alert } from '@/shared/ui/Alert'
import { Card, CardBody, EmptyState } from '@/shared/ui/Card'
import { ConfirmarDialogo } from '@/shared/ui/ConfirmarDialogo'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Interruptor } from '@/shared/ui/Interruptor'
import { Esqueleto } from '@/shared/ui/Spinner'

type Clave = 'datosSalud' | 'marketing' | 'compartirCumplimiento' | 'estadisticasAnonimas'

const CONSENTIMIENTOS: { clave: Clave; etiqueta: string; descripcion: string }[] = [
  {
    clave: 'datosSalud',
    etiqueta: 'Usar mis datos de salud para generar el plan',
    descripcion: 'Necesario para el servicio: sin esto no se guardan tu perfil, peso ni medidas.',
  },
  {
    clave: 'marketing',
    etiqueta: 'Correos con consejos y novedades',
    descripcion: 'Opcional. Puedes retirarlo cuando quieras.',
  },
  {
    clave: 'compartirCumplimiento',
    etiqueta: 'Compartir mi cumplimiento con un compañero',
    descripcion: 'Solo vería si cumples tus hábitos: nunca tu peso, tus medidas ni tus fotos.',
  },
  {
    clave: 'estadisticasAnonimas',
    etiqueta: 'Estadísticas de uso anónimas',
    descripcion: 'Ayudan a mejorar la aplicación y no incluyen datos que te identifiquen.',
  },
]

// Consentimientos (§5.5): cada cambio se guarda al instante. Retirar el de datos de salud pide
// confirmación porque desactiva el plan; la versión de los documentos que aceptaste no cambia.
export function PanelConsentimientos() {
  const consentimientos = useConsentimientos()
  const guardar = useGuardarConsentimientos()
  const [confirmando, setConfirmando] = useState(false)

  const actual = consentimientos.data
  const cambiar = (clave: Clave, valor: boolean) => {
    if (!actual) return
    // La fecha de aceptación la fija el servidor: no se reenvía. Las versiones se conservan.
    guardar.mutate({
      terminosVersion: actual.terminosVersion,
      privacidadVersion: actual.privacidadVersion,
      datosSalud: actual.datosSalud,
      marketing: actual.marketing,
      compartirCumplimiento: actual.compartirCumplimiento,
      estadisticasAnonimas: actual.estadisticasAnonimas,
      [clave]: valor,
    })
  }

  let contenido
  if (consentimientos.isPending) {
    contenido = <Esqueleto className="h-48" />
  } else if (consentimientos.isError) {
    contenido = esNoDisponible(consentimientos.error) ? (
      <EmptyState
        icono="calendario"
        titulo="Los consentimientos llegarán pronto"
        descripcion="Esta parte aún no está disponible en el servidor."
      />
    ) : esErrorHttp(consentimientos.error, 404) ? (
      <EmptyState
        icono="candado"
        titulo="Aún no has aceptado los documentos"
        descripcion="Completa tu perfil para registrar tus consentimientos."
        accion={
          <Link to="/bienvenida" className="font-semibold text-acento underline">
            Completar mi perfil
          </Link>
        }
      />
    ) : (
      <ErrorCarga
        error={consentimientos.error}
        porDefecto="No se pudieron cargar tus consentimientos."
        alReintentar={() => consentimientos.refetch()}
      />
    )
  } else if (actual) {
    contenido = (
      <Lista
        actual={actual}
        guardando={guardar.isPending}
        cambiar={cambiar}
        pedirConfirmacion={() => setConfirmando(true)}
      />
    )
  }

  return (
    <Card>
      <CardBody>
        <h2 className="text-lg font-bold">Consentimientos</h2>
        {guardar.isError && (
          <Alert tono="peligro" className="mt-3">
            {mensajeDeError(guardar.error, 'No se pudo guardar el cambio.')}
          </Alert>
        )}
        {/* Anuncia el resultado a quien no ve el cambio de estado. */}
        <p role="status" className="sr-only">
          {guardar.isSuccess ? 'Cambio guardado.' : ''}
        </p>
        <div className="mt-2">{contenido}</div>
      </CardBody>

      <ConfirmarDialogo
        abierto={confirmando}
        titulo="¿Retirar el uso de tus datos de salud?"
        mensaje="Dejaremos de poder guardar tu perfil, tu peso y tus medidas, y tu plan no se podrá generar. Tus datos actuales no se borran: para eso está la baja de cuenta."
        textoConfirmar="Retirar consentimiento"
        peligroso
        cargando={guardar.isPending}
        alConfirmar={() => {
          cambiar('datosSalud', false)
          setConfirmando(false)
        }}
        alCerrar={() => setConfirmando(false)}
      />
    </Card>
  )
}

function Lista({
  actual,
  guardando,
  cambiar,
  pedirConfirmacion,
}: {
  actual: ConsentimientosDto
  guardando: boolean
  cambiar: (clave: Clave, valor: boolean) => void
  pedirConfirmacion: () => void
}) {
  return (
    <>
      <div className="divide-y divide-borde">
        {CONSENTIMIENTOS.map((c) => (
          <Interruptor
            key={c.clave}
            etiqueta={c.etiqueta}
            descripcion={c.descripcion}
            activado={actual[c.clave]}
            deshabilitado={guardando}
            alCambiar={(nuevo) =>
              // Retirar los datos de salud es lo único que pide confirmación.
              c.clave === 'datosSalud' && !nuevo ? pedirConfirmacion() : cambiar(c.clave, nuevo)
            }
          />
        ))}
      </div>
      {actual.aceptadoEn && actual.terminosVersion && actual.privacidadVersion && (
        <p className="mt-3 text-sm text-atenuado">
          Aceptaste los{' '}
          <Link to="/legal/terminos" className="font-semibold text-acento underline">
            Términos {actual.terminosVersion}
          </Link>{' '}
          y la{' '}
          <Link to="/legal/privacidad" className="font-semibold text-acento underline">
            Política de privacidad {actual.privacidadVersion}
          </Link>{' '}
          el {formatearFecha(actual.aceptadoEn)}. Vigentes hoy: Términos{' '}
          {INFO_LEGAL.terminos.version} y Política {INFO_LEGAL.privacidad.version}.
        </p>
      )}
    </>
  )
}
