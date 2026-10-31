import type { ReactNode, SVGProps } from 'react'

// Set de iconos propio (trazo de 1.75 en rejilla de 24). Sin dependencias: pesa casi nada y
// hereda el color del texto con currentColor.
const trazos = {
  habitos: <path d="M4 13.5 9 18.5 20 6.5M4 7h6" />,
  permisos: (
    <>
      <path d="M12 3 5 6v5.5c0 4.2 2.8 7.4 7 9.5 4.2-2.1 7-5.3 7-9.5V6l-7-3Z" />
      <path d="m9 12 2.2 2.2L15.5 10" />
    </>
  ),
  usuarios: (
    <>
      <circle cx="9" cy="8.5" r="3.2" />
      <path d="M3.5 19c.6-3.2 2.9-5 5.5-5s4.9 1.8 5.5 5" />
      <path d="M16 5.6a3 3 0 0 1 0 5.8M17.5 14.4c1.7.6 2.7 2.2 3 4.6" />
    </>
  ),
  documentos: (
    <>
      <path d="M7 3.5h7l4 4V20a.5.5 0 0 1-.5.5h-10A.5.5 0 0 1 7 20V3.5Z" />
      <path d="M13.5 3.5V8H18M9.5 12.5h5M9.5 16h5" />
    </>
  ),
  notificaciones: (
    <>
      <path d="M6 16.5V11a6 6 0 1 1 12 0v5.5l1.5 2h-15L6 16.5Z" />
      <path d="M10 20.5a2 2 0 0 0 4 0" />
    </>
  ),
  reportes: <path d="M4 20V10m5.5 10V4m5.5 16v-7m5 7V8" />,
  correo: (
    <>
      <rect x="3.5" y="5.5" width="17" height="13" rx="2" />
      <path d="m4 7.5 8 6 8-6" />
    </>
  ),
  auditoria: (
    <>
      <circle cx="10.5" cy="10.5" r="6" />
      <path d="m15 15 5 5M8 10.5h5M10.5 8v5" />
    </>
  ),
  cuenta: (
    <>
      <circle cx="12" cy="8.5" r="3.5" />
      <path d="M5 20c.8-3.6 3.5-5.5 7-5.5s6.2 1.9 7 5.5" />
    </>
  ),
  salir: <path d="M10 4.5H6.5a1 1 0 0 0-1 1v13a1 1 0 0 0 1 1H10M15 8l4 4-4 4M19 12H9.5" />,
  tema: (
    <>
      <circle cx="12" cy="12" r="8.5" />
      <path d="M12 3.5v17" />
      <path d="M12 3.5a8.5 8.5 0 0 1 0 17Z" fill="currentColor" stroke="none" />
    </>
  ),
  menu: <path d="M4 7h16M4 12h16M4 17h16" />,
  cerrar: <path d="m6 6 12 12M18 6 6 18" />,
  mas: <path d="M12 5v14M5 12h14" />,
  check: <path d="m5 12.5 4.5 4.5L19 7.5" />,
  flecha: <path d="M5 12h14m-5-5 5 5-5 5" />,
  volver: <path d="M19 12H5m5-5-5 5 5 5" />,
  llama: (
    <path d="M12 3.5c.6 3-1.6 4.4-3 6.3-1.4 1.9-1.4 4.4.2 6.2A4.9 4.9 0 0 0 12 17.5c2.5 0 4.5-1.8 4.5-4.4 0-1.5-.7-2.7-1.6-3.7.1 1.2-.4 2.1-1.2 2.6.3-3-.8-6.2-1.7-8.5Z" />
  ),
  trofeo: (
    <>
      <path d="M8 4.5h8v5a4 4 0 0 1-8 0v-5Z" />
      <path d="M8 6H5v1.5A3 3 0 0 0 8 10.5M16 6h3v1.5a3 3 0 0 1-3 3M12 13.5v3.5m-3.5 3.5h7M10 17h4" />
    </>
  ),
  play: <path d="M8 5.5v13l11-6.5-11-6.5Z" />,
  pausa: <path d="M8.5 5.5v13m7-13v13" />,
  calendario: (
    <>
      <rect x="4" y="5.5" width="16" height="14.5" rx="2" />
      <path d="M4 10h16M8.5 3.5v4M15.5 3.5v4" />
    </>
  ),
  alerta: (
    <>
      <path d="M12 4 3.5 19h17L12 4Z" />
      <path d="M12 10v4.2M12 16.8v.1" />
    </>
  ),
  info: (
    <>
      <circle cx="12" cy="12" r="8.5" />
      <path d="M12 11v5M12 8v.1" />
    </>
  ),
  candado: (
    <>
      <rect x="5.5" y="10.5" width="13" height="9.5" rx="2" />
      <path d="M8.5 10.5V8a3.5 3.5 0 0 1 7 0v2.5" />
    </>
  ),
  rayo: <path d="m13 3-7 10.5h5L10 21l8-11.5h-5.5L13 3Z" />,
  buscar: (
    <>
      <circle cx="10.5" cy="10.5" r="6" />
      <path d="m15 15 5 5" />
    </>
  ),
} satisfies Record<string, ReactNode>

export type NombreIcono = keyof typeof trazos

interface IconoProps extends Omit<SVGProps<SVGSVGElement>, 'name'> {
  nombre: NombreIcono
  // Si se omite, el icono es decorativo y se oculta a lectores de pantalla.
  titulo?: string
}

export function Icono({ nombre, titulo, className = 'size-5', ...props }: IconoProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      role={titulo ? 'img' : undefined}
      aria-label={titulo}
      aria-hidden={titulo ? undefined : true}
      {...props}
    >
      {titulo && <title>{titulo}</title>}
      {trazos[nombre]}
    </svg>
  )
}
