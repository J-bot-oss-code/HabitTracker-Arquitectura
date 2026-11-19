import { describe, expect, it } from 'vitest'
import { errorDeArchivo } from './schemas'
import { TAMANO_MAXIMO_BYTES } from './types'

describe('errorDeArchivo (mismos límites que DocumentoService)', () => {
  it.each(['a.pdf', 'a.png', 'a.jpg', 'a.jpeg', 'a.txt', 'a.docx', 'a.xlsx'])(
    'acepta %s',
    (name) => {
      expect(errorDeArchivo({ name, size: 10 })).toBeNull()
    },
  )

  it('la extensión no distingue mayúsculas', () => {
    expect(errorDeArchivo({ name: 'FOTO.JPG', size: 10 })).toBeNull()
  })

  it('acepta justo 10 MB y rechaza un byte más, sin culpar a la persona', () => {
    expect(errorDeArchivo({ name: 'a.pdf', size: TAMANO_MAXIMO_BYTES })).toBeNull()
    expect(errorDeArchivo({ name: 'a.pdf', size: TAMANO_MAXIMO_BYTES + 1 })).toMatch(/10 MB/)
  })

  it('rechaza un archivo vacío', () => {
    expect(errorDeArchivo({ name: 'a.pdf', size: 0 })).toMatch(/vacío/)
  })

  it.each(['virus.exe', 'pagina.html', 'sin-extension', 'doble.pdf.exe', '.pdf.'])(
    'rechaza %s por su extensión',
    (name) => {
      expect(errorDeArchivo({ name, size: 10 })).toMatch(/tipo de archivo/)
    },
  )

  it('rechaza un nombre de más de 255 caracteres', () => {
    expect(errorDeArchivo({ name: `${'a'.repeat(252)}.pdf`, size: 10 })).toMatch(/largo/)
  })

  it('sin archivo pide elegir uno', () => {
    expect(errorDeArchivo(null)).toMatch(/Elige un archivo/)
    expect(errorDeArchivo(undefined)).toMatch(/Elige un archivo/)
  })
})
