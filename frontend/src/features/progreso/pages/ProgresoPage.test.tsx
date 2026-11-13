import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { db, sembrarPlanDemo } from '@/mocks/db'
import { problema } from '@/mocks/http'
import { server } from '@/mocks/server'
import { iniciarSesionComo, renderApp } from '@/test/render'

const url = (ruta: string) => `${window.location.origin}${ruta}`

// La línea de la meta de peso termina cuando termina el plan en curso.
beforeEach(() => sembrarPlanDemo())

describe('pantalla Progreso', () => {
  it('muestra peso, medidas, fotos, constancia, récords y logros de Ana', async () => {
    renderApp('/progreso', { como: 'estandar' })
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Tu progreso' }),
    ).toBeInTheDocument()

    // Peso: 71 → 68,4 kg en 9 semanas dentro de los 3 meses por defecto, con la línea del plan.
    const grafica = await screen.findByRole('img', { name: /Peso: de 71[.,]0 kg a 68[.,]4 kg/ })
    expect(grafica).toBeInTheDocument()
    expect(screen.getByText('Plan')).toBeInTheDocument()

    // Medidas con su cambio.
    const tabla = await screen.findByRole('table', { name: /Medidas al inicio y hoy/ })
    const cintura = within(tabla).getByRole('row', { name: /Cintura/ })
    expect(within(cintura).getByText('82 cm')).toBeInTheDocument()
    expect(within(cintura).getByText('78 cm')).toBeInTheDocument()
    expect(within(cintura).getByText('−4 cm')).toBeInTheDocument()

    expect(screen.getByRole('heading', { name: 'Fotos de progreso' })).toBeInTheDocument()
    expect(screen.getAllByText('Solo tú ves tu peso.').length).toBeGreaterThan(0)
    expect(await screen.findByRole('list', { name: 'Constancia por día' })).toBeInTheDocument()
    expect(await screen.findByText('Tu mes en números')).toBeInTheDocument()
    expect(screen.getByText('Primer paso')).toBeInTheDocument()
  })

  it('el selector de periodo filtra el peso y marca el activo con aria-pressed', async () => {
    const { user } = renderApp('/progreso', { como: 'estandar' })
    await screen.findByRole('img', { name: /Peso: de 71[.,]0 kg/ })
    const periodo = screen.getByRole('group', { name: 'Periodo del peso' })
    expect(within(periodo).getByRole('button', { name: '3 meses' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )

    await user.click(within(periodo).getByRole('button', { name: '4 semanas' }))
    expect(
      await screen.findByRole('img', { name: /Peso: de 69[.,]6 kg a 68[.,]4 kg en 4 semanas/ }),
    ).toBeInTheDocument()
    expect(within(periodo).getByRole('button', { name: '4 semanas' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
  })

  it('registra un peso nuevo y actualiza la gráfica', async () => {
    const { user } = renderApp('/progreso', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Registrar peso' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Registrar peso' })
    const campo = within(dialogo).getByLabelText('Peso (kg)')
    await user.clear(campo)
    await user.type(campo, '68')
    await user.click(within(dialogo).getByRole('button', { name: 'Guardar peso' }))

    expect(await screen.findByText('Peso registrado.')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(await screen.findByRole('img', { name: /a 68[.,]0 kg/ })).toBeInTheDocument()
  })

  it('valida el peso antes de enviarlo', async () => {
    const { user } = renderApp('/progreso', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Registrar peso' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Registrar peso' })
    const campo = within(dialogo).getByLabelText('Peso (kg)')
    await user.clear(campo)
    await user.type(campo, '500')
    await user.click(within(dialogo).getByRole('button', { name: 'Guardar peso' }))
    expect(await within(dialogo).findByText('Debe ser como máximo 300 kg.')).toBeInTheDocument()
    expect(db.pesos.get(db.usuarios[0].id)?.registros).toHaveLength(10)
  })

  it('registra una medición nueva y cambia el valor de "Hoy"', async () => {
    const { user } = renderApp('/progreso', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Nueva medición' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Nueva medición' })
    await user.type(within(dialogo).getByLabelText(/^Medida/), '75')
    await user.click(within(dialogo).getByRole('button', { name: 'Guardar medición' }))

    expect(await screen.findByText('Medición registrada.')).toBeInTheDocument()
    const tabla = await screen.findByRole('table', { name: /Medidas al inicio y hoy/ })
    expect(await within(tabla).findByText('75 cm')).toBeInTheDocument()
  })

  it('el rango de la medida cambia con la zona', async () => {
    const { user } = renderApp('/progreso', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Nueva medición' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Nueva medición' })
    expect(within(dialogo).getByText('Entre 40 y 250 cm.')).toBeInTheDocument()
    await user.selectOptions(within(dialogo).getByLabelText('Zona'), 'Grasa')
    expect(within(dialogo).getByText('Entre 3 y 60 %.')).toBeInTheDocument()
    await user.type(within(dialogo).getByLabelText(/^Medida/), '80')
    await user.click(within(dialogo).getByRole('button', { name: 'Guardar medición' }))
    expect(await within(dialogo).findByText('Debe estar entre 3 y 60.')).toBeInTheDocument()
  })

  it('sin consentimiento de salud el servidor rechaza y el motivo se ve', async () => {
    db.consentimientos.delete(db.usuarios[0].id)
    const { user } = renderApp('/progreso', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Registrar peso' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Registrar peso' })
    await user.click(within(dialogo).getByRole('button', { name: 'Guardar peso' }))
    expect(
      await within(dialogo).findByText(/necesitamos tu permiso para usar tus datos de salud/),
    ).toBeInTheDocument()
  })

  it('sin pesos ni medidas invita a registrarlos', async () => {
    db.pesos.clear()
    db.mediciones.clear()
    renderApp('/progreso', { como: 'estandar' })
    expect(await screen.findByText('Aún no registras tu peso')).toBeInTheDocument()
    expect(screen.getByText('Aún no registras medidas')).toBeInTheDocument()
  })

  it('con el backend real (sin el contrato) avisa en vez de fallar', async () => {
    server.use(
      http.get('/api/progress/weights', () => new HttpResponse(null, { status: 404 })),
      http.get('/api/progress/measurements', () => new HttpResponse(null, { status: 404 })),
    )
    renderApp('/progreso', { como: 'estandar' })
    expect(await screen.findByText('El registro de peso llegará pronto')).toBeInTheDocument()
    expect(screen.getByText('Las medidas llegarán pronto')).toBeInTheDocument()
    // La constancia sale de los hábitos reales y no depende de esas piezas.
    expect(await screen.findByRole('list', { name: 'Constancia por día' })).toBeInTheDocument()
  })

  it('un error de carga ofrece reintentar', async () => {
    let falla = true
    server.use(
      http.get('/api/progress/weights', () =>
        falla
          ? problema(500, 'Error interno.')
          : HttpResponse.json({ objetivoKg: null, registros: [] }),
      ),
    )
    const { user } = renderApp('/progreso', { como: 'estandar' })
    expect(await screen.findByRole('button', { name: 'Reintentar' })).toBeInTheDocument()
    falla = false
    await user.click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByText('Aún no registras tu peso')).toBeInTheDocument()
  })

  it('"Exportar PDF" imprime la página', async () => {
    let impresiones = 0
    window.print = () => {
      impresiones += 1
    }
    const { user } = renderApp('/progreso', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Exportar PDF' }))
    expect(impresiones).toBe(1)
  })

  it('"Progreso" está en el menú del Estándar y marca la página actual', async () => {
    renderApp('/progreso', { como: 'estandar' })
    const nav = await screen.findByRole('navigation', { name: 'Principal' })
    expect(within(nav).getByRole('link', { name: 'Progreso' })).toHaveAttribute(
      'aria-current',
      'page',
    )
  })

  it('el Administrador no tiene pantalla de progreso', async () => {
    renderApp('/progreso', { como: 'admin' })
    expect(await screen.findByText('No tienes acceso a esta sección')).toBeInTheDocument()
  })
})

