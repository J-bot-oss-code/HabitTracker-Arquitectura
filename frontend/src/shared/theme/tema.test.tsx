import { act, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { renderApp } from '@/test/render'
import { CLAVE_TEMA, aplicarTema, esTema, leerTemaGuardado, resolverTema } from './temas'

function simularPreferenciaOscura(oscuro: boolean) {
  const oyentes = new Set<() => void>()
  let valor = oscuro
  vi.stubGlobal('matchMedia', (consulta: string) => ({
    matches: consulta.includes('dark') ? valor : false,
    media: consulta,
    addEventListener: (_: string, fn: () => void) => oyentes.add(fn),
    removeEventListener: (_: string, fn: () => void) => oyentes.delete(fn),
  }))
  return {
    cambiar(nuevo: boolean) {
      valor = nuevo
      oyentes.forEach((fn) => fn())
    },
  }
}

afterEach(() => {
  vi.unstubAllGlobals()
  localStorage.clear()
  delete document.documentElement.dataset.theme
  delete document.documentElement.dataset.temaElegido
})

describe('lógica de temas', () => {
  it('reconoce solo los temas del catálogo', () => {
    expect(esTema('estadio')).toBe(true)
    expect(esTema('neon')).toBe(false)
    expect(esTema(undefined)).toBe(false)
  })

  it('"sistema" se resuelve según prefers-color-scheme', () => {
    simularPreferenciaOscura(true)
    expect(resolverTema('sistema')).toBe('oscuro')
    simularPreferenciaOscura(false)
    expect(resolverTema('sistema')).toBe('claro')
    expect(resolverTema('calido')).toBe('calido')
  })

  it('ignora un valor guardado inválido y usa "sistema"', () => {
    localStorage.setItem(CLAVE_TEMA, '<script>')
    expect(leerTemaGuardado()).toBe('sistema')
  })

  it('aplica data-theme y el tema elegido en <html>', () => {
    simularPreferenciaOscura(false)
    aplicarTema('estadio')
    expect(document.documentElement.dataset.theme).toBe('estadio')
    aplicarTema('sistema')
    expect(document.documentElement.dataset.theme).toBe('claro')
    expect(document.documentElement.dataset.temaElegido).toBe('sistema')
  })
})

describe('selector de tema', () => {
  it('desde Mi cuenta cambia el tema y lo recuerda', async () => {
    const { user } = renderApp('/cuenta', { como: 'estandar' })
    await user.click(await screen.findByRole('radio', { name: /Estadio/ }))

    expect(document.documentElement.dataset.theme).toBe('estadio')
    expect(localStorage.getItem(CLAVE_TEMA)).toBe('estadio')
    expect(screen.getByRole('radio', { name: /Estadio/ })).toBeChecked()
  })

  it('el menú de la cabecera funciona con teclado y se cierra con Escape', async () => {
    const { user } = renderApp('/habitos', { como: 'estandar' })
    const boton = await screen.findByRole('button', { name: /^Tema:/ })

    await user.click(boton)
    expect(boton).toHaveAttribute('aria-expanded', 'true')
    const opciones = screen.getAllByRole('menuitemradio')
    expect(opciones).toHaveLength(5)
    expect(screen.getByRole('menuitemradio', { name: /Sistema/ })).toHaveFocus()

    await user.keyboard('{ArrowDown}{ArrowDown}{Enter}')
    expect(document.documentElement.dataset.temaElegido).toBe('oscuro')
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()

    await user.click(boton)
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
    expect(boton).toHaveFocus()
  })

  it('con "Sistema" reacciona al cambio del dispositivo en vivo', async () => {
    const dispositivo = simularPreferenciaOscura(false)
    renderApp('/cuenta', { como: 'estandar' })
    await screen.findByRole('radio', { name: /Sistema/ })
    expect(document.documentElement.dataset.theme).toBe('claro')

    act(() => dispositivo.cambiar(true))
    expect(document.documentElement.dataset.theme).toBe('oscuro')
  })
})
