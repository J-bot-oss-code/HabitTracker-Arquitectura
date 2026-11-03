// "Cómo funciona" (§5.1, punto 5): tres pasos numerados.
const pasos = [
  {
    numero: '01',
    titulo: 'Elige tu meta',
    texto: 'Correr tus primeros 5 km, ganar fuerza, dormir mejor o bajar el estrés.',
  },
  {
    numero: '02',
    titulo: 'Sigue tu plan diario',
    texto: 'Cada día recibes pocas tareas concretas que marcas en segundos.',
  },
  {
    numero: '03',
    titulo: 'Mide tu avance',
    texto: 'Rachas, gráficas y récords te muestran que sí estás mejorando.',
  },
]

export function ComoFunciona() {
  return (
    <section
      id="como-funciona"
      aria-labelledby="como-funciona-titulo"
      className="scroll-mt-24 py-10 sm:py-14"
    >
      <h2 id="como-funciona-titulo" className="text-3xl font-extrabold sm:text-[2rem]">
        Cómo funciona
      </h2>
      <ol className="mt-6 grid gap-4 md:grid-cols-3">
        {pasos.map((p) => (
          <li key={p.numero} className="rounded-2xl border border-borde bg-superficie p-6">
            <p aria-hidden="true" className="cifra text-3xl text-acento">
              {p.numero}
            </p>
            <h3 className="mt-3 text-lg font-bold">{p.titulo}</h3>
            <p className="mt-1.5 text-sm text-atenuado">{p.texto}</p>
          </li>
        ))}
      </ol>
    </section>
  )
}
