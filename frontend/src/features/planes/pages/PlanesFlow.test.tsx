import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { PREGUNTAS_SEGURIDAD } from '@/features/perfil/types'
import { db } from '@/mocks/db'
import { server } from '@/mocks/server'
import { CORREOS, renderApp } from '@/test/render'

const idDe = (quien: keyof typeof CORREOS) =>
  db.usuarios.find((u) => u.correo === CORREOS[quien])!.id

// Un cuestionario con embarazo y el permiso de salud dado: el plan de peso deja de ser apto.
function conEmbarazo() {
  const id = idDe('estandar')
  const ahora = new Date().toISOString()
  db.consentimientos.set(id, { ...db.consentimientos.get(id)!, datosSalud: true })
  db.cuestionarios.set(id, {
    objetivos: {
      objetivos: ['ReducirEstres'],
      zonas: [],
      experiencia: 'Principiante',
      lugar: 'CasaSinEquipo',
    },
    objetivosRespondidoEn: ahora,
    seguridad: Object.fromEntries(PREGUNTAS_SEGURIDAD.map((k) => [k, false])) as Record<
      (typeof PREGUNTAS_SEGURIDAD)[number],
      boolean
    >,
    seguridadRespondidoEn: ahora,
    condiciones: { condiciones: ['Embarazo'], alergias: ['Ninguna'] },
    condicionesRespondidoEn: ahora,
  })
}

describe('detalle de un plan (público)', () => {
  it('muestra el aviso de seguridad, las tareas con su versión mínima y pide una cuenta para empezar', async () => {
    renderApp('/metas/mi-dia-turno-nocturno')

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Mi día saludable en turno de noche' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Antes de empezar')).toBeInTheDocument()
    expect(screen.getByText(/no sustituye el consejo de tu médico/)).toBeInTheDocument()

    const tareas = screen.getByRole('region', { name: 'Qué harás' })
    expect(within(tareas).getAllByRole('listitem')).toHaveLength(3)
    expect(within(tareas).getAllByText('Si hoy no puedes:')).toHaveLength(3)
    expect(
      within(tareas).getByText(/Pon el celular en silencio antes de acostarte/),
    ).toBeInTheDocument()
    expect(within(tareas).getByText(/al salir del trabajo/)).toBeInTheDocument()

    expect(screen.getByRole('link', { name: /Empezar este plan/ })).toHaveAttribute(
      'href',
      '/registro?meta=mi-dia-turno-nocturno',
    )
    expect(screen.getByRole('link', { name: 'Volver a los planes' })).toHaveAttribute(
      'href',
      '/metas',
    )
    // Un Básico no trae guía semanal.
    expect(screen.queryByRole('heading', { name: 'Semana a semana' })).not.toBeInTheDocument()
  })

  it('un Regular muestra su guía semana a semana y avisa que pedirá datos de salud', async () => {
    renderApp('/metas/primeros-5-km')

    const guia = await screen.findByRole('region', { name: 'Semana a semana' })
    expect(within(guia).getAllByRole('listitem')).toHaveLength(8)
    expect(within(guia).getByText('Semana 8')).toBeInTheDocument()
    // El tip extra se desbloquea al seguir el plan: el detalle público no lo trae.
    expect(screen.queryByText(/Si puedes hablar mientras trotas/)).not.toBeInTheDocument()

    expect(screen.getByText(/te haremos unas preguntas de salud/)).toBeInTheDocument()
    expect(screen.getByText('Incluye ejercicios de impacto.')).toBeInTheDocument()
  })

  it('un plan restringido por edad dice para quién es y dónde se hace', async () => {
    renderApp('/metas/ganar-masa-muscular')

    expect(await screen.findByText('Pensado para personas de 18 a 100 años.')).toBeInTheDocument()
    expect(screen.getByText(/Se hace en: en el gimnasio/)).toBeInTheDocument()
    expect(screen.getByText(/Trabaja: .*espalda/)).toBeInTheDocument()
  })

  it.each(['/metas/no-existe', '/metas/%3Cscript%3E', '/metas/Con%20Mayusculas'])(
    'un plan que no existe (%s) lo dice y ofrece volver',
    async (ruta) => {
      renderApp(ruta)

      expect(
        await screen.findByRole('heading', { level: 1, name: 'No encontramos ese plan' }),
      ).toBeInTheDocument()
      expect(screen.getByRole('link', { name: 'Ver todos los planes' })).toHaveAttribute(
        'href',
        '/metas',
      )
    },
  )

  it('el Administrador consulta el plan sin acciones de empezar', async () => {
    renderApp('/metas/dormir-mejor', { como: 'admin' })

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Dormir mejor' }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Empezar este plan/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Elegir este plan' })).not.toBeInTheDocument()
  })
})