describe('contrato propuesto del progreso', () => {
  const llamar = (ruta: string, token: string, metodo = 'GET', body?: unknown) =>
    fetch(url(ruta), {
      method: metodo,
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
      body: body === undefined ? undefined : JSON.stringify(body),
    })

  it('cada usuario solo ve sus datos y el Administrador recibe 403', async () => {
    const luis = iniciarSesionComo('otro')
    const pesos = await (await llamar('/api/progress/weights', luis)).json()
    expect(pesos).toEqual({ objetivoKg: null, registros: [] })
    expect(await (await llamar('/api/progress/measurements', luis)).json()).toEqual([])
    const admin = iniciarSesionComo('admin')
    expect((await llamar('/api/progress/weights', admin)).status).toBe(403)
    expect((await fetch(url('/api/progress/weights'))).status).toBe(401)
  })

  it.each([
    ['pesoKg', 29],
    ['pesoKg', 301],
    ['fecha', '2999-01-01T00:00:00Z'],
    ['fecha', 'ayer'],
    ['objetivoKg', 500],
  ])('rechaza un peso con %s = %s', async (campo, valor) => {
    const token = iniciarSesionComo('estandar')
    const valido = { fecha: new Date().toISOString(), pesoKg: 70 }
    expect(
      (await llamar('/api/progress/weights', token, 'POST', { ...valido, [campo]: valor })).status,
    ).toBe(400)
  })

  it.each([
    { zona: 'Hombros', valor: 100 },
    { zona: 'Grasa', valor: 80 },
    { zona: 'Cintura', valor: 10 },
  ])('rechaza la medición %o', async (parcial) => {
    const token = iniciarSesionComo('estandar')
    const cuerpo = { fecha: new Date().toISOString(), ...parcial }
    expect((await llamar('/api/progress/measurements', token, 'POST', cuerpo)).status).toBe(400)
  })

  it('un peso por día: registrar otro el mismo día lo reemplaza, y el objetivo se conserva', async () => {
    const token = iniciarSesionComo('estandar')
    const fecha = new Date().toISOString()
    await llamar('/api/progress/weights', token, 'POST', { fecha, pesoKg: 68 })
    const r = await llamar('/api/progress/weights', token, 'POST', { fecha, pesoKg: 67.5 })
    const cuerpo = await r.json()
    expect(cuerpo.objetivoKg).toBe(66)
    expect(
      cuerpo.registros.filter(
        (p: { fecha: string }) => p.fecha.slice(0, 10) === fecha.slice(0, 10),
      ),
    ).toHaveLength(1)
    expect(cuerpo.registros.at(-1).pesoKg).toBe(67.5)
  })

  it('el objetivo se puede quitar con null', async () => {
    const token = iniciarSesionComo('estandar')
    const r = await llamar('/api/progress/weights', token, 'POST', {
      fecha: new Date().toISOString(),
      pesoKg: 68,
      objetivoKg: null,
    })
    expect((await r.json()).objetivoKg).toBeNull()
  })
})

