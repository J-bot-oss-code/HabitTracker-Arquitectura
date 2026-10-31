// Configuración leída de variables de entorno de Vite (RD-10: nada sensible aquí,
// todo lo que empieza por VITE_ termina en el bundle público).
const raw = import.meta.env

export const env = {
  apiUrl: (raw.VITE_API_URL ?? 'http://localhost:5246').replace(/\/+$/, ''),
  // En desarrollo y pruebas la API simulada está activa salvo VITE_USE_MOCKS=false.
  // En un build de producción solo se activa con VITE_USE_MOCKS=true explícito, para no
  // publicar por accidente los datos y credenciales de demostración.
  // import.meta.env directo para que Vite lo sustituya y elimine el código de mocks del build.
  useMocks: import.meta.env.PROD
    ? import.meta.env.VITE_USE_MOCKS === 'true'
    : import.meta.env.VITE_USE_MOCKS !== 'false',
  metaIdPorDefecto: raw.VITE_META_ID_POR_DEFECTO ?? '',
} as const

export function apiBaseUrl(): string {
  // Con mocks se llama al propio origen y MSW intercepta /api/*.
  return env.useMocks ? window.location.origin : env.apiUrl
}
