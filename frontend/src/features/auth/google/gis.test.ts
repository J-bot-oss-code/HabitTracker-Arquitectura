import { afterEach, describe, expect, it, vi } from 'vitest'
import { cargarGoogle, reiniciarCargaGoogle, URL_GIS } from './gis'
import { armarTokenDemo, leerTokenDemo } from './tokenDemo'

const guiaFalsa = { initialize: vi.fn(), renderButton: vi.fn() }

function scripts() {
  return document.head.querySelectorAll(`script[src="${URL_GIS}"]`)
}

afterEach(() => {
  delete window.google
  reiniciarCargaGoogle()
  scripts().forEach((s) => s.remove())
})

describe('cargarGoogle (Google Identity Services)', () => {
  it('si Google ya está cargado no vuelve a descargar el script', async () => {
    window.google = { accounts: { id: guiaFalsa } }

    await expect(cargarGoogle()).resolves.toBe(guiaFalsa)
    expect(scripts()).toHaveLength(0)
  })

  it('descarga el script de Google una sola vez aunque se pida varias veces', async () => {
    const primera = cargarGoogle()
    const segunda = cargarGoogle()
    expect(scripts()).toHaveLength(1)
    expect(scripts()[0].getAttribute('src')).toBe('https://accounts.google.com/gsi/client')

    window.google = { accounts: { id: guiaFalsa } }
    scripts()[0].dispatchEvent(new Event('load'))

    await expect(Promise.all([primera, segunda])).resolves.toEqual([guiaFalsa, guiaFalsa])
  })

  it('si el script no se puede descargar (sin red o bloqueador) rechaza y permite reintentar', async () => {
    const fallida = cargarGoogle()
    scripts()[0].dispatchEvent(new Event('error'))
    await expect(fallida).rejects.toThrow()
    expect(scripts()).toHaveLength(0)

    const reintento = cargarGoogle()
    expect(scripts()).toHaveLength(1)
    window.google = { accounts: { id: guiaFalsa } }
    scripts()[0].dispatchEvent(new Event('load'))
    await expect(reintento).resolves.toBe(guiaFalsa)
  })

  it('si el script carga pero Google no queda disponible, rechaza', async () => {
    const carga = cargarGoogle()
    scripts()[0].dispatchEvent(new Event('load'))

    await expect(carga).rejects.toThrow()
  })
})

describe('token de la demostración', () => {
  it('guarda la identidad y se lee de vuelta', () => {
    const token = armarTokenDemo('  Ana@Demo.com ', 'Ana')

    expect(leerTokenDemo(token)).toEqual({
      sub: 'sub-ana@demo.com',
      correo: 'ana@demo.com',
      nombre: 'Ana',
      correoVerificado: true,
    })
  })

  it('un correo sin verificar se conserva', () => {
    expect(leerTokenDemo(armarTokenDemo('a@b.co', 'A', false))?.correoVerificado).toBe(false)
  })

  it.each([
    undefined,
    null,
    42,
    '',
    'abc',
    'otro|a|b|c|1',
    'mock-google|sub|correo',
    'mock-google|a|b|c|1|extra',
  ])('rechaza %s', (valor) => {
    expect(leerTokenDemo(valor)).toBeNull()
  })
})
