import { z } from 'zod'

// Mismos límites que los DTOs y UsuarioService del backend. Validar aquí solo mejora la
// experiencia: el servidor vuelve a validar todo (RD-07).
export const correoSchema = z
  .string()
  .trim()
  .min(1, 'El correo es obligatorio.')
  .max(150, 'El correo admite como máximo 150 caracteres.')
  .email('Escribe un correo válido.')

// Política de contraseñas del backend (PoliticaContrasena, RF-CA-14): 12 a 1024 caracteres con letras y
// números. El servidor además rechaza contraseñas que aparecen en filtraciones conocidas (Pwned Passwords).
// Única fuente de los requisitos: el esquema y la lista que ve el usuario mientras escribe salen de aquí.
export const LONGITUD_MINIMA_CLAVE = 12
const LONGITUD_MAXIMA_CLAVE = 1024

export interface RequisitoClave {
  id: 'longitud' | 'letra' | 'numero'
  texto: string
  error: string
  cumple: (clave: string) => boolean
}

export const REQUISITOS_CLAVE: readonly RequisitoClave[] = [
  {
    id: 'longitud',
    texto: `Al menos ${LONGITUD_MINIMA_CLAVE} caracteres`,
    error: `La contraseña debe tener al menos ${LONGITUD_MINIMA_CLAVE} caracteres.`,
    cumple: (clave) => clave.length >= LONGITUD_MINIMA_CLAVE,
  },
  {
    id: 'letra',
    texto: 'Al menos una letra',
    error: 'La contraseña debe incluir al menos una letra.',
    cumple: (clave) => /\p{L}/u.test(clave),
  },
  {
    id: 'numero',
    texto: 'Al menos un número',
    error: 'La contraseña debe incluir al menos un número.',
    cumple: (clave) => /\p{Nd}/u.test(clave),
  },
]

export const passwordNuevaSchema = REQUISITOS_CLAVE.reduce(
  (esquema, r) => esquema.refine(r.cumple, r.error),
  z.string().max(LONGITUD_MAXIMA_CLAVE, 'La contraseña es demasiado larga.'),
)

// Nombre y apellido: el backend solo exige 1 a 200 caracteres; el producto pide además nombre y
// apellido (al menos dos palabras con letras). Es más estricto que el servidor a propósito y está
// anotado en docs/PLAN-FRONTEND-NEXUS.md › Pendientes para el backend. Se admiten tildes, ñ,
// apóstrofos, guiones y puntos (O'Neill, 't Hooft, Pérez-Gil, Ma. José, Ana P.).
const PALABRA_NOMBRE = /^['’]?\p{L}[\p{L}\p{M}'’.-]*$/u

export const nombreCompletoSchema = z
  .string()
  .trim()
  .min(1, 'El nombre es obligatorio.')
  .max(200, 'El nombre admite como máximo 200 caracteres.')
  .refine(
    (nombre) => nombre.split(/\s+/).every((p) => PALABRA_NOMBRE.test(p)),
    'Usa solo letras, espacios, apóstrofos, guiones o puntos.',
  )
  .refine(
    (nombre) => nombre.split(/\s+/).filter((p) => /\p{L}/u.test(p)).length >= 2,
    'Escribe tu nombre y tu apellido.',
  )

const coinciden = <T extends { password: string; confirmacion: string }>(datos: T) =>
  datos.password === datos.confirmacion

export const loginSchema = z.object({
  email: correoSchema,
  password: z.string().min(1, 'La contraseña es obligatoria.').max(1024),
})

export const registroSchema = z
  .object({
    nombre: nombreCompletoSchema,
    correo: correoSchema,
    password: passwordNuevaSchema,
    confirmacion: z.string(),
  })
  .refine(coinciden, { message: 'Las contraseñas no coinciden.', path: ['confirmacion'] })

// El código de activación del correo son 6 dígitos (ActivateRequest.Codigo) y va junto al correo.
export const activarSchema = z.object({
  correo: correoSchema,
  codigo: z
    .string()
    .trim()
    .regex(/^\d{6}$/, 'El código tiene 6 dígitos.'),
})

export const soloCorreoSchema = z.object({ correo: correoSchema })

export const restablecerSchema = z
  .object({
    correo: correoSchema,
    codigo: z
      .string()
      .trim()
      .min(1, 'El código es obligatorio.')
      .max(64, 'El código no es válido.'),
    password: passwordNuevaSchema,
    confirmacion: z.string(),
  })
  .refine(coinciden, { message: 'Las contraseñas no coinciden.', path: ['confirmacion'] })

// Primera contraseña de una cuenta que solo entra con Google: el correo es el de la sesión.
export const crearPasswordSchema = z
  .object({
    codigo: z
      .string()
      .trim()
      .min(1, 'El código es obligatorio.')
      .max(64, 'El código no es válido.'),
    password: passwordNuevaSchema,
    confirmacion: z.string(),
  })
  .refine(coinciden, { message: 'Las contraseñas no coinciden.', path: ['confirmacion'] })

export const cambiarPasswordSchema = z
  .object({
    actual: z.string().min(1, 'Escribe tu contraseña actual.').max(1024),
    password: passwordNuevaSchema,
    confirmacion: z.string(),
  })
  .refine(coinciden, { message: 'Las contraseñas no coinciden.', path: ['confirmacion'] })
  .refine((d) => d.actual !== d.password, {
    message: 'La nueva contraseña debe ser distinta de la actual.',
    path: ['password'],
  })

export type LoginForm = z.infer<typeof loginSchema>
export type RegistroForm = z.infer<typeof registroSchema>
export type ActivarForm = z.infer<typeof activarSchema>
export type SoloCorreoForm = z.infer<typeof soloCorreoSchema>
export type RestablecerForm = z.infer<typeof restablecerSchema>
export type CambiarPasswordForm = z.infer<typeof cambiarPasswordSchema>
export type CrearPasswordForm = z.infer<typeof crearPasswordSchema>
