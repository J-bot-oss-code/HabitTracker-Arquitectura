import { describe, expect, it } from 'vitest'
import { CLAIM_ROL, estaVencido, leerToken } from './jwt'

function token(payload: Record<string, unknown>): string {
  const b64 = (o: unknown) =>
    btoa(JSON.stringify(o)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
  return `${b64({ alg: 'HS256' })}.${b64(payload)}.firma`
}

describe('leerToken', () => {
  const exp = Math.floor(Date.now() / 1000) + 3600

  it('lee id, correo y el rol del claim URI que emite JwtProvider', () => {
    const usuario = leerToken(
      token({ sub: 'u-1', email: 'a@b.com', [CLAIM_ROL]: 'Administrador', exp }),
    )
    expect(usuario).toEqual({
      id: 'u-1',
      correo: 'a@b.com',
      rol: 'Administrador',
      expiraEn: exp * 1000,
    })
  })

  it('rechaza un token con un rol desconocido', () => {
    expect(leerToken(token({ sub: 'u-1', [CLAIM_ROL]: 'Root', exp }))).toBeNull()
  })

  it('rechaza un token mal formado', () => {
    expect(leerToken('no-es-un-jwt')).toBeNull()
    expect(leerToken('a.%%%.c')).toBeNull()
  })

  it('detecta un token vencido', () => {
    const usuario = leerToken(token({ sub: 'u-1', [CLAIM_ROL]: 'Estandar', exp: 1 }))!
    expect(estaVencido(usuario)).toBe(true)
  })
})
