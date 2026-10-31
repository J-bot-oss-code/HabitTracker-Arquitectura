/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { TEMAS } from './temas'

// Lee los tokens de index.css y exige WCAG 2.2 AA en cada tema. Si alguien cambia un color y
// rompe el contraste, esta prueba falla antes de llegar a la interfaz.

// Vitest corre con css:false, así que el archivo se lee directamente del disco.
// Vitest se ejecuta desde frontend/.
const css = readFileSync(resolve(process.cwd(), 'src/index.css'), 'utf8')

type Tokens = Record<string, string>

function leerTemas(): Record<string, Tokens> {
  const resultado: Record<string, Tokens> = {}
  for (const [, selector, cuerpo] of css.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
    const tema = selector.match(/data-theme='(\w+)'/)?.[1]
    if (!tema) continue
    const tokens: Tokens = {}
    for (const [, nombre, valor] of cuerpo.matchAll(/--c-([\w-]+):\s*(#[0-9a-fA-F]{6})\s*;/g)) {
      tokens[nombre] = valor.toLowerCase()
    }
    resultado[tema] = tokens
  }
  return resultado
}

function luminancia(hex: string): number {
  const [r, g, b] = [1, 3, 5].map((i) => {
    const c = parseInt(hex.slice(i, i + 2), 16) / 255
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
  })
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

function contraste(a: string, b: string): number {
  const [claro, oscuro] = [luminancia(a), luminancia(b)].sort((x, y) => y - x)
  return (claro + 0.05) / (oscuro + 0.05)
}

const temas = leerTemas()
const temasConColor = TEMAS.filter((t) => t !== 'sistema')

describe('contraste de los temas (WCAG 2.2 AA)', () => {
  it('define todos los temas con colores sólidos', () => {
    expect(Object.keys(temas).sort()).toEqual([...temasConColor].sort())
  })

  describe.each(temasConColor)('tema %s', (nombre) => {
    const t = temas[nombre]

    it.each([
      ['texto', 'fondo'],
      ['texto', 'superficie'],
      ['texto', 'superficie-2'],
      ['atenuado', 'fondo'],
      ['atenuado', 'superficie'],
      ['atenuado', 'superficie-2'],
      ['acento-texto', 'acento'],
      ['acento', 'fondo'],
      ['acento', 'superficie'],
      ['acento-2', 'fondo'],
      ['acento-2', 'superficie'],
      ['exito', 'fondo'],
      ['exito', 'superficie'],
      ['aviso', 'fondo'],
      ['aviso', 'superficie'],
      ['peligro', 'fondo'],
      ['peligro', 'superficie'],
      ['info', 'fondo'],
      ['info', 'superficie'],
    ])('texto %s sobre %s ≥ 4.5:1', (primer, fondo) => {
      expect(contraste(t[primer], t[fondo])).toBeGreaterThanOrEqual(4.5)
    })

    it.each([
      ['borde-fuerte', 'fondo'],
      ['borde-fuerte', 'superficie'],
      ['foco', 'fondo'],
      ['foco', 'superficie'],
    ])('componente %s sobre %s ≥ 3:1', (primer, fondo) => {
      expect(contraste(t[primer], t[fondo])).toBeGreaterThanOrEqual(3)
    })
  })
})
