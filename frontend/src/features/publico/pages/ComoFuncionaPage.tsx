import { Link } from 'react-router'
import { useAuth } from '@/shared/auth/AuthContext'
import { Card } from '@/shared/ui/Card'
import { claseBoton } from '@/shared/ui/claseBoton'
import { Icono } from '@/shared/ui/Icono'
import { PASOS, PREGUNTAS, VENTAJAS } from '../guia'

const claseTituloSeccion = 'text-3xl font-extrabold sm:text-[2rem]'

// Guía pública (/como-funciona): qué ofrece la app y cómo empezar. Se ve con y sin sesión.
export function ComoFuncionaPage() {
  return (
    <div className="mx-auto max-w-4xl pb-12">
      <header className="py-10 sm:py-14">
        <p className="rotulo">Guía</p>
        <h1 className="mt-2 text-4xl font-extrabold leading-[1.1] tracking-[-0.02em] sm:text-5xl">
          Cómo funciona HabitTracker Nexus
        </h1>
        <p className="mt-5 max-w-2xl text-base leading-relaxed text-texto-2 sm:text-lg">
          Aquí lo que cuenta es volver. Pocas tareas al día y un ritmo que puedas sostener, también
          en los días difíciles.
        </p>
      </header>

      <section aria-labelledby="ofrece-titulo" className="py-6">
        <h2 id="ofrece-titulo" className={claseTituloSeccion}>
          Qué te ofrece
        </h2>
        <ul className="mt-6 grid gap-4 sm:grid-cols-2">
          {VENTAJAS.map((v) => (
            <li key={v.titulo}>
              <Card className="h-full p-5">
                <Icono nombre={v.icono} className="size-6 text-acento" />
                <h3 className="mt-3 text-lg font-bold">{v.titulo}</h3>
                <p className="mt-1.5 text-sm leading-relaxed text-atenuado">{v.texto}</p>
              </Card>
            </li>
          ))}
        </ul>
      </section>

      <section aria-labelledby="empezar-titulo" className="py-6">
        <h2 id="empezar-titulo" className={claseTituloSeccion}>
          Cómo empezar
        </h2>
        <ol className="mt-6 space-y-3">
          {PASOS.map((p, i) => (
            <li key={p.titulo}>
              <Card className="flex gap-4 p-5">
                <span aria-hidden="true" className="cifra w-8 shrink-0 text-3xl text-acento">
                  {i + 1}
                </span>
                <div className="min-w-0">
                  <h3 className="text-lg font-bold">{p.titulo}</h3>
                  <p className="mt-1 text-sm leading-relaxed text-atenuado">{p.texto}</p>
                </div>
              </Card>
            </li>
          ))}
        </ol>
      </section>

      <section aria-labelledby="preguntas-titulo" className="py-6">
        <h2 id="preguntas-titulo" className={claseTituloSeccion}>
          Preguntas frecuentes
        </h2>
        <div className="mt-6 space-y-3">
          {PREGUNTAS.map((q) => (
            <details
              key={q.pregunta}
              className="group rounded-2xl border border-borde bg-superficie"
            >
              <summary className="flex min-h-11 cursor-pointer list-none items-center justify-between gap-3 rounded-2xl px-5 py-3 font-bold [&::-webkit-details-marker]:hidden">
                {q.pregunta}
                <Icono
                  nombre="flecha"
                  className="size-4 shrink-0 text-atenuado transition-transform duration-150 group-open:rotate-90 motion-reduce:transition-none"
                />
              </summary>
              <p className="px-5 pb-4 text-sm leading-relaxed text-texto-2">{q.respuesta}</p>
            </details>
          ))}
        </div>
      </section>

      <LlamadaFinal />
    </div>
  )
}

// Con sesión se ofrece seguir; sin ella, crear la cuenta o entrar. El Administrador no tiene
// «Hoy» (es solo del Estándar): va a su panel.
function LlamadaFinal() {
  const { usuario } = useAuth()
  return (
    <section
      aria-labelledby="empieza-titulo"
      className="mt-6 rounded-2xl border border-borde bg-superficie-2 p-6 sm:p-8"
    >
      <h2 id="empieza-titulo" className={claseTituloSeccion}>
        {usuario ? 'Sigue donde lo dejaste' : 'Empieza hoy, a tu ritmo'}
      </h2>
      <p className="mt-2 max-w-xl text-sm leading-relaxed text-texto-2">
        {usuario
          ? 'Tu día te espera, sin prisa.'
          : 'Es gratis y tus datos son tuyos: puedes descargarlos o borrarlos cuando quieras.'}
      </p>
      <div className="mt-5 flex flex-wrap gap-3">
        {usuario ? (
          <Link
            to={usuario.rol === 'Estandar' ? '/hoy' : '/admin'}
            className={claseBoton({ tamano: 'lg' })}
          >
            {usuario.rol === 'Estandar' ? 'Ir a Hoy' : 'Ir a mi panel'}
          </Link>
        ) : (
          <>
            <Link to="/registro" className={claseBoton({ tamano: 'lg' })}>
              Crear cuenta
            </Link>
            <Link to="/login" className={claseBoton({ variante: 'secundario', tamano: 'lg' })}>
              Iniciar sesión
            </Link>
          </>
        )}
      </div>
    </section>
  )
}
