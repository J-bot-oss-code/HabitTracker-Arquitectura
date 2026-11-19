import type { HttpHandler } from 'msw'
import type { Rol } from '@/shared/auth/roles'
import { adminHandlers, contratosAdmin } from './admin'

// Contratos propuestos: endpoints que Host/WebApi todavía NO tiene y que el frontend necesita
// para mostrar la aplicación como debe verse en producción (docs/mejoras). Se simulan aquí
// para que las pantallas funcionen en modo demo; cuando el backend los implemente (después de
// las prácticas), se mueven a los handlers normales y se quitan de este registro.
//
// Reglas:
// - Cada handler de esta carpeta debe figurar en CONTRATOS_PROPUESTOS (lo exige
//   propuestas.test.ts) y ninguna ruta puede pisar un endpoint real.
// - Los límites de validación viven en el schemas.ts de cada feature, marcados como
//   "Límite propuesto para el backend (mejora futura)".
// - Con VITE_USE_MOCKS=false estos endpoints no existen: la interfaz lo detecta con
//   esNoDisponible() y muestra un aviso en lugar de fallar.
export type MetodoHttp = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'

export interface ContratoPropuesto {
  metodo: MetodoHttp
  ruta: string
  proposito: string
  // 'publico': sin sesión (p. ej. el catálogo de metas cuando venga del backend).
  roles: Rol[] | 'publico'
  // Sección de docs/mejoras que motiva el endpoint.
  origen: string
}

interface ModuloPropuesto {
  contratos: ContratoPropuesto[]
  handlers: HttpHandler[]
}

const modulos: ModuloPropuesto[] = [
  // Paso 7: métricas anónimas y auditoría del administrador.
  { contratos: contratosAdmin, handlers: adminHandlers },
]

export const CONTRATOS_PROPUESTOS: ContratoPropuesto[] = modulos.flatMap((m) => m.contratos)

export const propuestasHandlers: HttpHandler[] = modulos.flatMap((m) => m.handlers)