describe('casos límite de la pantalla Progreso', () => {
  it('con el backend real los botones de registro quedan inactivos', async () => {
    server.use(
      http.get('/api/progress/weights', () => new HttpResponse(null, { status: 404 })),
      http.get('/api/progress/measurements', () => new HttpResponse(null, { status: 404 })),
    )
    renderApp('/progreso', { como: 'estandar' })
    await screen.findByText('El registro de peso llegará pronto')
    expect(screen.getByRole('button', { name: 'Registrar peso' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Nueva medición' })).toBeDisabled()
  })

  it('la conclusión de la gráfica también describe el plan', async () => {
    renderApp('/progreso', { como: 'estandar' })
    const grafica = await screen.findByRole('img', { name: /Peso: de/ })
    expect(grafica).toHaveAccessibleName(/Plan: 66[.,]0 kg el /)
  })

  it('el cambio de peso no se pinta como un logro (sin verde)', async () => {
    renderApp('/progreso', { como: 'estandar' })
    const cambio = await screen.findByText(/−2[.,]6 kg/)
    expect(cambio.className).not.toContain('text-exito')
  })

  it('una medición rechazada por falta de consentimiento muestra el motivo', async () => {
    db.consentimientos.delete(db.usuarios[0].id)
    const { user } = renderApp('/progreso', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Nueva medición' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Nueva medición' })
    await user.type(within(dialogo).getByLabelText(/^Medida/), '75')
    await user.click(within(dialogo).getByRole('button', { name: 'Guardar medición' }))
    expect(
      await within(dialogo).findByText(/necesitamos tu permiso para usar tus datos de salud/),
    ).toBeInTheDocument()
  })

  it('una fecha futura es 400 fecha_futura y un objetivo sin perfil es 400 perfil_requerido', async () => {
    const token = iniciarSesionComo('estandar')
    const enviar = (body: unknown) =>
      fetch(`${window.location.origin}/api/progress/weights`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
        body: JSON.stringify(body),
      })
    const manana = new Date(Date.now() + 86_400_000).toISOString()
    const futura = await enviar({ fecha: manana, pesoKg: 70 })
    expect(futura.status).toBe(400)
    expect((await futura.json()).codigo).toBe('fecha_futura')

    db.perfiles.delete(db.usuarios[0].id)
    const sinPerfil = await enviar({ fecha: new Date().toISOString(), pesoKg: 70, objetivoKg: 65 })
    expect(sinPerfil.status).toBe(400)
    expect((await sinPerfil.json()).codigo).toBe('perfil_requerido')
  })

  it('rechaza fechas de antes del año 2000', async () => {
    const token = iniciarSesionComo('estandar')
    const r = await fetch(`${window.location.origin}/api/progress/weights`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
      body: JSON.stringify({ fecha: '0001-01-01T00:00:00Z', pesoKg: 70 }),
    })
    expect(r.status).toBe(400)
  })

  it('como el backend, solo acepta fechas en UTC con "Z" (el frontend envía toISOString)', async () => {
    const token = iniciarSesionComo('estandar')
    const enviar = (fecha: string) =>
      fetch(`${window.location.origin}/api/progress/weights`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
        body: JSON.stringify({ fecha, pesoKg: 70 }),
      })
    expect((await enviar('2026-11-01T22:00:00-05:00')).status).toBe(400)
    const r = await enviar('2001-03-04T03:00:00.000Z')
    expect(r.status).toBe(201)
    const cuerpo = await r.json()
    expect(
      cuerpo.registros.some((p: { fecha: string }) => p.fecha === '2001-03-04T03:00:00.000Z'),
    ).toBe(true)
  })
})

