// El backend no emite refresh token: se guarda solo el access token (vence en 60 min).
// sessionStorage limita la exposición a la pestaña actual; si está bloqueado,
// la sesión vive solo en memoria.
const CLAVE = 'habittracker.token'
let enMemoria: string | null = null

export const tokenStorage = {
  leer(): string | null {
    try {
      return sessionStorage.getItem(CLAVE) ?? enMemoria
    } catch {
      return enMemoria
    }
  },
  guardar(token: string) {
    enMemoria = token
    try {
      sessionStorage.setItem(CLAVE, token)
    } catch {
      // Almacenamiento bloqueado: queda en memoria.
    }
  },
  borrar() {
    enMemoria = null
    try {
      sessionStorage.removeItem(CLAVE)
    } catch {
      // Nada que borrar.
    }
  },
}
