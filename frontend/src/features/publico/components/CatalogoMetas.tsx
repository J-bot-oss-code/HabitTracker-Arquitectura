import { useId, useState } from 'react'
import { Segmentado, type OpcionSegmentada } from '@/shared/ui/Segmentado'
import { AREAS, etiquetaArea, METAS_CATALOGO, metasDelArea, type Area } from '../catalogo'
import { TarjetaMeta } from './TarjetaMeta'

type Filtro = Area | 'todas'

const opciones: OpcionSegmentada<Filtro>[] = [
  { valor: 'todas', etiqueta: 'Todas', contador: METAS_CATALOGO.length },
  ...AREAS.map((area) => ({
    valor: area,
    etiqueta: etiquetaArea[area],
    contador: metasDelArea(area).length,
  })),
]

// "Elige una meta" (§5.1, punto 3): se explora libremente; para empezar uno, se crea la cuenta.
export function CatalogoMetas({ nivelTitulo = 2 }: { nivelTitulo?: 1 | 2 }) {
  const [filtro, setFiltro] = useState<Filtro>('todas')
  const metas = metasDelArea(filtro)
  const Titulo = nivelTitulo === 1 ? 'h1' : 'h2'
  const idTitulo = useId()

  return (
    <section id="metas" aria-labelledby={idTitulo} className="scroll-mt-24 py-10 sm:py-14">
      <Titulo id={idTitulo} className="text-3xl font-extrabold sm:text-[2rem]">
        Elige una meta
      </Titulo>
      <p className="mt-2 text-texto-2">
        Explora los planes libremente. Para empezar uno, crea tu cuenta.
      </p>
      <div className="mt-6">
        <Segmentado
          etiqueta="Filtrar metas por área"
          opciones={opciones}
          valor={filtro}
          alCambiar={setFiltro}
        />
      </div>
      <ul className="mt-6 grid grid-cols-[repeat(auto-fill,minmax(min(100%,260px),1fr))] gap-4">
        {metas.map((meta) => (
          <li key={meta.slug} className="flex">
            <TarjetaMeta meta={meta} nivelTitulo={nivelTitulo === 1 ? 2 : 3} />
          </li>
        ))}
      </ul>
    </section>
  )
}
