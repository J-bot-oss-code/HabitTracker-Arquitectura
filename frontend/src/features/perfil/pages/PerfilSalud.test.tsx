import { screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { renderApp } from '@/test/render'

// Ana (estandar@demo.com) tiene perfil y el consentimiento de datos de salud en la semilla.
const ana = () => db.usuarios[0].id

describe('Configuración › Perfil y salud', () => {
  beforeEach(() => db.cuestionarios.delete(ana()))

  it('muestra el perfil, el cuestionario por bloques y lo que se tendrá en cuenta', async () => {
    renderApp('/ajustes/perfil-salud', { como: 'estandar' })
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Perfil y salud' }),
    ).toBeInTheDocument()
    expect(
      await screen.findByRole('heading', { name: 'Tu jornada y tus datos' }),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Tu jornada')).toHaveValue('OficinaDiurna')
    expect(screen.getByRole('heading', { name: '1. Tus objetivos' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: '2. Revisión de seguridad' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: '3. Condiciones y alergias' })).toBeInTheDocument()
    expect(screen.getByText('Puedes empezar cualquier plan apto para tu edad.')).toBeInTheDocument()
  })

  it('guarda el perfil y conserva el peso que ya tenía', async () => {
    const { user } = renderApp('/ajustes/perfil-salud', { como: 'estandar' })
    await user.selectOptions(await screen.findByLabelText('Tu jornada'), 'TurnoNocturno')
    await user.click(screen.getByRole('button', { name: 'Guardar perfil' }))
    expect(await screen.findByText('Perfil guardado.')).toBeInTheDocument()
    expect(db.perfiles.get(ana())).toMatchObject({
      perfilLaboral: 'TurnoNocturno',
      pesoKg: 68.4,
    })
  })

  it('sin el permiso se puede cambiar la jornada, pero un peso nuevo lo pide y se activa aquí', async () => {
    db.consentimientos.set(ana(), { ...db.consentimientos.get(ana())!, datosSalud: false })
    const { user } = renderApp('/ajustes/perfil-salud', { como: 'estandar' })
    // El formulario reenvía el peso guardado: eso no es un dato nuevo y no pide el permiso.
    await user.selectOptions(await screen.findByLabelText('Tu jornada'), 'Remoto')
    await user.click(screen.getByRole('button', { name: 'Guardar perfil' }))
    expect(await screen.findByText('Perfil guardado.')).toBeInTheDocument()

    const peso = screen.getByLabelText('Peso (kg)')
    await user.clear(peso)
    await user.type(peso, '67')
    await user.click(screen.getByRole('button', { name: 'Guardar perfil' }))
    expect(
      await screen.findByText('Para guardar datos de salud necesitas dar el permiso de arriba.'),
    ).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Permitir usar mis datos de salud' }))
    await screen.findByRole('button', { name: 'Guardar perfil' })
    expect(db.consentimientos.get(ana())?.datosSalud).toBe(true)
    await user.click(screen.getByRole('button', { name: 'Guardar perfil' }))
    await waitFor(() => expect(db.perfiles.get(ana())?.pesoKg).toBe(67))
  })

  it('en condiciones «Ninguna» es exclusiva y al guardar la evaluación se actualiza', async () => {
    const { user } = renderApp('/ajustes/perfil-salud', { como: 'estandar' })
    const condiciones = within(
      await screen.findByRole('group', { name: '¿Tienes alguna de estas condiciones?' }),
    )
    const alergias = within(screen.getByRole('group', { name: '¿Alergias o intolerancias?' }))

    // Marcar «Ninguna» quita lo demás, y marcar otra opción quita «Ninguna».
    await user.click(condiciones.getByRole('checkbox', { name: 'Asma' }))
    await user.click(condiciones.getByRole('checkbox', { name: 'Ninguna' }))
    expect(condiciones.getByRole('checkbox', { name: 'Asma' })).not.toBeChecked()
    await user.click(condiciones.getByRole('checkbox', { name: 'Embarazo' }))
    expect(condiciones.getByRole('checkbox', { name: 'Ninguna' })).not.toBeChecked()
    await user.click(alergias.getByRole('checkbox', { name: 'Ninguna' }))
    await user.click(screen.getByRole('button', { name: 'Guardar condiciones' }))

    expect(await screen.findByText('Condiciones y alergias guardadas.')).toBeInTheDocument()
    expect(
      await screen.findByText('Sin planes para bajar de peso ni de calorías'),
    ).toBeInTheDocument()
    expect(db.cuestionarios.get(ana())?.condiciones).toEqual({
      condiciones: ['Embarazo'],
      alergias: ['Ninguna'],
    })
  })

  it('la revisión de seguridad exige las 7 respuestas; un «sí» recomienda consultar al médico', async () => {
    const { user } = renderApp('/ajustes/perfil-salud', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Guardar revisión' }))
    expect(screen.getByText('Responde las 7 preguntas antes de guardar.')).toBeInTheDocument()

    const grupos = screen
      .getAllByRole('group')
      .filter((g) => g.getAttribute('aria-label')?.startsWith('¿'))
    for (const [i, grupo] of grupos.entries()) {
      await user.click(within(grupo).getByRole('button', { name: i === 1 ? 'Sí' : 'No' }))
    }
    await user.click(screen.getByRole('button', { name: 'Guardar revisión' }))
    expect(await screen.findByText(/consultar a tu médico antes de empezar/)).toBeInTheDocument()
  })

  it('no está en la Configuración del Administrador', async () => {
    renderApp('/ajustes/perfil-salud', { como: 'admin' })
    expect(await screen.findByText('No tienes acceso a esta sección')).toBeInTheDocument()
  })
})
