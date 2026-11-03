import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { CalendarioCalor, type CeldaCalor } from './CalendarioCalor'
import { GraficaLinea } from './GraficaLinea'

const formato = {
  formatoValor: (v: number) => v.toFixed(1),
  formatoFecha: (t: number) => `d${t}`,
  unidad: 'kg',
}

describe('GraficaLinea', () => {
  const real = [
    { t: 1000, v: 71 },
    { t: 2000, v: 70 },
    { t: 3000, v: 68.4 },
  ]

  it('es una imagen cuyo nombre es la conclusión, con los datos en una tabla aparte', () => {
    render(<GraficaLinea real={real} conclusion="Peso: de 71 kg a 68,4 kg." {...formato} />)
    expect(screen.getByRole('img', { name: 'Peso: de 71 kg a 68,4 kg.' })).toBeInTheDocument()
    const tabla = screen.getByRole('table', { name: 'Peso: de 71 kg a 68,4 kg.' })
    expect(within(tabla).getAllByRole('row')).toHaveLength(4) // encabezado + 3 datos
    expect(within(tabla).getByText('68.4')).toBeInTheDocument()
  })

  it('dibuja la leyenda del plan solo si hay plan', () => {
    const { rerender } = render(<GraficaLinea real={real} conclusion="c" {...formato} />)
    expect(screen.queryByText('Plan')).not.toBeInTheDocument()
    rerender(
      <GraficaLinea
        real={real}
        plan={[
          { t: 1000, v: 71 },
          { t: 5000, v: 66 },
        ]}
        conclusion="c"
        {...formato}
      />,
    )
    expect(screen.getByText('Plan')).toBeInTheDocument()
  })

  it('con un solo punto o sin datos no rompe', () => {
    const { container, rerender } = render(
      <GraficaLinea real={[{ t: 1000, v: 70 }]} conclusion="Un registro." {...formato} />,
    )
    expect(container.querySelectorAll('circle')).toHaveLength(1)
    expect(container.innerHTML).not.toContain('NaN')
    rerender(<GraficaLinea real={[]} conclusion="Nada." {...formato} />)
    expect(container).toBeEmptyDOMElement()
  })

  it('con valores iguales no divide entre cero', () => {
    const { container } = render(
      <GraficaLinea
        real={[
          { t: 1000, v: 70 },
          { t: 2000, v: 70 },
        ]}
        conclusion="Sin cambios."
        {...formato}
      />,
    )
    expect(container.innerHTML).not.toContain('NaN')
  })
})

describe('CalendarioCalor', () => {
  const celdas: CeldaCalor[] = [
    { clave: 'a', nivel: 0, descripcion: 'Lunes: sin hábitos' },
    { clave: 'b', nivel: 2, descripcion: 'Martes: 2 de 3 hábitos' },
    { clave: 'c', nivel: 3, descripcion: 'Miércoles: 3 de 3 hábitos', hoy: true },
  ]

  it('cada celda dice su estado como texto, no solo con color', () => {
    render(<CalendarioCalor celdas={celdas} columnas={1} etiqueta="Constancia" />)
    const lista = screen.getByRole('list', { name: 'Constancia' })
    expect(within(lista).getAllByRole('listitem')).toHaveLength(3)
    expect(within(lista).getByText('Martes: 2 de 3 hábitos')).toBeInTheDocument()
  })

  it('la leyenda nombra los cuatro niveles', () => {
    render(<CalendarioCalor celdas={celdas} columnas={1} etiqueta="Constancia" />)
    for (const texto of ['Sin avances', 'Poco', 'Bastante', 'Todo hecho']) {
      expect(screen.getByText(texto)).toBeInTheDocument()
    }
  })
})

describe('GraficaLinea con datos corruptos', () => {
  it('descarta los puntos no finitos en vez de romper la escala', () => {
    const { container } = render(
      <GraficaLinea
        real={[
          { t: 1000, v: 70 },
          { t: Number.NaN, v: 69 },
          { t: 3000, v: 68 },
        ]}
        plan={[
          { t: 1000, v: 70 },
          { t: Number.NaN, v: 66 },
        ]}
        conclusion="Con datos corruptos."
        formatoValor={(v) => v.toFixed(1)}
        formatoFecha={(t) => `d${t}`}
        unidad="kg"
      />,
    )
    expect(container.innerHTML).not.toContain('NaN')
    expect(container.querySelectorAll('circle')).toHaveLength(2)
  })
})
