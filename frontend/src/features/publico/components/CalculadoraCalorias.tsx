import { zodResolver } from '@hookform/resolvers/zod'
import { useId, useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router'
import { Button } from '@/shared/ui/Button'
import { Input, Select } from '@/shared/ui/Field'
import { Icono } from '@/shared/ui/Icono'
import { calcularKcal } from '../calorias'
import { calculadoraSchema, type CalculadoraForm } from '../schemas'
import { etiquetaSexo, infoActividad, LIMITES_CORPORALES, NIVELES_ACTIVIDAD, SEXOS } from '../types'

const formatoKcal = new Intl.NumberFormat('es-DO')

// Calculadora pública (§5.1, punto 4): sin cuenta y sin guardar nada. El resultado se anuncia
// a los lectores de pantalla y siempre va con el aviso de que es una estimación.
export function CalculadoraCalorias({ nivelTitulo = 2 }: { nivelTitulo?: 1 | 2 }) {
  const [resultado, setResultado] = useState<{ kcal: number; datos: CalculadoraForm } | null>(null)
  const idTitulo = useId()
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CalculadoraForm>({ resolver: zodResolver(calculadoraSchema) })
  const Titulo = nivelTitulo === 1 ? 'h1' : 'h2'
  const { edad, alturaCm, pesoKg } = LIMITES_CORPORALES

  // Con datos inválidos se retira el resultado anterior: nunca queda una cifra que no
  // corresponde a lo escrito.
  const calcular = handleSubmit(
    (datos) => setResultado({ kcal: calcularKcal(datos), datos }),
    () => setResultado(null),
  )

  return (
    <section id="calculadora" aria-labelledby={idTitulo} className="scroll-mt-24 py-10 sm:py-14">
      <Titulo id={idTitulo} className="text-3xl font-extrabold sm:text-[2rem]">
        Calculadora de calorías
      </Titulo>
      <p className="mt-2 text-texto-2">
        Estima cuántas calorías gastas al día. No necesitas cuenta: tus datos solo se guardan si
        después creas tu plan.
      </p>

      <div className="mt-6 grid gap-4 lg:grid-cols-[minmax(0,1.3fr)_minmax(0,1fr)]">
        <form
          onSubmit={calcular}
          noValidate
          aria-label="Datos para la calculadora"
          className="grid gap-4 rounded-2xl border border-borde bg-superficie p-5 sm:grid-cols-2 sm:p-6"
        >
          <Input
            etiqueta="Edad"
            type="number"
            inputMode="numeric"
            min={edad.min}
            max={edad.max}
            ayuda={`Entre ${edad.min} y ${edad.max} años.`}
            error={errors.edad?.message}
            {...register('edad', { valueAsNumber: true })}
          />
          <Select
            etiqueta="Sexo"
            defaultValue=""
            error={errors.sexo?.message}
            {...register('sexo')}
          >
            <option value="" disabled>
              Elige una opción
            </option>
            {SEXOS.map((s) => (
              <option key={s} value={s}>
                {etiquetaSexo[s]}
              </option>
            ))}
          </Select>
          <Input
            etiqueta="Altura (cm)"
            type="number"
            inputMode="decimal"
            min={alturaCm.min}
            max={alturaCm.max}
            ayuda={`Entre ${alturaCm.min} y ${alturaCm.max} cm.`}
            error={errors.alturaCm?.message}
            {...register('alturaCm', { valueAsNumber: true })}
          />
          <Input
            etiqueta="Peso (kg)"
            type="number"
            inputMode="decimal"
            step="0.1"
            min={pesoKg.min}
            max={pesoKg.max}
            ayuda={`Entre ${pesoKg.min} y ${pesoKg.max} kg.`}
            error={errors.pesoKg?.message}
            {...register('pesoKg', { valueAsNumber: true })}
          />
          <div className="sm:col-span-2">
            <Select
              etiqueta="Actividad"
              defaultValue=""
              error={errors.actividad?.message}
              {...register('actividad')}
            >
              <option value="" disabled>
                Elige tu nivel de actividad
              </option>
              {NIVELES_ACTIVIDAD.map((n) => (
                <option key={n} value={n}>
                  {infoActividad[n].etiqueta}
                </option>
              ))}
            </Select>
          </div>
          <div className="sm:col-span-2">
            <Button type="submit" tamano="lg" className="w-full sm:w-auto">
              Calcular
            </Button>
          </div>
        </form>

        <div
          aria-live="polite"
          aria-atomic="true"
          className="flex flex-col justify-center rounded-2xl panel-tinta p-6 sm:p-8"
        >
          {resultado === null ? (
            <p className="text-tinta-atenuado">Completa tus datos para ver una estimación.</p>
          ) : (
            <>
              <p className="text-sm font-medium text-tinta-atenuado">Tu gasto estimado</p>
              <p className="mt-2">
                <span className="cifra text-5xl">{formatoKcal.format(resultado.kcal)}</span>{' '}
                <span className="text-lg font-semibold">kcal / día</span>
              </p>
              <p className="mt-4 text-sm text-tinta-atenuado">
                Es una estimación, no una indicación médica. Consulta a un profesional si tienes una
                condición de salud.
              </p>
              <Link
                to="/registro"
                // Los datos viajan al registro y el onboarding los propone: no hay que escribirlos dos veces.
                state={{ datosCalculadora: resultado.datos }}
                className="mt-6 inline-flex min-h-11 items-center gap-1.5 self-start rounded-lg font-semibold text-tinta-texto underline underline-offset-4 hover:text-acento-claro"
              >
                Guardar y crear mi plan
                <Icono nombre="flecha" className="size-4" />
              </Link>
            </>
          )}
        </div>
      </div>
    </section>
  )
}
