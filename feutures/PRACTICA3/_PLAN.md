# Plan de implementación — Práctica 3

## Propósito y fuente funcional

Implementar la etapa evaluada del módulo Habit Tracker, proteger su máquina de estados con pruebas e integrar una operación con Gestión de Permisos. La especificación funcional y la rúbrica detalladas están en [practica-3-modulo-negocio-requisitos.md](practica-3-modulo-negocio-requisitos.md) y se basan en el PDF oficial de la práctica.

El alcance es RF-NEG-02, RF-NEG-03, RF-NEG-04, RF-NEG-05, RF-NEG-06 y RF-NEG-09, además de RD-04, RD-07, RD-09 y RD-12. RF-NEG-07, RF-NEG-08 y RF-NEG-10 quedan fuera de esta práctica según el PDF.

## Fase 0: Preparación del entorno y Git

1. Actualizar `main` desde `origin/main` y confirmar que no tenga cambios locales.
2. Trabajar en `feature/practica-3`. Como una versión anterior de esta rama ya se integró, adelantarla desde `main` solo mediante fast-forward. Si hay commits únicos que impiden ese avance, conservarlos y crear `feature/practica-3-implementacion` desde `main`; no usar reset.
3. Confirmar que el árbol de trabajo esté limpio antes de iniciar la implementación.
4. Hacer commits pequeños y atómicos, en español, con los RF/RD correspondientes.

## Fase 1: Módulo de negocio Habit Tracker

Implementar el contrato detallado en el documento de requisitos:

- La máquina tendrá exactamente cuatro estados: `Planificado` (inicial), `Activo`, `Pausado` y `Completado` (único terminal). Las reglas se declaran en un solo lugar del dominio y son independientes de la máquina de Gestión de Permisos.
- La entidad registra propietario, objetivo medible, unidad y periodicidad diaria o semanal. El identificador del propietario se obtiene de la identidad autenticada, no del cuerpo de la petición.
- Implementar las cinco operaciones separables con sus rutas y resultados observables: crear hábito, consultar hábitos propios, cambiar estado, registrar progreso y reconfigurar/resetear el hábito con el permiso aprobado `Habits.AdvancedReconfig`.
- Validar la propiedad en el dominio para las transiciones que la exigen. Completar requiere que el objetivo del período actual se haya alcanzado.
- Proteger todas las operaciones en el servidor con JWT y autorización por rol. La autorización de propietario se verifica en cada acceso a un hábito; la aprobación del permiso corresponde a un Administrador en Gestión de Permisos.
- Persistir hábitos, objetivos y avances fuera del proceso (**RD-09**) y preparar la migración de esquema. Antes de retirar los estados actuales `Pendiente` y `Abandonado`, revisar si hay datos existentes; no convertir silenciosamente `Abandonado` a otro estado.

## Fase 2: Suite de la máquina de estados

- Crear pruebas independientes con fases Preparar–Actuar–Verificar (AAA) y nombres que describan el comportamiento.
- Probar cada fila permitida y prohibida del documento de requisitos. En cada transición prohibida, comprobar el rechazo y que el estado permanezca igual.
- Probar expresamente el estado terminal, la condición de propietario de `Planificado → Activo` y la condición de objetivo para `Activo → Completado`.
- Ejecutar la suite sin levantar la API completa ni depender de una base externa (**RD-12**). Documentar un solo comando, `dotnet test`, y comprobarlo desde un clon limpio.

## Fase 3: Integración real con Gestión de Permisos

- Probar la operación `advanced-reconfig` usando los servicios, repositorios y dominio reales de ambos módulos, sin mocks/dobles ni servidor HTTP.
- Con una solicitud pendiente o rechazada, comprobar que la acción se rechaza. Tras la aprobación del Administrador y el estado efectivo `Aplicada`, comprobar que se permite.
- Asegurar que la prueba falle si se omite la consulta a Gestión de Permisos (defecto 4). Mantener la dirección de dependencias y las interfaces del diseño por capas.

## Fase 4: Defectos plantados y tags

La secuencia exacta de cada mutación y sus pruebas está en el documento de requisitos. La primera fila prohibida se fija como `Planificado → Completado`; así el defecto 1 no se confunde con el defecto 2, que permite salir del estado terminal.

- Crear el tag base `practica-3` sobre el commit donde toda la suite pasa.
- Crear cuatro commits independientes, todos hijos directos del commit `practica-3`. Cada commit cambia un único archivo de código de producción, no modifica pruebas y menciona archivo y línea en el mensaje.
- Crear los tags `practica-3-defecto-1` a `practica-3-defecto-4`. En cada tag de defecto, el comando documentado debe hacer fallar al menos una prueba correspondiente; en `practica-3`, todas pasan.
- No corregir las mutaciones en sus tags.

## Fase 5: Documentación de entrega

- Documentar en `README.md` cómo ejecutar y provocar individualmente las cinco funcionalidades, cómo ejecutar todas las pruebas con un comando y cómo iniciar la API para la revisión acumulada.
- Actualizar `docs/maquina-de-estados.md` para que sus estados, filas, condiciones, rechazos y estado terminal coincidan exactamente con el código y las pruebas. Mantener `Planificado → Completado` como primera fila prohibida.
- Añadir en `README.md` la sección `## Defectos`, con los cuatro tags, el requisito, la prueba que falla y el comportamiento mutado.
- No documentar como implementado lo que aún no se haya probado.

## Fase 6: Bitácora

Crear `docs/bitacora-practica-3.md` con solicitudes reales al agente, respuestas y evidencia de la sesión. Registrar al menos un error del agente, cómo se detectó y cómo se corrigió; no inventar evidencia.

## Puntaje oficial — 8.0 puntos

| Criterio | Puntaje |
|---|---:|
| Módulo de negocio RF-NEG-02 a RF-NEG-06 y RF-NEG-09 | 1.5 |
| Suite de la máquina de estados | 1.0 |
| Detección de los defectos 1 a 3 y forma de sus tags | 1.5 |
| Prueba de integración con Gestión de Permisos | 2.0 |
| Revisión acumulada: Control de Acceso y Gestión de Permisos | 2.0 |
| **Total** | **8.0** |

Los descriptores completos de No logrado, Parcialmente logrado y Logrado están en el documento de requisitos y deben aplicarse literalmente.

## Revisión final como evaluador

Desde un clon limpio, seguir el README y verificar `git checkout practica-3`, el comando único de pruebas y los cinco frentes acumulados. Para cada defecto, confirmar que su único padre sea `practica-3`, revisar `git diff --stat practica-3 practica-3-defecto-N`, verificar que no toque pruebas y ejecutar el mismo comando de pruebas. Contrastar el README, `docs/maquina-de-estados.md`, las pruebas y el código; presentar la matriz con la rúbrica oficial de 8 puntos del documento de requisitos.
