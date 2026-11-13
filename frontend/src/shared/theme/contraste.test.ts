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
      ['texto-2', 'fondo'],
      ['texto-2', 'superficie'],
      ['atenuado', 'fondo'],
      ['atenuado', 'superficie'],
      ['atenuado', 'superficie-2'],
      ['acento-texto', 'acento'],
      ['acento', 'fondo'],
      ['acento', 'superficie'],
      ['acento-fuerte', 'acento-suave'],
      ['acento-fuerte', 'superficie'],
      ['tinta-texto', 'tinta'],
      ['marcador-texto', 'marcador'],
      ['tinta-atenuado', 'tinta'],
      ['racha-texto', 'racha-fondo'],
      ['nota-texto', 'nota-fondo'],
      ['exito', 'exito-fondo'],
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

    // `control` es el borde de inputs, casillas e interruptores (WCAG 1.4.11). `borde-fuerte`
    // es decorativo (separadores, botón secundario con texto) y no necesita 3:1.
    it.each([
      ['control', 'fondo'],
      ['control', 'superficie'],
      ['foco', 'fondo'],
      ['foco', 'superficie'],
      ['calor-1', 'superficie'],
      ['calor-3', 'superficie'],
      ['acento-claro', 'tinta'],
      ['tinta-foco', 'tinta'],
    ])('componente %s sobre %s ≥ 3:1', (primer, fondo) => {
      expect(contraste(t[primer], t[fondo])).toBeGreaterThanOrEqual(3)
    })
  })
})

// Copias que deben coincidir y que nadie vigilaría a simple vista.
function todosLosTokens(cuerpo: string): Tokens {
  const tokens: Tokens = {}
  for (const [, nombre, valor] of cuerpo.matchAll(/--c-([\w-]+):\s*([^;]+);/g)) {
    tokens[nombre] = valor.trim().toLowerCase()
  }
  return tokens
}

describe('sincronía de los temas', () => {
  it('el bloque sin JavaScript (prefers-color-scheme: dark) es idéntico al tema oscuro', () => {
    const oscuro = css.match(/:root\[data-theme='oscuro'\]\s*\{([^}]*)\}/)?.[1] ?? ''
    const sistema =
      css.match(
        /@media \(prefers-color-scheme: dark\)\s*\{\s*:root:not\(\[data-theme\]\)\s*\{([^}]*)\}/,
      )?.[1] ?? ''
    expect(Object.keys(todosLosTokens(oscuro)).length).toBeGreaterThan(30)
    expect(todosLosTokens(sistema)).toEqual(todosLosTokens(oscuro))
  })

  it('el script anti-parpadeo de index.html conoce exactamente los temas del catálogo', () => {
    const html = readFileSync(resolve(process.cwd(), 'index.html'), 'utf8')
    const lista = html.match(/var validos = \[([^\]]*)\]/)?.[1] ?? ''
    const validos = [...lista.matchAll(/'(\w+)'/g)].map((m) => m[1])
    expect(validos).toEqual([...TEMAS])
  })
})

// El mapa de constancia distingue sus niveles por luminosidad (más borde en el 0): cada salto
// entre niveles consecutivos debe notarse (>= 2:1) para quien no distingue bien los tonos.
describe('niveles del mapa de constancia', () => {
  describe.each(temasConColor)('tema %s', (nombre) => {
    const t = temas[nombre]
    it.each([
      ['calor-1', 'calor-2'],
      ['calor-2', 'calor-3'],
    ])('%s frente a %s >= 2:1', (a, b) => {
      expect(contraste(t[a], t[b])).toBeGreaterThanOrEqual(2)
    })
  })
})
