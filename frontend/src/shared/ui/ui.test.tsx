import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { Button } from './Button'
import { ConfirmarDialogo } from './ConfirmarDialogo'
import { Estadistica } from './Estadistica'
import { Icono } from './Icono'
import { MiniBarras } from './MiniBarras'
import { Modal } from './Modal'
import { Segmentado } from './Segmentado'

describe('Segmentado', () => {
  it('es un grupo de botones con aria-pressed (no pestañas)', async () => {
    const alCambiar = vi.fn()
    render(
      <Segmentado
        etiqueta="Filtro"
        valor="a"
        alCambiar={alCambiar}
        opciones={[
          { valor: 'a', etiqueta: 'Uno', contador: 2 },
          { valor: 'b', etiqueta: 'Dos' },
        ]}
      />,
    )
    expect(screen.getByRole('group', { name: 'Filtro' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Uno (2)' })).toHaveAttribute('aria-pressed', 'true')
    await userEvent.click(screen.getByRole('button', { name: 'Dos' }))
    expect(alCambiar).toHaveBeenCalledWith('b')
  })
})

describe('Modal', () => {
  it('no roba el foco cuando el padre se vuelve a renderizar con un alCerrar nuevo', async () => {
    const user = userEvent.setup()
    function Padre() {
      const [abierto, setAbierto] = useState(true)
      const [texto, setTexto] = useState('')
      return (
        <>
          <input aria-label="Fuera" value={texto} onChange={(e) => setTexto(e.target.value)} />
          <Modal abierto={abierto} titulo="Hola" alCerrar={() => setAbierto(false)}>
            <input aria-label="Dentro" />
          </Modal>
        </>
      )
    }
    render(<Padre />)
    const dentro = screen.getByLabelText('Dentro')
    expect(dentro).toHaveFocus()

    await user.type(dentro, 'abc')
    expect(dentro).toHaveValue('abc')
    expect(dentro).toHaveFocus()
  })

  it('devuelve el foco al control que lo abrió y cierra con Escape', async () => {
    const user = userEvent.setup()
    function Padre() {
      const [abierto, setAbierto] = useState(false)
      return (
        <>
          <Button onClick={() => setAbierto(true)}>Abrir</Button>
          <Modal abierto={abierto} titulo="Hola" alCerrar={() => setAbierto(false)}>
            <input aria-label="Dentro" />
          </Modal>
        </>
      )
    }
    render(<Padre />)
    const abrir = screen.getByRole('button', { name: 'Abrir' })
    await user.click(abrir)
    expect(screen.getByLabelText('Dentro')).toHaveFocus()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(abrir).toHaveFocus()
  })
})

describe('ConfirmarDialogo', () => {
  it('confirma o cancela', async () => {
    const user = userEvent.setup()
    const alConfirmar = vi.fn()
    const alCerrar = vi.fn()
    render(
      <ConfirmarDialogo
        abierto
        titulo="Borrar"
        mensaje="¿Seguro?"
        textoConfirmar="Sí, borrar"
        peligroso
        alConfirmar={alConfirmar}
        alCerrar={alCerrar}
      />,
    )
    await user.click(screen.getByRole('button', { name: 'Sí, borrar' }))
    expect(alConfirmar).toHaveBeenCalledOnce()
    await user.click(screen.getByRole('button', { name: 'Cancelar' }))
    expect(alCerrar).toHaveBeenCalledOnce()
  })
})

describe('MiniBarras', () => {
  it('describe el gráfico con un resumen y marca la meta sin depender del color', () => {
    render(
      <MiniBarras
        resumen="Avance de 3 días"
        objetivo={10}
        datos={[
          { etiqueta: 'lun', valor: 12 },
          { etiqueta: 'mar', valor: 0 },
          { etiqueta: 'mié', valor: 4 },
        ]}
      />,
    )
    expect(screen.getByRole('img', { name: 'Avance de 3 días' })).toBeInTheDocument()
    expect(screen.getByText('Meta')).toBeInTheDocument()
  })
})

describe('Estadistica e Icono', () => {
  it('Estadistica muestra rótulo, cifra y detalle', () => {
    render(<Estadistica etiqueta="Racha" valor={7} detalle="días" icono="llama" destacada />)
    expect(screen.getByText('Racha')).toBeInTheDocument()
    expect(screen.getByText('7')).toBeInTheDocument()
    expect(screen.getByText('días')).toBeInTheDocument()
  })

  it('Icono es decorativo salvo que lleve título', () => {
    const { container, rerender } = render(<Icono nombre="check" />)
    expect(container.querySelector('svg')).toHaveAttribute('aria-hidden', 'true')
    rerender(<Icono nombre="check" titulo="Hecho" />)
    expect(screen.getByRole('img', { name: 'Hecho' })).toBeInTheDocument()
  })
})
