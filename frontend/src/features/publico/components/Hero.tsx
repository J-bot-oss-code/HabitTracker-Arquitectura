import { Link } from 'react-router'
import { claseBoton } from '@/shared/ui/claseBoton'
import { Icono } from '@/shared/ui/Icono'
import { TarjetaEjemploHoy } from './TarjetaEjemploHoy'

// Hero del inicio público (§5.1, punto 2): promesa, dos acciones y la tarjeta de ejemplo.
export function Hero() {
  return (
    <section
      aria-labelledby="hero-titulo"
      className="grid items-center gap-10 py-10 sm:py-16 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)] lg:gap-14"
    >
      <div>
        <p className="inline-flex rounded-full bg-acento-suave px-3 py-1 text-sm font-semibold text-acento-fuerte">
          Para deportistas, gimnasio y cualquiera que quiera mejorar
        </p>
        <h1
          id="hero-titulo"
          className="mt-5 text-4xl font-extrabold leading-[1.08] tracking-[-0.02em] sm:text-5xl lg:text-[3.25rem]"
        >
          Convierte tu meta en un plan diario que sí puedes cumplir.
        </h1>
        <p className="mt-5 max-w-xl text-base leading-relaxed text-texto-2 sm:text-lg">
          Elige un objetivo, recibe pocas tareas concretas cada día y mira tu progreso semana a
          semana. Sin dietas extremas ni rutinas imposibles.
        </p>
        <div className="mt-8 flex flex-wrap gap-3">
          <Link to="/metas" className={claseBoton({ tamano: 'lg' })}>
            Elegir mi meta
          </Link>
          <Link to="/calculadoras" className={claseBoton({ variante: 'secundario', tamano: 'lg' })}>
            Probar sin cuenta
          </Link>
        </div>
        <p className="mt-6 flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-atenuado">
          <Icono nombre="candado" className="size-4" />
          <span>Gratis</span>
          <span aria-hidden="true">·</span>
          <span>Crea tu cuenta en un minuto</span>
          <span aria-hidden="true">·</span>
          <span>Descarga o borra tus datos cuando quieras</span>
        </p>
      </div>
      <div className="flex justify-center lg:justify-end">
        <TarjetaEjemploHoy />
      </div>
    </section>
  )
}
