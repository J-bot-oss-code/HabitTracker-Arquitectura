import { http, HttpResponse } from 'msw'
import { leerTokenDemo } from '@/features/auth/google/tokenDemo'
import type { ConsentimientosDto, GuardarConsentimientosRequest } from '@/features/perfil/types'
import { VERSION_PRIVACIDAD, VERSION_TERMINOS } from '@/features/publico/legal'
import { auditar } from '../auditoria'
import { db } from '../db'
import { autenticar, esTexto, latencia, leerJson, respuestas } from '../http'
import { documentoADto } from './documentos'
import { aDto as habitoADto } from './habits'
import { datosPerfilSalud } from './perfil'

// Centro de privacidad real (PrivacidadController): consentimientos, baja de cuenta con periodo de gracia y
// descarga de datos. Réplica de Host/WebApi; ver README raíz › «Baja de cuenta y descarga de datos».

const DIAS_DE_GRACIA = 30
const BLOQUEO_MS = 15 * 60 * 1000
const MAX_INTENTOS = 5
const DIA = 86_400_000

function consentimientosValidos(
  b: Partial<GuardarConsentimientosRequest> | null,
  aceptadas?: ConsentimientosDto,
): b is GuardarConsentimientosRequest {
  // Se acepta la versión vigente de cada documento o la que la persona ya había aceptado: cambiar un
  // interruptor no debe obligar a aceptar documentos nuevos que no ha visto.
  const versionOk = (enviada: unknown, vigente: string, previa: string | null | undefined) =>
    enviada === vigente || (previa != null && enviada === previa)
  return (
    !!b &&
    versionOk(b.terminosVersion, VERSION_TERMINOS, aceptadas?.terminosVersion) &&
    versionOk(b.privacidadVersion, VERSION_PRIVACIDAD, aceptadas?.privacidadVersion) &&
    typeof b.datosSalud === 'boolean' &&
    typeof b.marketing === 'boolean' &&
    typeof b.compartirCumplimiento === 'boolean' &&
    typeof b.estadisticasAnonimas === 'boolean'
  )
}

const bloqueada = (u: { bloqueadoHasta: number | null }) =>
  u.bloqueadoHasta !== null && u.bloqueadoHasta > Date.now()

