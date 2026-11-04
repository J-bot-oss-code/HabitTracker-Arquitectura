import { Icono, type NombreIcono } from '@/shared/ui/Icono'

// Banda oscura "Tus datos son tuyos" (§5.1, punto 6): la privacidad se promete en el inicio y
// se cumple en el Centro de privacidad.
const promesas: { icono: NombreIcono; titulo: string; texto: string }[] = [
  {
    icono: 'documentos',
    titulo: 'Descarga todo',
    texto: 'Tus hábitos, avances y mediciones en un archivo, cuando quieras.',
  },
  {
    icono: 'cerrar',
    titulo: 'Borra tu cuenta',
    texto: 'Desde tu perfil, sin escribir a nadie. Tus datos se eliminan.',
  },
  {
    icono: 'candado',
    titulo: 'Tú decides',
    texto: 'Activa o retira cada permiso. Nadie ve tu peso ni tus medidas.',
  },
]

export function BandaDatosTuyos() {
  return (
    <section
      aria-labelledby="datos-titulo"
      className="my-10 rounded-[20px] panel-tinta px-6 py-10 sm:my-14 sm:px-10"
    >
      <h2 id="datos-titulo" className="text-3xl font-extrabold sm:text-[2rem]">
        Tus datos son tuyos
      </h2>
      <ul className="mt-8 grid gap-6 md:grid-cols-3">
        {promesas.map((p) => (
          <li key={p.titulo} className="flex gap-3">
            <Icono nombre={p.icono} className="mt-0.5 size-5 shrink-0 text-acento-claro" />
            <div>
              <h3 className="font-bold">{p.titulo}</h3>
              <p className="mt-1 text-sm text-tinta-atenuado">{p.texto}</p>
            </div>
          </li>
        ))}
      </ul>
    </section>
  )
}
