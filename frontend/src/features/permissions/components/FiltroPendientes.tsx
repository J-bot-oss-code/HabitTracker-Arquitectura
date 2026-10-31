import { Segmentado } from '@/shared/ui/Segmentado'

export function FiltroPendientes({
  valor,
  alCambiar,
}: {
  valor: boolean
  alCambiar: (soloPendientes: boolean) => void
}) {
  return (
    <Segmentado
      etiqueta="Filtrar solicitudes"
      valor={valor}
      alCambiar={alCambiar}
      opciones={[
        { valor: true, etiqueta: 'Pendientes' },
        { valor: false, etiqueta: 'Todas' },
      ]}
    />
  )
}