export const privacidadHandlers = [
  // Cualquier usuario autenticado (un administrador también acepta los documentos).
  http.get('/api/privacy/consents', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    const consentimientos = db.consentimientos.get(sesion.usuario.id)
    if (!consentimientos) return respuestas.noEncontrado('No hay consentimientos registrados.')
    return HttpResponse.json(consentimientos)
  }),

  http.put('/api/privacy/consents', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    const body = await leerJson<GuardarConsentimientosRequest>(request)
    const anterior = db.consentimientos.get(sesion.usuario.id)
    if (!consentimientosValidos(body, anterior)) return respuestas.invalida()
    const mismasVersiones =
      anterior?.terminosVersion === body.terminosVersion &&
      anterior?.privacidadVersion === body.privacidadVersion
    const consentimientos: ConsentimientosDto = {
      terminosVersion: body.terminosVersion,
      privacidadVersion: body.privacidadVersion,
      // La fecha de aceptación la fija el servidor y solo cambia con una versión nueva.
      aceptadoEn:
        mismasVersiones && anterior?.aceptadoEn ? anterior.aceptadoEn : new Date().toISOString(),
      datosSalud: body.datosSalud,
      marketing: body.marketing,
      compartirCumplimiento: body.compartirCumplimiento,
      estadisticasAnonimas: body.estadisticasAnonimas,
    }
    db.consentimientos.set(sesion.usuario.id, consentimientos)
    return HttpResponse.json(consentimientos)
  }),

  // Desactiva la cuenta o programa su eliminación. Los administradores quedan fuera (403).
  http.post('/api/privacy/deletion-request', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Estandar'])
    if (error) return error
    const body = await leerJson<{ modo: string; password: string; idTokenGoogle: string }>(request)
    const modo = typeof body?.modo === 'string' ? body.modo.toLowerCase() : ''
    if (!body || (modo !== 'desactivar' && modo !== 'eliminar')) return respuestas.invalida()
    const usuario = sesion.usuario

    // Confirmación: la contraseña o, si la cuenta no tiene, el token de la cuenta de Google vinculada.
    // Un error es siempre un 400 (un 401 cerraría la sesión en el cliente) y cuenta para el bloqueo.
    if (usuario.password !== null) {
      if (!esTexto(body.password, 1, 1024) || body.password !== usuario.password) {
        if (esTexto(body.password, 1, 1024)) {
          usuario.intentosFallidos += 1
          if (usuario.intentosFallidos >= MAX_INTENTOS) {
            usuario.bloqueadoHasta = Date.now() + BLOQUEO_MS
          }
        }
        return bloqueada(usuario)
          ? respuestas.codigo('cuenta_bloqueada', 'La cuenta está bloqueada temporalmente.')
          : respuestas.codigo('clave_actual_incorrecta', 'La contraseña actual no coincide.')
      }
      if (bloqueada(usuario)) {
        return respuestas.codigo('cuenta_bloqueada', 'La cuenta está bloqueada temporalmente.')
      }
    } else {
      const identidad = leerTokenDemo(body.idTokenGoogle)
      const vinculo = db.vinculosGoogle.get(usuario.id)
      if (!identidad?.correoVerificado || vinculo?.subject !== identidad.sub) {
        return respuestas.codigo(
          'confirmacion_incorrecta',
          'No se pudo confirmar la operación con tu cuenta de Google.',
        )
      }
    }

    const ahora = Date.now()
    let borradoEn: string | null = null
    if (modo === 'desactivar') {
      // Inactiva por su dueño: un inicio de sesión correcto la reactiva.
      usuario.activo = false
      usuario.desactivadaPorUsuario = true
    } else {
      // Pedirlo otra vez conserva la fecha original.
      const existente = db.bajas.get(usuario.id)
      const baja = existente ?? {
        solicitadaEn: new Date(ahora).toISOString(),
        borradoEn: new Date(ahora + DIAS_DE_GRACIA * DIA).toISOString(),
      }
      db.bajas.set(usuario.id, baja)
      borradoEn = baja.borradoEn
    }
    // Las sesiones abiertas dejan de servir en el momento.
    usuario.sesionesValidasDesde = ahora
    db.sesiones = db.sesiones.filter((s) => s.usuarioId !== usuario.id)
    if (db.refresh?.usuarioId === usuario.id) db.refresh = null
    auditar({
      actor: usuario,
      accion: 'Solicitud de baja de cuenta',
      entidad: 'Usuario',
      entidadId: usuario.id,
      valorAnterior: 'Cuenta activa',
      valorNuevo: modo === 'desactivar' ? 'Desactivada por su dueño' : 'Eliminación programada',
    })
    return HttpResponse.json({ modo, borradoEn }, { status: 202 })
  }),

  http.get('/api/privacy/deletion-request', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Estandar'])
    if (error) return error
    const baja = db.bajas.get(sesion.usuario.id)
    if (!baja) return respuestas.noEncontrado('La cuenta no tiene una eliminación programada.')
    return HttpResponse.json({ borradoEn: baja.borradoEn })
  }),

  // Cancelar durante el periodo de gracia no pide la contraseña: solo conserva la cuenta.
  http.delete('/api/privacy/deletion-request', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Estandar'])
    if (error) return error
    if (!db.bajas.delete(sesion.usuario.id)) {
      return respuestas.noEncontrado('La cuenta no tiene una eliminación programada.')
    }
    return new HttpResponse(null, { status: 204 })
  }),

  // Todo lo que el sistema guarda de la persona. Nunca la contraseña, su hash ni los tokens.
  http.get('/api/privacy/export', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Estandar'])
    if (error) return error
    const u = sesion.usuario
    const consentimiento = db.consentimientos.get(u.id)
    const vinculo = db.vinculosGoogle.get(u.id)
    const baja = db.bajas.get(u.id)
    auditar({
      actor: u,
      accion: 'Descarga de datos personales',
      entidad: 'Usuario',
      entidadId: u.id,
      valorAnterior: '-',
      valorNuevo: '-',
    })
    return HttpResponse.json({
      generadoEn: new Date().toISOString(),
      cuenta: {
        id: u.id,
        nombre: u.nombre,
        correo: u.correo,
        rol: u.rol,
        activo: u.activo,
        fechaCreacion: new Date().toISOString(),
        tienePassword: u.password !== null,
        desactivadaPorUsuarioEn: null,
        bajaProgramadaPara: baja?.borradoEn ?? null,
      },
      consentimientos: consentimiento
        ? [
            {
              terminosVersion: consentimiento.terminosVersion,
              privacidadVersion: consentimiento.privacidadVersion,
              datosSalud: consentimiento.datosSalud,
              marketing: consentimiento.marketing,
              compartirCumplimiento: consentimiento.compartirCumplimiento,
              estadisticasAnonimas: consentimiento.estadisticasAnonimas,
              aceptadoEn: consentimiento.aceptadoEn,
              actualizadoEn: consentimiento.aceptadoEn,
              origen: 'registro',
            },
          ]
        : [],
      vinculos: vinculo ? [{ proveedor: 'google', vinculadoEn: vinculo.vinculadoEn }] : [],
      sesiones: db.sesiones
        .filter((s) => s.usuarioId === u.id)
        .map((s) => ({
          id: s.id,
          dispositivo: s.dispositivo,
          ip: null,
          creadaEn: s.creadaEn,
          ultimoUsoEn: s.ultimoUsoEn,
          expiraEn: new Date(Date.parse(s.ultimoUsoEn) + 14 * DIA).toISOString(),
        })),
      modulos: {
        habitos: db.habitos.filter((h) => h.usuarioId === u.id).map(habitoADto),
        permisos: db.solicitudes.filter((s) => s.solicitante.toLowerCase() === u.correo),
        // ParticipanteDocumentos: los metadatos de sus archivos sin eliminar (los bytes se bajan aparte).
        documentos: db.documentos
          .filter((d) => d.usuarioId === u.id && !d.eliminado)
          .map(documentoADto),
        // ParticipantePlanes (PlanExportadoDto y LogroDto): los planes con sus pausas y los archivos vinculados
        // (los archivos mismos van en `documentos`), y los logros.
        planes: {
          planes: db.planesUsuario
            .filter((p) => p.usuarioId === u.id)
            .map((p) => ({
              id: p.id,
              slug: p.slug,
              nombre: p.nombre,
              estado: p.estado,
              fechaInicio: p.fechaInicio,
              fechaFinPrevista: p.fechaFinPrevista,
              extensiones: p.extensiones,
              modoSuaveHasta: p.modoSuaveHasta,
              horaRecordatorio: p.horaRecordatorio,
              recordatoriosActivos: p.recordatoriosActivos,
              pausas: p.pausas,
              archivosVinculados: db.adjuntos
                .filter((a) => a.planUsuarioId === p.id)
                .map((a) => a.documentoId),
            })),
          logros: db.logros
            .filter((l) => l.usuarioId === u.id)
            .map((l) => ({ clave: l.clave, obtenidoEn: l.obtenidoEn, visto: l.vistoEn !== null })),
        },
        // ParticipanteNotificaciones: la preferencia de correo y la bandeja completa.
        notificaciones: {
          preferencias: { correoActivo: db.preferenciasCorreo.get(u.id) ?? true },
          notificaciones: db.notificaciones
            .filter((n) => n.usuarioId === u.id)
            .map((n) => ({
              id: n.id,
              tipo: n.tipo,
              titulo: n.titulo,
              mensaje: n.mensaje,
              creadaEnUtc: n.creadaEnUtc,
              leida: n.leida,
              leidaEnUtc: n.leidaEnUtc,
            })),
        },
        // ParticipanteSugerencias: las quejas y mejoras que escribió (SugerenciaDto), de la más reciente a la más antigua.
        sugerencias: db.sugerencias
          .filter((s) => s.usuarioId === u.id)
          .sort((a, b) => Date.parse(b.creadaEn) - Date.parse(a.creadaEn))
          .map((s) => ({
            id: s.id,
            tipo: s.tipo,
            texto: s.texto,
            creadaEn: s.creadaEn,
            estado: s.estado,
          })),
        // ParticipantePerfilSalud: el cuestionario va descifrado para su dueño.
        perfilYSalud: datosPerfilSalud(u.id),
      },
    })
  }),
]
