import { http, HttpResponse } from 'msw'
import type { PaginaAuditoriaDto } from '@/features/auditoria/types'
import { db } from '../db'
import { autenticar, latencia, respuestas } from '../http'

// Réplica de GET /api/me/activity (MiActividadController + RegistroAuditoriaService.MiActividadAsync): cualquier
// persona con sesión, y SOLO lo suyo (la persona sale de la sesión, nunca de la URL). `dias` es 7 o 30 (por
// defecto 7); otro valor, una página menor que 1 o un tamaño fuera de 1 a 200 es 400. Solo lectura (RF-AUD-06).
const DIA = 86_400_000
const TAMANO_MAXIMO = 200

const entero = (v: string | null, porDefecto: number) => (v === null ? porDefecto : Number(v))

export const miActividadHandlers = [
  http.get('/api/me/activity', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error

    const q = new URL(request.url).searchParams
    const dias = entero(q.get('dias'), 7)
    const pagina = entero(q.get('pagina'), 1)
    const tamano = entero(q.get('tamano'), 50)
    // RD-07: el "servidor" valida cada dato antes de usarlo.
    if (
      ![7, 30].includes(dias) ||
      !Number.isInteger(pagina) ||
      pagina < 1 ||
      !Number.isInteger(tamano) ||
      tamano < 1 ||
      tamano > TAMANO_MAXIMO
    ) {
      return respuestas.invalida()
    }

    const desde = Date.now() - dias * DIA
    const propios = db.auditoria
      .filter((e) => e.usuarioId === sesion.usuario.id && Date.parse(e.fecha) >= desde)
      .sort((a, b) => Date.parse(b.fecha) - Date.parse(a.fecha))
    const respuesta: PaginaAuditoriaDto = {
      total: propios.length,
      pagina,
      tamano,
      registros: propios.slice((pagina - 1) * tamano, pagina * tamano),
    }
    return HttpResponse.json(respuesta)
  }),
]
