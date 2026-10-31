import { useState } from 'react'
import type { UsuarioResponseDto } from '@/features/auth/types'
import { mensajeDeError } from '@/shared/api/problem'
import { useAuth } from '@/shared/auth/AuthContext'
import { ROLES, etiquetaRol, type Rol } from '@/shared/auth/roles'
import { Alert } from '@/shared/ui/Alert'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { EmptyState, PageHeader } from '@/shared/ui/Card'
import { Modal } from '@/shared/ui/Modal'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useToast } from '@/shared/ui/Toast'
import { useCambiarEstado, useCambiarRol, useForzarRestablecimiento, useUsuarios } from '../api'

export function UsersPage() {
  const usuarios = useUsuarios()
  const [busqueda, setBusqueda] = useState('')
  const [aRestablecer, setARestablecer] = useState<UsuarioResponseDto | null>(null)

  const termino = busqueda.trim().toLowerCase()
  const visibles = (usuarios.data ?? []).filter(
    (u) =>
      !termino ||
      u.nombre.toLowerCase().includes(termino) ||
      u.correo.toLowerCase().includes(termino),
  )

  return (
    <>
      <PageHeader
        titulo="Usuarios"
        descripcion="Gestiona roles, estado de las cuentas y restablecimientos de contraseña."
      />
      <div className="mb-4 max-w-sm">
        <label htmlFor="buscar-usuario" className="sr-only">
          Buscar usuario
        </label>
        <input
          id="buscar-usuario"
          type="search"
          placeholder="Buscar por nombre o correo"
          value={busqueda}
          onChange={(e) => setBusqueda(e.target.value)}
          className="block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-900"
        />
      </div>

      {usuarios.isPending ? (
        <CargandoPagina texto="Cargando usuarios…" />
      ) : usuarios.isError ? (
        <Alert tono="peligro">{mensajeDeError(usuarios.error)}</Alert>
      ) : visibles.length === 0 ? (
        <EmptyState titulo="No hay usuarios que coincidan." />
      ) : (
        <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
          <table className="w-full min-w-[720px] text-left text-sm">
            <thead className="border-b border-slate-200 text-xs uppercase tracking-wide text-slate-500 dark:border-slate-800">
              <tr>
                <th scope="col" className="px-4 py-3 font-medium">
                  Usuario
                </th>
                <th scope="col" className="px-4 py-3 font-medium">
                  Rol
                </th>
                <th scope="col" className="px-4 py-3 font-medium">
                  Estado
                </th>
                <th scope="col" className="px-4 py-3 font-medium">
                  <span className="sr-only">Acciones</span>
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
              {visibles.map((u) => (
                <FilaUsuario key={u.id} usuario={u} alRestablecer={() => setARestablecer(u)} />
              ))}
            </tbody>
          </table>
        </div>
      )}

      <Modal
        abierto={aRestablecer !== null}
        titulo="Forzar restablecimiento"
        alCerrar={() => setARestablecer(null)}
      >
        {aRestablecer && (
          <ConfirmarRestablecimiento
            usuario={aRestablecer}
            alTerminar={() => setARestablecer(null)}
          />
        )}
      </Modal>
    </>
  )
}

function FilaUsuario({
  usuario,
  alRestablecer,
}: {
  usuario: UsuarioResponseDto
  alRestablecer: () => void
}) {
  const { usuario: sesion } = useAuth()
  const { notificar } = useToast()
  const cambiarRol = useCambiarRol()
  const cambiarEstado = useCambiarEstado()
  const esYo = sesion?.id === usuario.id
  const error = (e: unknown) => notificar(mensajeDeError(e), 'peligro')

  return (
    <tr>
      <td className="px-4 py-3">
        <div className="font-medium">
          {usuario.nombre} {esYo && <span className="text-xs text-slate-500">(tú)</span>}
        </div>
        <div className="text-xs text-slate-500">{usuario.correo}</div>
      </td>
      <td className="px-4 py-3">
        <label htmlFor={`rol-${usuario.id}`} className="sr-only">
          Rol de {usuario.nombre}
        </label>
        <select
          id={`rol-${usuario.id}`}
          value={usuario.rol}
          disabled={esYo || cambiarRol.isPending}
          onChange={(e) =>
            cambiarRol.mutate(
              { id: usuario.id, rol: e.target.value as Rol },
              { onSuccess: () => notificar('Rol actualizado.'), onError: error },
            )
          }
          className="rounded-lg border border-slate-300 bg-white px-2 py-1 text-sm disabled:opacity-60 dark:border-slate-700 dark:bg-slate-900"
        >
          {ROLES.map((r) => (
            <option key={r} value={r}>
              {etiquetaRol[r]}
            </option>
          ))}
        </select>
      </td>
      <td className="px-4 py-3">
        <Badge tono={usuario.activo ? 'exito' : 'neutro'}>
          {usuario.activo ? 'Activo' : 'Inactivo'}
        </Badge>
      </td>
      <td className="px-4 py-3">
        <div className="flex justify-end gap-2">
          <Button
            tamano="sm"
            variante="secundario"
            disabled={esYo}
            cargando={cambiarEstado.isPending}
            onClick={() =>
              cambiarEstado.mutate(
                { id: usuario.id, activo: !usuario.activo },
                {
                  onSuccess: () =>
                    notificar(usuario.activo ? 'Cuenta desactivada.' : 'Cuenta activada.'),
                  onError: error,
                },
              )
            }
          >
            {usuario.activo ? 'Desactivar' : 'Activar'}
          </Button>
          <Button tamano="sm" variante="fantasma" disabled={esYo} onClick={alRestablecer}>
            Forzar restablecimiento
          </Button>
        </div>
      </td>
    </tr>
  )
}

function ConfirmarRestablecimiento({
  usuario,
  alTerminar,
}: {
  usuario: UsuarioResponseDto
  alTerminar: () => void
}) {
  const forzar = useForzarRestablecimiento()
  const { notificar } = useToast()
  return (
    <div className="space-y-4">
      <p className="text-sm">
        La contraseña de <strong>{usuario.correo}</strong> dejará de funcionar de inmediato y se
        cerrarán sus sesiones. Deberá restablecerla con un código enviado por correo.
      </p>
      {forzar.isError && <Alert tono="peligro">{mensajeDeError(forzar.error)}</Alert>}
      <div className="flex justify-end gap-2">
        <Button variante="fantasma" onClick={alTerminar}>
          Cancelar
        </Button>
        <Button
          variante="peligro"
          cargando={forzar.isPending}
          onClick={() =>
            forzar.mutate(usuario.id, {
              onSuccess: () => {
                notificar('Restablecimiento forzado.')
                alTerminar()
              },
            })
          }
        >
          Forzar restablecimiento
        </Button>
      </div>
    </div>
  )
}
