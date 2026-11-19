import { http, HttpResponse } from 'msw'
import { ESTADOS_CORREO, MAX_INTENTOS_CORREO } from '@/features/cola-correos/types'
import { db } from '../db'
import { autenticar, latencia, respuestas } from '../http'

const MAXIMO_DE_CORREOS = 200
// Correo:EsperaReintentoSegundos: tras cada fallo se espera 1 minuto × 2^intentos antes de reintentar.
const ESPERA_BASE_MS = 60_000
// El envío simulado falla para los destinatarios de este dominio: así se ven los reintentos y el tope.
export const DOMINIO_QUE_FALLA = 'fallo.test'
const ERROR_DE_ENVIO = 'No se pudo entregar el correo.'

export const colaCorreosHandlers = [
  http.get('/api/admin/email-queue', async ({ request }) => {
    await latencia()
    const { error } = autenticar(request, ['Administrador'])
    if (error) return error
    const texto = new URL(request.url).searchParams.get('estado')?.trim()
    // Solo valen los nombres de los estados (sin mayúsculas); "" es como no filtrar.
    const estado = texto
      ? ESTADOS_CORREO.find((e) => e.toLowerCase() === texto.toLowerCase())
      : null
    if (texto && !estado) return respuestas.invalida()
    const lista = db.correos
      .filter((c) => !estado || c.estado === estado)
      .sort((a, b) => Date.parse(b.fechaCreacion) - Date.parse(a.fechaCreacion))
      .slice(0, MAXIMO_DE_CORREOS)
    return HttpResponse.json(lista)
  }),

  http.post('/api/admin/email-queue/process', async ({ request }) => {
    await latencia()
    const { error } = autenticar(request, ['Administrador'])
    if (error) return error
    const ahora = Date.now()
    let enviados = 0
    let fallidos = 0
    for (const correo of db.correos) {
      // CandidatosDeEnvio + PuedeReintentarse: Pendiente, o Fallido con intentos y la espera cumplida.
      const listoParaReintentar =
        correo.estado === 'Fallido' &&
        correo.intentos < MAX_INTENTOS_CORREO &&
        Date.parse(correo.fechaUltimoIntento ?? correo.fechaCreacion) +
          ESPERA_BASE_MS * 2 ** correo.intentos <=
          ahora
      if (correo.estado !== 'Pendiente' && !listoParaReintentar) continue

      correo.fechaUltimoIntento = new Date(ahora).toISOString()
      if (correo.destinatario.toLowerCase().endsWith(`@${DOMINIO_QUE_FALLA}`)) {
        // RegistrarFallo: solo un fallo suma intentos.
        correo.estado = 'Fallido'
        correo.intentos += 1
        correo.ultimoError = ERROR_DE_ENVIO
        fallidos += 1
      } else {
        correo.estado = 'Enviado'
        correo.fechaEnvio = correo.fechaUltimoIntento
        enviados += 1
      }
    }
    // El mock no deja correos atascados en Procesando: nunca hay nada que recuperar.
    return HttpResponse.json({ enviados, fallidos, recuperados: 0 })
  }),
]
