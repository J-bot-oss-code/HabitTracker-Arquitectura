import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useState } from 'react'
import type { ReactNode } from 'react'
import { ApiError } from '@/shared/api/problem'
import { AuthProvider } from '@/shared/auth/AuthContext'
import { ThemeProvider } from '@/shared/theme/ThemeContext'
import { ToastProvider } from '@/shared/ui/Toast'

export function crearQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        refetchOnWindowFocus: false,
        // Reintentar un 4xx no cambia la respuesta; solo se reintentan fallos de red o 5xx.
        retry: (intentos, error) =>
          !(error instanceof ApiError && error.status < 500) && intentos < 2,
      },
    },
  })
}

export function Providers({
  children,
  queryClient,
}: {
  children: ReactNode
  queryClient?: QueryClient
}) {
  const [cliente] = useState(() => queryClient ?? crearQueryClient())
  return (
    <QueryClientProvider client={cliente}>
      <ThemeProvider>
        <AuthProvider>
          <ToastProvider>{children}</ToastProvider>
        </AuthProvider>
      </ThemeProvider>
    </QueryClientProvider>
  )
}
