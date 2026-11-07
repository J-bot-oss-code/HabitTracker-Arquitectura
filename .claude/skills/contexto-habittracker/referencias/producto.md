# Producto: principios, motivación y contenido local

> Plan completo: `docs/plan-habittracker-v2.md`. Esto es lo que toda sesión debe respetar sin releerlo.

**Problema:** la gente con jornadas largas descuida su salud y abandona las apps de hábitos en las dos primeras semanas.
**Usuario:** persona adulta en República Dominicana (oficina, remoto, turno nocturno o trabajo físico), sin tiempo ni gimnasio garantizados.
**Pregunta para juzgar cada pantalla, fase y correo:** ¿ayuda a que esta persona **empiece, siga o vuelva**?

## Principios P1–P8

| # | Principio | Regla concreta |
|---|---|---|
| P1 | Primer valor en menos de 2 minutos | Un plan Básico se empieza en 3 toques y sin cuestionario de salud; los datos sensibles se piden solo cuando un plan los necesita |
| P2 | Diseñar desde la jornada laboral | `perfilLaboral` es la puerta de entrada; la primera recomendación es "Mi día de trabajo saludable" |
| P3 | Fallar es normal y nada castiga | Versión mínima, racha tolerante (un día sin registro no la corta, dos seguidos sí), modo suave, pausa justificada y ajuste del plan |
| P4 | Motivar la conducta, nunca el cuerpo | Se premian la constancia y el regreso; nunca el peso, las medidas, las calorías ni la comparación |
| P5 | El correo es un canal de acción | Cada correo permite hacer algo (marcar en un clic) y se espacia si la persona se aleja (3, 7 y 21 días) |
| P6 | Medir sin datos de salud | Métricas agregadas de adherencia, sin identificadores y con nulo para cohortes de menos de 5 |
| P7 | Contenido local | Calor, guagua, comida dominicana, turnos reales y personas sin gimnasio |
| P8 | Corte vertical antes que ancho | Catálogo pequeño y bueno → producción → piloto; las Fases 7–9 dependen del informe del piloto (docs/piloto.md, Fase 6) |

## Decisiones fijas

- **Básico** = de 1 a 3 hábitos simples durante 2 a 4 semanas. **Regular** = programa de 4 a 12 semanas con guía semanal. Los dos son gratis; Premium vendrá después.
- Un Básico nunca es de ControlDePeso ni de intensidad Alta.
- **Planes no aptos:** se muestran como "No recomendado para ti" con el motivo y el servidor responde 400 `plan_no_apto`.
- **Cifrado en reposo** (Data Protection) del cuestionario de salud y del contenido de las notas.
- **Correos:** opt-in explícito (casilla desmarcada), baja en un clic y nunca con datos de salud ni con el contenido de las notas.

## Motivación: permitido y prohibido

- **Mensajes:**
  - en `mensajes-motivacion.json`, con las categorías inicio, constancia, recaida, hito, cierre y regreso;
  - tuteo y 160 caracteres o menos;
  - 1 al día como máximo en la app y 1 dentro del resumen por correo.
- **Logros** simbólicos, por conducta y felicitados una sola vez: primer hábito, primera semana, 7/21/30 días, regreso, mitad del plan y plan completado.
- **Prohibido** (con prueba):
  - mensajes o logros ligados a peso, kg, IMC, calorías, dieta o apariencia;
  - rankings, puntos competitivos, comparaciones y premios externos o en dinero;
  - textos de culpa ("perdiste tu racha", "fallaste") y el rojo cuando se falla un día.
- **Prueba de contenido:** ningún mensaje contiene peso, kg, calorías, dieta, gordo, perdiste, fallaste ni culpa (comparando sin acentos ni mayúsculas).

## Guía de contenido local

- **Tono:** español dominicano neutro, tuteo, frases cortas, unidades métricas. Sin moralizar sobre la comida ni hablar de "malos" alimentos.
- **Calor:**
  - hidratación prudente (5 L o 20 vasos al día como máximo);
  - caminar o correr temprano o por la tarde, sin el sol del mediodía;
  - aviso médico por hipertensión, enfermedad cardíaca o renal.
- **Transporte:** bajarse una parada antes de la guagua o del carro público, usar las escaleras, estirarse en la espera, pausas para quien va en moto o conduce.
- **Comida:** plátano, yuca, arroz, habichuelas, aguacate, mango, chinola y guineo. En "menos azúcar", hablar de refrescos y jugos sin culpar.
- **Jornadas:** call center, zona franca, turno nocturno, delivery en moto y trabajo de pie, con anclas y horas que tengan sentido para cada una.
- **Sin gasto:**
  - planes en casa sin equipo (una botella o un galón de agua como peso);
  - sin suplementos ni productos de pago.
- **Prudencia:**
  - guías generales de salud pública (OMS sobre actividad y sueño);
  - aviso de que no sustituyen el consejo médico;
  - sin metas calóricas por debajo de umbrales seguros.
- **Revisión humana:** una persona del perfil laboral correspondiente lee cada plan nuevo antes de publicarlo.
- **Anclas** (lista cerrada): AlDespertar, AlLlegarAlTrabajo, AMediaManana, DespuesDelAlmuerzo, AMediaTarde, AlSalirDelTrabajo, AntesDeCenar y AntesDeDormir.
