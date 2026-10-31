import { useState } from 'react'
import type { UsuarioResponseDto } from '@/features/auth/types'
import { mensajeDeError } from '@/shared/api/problem'
import { useAuth } from '@/shared/auth/AuthContext'
import { ROLES, etiquetaRol, type Rol } from '@/shared/auth/roles'
import { Alert } from '@/shared/ui/Alert'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { EmptyState, PageHeader } from '@/shared/ui/Card'
import { ConfirmarDialogo } from '@/shared/ui/ConfirmarDialogo'
import { Estadistica } from '@/shared/ui/Estadistica'
import { Input } from '@/shared/ui/Field'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useToast } from '@/shared/ui/Toast'
import { useCambiarEstado, useCambiarRol, useForzarRestablecimiento, useUsuarios } from '../api'

// Acción pendiente de confirmar. Todo lo que cambia el acceso de una cuenta pide confirmación.
type AccionPendiente =
  | { tipo: 'rol'; usuario: UsuarioResponseDto; rol: Rol }
  | { tipo: 'desactivar'; usuario: UsuarioResponseDto }
  | { tipo: 'restablecer'; usuario: UsuarioResponseDto }

const celda = 'block px-4 py-1.5 md:table-cell md:py-3'
const conRotulo =
  'before:mb-0.5 before:block before:text-xs before:font-semibold before:uppercase before:tracking-wider before:text-atenuado before:content-[attr(data-etiqueta)] md:before:hidden'
const encabezado = 'px-4 py-3 text-xs font-semibold uppercase tracking-wider text-atenuado'

export function UsersPage() {
  const usuarios = useUsuarios()
  const [busqueda, setBusqueda] = useState('')
  const [pendiente, setPendiente] = useState<AccionPendiente | null>(null)

  const lista = usuarios.data ?? []
  const termino = busqueda.trim().toLowerCase()
  const visibles = lista.filter(
    (u) =>
      !termino ||
      u.nombre.toLowerCase().includes(termino) ||
      u.correo.toLowerCase().includes(termino),
  )

  return (
    <>
      <PageHeader
        rotulo="Administración"
        titulo="Usuarios"
        descripcion="Gestiona roles, estado de las cuentas y restablecimientos de contraseña."
      />

      {usuarios.isSuccess && (
        <section aria-label="Resumen de usuarios" className="mb-6 grid grid-cols-3 gap-3">
          <Estadistica
            etiqueta="Cuentas"
            icono="usuarios"
            valor={lista.length}
            detalle="registradas"
          />
          <Estadistica
            etiqueta="Activas"
            icono="check"
            valor={lista.filter((u) => u.activo).length}
            detalle="pueden iniciar sesión"
          />
          <Estadistica
            etiqueta="Admins"
            icono="permisos"
            valor={lista.filter((u) => u.rol === 'Administrador').length}
          />
        </section>
      )}

      <div className="mb-4 max-w-sm">
        <Input
          etiqueta="Buscar usuario"
          type="search"
          placeholder="Nombre o correo"
          value={busqueda}
          onChange={(e) => setBusqueda(e.target.value)}
        />
      </div>

      {usuarios.isPending ? (
        <CargandoPagina texto="Cargando usuarios…" />
      ) : usuarios.isError ? (
        <Alert tono="peligro">{mensajeDeError(usuarios.error)}</Alert>
      ) : visibles.length === 0 ? (
        <EmptyState
          icono="buscar"
          titulo="No hay usuarios que coincidan."
          accion={
            <Button variante="secundario" onClick={() => setBusqueda('')}>
              Limpiar búsqueda
            </Button>
          }
        />
      ) : (
        <div className="md:overflow-hidden md:rounded-xl md:border md:border-borde md:bg-superficie md:shadow-tarjeta">
          <table role="table" className="block w-full text-left text-sm md:table">
            <thead role="rowgroup" className="sr-only md:not-sr-only md:table-header-group">
              <tr role="row" className="md:table-row">
                <th scope="col" className={encabezado}>
                  Usuario
                </th>
                <th scope="col" className={encabezado}>
                  Rol
                </th>
                <th scope="col" className={encabezado}>
                  Estado
                </th>
                <th scope="col" className="px-4 py-3">
                  <span className="sr-only">Acciones</span>
                </th>
              </tr>
            </thead>
            <tbody
              role="rowgroup"
              className="block space-y-3 md:table-row-group md:space-y-0 md:divide-y md:divide-borde"
            >
              {visibles.map((u) => (
                <FilaUsuario key={u.id} usuario={u} alPedir={setPendiente} />
              ))}
            </tbody>
          </table>
        </div>
      )}

      <ConfirmacionUsuario pendiente={pendiente} alCerrar={() => setPendiente(null)} />
    </>
  )
}

