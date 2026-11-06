// El token de acceso (JWT, 15 min) vive SOLO en memoria: ni sessionStorage ni localStorage, así un XSS
// no puede leerlo de un almacenamiento persistente. La sesión larga la sostiene el refresh token, que
// el navegador guarda en una cookie HttpOnly que JavaScript no puede leer (ver shared/api/sesion.ts).
let enMemoria: string | null = null

// Marca NO sensible ("esta persona abrió sesión en este navegador"): solo sirve para decidir si al
// cargar la página vale la pena intentar renovar la sesión con la cookie, sin hacer una petición
// inútil (y un 401) a cada visitante de las páginas públicas.
const CLAVE_SESION = 'habittracker.sesion'

function marca(valor: boolean) {
  try {
    if (valor) localStorage.setItem(CLAVE_SESION, '1')
    else localStorage.removeItem(CLAVE_SESION)
  } catch {
    // Almacenamiento bloqueado: sin marca, la sesión no se restaura al recargar.
  }
}

export const tokenStorage = {
  leer(): string | null {
    return enMemoria
  },
  guardar(token: string) {
    enMemoria = token
    marca(true)
  },
  borrar() {
    enMemoria = null
    marca(false)
  },
  // ¿Hubo una sesión abierta en este navegador? (no dice si la cookie sigue siendo válida)
  huboSesion(): boolean {
    try {
      return localStorage.getItem(CLAVE_SESION) === '1'
    } catch {
      return false
    }
  },
}
