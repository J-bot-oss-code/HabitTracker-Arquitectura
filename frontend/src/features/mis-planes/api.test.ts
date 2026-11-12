import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { createElement, type ReactNode } from 'react'
import { beforeEach, describe, expect, it } from 'vitest'
import { habitKeys, useRegistrarAvance } from '@/features/habits/api'
import { db, PLAN_DEMO_ID, sembrarPlanDemo } from '@/mocks/db'
import { iniciarSesionComo } from '@/test/render'
import {
  esPlanNoEncontrado,
  misPlanesKeys,
  useAjustarPlan,
  useCambiarEstadoPlan,
  useHoy,
  useInscribirse,
  useMarcarLogrosVistos,
  useMiPlan,
  useMisPlanes,
  useRecordatorioPlan,
  useResumenProgreso,
} from './api'

// Los hooks contra la API simulada: qué piden y qué consultas dejan por refrescar.
function preparar() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const wrapper = ({ children }: { children: ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  // Datos en caché de todo el seguimiento, para ver cuáles quedan invalidados.
  const claves = {
    habitos: habitKeys.todos,
    lista: misPlanesKeys.lista,
    detalle: misPlanesKeys.detalle(PLAN_DEMO_ID),
    hoy: misPlanesKeys.hoy,
    resumen: misPlanesKeys.resumen,
  }
  for (const clave of Object.values(claves)) queryClient.setQueryData(clave, 'en caché')
  const invalidadas = () =>
    Object.entries(claves)
      .filter(([, clave]) => queryClient.getQueryState(clave)?.isInvalidated)
      .map(([nombre]) => nombre)
  return { queryClient, wrapper, invalidadas }
}

const TODO_EL_SEGUIMIENTO = ['habitos', 'lista', 'detalle', 'hoy', 'resumen']

beforeEach(() => {
  sembrarPlanDemo()
  iniciarSesionComo('estandar')
})

describe('consultas de Mis planes', () => {
  it('useMisPlanes, useHoy y useResumenProgreso traen lo del servidor', async () => {
    const { wrapper } = preparar()
    const { result } = renderHook(
      () => ({ planes: useMisPlanes(), hoy: useHoy(), resumen: useResumenProgreso() }),
      { wrapper },
    )
    await waitFor(() => expect(result.current.resumen.data?.planesEnCurso).toBe(1))
    await waitFor(() => expect(result.current.planes.data?.[0].id).toBe(PLAN_DEMO_ID))
    await waitFor(() => expect(result.current.hoy.data?.tareas).toHaveLength(3))
  })

  it('useMiPlan da null con un plan de otra persona y ni consulta un id que no es GUID', async () => {
    const { queryClient, wrapper } = preparar()
    queryClient.removeQueries({ queryKey: misPlanesKeys.detalle(PLAN_DEMO_ID) })
    iniciarSesionComo('otro')
    const ajeno = renderHook(() => useMiPlan(PLAN_DEMO_ID), { wrapper })
    await waitFor(() => expect(ajeno.result.current.isSuccess).toBe(true))
    expect(ajeno.result.current.data).toBeNull()

    const sinGuid = renderHook(() => useMiPlan('no-es-guid'), { wrapper })
    await waitFor(() => expect(sinGuid.result.current.isSuccess).toBe(true))
    expect(sinGuid.result.current.data).toBeNull()
  })

  it('useMiPlan trae el plan propio', async () => {
    const { queryClient, wrapper } = preparar()
    queryClient.removeQueries({ queryKey: misPlanesKeys.detalle(PLAN_DEMO_ID) })
    const { result } = renderHook(() => useMiPlan(PLAN_DEMO_ID), { wrapper })
    await waitFor(() => expect(result.current.data?.progreso.semanaActual).toBe(2))
  })
})

describe('qué invalida cada cambio', () => {
  it('registrar el avance de un hábito refresca también Hoy, Mis planes y el resumen', async () => {
    const { wrapper, invalidadas } = preparar()
    const habito = db.habitos.find((h) => h.planUsuarioId === PLAN_DEMO_ID)!
    const { result } = renderHook(() => useRegistrarAvance(habito.id), { wrapper })
    await result.current.mutateAsync({
      cantidad: 1,
      fechaUtc: new Date().toISOString(),
      esVersionMinima: true,
    })
    expect(invalidadas()).toEqual(TODO_EL_SEGUIMIENTO)
    expect(habito.registros[habito.registros.length - 1].esVersionMinima).toBe(true)
  })

  it('inscribirse refresca todo el seguimiento y devuelve el plan creado', async () => {
    const { wrapper, invalidadas } = preparar()
    const { result } = renderHook(() => useInscribirse(), { wrapper })
    const plan = await result.current.mutateAsync({ slug: 'dormir-mejor' })
    expect(plan).toMatchObject({ slug: 'dormir-mejor', estado: 'Activo' })
    expect(invalidadas()).toEqual(TODO_EL_SEGUIMIENTO)
  })

  it('cambiar el estado, ajustar y configurar el recordatorio refrescan todo el seguimiento', async () => {
    const estado = preparar()
    const transicion = renderHook(() => useCambiarEstadoPlan(PLAN_DEMO_ID), {
      wrapper: estado.wrapper,
    })
    const pausado = await transicion.result.current.mutateAsync({
      nuevoEstado: 'Pausado',
      motivoPausa: 'Viaje',
    })
    expect(pausado.estado).toBe('Pausado')
    expect(estado.invalidadas()).toEqual(TODO_EL_SEGUIMIENTO)
    await transicion.result.current.mutateAsync({ nuevoEstado: 'Activo' })

    const ajuste = preparar()
    const ajustar = renderHook(() => useAjustarPlan(PLAN_DEMO_ID), { wrapper: ajuste.wrapper })
    expect((await ajustar.result.current.mutateAsync('Extender')).extensiones).toBe(1)
    expect(ajuste.invalidadas()).toEqual(TODO_EL_SEGUIMIENTO)

    const aviso = preparar()
    const recordatorio = renderHook(() => useRecordatorioPlan(PLAN_DEMO_ID), {
      wrapper: aviso.wrapper,
    })
    const plan = await recordatorio.result.current.mutateAsync({
      hora: '07:15:00',
      recordatoriosActivos: true,
    })
    expect(plan).toMatchObject({ horaRecordatorio: '07:15', recordatoriosActivos: true })
    expect(aviso.invalidadas()).toEqual(TODO_EL_SEGUIMIENTO)
  })

  it('un rechazo del servidor también refresca: el estado en caché podía estar viejo', async () => {
    const { wrapper, invalidadas } = preparar()
    const { result } = renderHook(() => useCambiarEstadoPlan(PLAN_DEMO_ID), { wrapper })
    await expect(result.current.mutateAsync({ nuevoEstado: 'Completado' })).rejects.toMatchObject({
      status: 400,
      problem: { codigo: 'plan_aun_no_termina' },
    })
    expect(invalidadas()).toEqual(TODO_EL_SEGUIMIENTO)
  })

  it('marcar los logros como vistos solo refresca Hoy y el resumen', async () => {
    const { wrapper, invalidadas } = preparar()
    const { result } = renderHook(() => useMarcarLogrosVistos(), { wrapper })
    await result.current.mutateAsync()
    expect(invalidadas()).toEqual(['hoy', 'resumen'])
  })
})

describe('esPlanNoEncontrado', () => {
  it('reconoce el 404 con código y no un 404 sin cuerpo (endpoint inexistente)', async () => {
    const { wrapper } = preparar()
    const { result } = renderHook(
      () => useCambiarEstadoPlan('00000000-0000-4000-8000-000000000999'),
      {
        wrapper,
      },
    )
    const error = await result.current.mutateAsync({ nuevoEstado: 'Cancelado' }).catch((e) => e)
    expect(esPlanNoEncontrado(error)).toBe(true)

    const sinCuerpo = renderHook(() => useCambiarEstadoPlan('no-es-guid'), { wrapper })
    const otro = await sinCuerpo.result.current
      .mutateAsync({ nuevoEstado: 'Cancelado' })
      .catch((e) => e)
    expect(esPlanNoEncontrado(otro)).toBe(false)
  })
})
