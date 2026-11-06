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
export const passwordNuevaSchema = z
  .string()
  .min(12, 'La contraseña debe tener al menos 12 caracteres.')
  .max(1024, 'La contraseña es demasiado larga.')
  .regex(/\p{L}/u, 'La contraseña debe incluir al menos una letra.')
  .regex(/\d/, 'La contraseña debe incluir al menos un número.')

const coinciden = <T extends { password: string; confirmacion: string }>(datos: T) =>
  datos.password === datos.confirmacion

export const loginSchema = z.object({
  email: correoSchema,
  password: z.string().min(1, 'La contraseña es obligatoria.').max(1024),
})

export const registroSchema = z
  .object({
    nombre: z
      .string()
      .trim()
      .min(1, 'El nombre es obligatorio.')
      .max(200, 'El nombre admite como máximo 200 caracteres.'),
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