function FilaUsuario({
  usuario,
  alPedir,
}: {
  usuario: UsuarioResponseDto
  alPedir: (accion: AccionPendiente) => void
}) {
  const { usuario: sesion } = useAuth()
  const { notificar } = useToast()
  const cambiarEstado = useCambiarEstado()
  const esYo = sesion?.id === usuario.id
  const motivoBloqueo = esYo ? 'No puedes modificar tu propia cuenta' : undefined

  return (
    <tr
      role="row"
      className="block rounded-xl border border-borde bg-superficie py-3 shadow-tarjeta md:table-row md:rounded-none md:border-0 md:bg-transparent md:py-0 md:shadow-none"
    >
      <td role="cell" data-etiqueta="Usuario" className={`${celda} ${conRotulo}`}>
        <div className="font-semibold">
          {usuario.nombre} {esYo && <span className="text-xs text-atenuado">(tú)</span>}
        </div>
        <div className="text-xs text-atenuado">{usuario.correo}</div>
      </td>
      <td role="cell" data-etiqueta="Rol" className={`${celda} ${conRotulo}`}>
        <label htmlFor={`rol-${usuario.id}`} className="sr-only">
          Rol de {usuario.nombre}
        </label>
        {/* Controlado: si se cancela la confirmación, vuelve al rol actual. */}
        <select
          id={`rol-${usuario.id}`}
          value={usuario.rol}
          disabled={esYo}
          title={motivoBloqueo}
          onChange={(e) => alPedir({ tipo: 'rol', usuario, rol: e.target.value as Rol })}
          className="min-h-11 rounded-lg border border-borde-fuerte bg-superficie px-2 text-sm text-texto disabled:cursor-not-allowed disabled:text-atenuado"
        >
          {ROLES.map((r) => (
            <option key={r} value={r}>
              {etiquetaRol[r]}
            </option>
          ))}
        </select>
      </td>
      <td role="cell" data-etiqueta="Estado" className={`${celda} ${conRotulo}`}>
        <Badge
          tono={usuario.activo ? 'exito' : 'neutro'}
          icono={usuario.activo ? 'check' : 'pausa'}
        >
          {usuario.activo ? 'Activo' : 'Inactivo'}
        </Badge>
      </td>
      <td role="cell" className={`${celda} md:text-right`}>
        <div className="flex flex-wrap gap-2 md:justify-end">
          <Button
            tamano="sm"
            variante="secundario"
            disabled={esYo}
            title={motivoBloqueo}
            cargando={cambiarEstado.isPending}
            aria-label={`${usuario.activo ? 'Desactivar' : 'Activar'} a ${usuario.nombre}`}
            onClick={() =>
              usuario.activo
                ? alPedir({ tipo: 'desactivar', usuario })
                : cambiarEstado.mutate(
                    { id: usuario.id, activo: true },
                    {
                      onSuccess: () => notificar('Cuenta activada.'),
                      onError: (e) => notificar(mensajeDeError(e), 'peligro'),
                    },
                  )
            }
          >
            {usuario.activo ? 'Desactivar' : 'Activar'}
          </Button>
          <Button
            tamano="sm"
            variante="fantasma"
            disabled={esYo}
            title={motivoBloqueo}
            aria-label={`Forzar restablecimiento de ${usuario.nombre}`}
            onClick={() => alPedir({ tipo: 'restablecer', usuario })}
          >
            Forzar restablecimiento
          </Button>
        </div>
      </td>
    </tr>
  )
}

