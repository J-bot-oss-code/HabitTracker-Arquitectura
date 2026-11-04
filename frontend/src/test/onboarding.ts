import { screen } from '@testing-library/react'
import type { UserEvent } from '@testing-library/user-event'

// Completa los pasos 3 a 5 del alta con datos válidos. Supone que el onboarding está en
// pantalla y que el registro dejó recordados los consentimientos (no se vuelven a pedir).
export async function completarOnboarding(
  user: UserEvent,
  {
    edad = '31',
    aceptarConsentimientos = false,
  }: { edad?: string; aceptarConsentimientos?: boolean } = {},
) {
  const edadInput = await screen.findByLabelText('Edad')
  await user.clear(edadInput)
  await user.type(edadInput, edad)
  await user.selectOptions(screen.getByLabelText(/^Sexo/), 'no-decir')
  await user.clear(screen.getByLabelText('Altura (cm)'))
  await user.type(screen.getByLabelText('Altura (cm)'), '170')
  await user.clear(screen.getByLabelText('Peso (kg)'))
  await user.type(screen.getByLabelText('Peso (kg)'), '70')
  await user.click(screen.getByRole('button', { name: 'Continuar' }))

  await user.selectOptions(await screen.findByLabelText('Nivel de actividad'), 'moderada')
  await user.click(screen.getByRole('button', { name: 'Continuar' }))

  await user.click(await screen.findByRole('checkbox', { name: /He leído el aviso de salud/ }))
  if (aceptarConsentimientos) {
    await user.click(screen.getByRole('checkbox', { name: /Términos de uso/ }))
    await user.click(screen.getByRole('checkbox', { name: /Política de privacidad/ }))
  }
  await user.click(screen.getByRole('button', { name: 'Empezar mi plan' }))
}
