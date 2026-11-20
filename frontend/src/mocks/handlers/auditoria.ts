import { http, HttpResponse } from 'msw'
import { esDiaReal } from '@/features/auditoria/schemas'
import type { PaginaAuditoriaDto } from '@/features/auditoria/types'
import { db } from '../db'
import { autenticar, latencia, respuestas } from '../http'

// Réplica de GET /api/audit (AuditController + RegistroAuditoriaService): solo Administrador, solo lectura
// (RF-AUD-06: no hay ninguna ruta para editar ni borrar) y filtros combinables. Un filtro inválido es 400.
const DIA = 86_400_000
const MAXIMO_DIAS_RANGO = 366
const TAMANO_MAXIMO = 200

// Texto opcional: vacío = sin filtro; recortado y con tope de longitud (null si se pasa del máximo).
const texto = (v: string | null, maximo: number): string | undefined | null => {
  const limpio = v?.trim()
  if (!limpio) return undefined
  return limpio.length > maximo ? null : limpio
}

// AAAA-MM-DD de un día real (año 2000 a 9000); undefined si no viene; null si es inválido.
const dia = (v: string | null): number | undefined | null => {
  if (v === null || v === '') return undefined
  if (!/^\d{4}-\d{2}-\d{2}$/.test(v) || !esDiaReal(v)) return null
  const anio = Number(v.slice(0, 4))
  return anio < 2000 || anio > 9000 ? null : Date.parse(`${v}T00:00:00Z`)
}

const entero = (v: string | null, porDefecto: number) => (v === null ? porDefecto : Number(v))

export const auditoriaHandlers = [
  http.get('/api/audit', async ({ request }) => {
    await latencia()
    const { error } = autenticar(request, ['Administrador'])
    if (error) return error

    const q = new URL(request.url).searchParams
    const usuario = texto(q.get('usuario'), 254)
    const entidad = texto(q.get('entidad'), 100)
    const entidadId = texto(q.get('entidadId'), 100)
    const accion = texto(q.get('accion'), 100)
    const desde = dia(q.get('desde'))
    const hasta = dia(q.get('hasta'))
    const pagina = entero(q.get('pagina'), 1)
    const tamano = entero(q.get('tamano'), 50)
    // RD-07: el "servidor" valida cada filtro antes de usarlo.
    if (
      [usuario, entidad, entidadId, accion, desde, hasta].includes(null) ||
      (desde != null && hasta != null && hasta < desde) ||
      (desde != null && hasta != null && (hasta - desde) / DIA + 1 > MAXIMO_DIAS_RANGO) ||
      !Number.isInteger(pagina) ||
      pagina < 1 ||
      !Number.isInteger(tamano) ||
      tamano < 1 ||
      tamano > TAMANO_MAXIMO
    ) {
      return respuestas.invalida()
    }

    const coinciden = db.auditoria
      .filter((e) => {
        const t = Date.parse(e.fecha)
        return (
          (desde == null || t >= desde) &&
          // "hasta" incluye todo ese día UTC: el límite es el día siguiente.
          (hasta == null || t < hasta + DIA) &&
          (!accion || e.accion === accion) &&
          (!entidad || e.entidad === entidad) &&
          (!entidadId || e.entidadId === entidadId) &&
          (!usuario || e.actor.toLowerCase().includes(usuario.toLowerCase()))
        )
      })
      .sort((a, b) => Date.parse(b.fecha) - Date.parse(a.fecha))
    const respuesta: PaginaAuditoriaDto = {
      total: coinciden.length,
      pagina,
      tamano,
      registros: coinciden.slice((pagina - 1) * tamano, pagina * tamano),
    }
    return HttpResponse.json(respuesta)
  }),
]