describe('Planes (con sesión)', () => {
  it('"Mi día" de la jornada va primero y se explica que se recomienda por la jornada', async () => {
    renderApp('/planes', { como: 'estandar' })

    const miDia = await screen.findByRole('region', { name: 'Tu día de trabajo saludable' })
    expect(
      within(miDia).getByRole('heading', { name: 'Mi día de oficina saludable' }),
    ).toBeInTheDocument()
    expect(within(miDia).getByText('Pensado para tu jornada de oficina.')).toBeInTheDocument()
    expect(within(miDia).getByRole('link', { name: /Ver el plan/ })).toHaveAttribute(
      'href',
      '/planes/mi-dia-oficina',
    )

    // Sin el permiso de salud no es una recomendación personalizada: se invita a afinarla.
    expect(screen.getByText('Recomendados por tu jornada')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Responder las preguntas de salud' })).toHaveAttribute(
      'href',
      '/ajustes/perfil-salud',
    )

    // Las variantes de otras jornadas no se recomiendan (siguen en el catálogo de abajo).
    const recomendados = screen.getByRole('region', { name: 'Recomendados para ti' })
    expect(
      within(recomendados).queryByRole('heading', { name: 'Mi día saludable en turno de noche' }),
    ).not.toBeInTheDocument()
    expect(
      within(recomendados).queryByRole('heading', { name: 'Mi día de oficina saludable' }),
    ).not.toBeInTheDocument()
    // Debajo está todo el catálogo.
    const catalogo = screen.getByRole('region', { name: 'Todos los planes' })
    expect(await within(catalogo).findAllByRole('article')).toHaveLength(13)
  })

  it('un plan de otra jornada ve el suyo', async () => {
    renderApp('/planes', { como: 'otro' })

    const miDia = await screen.findByRole('region', { name: 'Tu día de trabajo saludable' })
    expect(
      within(miDia).getByRole('heading', { name: 'Mi día saludable en turno de noche' }),
    ).toBeInTheDocument()
  })

  it('lo que no conviene se muestra aparte, con su razón, y no se puede elegir', async () => {
    conEmbarazo()
    const { user } = renderApp('/planes', { como: 'estandar' })

    const noAptos = await screen.findByRole('region', { name: 'No recomendado para ti' })
    const tarjeta = within(noAptos).getByRole('article')
    expect(
      within(tarjeta).getByRole('heading', { name: 'Perder grasa de forma sostenible' }),
    ).toBeInTheDocument()
    expect(
      within(tarjeta).getByText(/ahora no recomendamos planes de control de peso/),
    ).toBeInTheDocument()
    // La razón no nombra la condición de salud.
    expect(tarjeta).not.toHaveTextContent(/embarazo/i)
    // En el catálogo completo el mismo plan lleva la marca, para no verlo como uno cualquiera.
    const catalogo = screen.getByRole('region', { name: 'Todos los planes' })
    const enCatalogo = (await within(catalogo).findAllByRole('article')).find((a) =>
      within(a).queryByRole('heading', { name: 'Perder grasa de forma sostenible' }),
    )!
    expect(within(enCatalogo).getByText('No recomendado para ti')).toBeInTheDocument()
    expect(within(catalogo).getAllByText('No recomendado para ti')).toHaveLength(1)
    // Con el cuestionario respondido ya no es "solo por tu jornada".
    expect(screen.queryByText('Recomendados por tu jornada')).not.toBeInTheDocument()

    await user.click(within(tarjeta).getByRole('link', { name: /Ver el plan/ }))
    // Primero el plan nuevo: la página de planes sigue unos instantes en pantalla y tiene su propio encabezado.
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Perder grasa de forma sostenible' }),
    ).toBeInTheDocument()
    expect(await screen.findByText('No recomendado para ti')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Elegir este plan' })).toBeDisabled()
  })

  it('"Elegir este plan" lo guarda como el plan de la persona', async () => {
    const { user } = renderApp('/planes/dormir-mejor', { como: 'estandar' })

    await user.click(await screen.findByRole('button', { name: 'Elegir este plan' }))

    expect(await screen.findByText('Listo: este es ahora tu plan.')).toBeInTheDocument()
    expect(db.perfiles.get(idDe('estandar'))?.metaSlug).toBe('dormir-mejor')
    expect(await screen.findByText('Es tu plan actual')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Elegir este plan' })).not.toBeInTheDocument()
    // Cambiar el plan no toca el resto del perfil (peso y altura sin cambios no piden permiso).
    expect(db.perfiles.get(idDe('estandar'))).toMatchObject({ pesoKg: 68.4, edad: 29 })
  })

  it('al elegir parte del perfil vigente: un peso registrado hace un momento no se pisa', async () => {
    const { user } = renderApp('/planes/dormir-mejor', { como: 'estandar' })
    const boton = await screen.findByRole('button', { name: 'Elegir este plan' })
    await vi.waitFor(() => expect(boton).toBeEnabled())
    // Otro dispositivo (o Progreso) cambió el peso mientras esta pantalla guardaba el perfil en caché.
    db.perfiles.set(idDe('estandar'), { ...db.perfiles.get(idDe('estandar'))!, pesoKg: 70.2 })

    await user.click(boton)

    // Con el perfil viejo el servidor vería un peso nuevo y pediría el permiso de salud (409).
    expect(await screen.findByText('Listo: este es ahora tu plan.')).toBeInTheDocument()
    expect(db.perfiles.get(idDe('estandar'))).toMatchObject({
      metaSlug: 'dormir-mejor',
      pesoKg: 70.2,
    })
  })

  it('sin saber si el plan es para la persona no se puede elegir', async () => {
    server.use(
      http.get('/api/plans/recommended', () =>
        HttpResponse.json({ title: 'Error.', status: 500 }, { status: 500 }),
      ),
    )
    renderApp('/planes/dormir-mejor', { como: 'estandar' })

    const boton = await screen.findByRole('button', { name: 'Elegir este plan' })
    await vi.waitFor(() => expect(db.perfiles.size).toBeGreaterThan(0))
    expect(boton).toBeDisabled()
    await new Promise((r) => setTimeout(r, 50))
    expect(boton).toBeDisabled()
  })

  it('el plan actual se reconoce al entrar', async () => {
    renderApp('/planes/primeros-5-km', { como: 'estandar' })

    expect(await screen.findByText('Es tu plan actual')).toBeInTheDocument()
  })

  it('el menú ofrece Planes solo al rol Estándar', async () => {
    renderApp('/hoy', { como: 'estandar' })
    const menu = await screen.findByRole('navigation', { name: 'Principal' })
    expect(within(menu).getByRole('link', { name: 'Planes' })).toHaveAttribute('href', '/planes')
  })

  it('el Administrador no ve Planes en el menú ni entra a la ruta', async () => {
    renderApp('/planes', { como: 'admin' })

    expect(await screen.findByText('No tienes acceso a esta sección')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Planes' })).not.toBeInTheDocument()
  })
})

describe('Hoy: tu día de trabajo saludable', () => {
  it('quien aún no tiene hábitos ve su "Mi día" como acción principal', async () => {
    db.habitos = []
    renderApp('/hoy', { como: 'estandar' })

    const tarjeta = await screen.findByRole('region', { name: 'Tu día de trabajo saludable' })
    expect(within(tarjeta).getByText('Mi día de oficina saludable')).toBeInTheDocument()
    expect(within(tarjeta).getByRole('link', { name: /Empezar mi día saludable/ })).toHaveAttribute(
      'href',
      '/planes/mi-dia-oficina',
    )
  })

  it('quien ya tiene hábitos no la ve ni siquiera pide la recomendación', async () => {
    // La ausencia de la tarjeta no prueba nada si la consulta aún no terminó: se mira que no se pida.
    const pedidas: string[] = []
    server.events.on('request:start', ({ request }) => pedidas.push(new URL(request.url).pathname))
    try {
      renderApp('/hoy', { como: 'estandar' })

      expect(await screen.findByRole('button', { name: 'Marcar hecha: Leer' })).toBeInTheDocument()
      expect(pedidas).toContain('/api/habits')
      expect(pedidas).not.toContain('/api/plans/recommended')
      expect(
        screen.queryByRole('region', { name: 'Tu día de trabajo saludable' }),
      ).not.toBeInTheDocument()
    } finally {
      server.events.removeAllListeners()
    }
  })
})