describe('ajustes de la ronda 2 de crítica', () => {
  it('llegando desde Hoy (?registrar=peso) abre el formulario de peso directamente', async () => {
    const { user, router } = renderApp('/progreso?registrar=peso', { como: 'estandar' })
    const dialogo = await screen.findByRole('dialog', { name: 'Registrar peso' })
    expect(dialogo).toBeInTheDocument()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    // El parámetro se limpia: recargar no vuelve a abrir el diálogo.
    expect(router.state.location.search).toBe('')
  })

  it('el plan no cambia de pendiente con el periodo: se recorta sin desviarse', async () => {
    const { user } = renderApp('/progreso', { como: 'estandar' })
    const completa = await screen.findByRole('img', { name: /Plan: 66[.,]0 kg el / })
    const nombre3m = completa.getAttribute('aria-label') ?? ''
    await user.click(screen.getByRole('button', { name: '4 semanas' }))
    const recortada = await screen.findByRole('img', {
      name: /en 4 semanas\. Plan: 66[.,]0 kg el /,
    })
    // Mismo objetivo y misma fecha de fin: solo cambia lo que se ve del eje.
    const fin = (t: string) => /Plan: [^.]*\./.exec(t)?.[0]
    expect(fin(recortada.getAttribute('aria-label') ?? '')).toBe(fin(nombre3m))
  })

  it('los logros son los del servidor y solo hablan de constancia', async () => {
    renderApp('/progreso', { como: 'estandar' })
    expect(await screen.findByText('Primera semana')).toBeInTheDocument()
    expect(screen.getByText('25 avances')).toBeInTheDocument()
    expect(screen.getAllByText('Obtenido').length).toBeGreaterThanOrEqual(3)
    expect(screen.getByText('Días cumplidos')).toBeInTheDocument()
  })

  it('el mapa de constancia lleva los días de la semana y el resumen de esta semana', async () => {
    renderApp('/progreso', { como: 'estandar' })
    expect(
      await screen.findByText(/^Esta semana: \d de \d días? con todo hecho\.$/),
    ).toBeInTheDocument()
    expect(screen.getByText('Aún no llega')).toBeInTheDocument()
  })
})