function ConfirmacionUsuario({
  pendiente,
  alCerrar,
}: {
  pendiente: AccionPendiente | null
  alCerrar: () => void
}) {
  const { notificar } = useToast()
  const cambiarRol = useCambiarRol()
  const cambiarEstado = useCambiarEstado()
  const forzar = useForzarRestablecimiento()

  const terminar = (mensaje: string) => {
    notificar(mensaje)
    alCerrar()
  }

  if (!pendiente)
    return (
      <ConfirmarDialogo
        abierto={false}
        titulo=""
        mensaje=""
        textoConfirmar=""
        alConfirmar={() => {}}
        alCerrar={alCerrar}
      />
    )

  const { usuario } = pendiente
  const cargando = cambiarRol.isPending || cambiarEstado.isPending || forzar.isPending
  const fallo = cambiarRol.error ?? cambiarEstado.error ?? forzar.error

  const contenido = {
    rol: {
      titulo: 'Cambiar rol',
      peligroso: pendiente.tipo === 'rol' && pendiente.rol === 'Administrador',
      texto: 'Confirmar cambio',
      mensaje: (
        <p>
          <strong>{usuario.correo}</strong> pasará de <strong>{etiquetaRol[usuario.rol]}</strong> a{' '}
          <strong>{pendiente.tipo === 'rol' ? etiquetaRol[pendiente.rol] : ''}</strong>. Sus
          sesiones actuales se cerrarán.
          {pendiente.tipo === 'rol' && pendiente.rol === 'Administrador' && (
            <> Tendrá acceso a la administración de usuarios y permisos.</>
          )}
        </p>
      ),
      ejecutar: () =>
        pendiente.tipo === 'rol' &&
        cambiarRol.mutate(
          { id: usuario.id, rol: pendiente.rol },
          { onSuccess: () => terminar('Rol actualizado.') },
        ),
    },
    desactivar: {
      titulo: 'Desactivar cuenta',
      peligroso: true,
      texto: 'Desactivar',
      mensaje: (
        <p>
          <strong>{usuario.correo}</strong> no podrá iniciar sesión hasta que la reactives.
        </p>
      ),
      ejecutar: () =>
        cambiarEstado.mutate(
          { id: usuario.id, activo: false },
          { onSuccess: () => terminar('Cuenta desactivada.') },
        ),
    },
    restablecer: {
      titulo: 'Forzar restablecimiento',
      peligroso: true,
      texto: 'Forzar restablecimiento',
      mensaje: (
        <p>
          La contraseña de <strong>{usuario.correo}</strong> dejará de funcionar de inmediato y se
          cerrarán sus sesiones. Deberá restablecerla con un código enviado por correo.
        </p>
      ),
      ejecutar: () =>
        forzar.mutate(usuario.id, { onSuccess: () => terminar('Restablecimiento forzado.') }),
    },
  }[pendiente.tipo]

  return (
    <ConfirmarDialogo
      abierto
      titulo={contenido.titulo}
      mensaje={contenido.mensaje}
      textoConfirmar={contenido.texto}
      peligroso={contenido.peligroso}
      cargando={cargando}
      error={fallo && <Alert tono="peligro">{mensajeDeError(fallo)}</Alert>}
      alConfirmar={contenido.ejecutar}
      alCerrar={() => {
        cambiarRol.reset()
        cambiarEstado.reset()
        forzar.reset()
        alCerrar()
      }}
    />
  )
}
