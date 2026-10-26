# Plan de implementación — Práctica 3

## Objetivo

Implementar el módulo de negocio Habit Tracker de Programación III, con una máquina de estados propia, protegida por Control de Acceso, integrada con Gestión de Permisos, cubierta por pruebas y documentada para evaluación.

## Fase 0: Preparación del entorno y rama Git

1. Confirmar que `main` esté actualizada y limpia antes de iniciar el trabajo.
2. Crear `feature/practica-3` desde `main`.
3. Mantener el trabajo de esta práctica en esa rama y registrar cambios en commits pequeños, cada uno asociado a los requisitos funcionales o de diseño que atiende.

## Fase 1: Implementación del módulo de negocio Habit Tracker

- Implementar al menos cinco funcionalidades separables del negocio y explicarlas en `README.md` (**RF-NEG-02**).
- Definir una máquina de estados propia de tres a cinco estados en un único lugar del código, independiente de Gestión de Permisos (**RF-NEG-03, RF-NEG-09**). Como propuesta inicial: `Planificado → Activo → Pausado → Completado` o `Cancelado`; `Completado` y `Cancelado` serán terminales.
- Rechazar explícitamente al menos una transición prohibida sin modificar el estado (**RF-NEG-04**).
- Impedir cualquier salida desde un estado terminal (**RF-NEG-05**).
- Proteger en el servidor todas las operaciones del módulo con JWT y autorización por rol (**RF-NEG-06, RD-06**).
- Incluir una operación de negocio que requiera un permiso previamente aprobado por un Administrador mediante Gestión de Permisos.

## Fase 2: Suite unitaria de la máquina de estados

- Crear pruebas independientes con fases Arrange, Act y Assert identificables.
- Cubrir cada fila de la tabla de transiciones de `docs/maquina-de-estados.md`, tanto las permitidas como las prohibidas.
- Comprobar expresamente que una transición prohibida rechaza la operación y deja el estado intacto, y que los estados terminales no permiten transiciones de salida.
- Ejecutar las pruebas en milisegundos, sin iniciar la API completa ni depender de una base de datos externa (**RD-12**).

## Fase 3: Integración real entre Negocio y Gestión de Permisos

- Implementar una prueba unitaria de integración que use componentes reales, sin mocks, dobles ni servidor HTTP.
- Verificar ambos lados del flujo: la operación de negocio se rechaza antes de aprobar la solicitud de permiso y se ejecuta correctamente después de que un Administrador la apruebe.
- Comprobar que la prueba falla si la integración con Gestión de Permisos se omite.

## Fase 4: Cuatro defectos y tags Git

Plantar cada defecto en un commit independiente, descendiente directo de `practica-3`. Cada commit modificará únicamente código de producción, sin archivos de pruebas, e identificará el archivo y la línea afectados.

| Tag | Defecto plantado | Evidencia esperada |
|---|---|---|
| `practica-3-defecto-1` | Una transición prohibida pasa a permitirse. | Falla la prueba de esa transición. |
| `practica-3-defecto-2` | Se permite salir de un estado terminal. | Falla la prueba del estado terminal. |
| `practica-3-defecto-3` | Se omite la condición de una transición condicional. | Falla la prueba de la condición. |
| `practica-3-defecto-4` | Se omite la consulta a Gestión de Permisos en la acción del negocio. | Falla la prueba de integración real. |

## Fase 5: Documentación

- Actualizar `README.md` con las instrucciones para ejecutar el proyecto y provocar cada una de las cinco funcionalidades de negocio.
- Documentar los requisitos implementados, cómo ejecutar las pruebas y qué resultado se espera para transiciones permitidas, prohibidas y terminales.
- Crear o actualizar `docs/maquina-de-estados.md` con estados, transiciones permitidas, condiciones, rechazos y estados terminales.
- Añadir a `README.md` la tabla de los cuatro defectos, los tags, el requisito afectado y la prueba que evidencia cada mutación.

## Fase 6: Bitácora de la sesión

Crear `docs/bitacora-practica-3.md` con las tareas solicitadas al agente, sus respuestas y evidencia de la sesión. Registrar al menos un error del agente, cómo se detectó y cómo se corrigió.

## Rúbrica oficial — 8 puntos

| Criterio | Puntaje máximo |
|---|---:|
| Módulo de negocio implementado (**RF-NEG-02 a RF-NEG-06 y RF-NEG-09**) | 1.5 |
| Suite de la máquina de estados: cobertura de todas las filas y del estado terminal | 1.0 |
| Detección de los defectos 1 a 3 y forma de sus tags: hijos directos de `practica-3`, solo código de producción | 1.5 |
| Integración real con Gestión de Permisos: ambas caras del flujo y detección del defecto 4 | 2.0 |
| Revisión acumulada: Control de Acceso y Gestión de Permisos en los cinco frentes requeridos | 2.0 |
| **Total** | **8.0** |

Para cada criterio, la evaluación distingue **No logrado (0 %)**, **Parcialmente logrado (50 %)** y **Logrado (100 %)**.

## Verificación final como evaluador

Al terminar, revisar la entrega como profesor de Programación III desde un clon limpio: verificar los comandos de checkout de `practica-3` y de los cuatro tags, comprobar `git diff --stat practica-3 practica-3-defecto-N` para cada defecto, confirmar que cada diff toque un único archivo productivo y ningún archivo de pruebas, ejecutar la suite en la base y en los cuatro mutantes, y presentar la matriz cuantitativa final sobre 8.0 puntos con la evidencia de cada criterio.
