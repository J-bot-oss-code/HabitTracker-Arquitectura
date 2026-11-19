import { screen } from '@testing-library/react'
import type { UserEvent } from '@testing-library/user-event'

// Completa el onboarding de 3 toques (jornada → zona horaria → aviso de salud). Supone que el onboarding está
// en pantalla y que el registro dejó recordados los consentimientos (no se vuelven a pedir), salvo que se indique.
export async function completarOnboarding(
  user: UserEvent,
  {
    jornada = 'Oficina de día',
    aceptarConsentimientos = false,
  }: { jornada?: string; aceptarConsentimientos?: boolean } = {},
) {
  await user.click(await screen.findByRole('radio', { name: new RegExp(jornada) }))
  await user.click(screen.getByRole('button', { name: 'Continuar' }))
  // La zona viene detectada del dispositivo: solo se confirma.
  await screen.findByLabelText('Zona horaria')
  await user.click(screen.getByRole('button', { name: 'Continuar' }))

  await user.click(await screen.findByRole('checkbox', { name: /He leído el aviso de salud/ }))
  if (aceptarConsentimientos) {
    await user.click(screen.getByRole('checkbox', { name: /Términos de uso/ }))
    await user.click(screen.getByRole('checkbox', { name: /Política de privacidad/ }))
  }
  await user.click(screen.getByRole('button', { name: 'Empezar mi día' }))
}
