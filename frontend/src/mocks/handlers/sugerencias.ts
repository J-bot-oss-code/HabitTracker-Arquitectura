import { http, HttpResponse } from 'msw'
import {
  CODIGO_LIMITE,
  ESTADOS_SUGERENCIA,
  LONGITUD_MAXIMA,
  LONGITUD_MINIMA,
  MAXIMO_POR_DIA,
  TAMANO_MAXIMO,
  TIPOS_SUGERENCIA,
  type EstadoSugerencia,
  type SugerenciaAdminDto,
  type SugerenciaDto,
  type TipoSugerencia,
} from '@/features/sugerencias/types'
import { db, type SugerenciaMock } from '../db'
import { autenticar, esGuid, latencia, leerJson, problema, respuestas } from '../http'

// Réplica de SugerenciasController y SugerenciasService: Estándar envía y lee lo suyo (api/feedback); el Administrador lee
// la bandeja y marca el estado (api/admin/feedback). Cada rol recibe 403 en las rutas del otro.

const DIA = 86_400_000

// El enum viaja como texto; System.Text.Json lo acepta sin distinguir mayúsculas y rechaza cualquier otro valor.
const aEnum = <T extends string>(valores: readonly T[], v: unknown): T | undefined =>
  typeof v === 'string' ? valores.find((x) => x.toLowerCase() === v.toLowerCase()) : undefined

const aDto = (s: SugerenciaMock): SugerenciaDto => ({
  id: s.id,
  tipo: s.tipo,
  texto: s.texto,
  creadaEn: s.creadaEn,
  estado: s.estado,
})

const aDtoAdmin = (s: SugerenciaMock): SugerenciaAdminDto => ({
  ...aDto(s),
  usuarioId: s.usuarioId,
})

// De la más reciente a la más antigua, como el repositorio.
const recientesPrimero = (lista: SugerenciaMock[]) =>
  [...lista].sort((a, b) => Date.parse(b.creadaEn) - Date.parse(a.creadaEn))

// [FromQuery] int?: un valor que no es un entero es un 400 de validación del modelo.
const enteroOValor = (valor: string | null, porDefecto: number): number | null => {
  if (valor === null) return porDefecto
  return /^-?\d+$/.test(valor) ? Number(valor) : null
}

export const sugerenciasHandlers = [
  http.post('/api/feedback', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Estandar'])
    if (error) return error
    const body = await leerJson<{ tipo: unknown; texto: unknown }>(request)
    const tipo = aEnum<TipoSugerencia>(TIPOS_SUGERENCIA, body?.tipo)
    // [Required] string con [StringLength(2000)]: el tope se mide sobre el texto tal cual llega.
    if (!body || !tipo || typeof body.texto !== 'string' || body.texto.length > LONGITUD_MAXIMA) {
      return respuestas.invalida()
    }
    // Sugerencia (dominio): entre 10 y 2000 caracteres DESPUÉS de recortar los espacios.
    const texto = body.texto.trim()
    if (texto.length < LONGITUD_MINIMA || texto.length > LONGITUD_MAXIMA) {
      return respuestas.invalida()
    }
    // Tope contra el abuso: como mucho 5 en las últimas 24 horas (la validación va antes: RD-07).
    const desde = Date.now() - DIA
    const recientes = db.sugerencias.filter(
      (s) => s.usuarioId === sesion.usuario.id && Date.parse(s.creadaEn) >= desde,
    ).length
    if (recientes >= MAXIMO_POR_DIA) {
      return respuestas.codigo(
        CODIGO_LIMITE,
        `Ya nos escribiste ${MAXIMO_POR_DIA} veces hoy. Gracias: vuelve a escribirnos mañana.`,
      )
    }
    const nueva: SugerenciaMock = {
      id: crypto.randomUUID(),
      usuarioId: sesion.usuario.id,
      tipo,
      texto,
      creadaEn: new Date().toISOString(),
      estado: 'Nueva',
    }
    db.sugerencias.push(nueva)
    return HttpResponse.json(aDto(nueva), { status: 201 })
  }),

  http.get('/api/feedback', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Estandar'])
    if (error) return error
    return HttpResponse.json(
      recientesPrimero(db.sugerencias.filter((s) => s.usuarioId === sesion.usuario.id)).map(aDto),
    )
  }),

  http.get('/api/admin/feedback', async ({ request }) => {
    await latencia()
    const { error } = autenticar(request, ['Administrador'])
    if (error) return error
    const consulta = new URL(request.url).searchParams
    const crudo = consulta.get('estado')
    const estado =
      crudo === null || crudo === ''
        ? undefined
        : aEnum<EstadoSugerencia>(ESTADOS_SUGERENCIA, crudo)
    const pagina = enteroOValor(consulta.get('pagina'), 1)
    const tamano = enteroOValor(consulta.get('tamano'), 20)
    if (
      (crudo && !estado) ||
      pagina === null ||
      tamano === null ||
      pagina < 1 ||
      tamano < 1 ||
      tamano > TAMANO_MAXIMO
    ) {
      return respuestas.invalida()
    }
    const filtradas = recientesPrimero(
      db.sugerencias.filter((s) => estado === undefined || s.estado === estado),
    )
    return HttpResponse.json({
      total: filtradas.length,
      pagina,
      tamano,
      // Las que siguen sin leer, con cualquier filtro.
      nuevas: db.sugerencias.filter((s) => s.estado === 'Nueva').length,
      sugerencias: filtradas.slice((pagina - 1) * tamano, pagina * tamano).map(aDtoAdmin),
    })
  }),

  http.post<{ id: string }>('/api/admin/feedback/:id/status', async ({ request, params }) => {
    await latencia()
    const { error } = autenticar(request, ['Administrador'])
    if (error) return error
    // La ruta {id:guid} no atiende otro formato: 404 sin cuerpo.
    if (!esGuid(params.id)) return new HttpResponse(null, { status: 404 })
    const body = await leerJson<{ estado: unknown }>(request)
    const estado = aEnum<EstadoSugerencia>(ESTADOS_SUGERENCIA, body?.estado)
    if (!estado) return respuestas.invalida()
    const sugerencia = db.sugerencias.find((s) => s.id === params.id)
    if (!sugerencia) {
      return problema(404, 'No encontrado.', 'La sugerencia no existe.', 'sugerencia_no_encontrada')
    }
    // Sugerencia.CambiarEstado (RD-04): nada vuelve a «Nueva» una vez vista; repetir el mismo estado no cambia nada.
    if (estado === 'Nueva' && sugerencia.estado !== 'Nueva') return respuestas.invalida()
    sugerencia.estado = estado
    return HttpResponse.json(aDtoAdmin(sugerencia))
  }),
]
