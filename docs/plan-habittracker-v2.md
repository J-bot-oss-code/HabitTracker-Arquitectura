# Plan de mejora — Módulo de negocio HabitTracker v2 (revisión 2: adherencia primero)

## Contexto

**El problema.** La gente trabaja muchas horas y descuida su salud: pasa el día sentada, toma poca agua, duerme mal y no encuentra tiempo para moverse. En las apps de hábitos el fallo típico no es la falta de planes, sino **el abandono en las dos primeras semanas**.

**El objetivo.** Una persona con jornada larga:
- empieza en **menos de 2 minutos**;
- sostiene **2 o 3 micro-hábitos** ligados a su jornada;
- **vuelve** después de fallar.

Cada fase, pantalla y correo se juzga con una pregunta: *¿esto ayuda a que esta persona empiece, siga o vuelva?*

**Usuario objetivo:** persona adulta en República Dominicana, con trabajo de oficina, remoto, turno nocturno o trabajo físico, sin tiempo ni gimnasio garantizados.

**Punto de partida (código real):**
- HabitTracker solo registra hábitos sueltos: `Habito` + `RegistroDiario`, máquina `Planificado → Activo ⇄ Pausado → Completado`, 5 endpoints en `api/habits` y `GET /api/habits/plan-del-dia` (clima, P4).
- El frontend tiene:
  - un catálogo **estático** de 6 metas (`frontend/src/features/publico/catalogo.ts`);
  - un panel Hoy calculado en el navegador (`features/habits/hoy.ts`);
  - contratos **propuestos sin backend**: `/api/profile`, `/api/progress/weights`, `/api/progress/measurements` y `/api/admin/metrics`.
- La única cola de correos vive en Control de Acceso (`IEmailQueue`, con 2 métodos específicos).

**El enfoque.** Primero un **corte vertical** pequeño y bueno (Fases 0–5), llevado a producción con métricas y un **piloto con personas reales** (Fase 6). El piloto decide si se hacen las Fases 7–9 y en qué orden.

### Decisiones tomadas con el usuario (07-11-2026, vigentes)

1. **Básico y Regular se distinguen por su estructura.**
   - Básico: de 1 a 3 hábitos simples durante 2 a 4 semanas.
   - Regular: un programa de 4 a 12 semanas con guía semanal.
   - Los dos son gratis. **Premium** queda para después.
2. **Los planes no aptos se muestran y se bloquean.** Aparecen como "No recomendado para ti", con el motivo, y el servidor responde **400 `plan_no_apto`** si se intenta empezarlos.
3. **Cifrado en reposo desde el inicio** con ASP.NET Core Data Protection: cuestionario de salud (Fase 2) y contenido de las notas (Fase 9).
4. **La skill va en el repo y el ESTADO queda compacto** (Fase 0).
5. **Revisión del 07-11-2026:**
   - principios P1–P8;
   - motivación y recompensas;
   - fases renumeradas;
   - piloto como puerta de decisión.
6. **Muro de logros (08-11-2026):** el usuario pidió un top de las personas que han logrado metas, como motivación. Para no romper P4 ni la privacidad entra como **Fase 10 ⚑**, con estas condiciones:
   - es voluntario (consentimiento `compartirCumplimiento`);
   - usa un alias, nunca el nombre ni el correo;
   - solo cuenta constancia y planes completados, nunca peso ni nombres de planes;
   - es un top 10 mensual, sin posiciones bajas.

## Reglas que se respetan en todas las fases

**Arquitectura y calidad (sin cambios):**
- Capas `Api → Infrastructure → Application → Domain`. El dominio no tiene referencias ni EF. Interfaz explícita por pieza (RD-01) y lógica fuera del controller (RD-02).
- Los módulos se hablan por **puertos**: la interfaz vive en `HabitTracker.Application` y la implementa un **adaptador del Host**, como `PermisoServiceAdapter` y los `IParticipanteDatosUsuario`. El Core nunca referencia a HabitTracker (RD-03, verificado por el CI).
- Cada máquina de estados se valida en **un solo punto**: `TransicionesHabito` y la nueva `TransicionesPlanUsuario` (RD-04).
- Entradas validadas con listas cerradas, rangos y longitudes (RD-07).
- Errores en `ProblemDetails` con `codigo` estable y mensaje genérico (RD-08). Un 404 real siempre lleva cuerpo: `esNoDisponible` del frontend toma un 404 vacío como "endpoint inexistente".
- Fechas UTC con `TimeProvider` en el código nuevo. La hora local se convierte en un solo punto (`CalculadoraProximoEnvio`, `ResolvedorAncla`) (RD-11). Claves en variables de entorno (RD-10). Pruebas sin la app completa (RD-12).
- KISS/YAGNI: cada tabla, columna o puerto nace en la fase que lo usa.
- **Flujo de trabajo:**
  - rama a = backend y rama b = frontend;
  - skill `flujo-pr`, con el PR en 4 secciones;
  - `core-transversal-reviewer` antes de cada commit;
  - `frontend-reviewer` y `frontend-design-critic` en las PR b;
  - `ci-ok` y `seguridad-ok` en verde;
  - los tags `practica-*` no se tocan.
- **Commits** con IDs **existentes** del PDF y exactamente 2 dígitos (`RF-NEG-02.1` no pasa el CI): RF-NEG-01/02/03/04/05/06/07, RF-NOT-03/08/12 y RD-xx. No se inventan IDs. La tabla funcionalidad → RF va en el README del módulo.
- **Nombres de prueba:** `RF_NEG_xx_Sujeto_Escenario_Resultado` con `[Trait("Requirement", ...)]` y dobles escritos a mano (patrón de `HabitoServiceTests`).

**Principios de producto (nuevos, cada uno con su verificación):**

| # | Principio | Cómo se verifica |
|---|---|---|
| P1 | **Primer valor en menos de 2 minutos.** Un Básico se empieza en 3 toques y sin cuestionario de salud. Los datos sensibles se piden justo cuando un plan los necesita. | Fase 2: el onboarding de nivel 1 no exige edad, peso ni cuestionario, y un Básico no devuelve 409. Fase 6: el E2E cuenta 3 toques desde el onboarding hasta el plan empezado. |
| P2 | **Diseñar desde la jornada laboral.** `perfilLaboral` es la puerta de entrada y la primera recomendación es "Mi día de trabajo saludable". | Fase 3: cada perfil laboral tiene su variante y es el primer resultado sin cuestionario. |
| P3 | **Fallar es normal y nada castiga.** Versión mínima, racha tolerante, modo suave, pausa justificada y ajuste del plan. | Fase 4: pruebas de `CalculadoraProgreso` y `SugeridorAjuste`. Prueba de contenido sin palabras de culpa. |
| P4 | **Motivar la conducta, nunca el cuerpo.** La única comparación entre personas permitida es el Muro de logros voluntario de la Fase 10 (constancia, alias, top 10 mensual). | Fase 4: `CalculadoraLogros` no recibe peso ni medidas (prueba por reflexión). Prueba de contenido de los mensajes. Fase 10: el muro no expone salud, planes ni a quien no lo activó. |
| P5 | **El correo es un canal de acción, no de ruido.** | Fase 5: check-in en un clic y `CalculadoraCadencia` (3/7/21 días, nunca reactiva). |
| P6 | **Medir sin datos de salud.** | Fase 6: las métricas no leen tablas de salud, no devuelven IDs y una cohorte menor de 5 da nulo. |
| P7 | **Contenido local** (calor, guagua, comida dominicana, turnos reales, sin gimnasio). | Prueba de contenido (topes de hidratación, aviso médico, anclas cerradas) y revisión humana de cada plan nuevo (Fase 7). |
| P8 | **Corte vertical antes que ancho.** | Las Fases 7–9 no arrancan sin `docs/piloto.md` con el informe y la decisión. |

**Definición de terminado de cada fase:**
- referencias de la skill, inventario y ESTADO actualizados;
- ningún texto nuevo para la persona culpa ni compara;
- el flujo de primer valor sigue en 3 toques;
- pruebas y mutaciones de la fase en verde.

## Tabla de fases y dependencias

| Fase | Ramas | Contenido | Depende de |
|---|---|---|---|
| 0 | `docs/skill-contexto-proyecto` | Skill de contexto rápido, `producto.md` y ESTADO compacto | — |
| 1 | `refactor/habittracker-cimientos` | Cimientos y deuda técnica | 0 |
| 2 | `feature/habittracker-perfil-salud` · `feature/frontend-perfil-salud` | Perfil mínimo, salud justo a tiempo, progreso corporal y `ICifradorDatos` | 1 |
| 3 | `feature/habittracker-catalogo-planes` · `feature/frontend-catalogo-planes` | Catálogo de lanzamiento (13 planes) y recomendador | 2 |
| 4 | `feature/habittracker-mis-planes` · `feature/frontend-mis-planes` | Mis planes, check-in diario, adherencia, logros y mensajes | 3 |
| 5 | `feature/habittracker-recordatorios-correo` · `feature/frontend-notificaciones` | Correo como canal de acción | 4 (y `ICifradorDatos`/Data Protection de la 2) |
| 6 | `chore/habittracker-produccion` | Producción del corte vertical, métricas y **piloto** | 5 |
| 7 ⚑ | `feature/habittracker-catalogo-ampliado` · `feature/frontend-catalogo-ampliado` | Catálogo hasta ~35 planes | 6 (informe), `RecomendadorPlanes` y `ReglasSeguridadPlanes` (3) |
| 8 ⚑ | `feature/habittracker-planes-propios` · `feature/frontend-planes-propios` | Planes personalizados con sugerencias | 6 (informe), 3 y 4 |
| 9 ⚑ | `feature/habittracker-notas` · `feature/frontend-notas` | Notas, apuntes y recordatorios | 6 (informe), `ICifradorDatos` (2) y `ProgramacionEnvio` (5) |
| 10 ⚑ | `feature/habittracker-muro-logros` · `feature/frontend-muro-logros` | Muro de logros: top voluntario de constancia | 6 (informe), `CalculadoraProgreso` y `LogroUsuario` (4), regla de cohorte mínima (6) |

⚑ = **condicionada al piloto**. Cada fase posterior a la 6 repite, para sus endpoints nuevos, su parte de la lista de producción: límites de peticiones, auditoría, índices, mutación y ampliación del E2E.

Las fases 0, 1 y 6 tienen una sola rama. Los cambios del frontend que haya en ellas son pequeños y van en la misma rama, con commits separados.

---

## Fase 0 — Skill de contexto rápido y ESTADO compacto

### Diagnóstico: qué costó ponerse al día en esta sesión

| Paso | Costo | Causa |
|---|---|---|
| Leer `docs/ESTADO-DEL-PROYECTO.md` (lo exige AGENTS.md) | ~25.300 tokens, 274 líneas, 2 lecturas por superar el tope | ~70 % es historia (§11–§13) |
| Explorador de requisitos | ~101.000 tokens, 26 herramientas, ~2,5 min | El PDF no se renderiza y hubo que usar `pdftotext` |
| Explorador del frontend | ~187.000 tokens, 53 herramientas, ~3 min | Sin un mapa de features ni de contratos reales y propuestos |
| Explorador del backend | ~236.000 tokens, 45 herramientas, ~5,5 min | Sin un mapa de módulos, endpoints, puertos ni workers. Además, AGENTS.md dice que el cableado está en `ServiceExtensions.cs`, pero el de HabitTracker está en `HabitTrackerModuleExtensions.cs` |

Hallazgos que fueron difíciles de encontrar:
- la regex de commits (2 dígitos);
- el patrón de adaptadores del Host;
- que la cola de correos solo tiene 2 métodos;
- el orden de los `catch` del middleware;
- `esNoDisponible`;
- el 409 de `datosSalud`;
- los slugs del catálogo;
- las convenciones de las pruebas.

**Total: ~550.000 tokens y ~11 min.** Objetivo: **menos de 15.000 tokens** para estar al día.

### Alcance

**`.claude/skills/contexto-habittracker/SKILL.md`** (≤150 líneas):
- cuándo usarla: al iniciar una sesión y antes de planificar;
- reglas de ahorro: no leer el ESTADO completo ni el PDF, `Grep` con `path` acotado, y un Explore solo si la skill dice "no cubierto";
- tabla de ruteo "si necesitas X → abre Y";
- reglas de oro.

**`referencias/`:**
- `requisitos.md`: una línea por cada RF/RD del PDF.
- `backend.md`:
  - módulos y su cableado;
  - endpoints con su rol;
  - tabla excepción → HTTP → `codigo`, con el orden de los `catch`;
  - puertos y adaptadores, workers y configuración;
  - migraciones por contexto;
  - ayudantes de prueba: dobles y SQLite con `EnsureCreated`.
- `frontend.md`: features, contratos reales vs `CONTRATOS_PROPUESTOS`, guards, peculiaridades y flujo de agentes.
- `recetas.md`: endpoint nuevo, entidad + migración (`ValueGeneratedNever`, UTC), puerto con su adaptador, worker al estilo `BajaCuentasWorker`, feature conectada con su mock, verificación y PR.
- **`producto.md` (≤80 líneas):** P1–P8, lo prohibido en motivación y la guía de contenido local.

**Scripts:**
- `scripts/contexto/generar-inventario.sh` genera `referencias/inventario.generado.md` con:
  - controllers y rutas;
  - migraciones por contexto;
  - proyectos de prueba con su número de `[Fact]`/`[Theory]`;
  - features del frontend y `CONTRATOS_PROPUESTOS`;
  - **`catalogo-planes.json` y `mensajes-motivacion.json` con su número de planes y de mensajes**, o "aún no existe" antes de las fases 3 y 4.
- `scripts/ci/verificar-contexto.sh`, en el job de reglas de `ci.yml`:
  - falla si la skill cita rutas inexistentes;
  - falla si el inventario quedó viejo (lo regenera y compara).

**Documentación y memoria:**
- `docs/ESTADO-DEL-PROYECTO.md` queda en ≤120 líneas de estado actual. La historia pasa a `docs/historial/` (`control-acceso-v2.md`, `practica-4.md`, `frontend.md`) sin perder nada.
- `AGENTS.md`:
  - "al iniciar, usar la skill `contexto-habittracker`";
  - se corrige el dato del cableado: cada módulo tiene su `*ModuleExtensions` o su `DependencyInjection/`.
- Memoria del usuario: prioriza ahorrar tokens y la skill es el punto de entrada.

### Pruebas y terminado

- Mutación: `verificar-contexto.sh` en verde, y en rojo si se renombra un archivo citado.
- Sesión nueva de prueba: "resume el módulo X y dime dónde agregar un endpoint" con menos de 15.000 tokens.

---

## Fase 1 — Cimientos y deuda técnica

Sin cambios de alcance respecto al plan anterior, con un ajuste YAGNI: `Habito.PlanUsuarioId` y `Descripcion` **pasan a la Fase 4**, donde se usan. Así se aplica a todas las columnas la misma regla que la revisión pide para versión mínima y ancla.

**Alcance:**
- `IHabitoService`, y el controller depende de la interfaz (deuda 6.2).
- Excepciones con código según el patrón de `ControlAccesoException`:
  - base `HabitTrackerException(codigo, mensaje)`;
  - `RecursoNoEncontradoException` → 404 `habito_no_encontrado`;
  - el resto → 400.
  - Se traducen en `GlobalExceptionMiddleware`, **antes** de los `catch` de `InvalidOperationException` y `ArgumentException`.
  - Se elimina `EsNoEncontrado` (deuda 6.1).
- `RegistrarAvance` rechaza fechas futuras con `fecha_futura` (deuda 6.3).
- `ConfigureConventions`: todo `DateTime` de `HabitTrackerDbContext` vuelve con `Kind=Utc`, así la API devuelve `Z` (deuda 6.4).
- `GET /api/habits/{id}`.
- `Habito.MetaId` pasa a opcional (`Guid?`; si viene, debe existir) y el FK Hábito→Meta pasa de `Cascade` a `SetNull` (hoy borrar una Meta borra los hábitos).
- Las entidades nuevas usan `IEntityTypeConfiguration` + `ApplyConfigurationsFromAssembly`, con longitudes máximas y `HasPrecision`.

**Frontend (misma rama, en commits separados):** quitar el campo GUID, usar el detalle real y aceptar fechas con `Z`.

**Migraciones:** `HabitoMetaOpcional` (FK anulable con `SetNull`) y `LongitudesYPrecision`. El nombre ≤200 y la unidad ≤50 ya se validan en la API, así que los datos existentes caben.

**Pruebas:**
- Nuevo `tests/HabitTracker.Api.Tests` con las pruebas del controller: 404 con cuerpo y `codigo`, y 400 `fecha_futura`. `PlanDelDiaTests` se mueve ahí desde `DocumentManager.Api.Tests`.
- Se ajustan las pruebas de la P3 que exigían `MetaId`.
- La máquina del hábito queda intacta: sus 43 pruebas de dominio siguen en verde.

**Terminado:** sin cambios visibles salvo las correcciones; `dotnet test` en verde.

---

## Fase 2 — Perfil mínimo, salud justo a tiempo y progreso corporal

### Dominio

**`PerfilUsuario`** (clave `UsuarioId`), en dos niveles:
- **Nivel 1, obligatorio:** `perfilLaboral` (Oficina diurna, Turno nocturno, Turnos rotativos, Remoto, Estudiante, Trabajo físico o de pie, Sin empleo/jubilado), `zonaHoraria` IANA (validada con `TimeZoneInfo`; el navegador la detecta con `Intl` y la persona solo la confirma) y `avisoSaludAceptado`.
- **Opcional:**
  - `horaEntradaTrabajo?` y `horaSalidaTrabajo?`, para sugerir anclas y horas, sobre todo en turno nocturno;
  - `horaDespertar?` y `horaDormir?`;
  - edad (16–100), sexo, altura, peso, actividad, días y minutos (límites de `LIMITES_CORPORALES`).
- El mínimo de 16 años lo cubren los términos aceptados en el registro, así que un Básico no exige la edad.

**`CuestionarioSalud`**, por **bloques**, cada uno con su `RespondidoEn`. Solo listas cerradas, sin texto libre:
- **(a) Objetivos (hasta 3), experiencia y lugar:**
  - objetivos: perder peso, ganar masa, definir o tonificar por zona, reducir estrés, concentración, dormir mejor, hidratarme, energía, postura y movilidad;
  - experiencia: ninguna, principiante o intermedio;
  - lugar: casa sin equipo, casa con equipo básico o gimnasio.
- **(b) Revisión de seguridad:** 7 preguntas sí/no inspiradas en el PAR-Q+, con redacción propia.
- **(c) Condiciones y alergias:**
  - condiciones: hipertensión, diabetes, asma, enfermedad cardíaca, **enfermedad renal**, rodilla, espalda baja, hombro, osteoporosis, embarazo, posparto (<6 meses), historial de trastorno de la conducta alimentaria o ninguna;
  - alergias: lactosa, gluten/celiaquía, maní, frutos secos, mariscos, pescado, huevo, soya o ninguna;
  - "Ninguna" excluye a las demás opciones.

**`EvaluadorSalud`** (puro, la única definición de estas reglas):
- devuelve la `EvaluacionSalud`: precaución Normal, Precaución o ConsultarMédico; restricciones (sin intensidad alta, sin impacto, sin restricción calórica, zonas a evitar); y alérgenos;
- devuelve también la **completitud por bloque** y la operación `BloquesFaltantesPara(plan)`:
  - el bloque de seguridad, siempre que el plan requiera cuestionario;
  - el de alergias, si el plan tiene alérgenos;
  - el de condiciones, si tiene contraindicaciones.

**`RegistroPeso`** (uno por día UTC; alimenta `PerfilUsuario.PesoKg`) y **`MedicionCorporal`**.

### Application, Infrastructure y Host

**Puerto `IConsentimientoSalud`** (adaptador del Host → `IConsentimientoService` de Control de Acceso). El **409 `consentimiento_salud_requerido`** aplica solo:
- al guardar el cuestionario, el peso o las medidas;
- al guardar el perfil **cuando trae peso o altura**, porque también son datos de salud (ver Riesgos, punto 4);
- al inscribirse en un plan con `RequiereCuestionario` (Fase 4).

Guardar el nivel 1 o inscribirse en un Básico no lo pide.

**Puerto `ICifradorDatos`** (Data Protection, con un propósito por tipo de dato):
- las claves van en `DATAPROTECTION_KEYS_PATH`, con el volumen con nombre `dataprotection-claves` en Docker;
- sin esa ruta, la app no arranca fuera de Development;
- cada bloque del cuestionario se guarda como JSON cifrado en su columna.

**Otros:**
- Servicios `IPerfilService`, `ICuestionarioSaludService` e `IProgresoCorporalService`.
- `ParticipanteHabitos` exporta y borra el perfil, el cuestionario (descifrado en la exportación), el peso y las medidas.

### Endpoints (rol Estandar, identidad desde el token)

- `GET/PUT /api/profile`: los campos del nivel 2 son opcionales. Esto cambia el contrato propuesto del frontend.
- `GET /api/profile/health` → bloques, evaluación y completitud.
- `PUT /api/profile/health/{bloque}` con `objetivos`, `seguridad` o `condiciones`.
- `GET/POST /api/progress/weights` y `GET/POST /api/progress/measurements`.

**Migración:** `AgregarPerfilYSalud` (`PerfilesUsuario`, `CuestionariosSalud`, `RegistrosPeso` y `MedicionesCorporales`).

### Frontend (b)

- **Onboarding de 3 toques:** perfil laboral → confirmar zona → aviso de salud. Al terminar se ve "Mi día de trabajo saludable" (con el mock hasta la Fase 3).
- El paso **"Tu salud"** aparece solo al activar el nivel 2. Explica por qué se pregunta cada bloque y cómo se protege.
- Configuración › **Perfil y salud** (rectificación) y Progreso conectado.
- Estos contratos salen de `CONTRATOS_PROPUESTOS`.
- `metaSlug` se sigue aceptando como "interés" hasta que la Fase 4b lo reemplace por los planes reales.

### Pruebas

- **Requeridas:**
  - el nivel 1 no exige edad, peso ni cuestionario;
  - inscribirse en un Básico no devuelve 409 (se prueba en la Fase 4 con el servicio, y aquí la regla del puerto);
  - los bloques se guardan por separado;
  - `BloquesFaltantesPara` exige solo los bloques que el plan necesita.
- **Cifrado:** ida y vuelta; el texto cifrado no contiene el claro; otra clave no descifra; la fila en SQLite no muestra datos.
- 409 sin consentimiento.
- Exportación y borrado con aislamiento entre personas.

**Terminado:** onboarding real en 3 toques contra la API local; la base de datos no guarda el cuestionario en claro.

---

## Fase 3 — Catálogo de lanzamiento y recomendador

### Dominio

**`PlanPlantilla`** (los campos del plan anterior) más:
- **`RequiereCuestionario`** (bool);
- `GuiaSemanal[]` con **`TipExtra?`**.

**`TareaPlantilla`** (los campos del plan anterior) más:
- **`VersionMinima`** (≤200, obligatoria);
- **`AnclaSugerida`**, de una lista cerrada `Ancla`: AlDespertar, AlLlegarAlTrabajo, AMediaManana, DespuesDelAlmuerzo, AMediaTarde, AlSalirDelTrabajo, AntesDeCenar o AntesDeDormir.

**`RecomendadorPlanes`:**
- **Sin cuestionario ni consentimiento:** ordena por `perfilLaboral`, y por edad si existe, y lo avisa. Los planes que no requieren cuestionario y encajan con la jornada salen primero.
- **Con cuestionario:** afina el orden y aplica `ReglasSeguridadPlanes`, que se mantienen e incluyen embarazo, trastorno de la conducta alimentaria, menor de 18, IMC, lesiones, ConsultarMédico y alérgenos. Además, los planes de hidratación muestran el aviso de consultar al médico si hay hipertensión, enfermedad cardíaca o renal.

**Variantes de "Mi día de trabajo saludable"** (un mapa cerrado en el dominio de `perfilLaboral` a variante):
- **Oficina** (Oficina diurna, Remoto, Estudiante, Sin empleo): agua, pausa activa cada 2 h, y vista y postura.
- **Turno nocturno** (Turno nocturno, Turnos rotativos): agua, pausa activa y rutina de sueño.
- **Trabajo físico** (Trabajo físico o de pie): agua con calor, estiramiento y descanso.

### Contenido de lanzamiento (13 planes, `catalogo-planes.json` embebido)

| Nivel | Planes |
|---|---|
| Regular (slugs de `catalogo.ts`) | `perder-grasa-sostenible`, `reducir-estres` (21 días), `dormir-mejor`, `primeros-5-km`, `ganar-masa-muscular` y `fuerza-en-casa` |
| Básico | `mi-dia-oficina`, `mi-dia-turno-nocturno`, `mi-dia-trabajo-fisico`, `hidratacion-con-calor`, `caminar-para-empezar`, `respiracion-anti-estres` y `desconexion-digital-nocturna` |

- Cada tarea lleva su versión mínima y su ancla.
- Se aplica la guía de contenido local (ver la sección de contenido).
- `SembradorCatalogoPlanes` hace un upsert por `Slug` + `VersionContenido` al arrancar, y nunca borra: un plan retirado queda con `Activo=false`.

### Endpoints

Se mantienen los del plan anterior:
- `GET /api/plans`: anónimo, con filtros, `Cache-Control` público de 5 min y límite por IP;
- `GET /api/plans/{slug}`;
- `GET /api/plans/recommended` (Estandar): recomendados con motivos, más los no aptos con su razón.

**Migración:** `AgregarCatalogoPlanes` (`PlanesPlantilla`, `TareasPlantilla`). Las listas de valores (perfiles, contraindicaciones, alérgenos y guía) van como JSON (`OwnsMany(...).ToJson()`).

### Frontend (b)

- La pantalla inicial muestra **"Tu día de trabajo saludable"** como acción principal.
- La landing y `/metas` consumen la API; `catalogo.ts` se reemplaza y el mock MSW replica el contrato.
- Página Planes con filtros, "Recomendados para ti" y detalle con avisos de seguridad.
- Las áreas del frontend se adaptan a la lista nueva.

### Pruebas

**Prueba de contenido** (carga el JSON y lo valida con el dominio):
- slugs únicos y rangos válidos;
- `RequiereCuestionario=true` si el área es ControlDePeso o Gimnasio, si la intensidad es Media o Alta, si hay contraindicaciones o alérgenos, **o si el plan restringe la edad (>16 o <100) o el sexo**, porque esos datos solo se piden en el nivel 2;
- un Básico nunca es de ControlDePeso ni de intensidad Alta;
- cada tarea tiene `VersionMinima` y un ancla de la lista;
- hidratación ≤5 L o ≤20 vasos al día, con el aviso médico;
- ninguna palabra prohibida (ver la sección de motivación).

**Recomendador:**
- cada perfil laboral tiene su variante de "Mi día";
- sin cuestionario, "Mi día" es el primer resultado;
- se aplican las exclusiones de seguridad.

**Terminado:** la landing muestra los 13 planes desde la API y un perfil nuevo ve "Mi día" primero.

---

## Bloque transversal — Motivación y recompensas (se construye en las Fases 4 y 5)

**Mensajes:**
- `mensajes-motivacion.json`, embebido en Infrastructure, con versión y las categorías inicio, constancia, recaida, hito, cierre y regreso.
- Español dominicano neutro, tuteo y ≤160 caracteres por mensaje.

**`SelectorMensajesMotivacion`** (dominio puro, Fase 4):
- recibe el resultado de `CalculadoraProgreso` y la hora del día;
- es determinista: índice = (hash **estable** del usuario + número de día local) mod n, dentro de la categoría. El hash sale de los bytes del `Guid` y no de `string.GetHashCode`, que cambia en cada proceso. Con n ≥ 2 no se repite en días seguidos;
- elige el tono según el momento: inicio, racha, vuelta tras fallar, mitad del plan o cierre. Con 2 o más días sin registro el mensaje es de **regreso**, nunca de reproche;
- máximo 1 mensaje al día en la app y 1 dentro del resumen diario por correo, nunca un correo aparte;
- se controla con la preferencia `MensajesMotivacion` (Fase 5).

**Recompensas** (simbólicas y por conducta):
- `LogroUsuario` con claves cerradas: primer hábito, primera semana, 7/21/30 días de constancia (racha tolerante), regreso, mitad del plan y plan completado, más los 5 logros actuales del frontend.
- **Celebración** al cerrar una semana con ≥5 de 7 días cumplidos (completos o mínimos). Desbloquea el `TipExtra`.
- "Tu mes en números": días cumplidos, hábitos mantenidos y regresos.
- Cada logro se felicita una sola vez, en la app y en el siguiente resumen.

**Prohibido**, con prueba que lo verifique:
- recompensas o mensajes ligados a peso, medidas, IMC, calorías o apariencia;
- rankings por peso, cuerpo o calorías, puntos competitivos, premios externos o en dinero, y cualquier comparación fuera del Muro de logros voluntario de la Fase 10;
- rachas con castigo y textos de culpa.

---

## Fase 4 — Mis planes, check-in diario y adherencia

### Dominio

**`PlanUsuario`:**
- campos: `UsuarioId`, `PlanPlantillaId?`, `Origen`, `Nombre`, `Semanas`, `FechaInicio` y `FechaFinPrevista`;
- estado: `Estado`, **`MotivoPausa?`** (Lesion, Viaje, Enfermedad, Trabajo u Otro) y **`ModoSuaveHasta?`**;
- **`Extensiones`** (máximo 2), `HoraRecordatorio`, `RecordatoriosActivos`;
- copia de las tareas en sus hábitos;
- hijo **`PeriodoPausa`** (inicio, fin y motivo; `ValueGeneratedNever`), que la racha necesita para no contar los días de pausa.

**`TransicionesPlanUsuario`:** Activo ⇄ Pausado; Completado (solo desde Activo al llegar a `FechaFinPrevista`) y Cancelado como terminales; prohibidas Pausado→Completado y salir de un terminal. **Extender no es una transición.**

**Inscribirse:**
- crea los `Habito` con `PlanUsuarioId`, `Descripcion` (instrucciones), `VersionMinima` y `Ancla` copiadas;
- los activa con `Habito.Activar`;
- valida `ReglasSeguridadPlanes` y `BloquesFaltantesPara`;
- exige el consentimiento solo si `RequiereCuestionario`;
- límite de 3 planes activos sin repetir plantilla;
- se mantiene la propagación al pausar, reanudar, completar o cancelar, con `conservarHabitos`.

**`ResolvedorAncla`** (puro): ancla + jornada (entrada, salida, despertar, dormir y `perfilLaboral`) → hora local sugerida. El turno nocturno tiene sus propios valores por defecto. Lo usan la inscripción y, en la Fase 5, la programación de los correos.

**`CalculadoraProgreso`** (la única definición):
- un "día cumplido" es un día local con al menos un registro en los hábitos del plan; "completo" si se cumplieron todas las tareas diarias;
- en modo suave, la versión mínima cuenta como completa;
- **un día sin registro no corta la racha y dos seguidos sí**; los días de pausa justificada no cuentan ni la cortan;
- devuelve el cumplimiento de 7 y 30 días (completo y mínimo por separado), la racha, el récord, la semana X de N y los días restantes.

**Otras piezas puras:**
- **`SugeridorAjuste`**: si hubo 4 o más días sin registro en los últimos 7, sugiere ModoSuave (7 días), Extender (+1 semana, máximo 2) o pausar con motivo. **Nunca se aplica solo.**
- **`CalculadoraLogros`**: su firma solo recibe el progreso y los registros de hábitos.
- **`SelectorMensajesMotivacion`**.
- Aviso "¿retomamos?" a los 14 días de pausa: aquí queda marcado como pendiente y la Fase 5 lo envía.

### Application y Host

- `IPlanUsuarioService` e `IHoyService`.
- **"Hoy" es un endpoint nuevo, `GET /api/today`, y no una extensión de `plan-del-dia`** (ver Riesgos, punto 1). Devuelve:
  - las tareas del día con su ancla y su estado (hecho, mínimo o pendiente);
  - el mensaje del día y el `TipExtra` si corresponde;
  - la sugerencia de ajuste;
  - un consejo del clima opcional: reutiliza `IServicioClima`/`RecomendadorClima` si hay ciudad y la API responde, y si falla no rompe nada.
- `plan-del-dia` y sus pruebas no cambian.

### Endpoints (Estandar)

- `POST /api/my-plans` `{slug, ancla?, hora?}` → 201.
- `GET /api/my-plans` y `GET /api/my-plans/{id}`.
- `POST /api/my-plans/{id}/transition` `{nuevoEstado, motivoPausa?, conservarHabitos?}`.
- `POST /api/my-plans/{id}/adjust` `{tipo: ModoSuave|Extender}`.
- `PUT /api/my-plans/{id}/reminder`.
- `GET /api/today`.
- `POST /api/habits/{id}/progress` gana `esVersionMinima`.
- `GET /api/progress/summary`: racha, récords, logros, "tu mes en números" y el mensaje del día.

**Migración:** `AgregarPlanesUsuario`:
- tablas `PlanesUsuario`, `PeriodosPausa` y `LogrosUsuario` (único por usuario + clave);
- columnas en `Habitos`: `PlanUsuarioId?` (FK `SetNull`), `Descripcion?`, `VersionMinima?` y `Ancla?`;
- columna en `RegistrosDiarios`: **`EsVersionMinima`** (falso por defecto).

### Frontend (b)

- "Empezar este plan" con la pregunta "¿cuándo lo harás?": anclas y hora sugerida.
- Hoy con **"Hecho"** y **"Hoy solo lo mínimo"**.
- Mis planes: avance, pausar con motivo, ajustar, cancelar y completar con confirmación.
- Celebración semanal.
- Progreso con los logros del servidor y "tu mes en números".
- Sin rojo ni textos de castigo.
- `metaSlug` sale del contrato del perfil.

### Pruebas

- **`CalculadoraProgreso`:** un día fallado no corta la racha; dos seguidos sí; la pausa no corta; el mínimo cuenta; modo suave.
- **`SugeridorAjuste`:** umbral de 4 días; máximo 2 extensiones; nunca se aplica solo.
- **`CalculadoraLogros`:** prueba por reflexión de que ningún parámetro es ni contiene `RegistroPeso` o `MedicionCorporal`.
- **`SelectorMensajesMotivacion`:** determinista, sin repetir en días seguidos y con la categoría correcta.
- **Contenido de los mensajes:** sin peso, kg, calorías, dieta, gordo, perdiste, fallaste ni culpa, comparando sin acentos ni mayúsculas, y con ≤160 caracteres.
- **Máquina del plan:** transiciones válidas y prohibidas; un terminal sin salida.
- **Inscripción:** `plan_no_apto`, los bloques faltantes, Básico sin 409 y el límite de 3.
- **Mutaciones:** racha tolerante, tope de 2 extensiones, exclusión del peso en los logros y determinismo del selector.

**Terminado:** de punta a punta en local: elegir "Mi día", marcar "Hecho" y "mínimo", ver la racha y el logro, sin textos de culpa.

---

## Fase 5 — Correo como canal de acción

Cubre RF-NEG-07 y RF-NOT-03 por el canal de correo. La bandeja interna (RF-NOT-06) llegará con el módulo Notifications sin tocar HabitTracker.

### Se mantiene del plan anterior

- **`PreferenciasNotificacion`:**
  - opt-in explícito, con `ConsentidoEn` y `RevocadoEn`;
  - resumen diario y semanal, horario silencioso y tope de 10 correos al día;
  - hash del token de baja;
  - nuevas: **`MensajesMotivacion`** y **`UltimaActividadEn`**.
- **`ProgramacionEnvio`** con los tipos **RecordatorioPlan, ResumenDiario, ResumenSemanal y AvisoRetomar**. RecordatorioNota se añade en la Fase 9.
- `CalculadoraProximoEnvio` como el único punto que pasa de la hora local a UTC.
- `ServicioRecordatorios` con el **reclamo atómico**.
- `ConstructorMensajes`, que **nunca** incluye datos de salud.
- Puertos `INotificadorUsuario` e `IDirectorioUsuarios`, cada uno con su adaptador del Host.
- `RecordatoriosWorker`, al estilo `BajaCuentasWorker`, con `OpcionesRecordatorios.Validar()` y el paquete `Hosting.Abstractions`.
- **Ajustes en la cola de Control de Acceso:**
  - `IEmailQueue.EncolarCorreoAsync` genérico con una plantilla sin la caja del código;
  - índice por `Estado`;
  - recuperar los correos que se quedaron en `Procesando` pasados N minutos.
- **Baja en un clic:** un GET sin efectos (CSP, `no-store`) y un POST.
- `GET/PUT /api/notifications/preferences` y `POST .../test` (3 por hora).

### Cambios

1. **Check-in en un clic.** Cada tarea del día con cantidad 1 lleva el botón "Ya lo hice"; las demás abren la app.
   - `GET /api/notifications/checkin?token=` muestra una página fija, sin efectos, con CSP y `no-store`. `POST` la ejecuta.
   - Puerto `IFirmadorEnlaces` (Application), implementado con `ITimeLimitedDataProtector` con propósito propio sobre las mismas claves de Data Protection. El token lleva usuario, hábito y fecha local, caduca a las **36 h** y no abre sesión.
   - `ServicioCheckinCorreo` valida el token y comprueba que la cuenta, el plan y el hábito estén activos y que la fecha no sea futura. Después llama a **`IHabitoService.RegistrarAvanceAsync`**: nada de lógica en el controller.
   - La fecha del avance es el mínimo entre ahora y el mediodía local de la fecha del token, pasado a UTC.
   - **Idempotencia:** `RegistroDiario` gana `Origen` (App o Correo) y `ClaveIdempotencia?`, con un índice único filtrado (`HabitoId`, `ClaveIdempotencia`) y la clave `correo:{fechaLocal}`. Un segundo POST no duplica el avance. `Origen` también alimenta la métrica "check-in por correo" (ver Riesgos, punto 6).
   - Límite de peticiones por IP. Un token inválido o caducado responde 400 `enlace_no_valido`, con un mensaje genérico.
2. **`CalculadoraCadencia`** (pura). Mide los días sin actividad con `UltimaActividadEn`, que se actualiza como mucho una vez al día al abrir Hoy o al registrar un avance, sin píxeles de seguimiento.
   - A los 3 días, el resumen diario sale con el mensaje de regreso.
   - A los 7, el resumen pasa a semanal.
   - A los 21, se envía un único "¿seguimos?" (tipo AvisoRetomar) y se detienen los diarios hasta que la persona vuelva.
   - Los umbrales viven en `OpcionesRecordatorios`. **Solo reduce:** nunca reactiva ni cambia el consentimiento.
3. **Motivación en el resumen diario:** un mensaje del selector y los logros nuevos, según `MensajesMotivacion`.
4. **Hora y ancla:** cada tarea se programa con `ResolvedorAncla` + `CalculadoraProximoEnvio`; sin ancla, con la hora de la inscripción. **Las tareas de un plan que caen en la misma franja de ±30 min van en un solo correo** (P5), siempre dentro del tope diario.
5. **"¿Retomamos?"** a los 14 días de una pausa justificada: un solo envío de tipo AvisoRetomar.
6. **WhatsApp** va al backlog. `INotificadorUsuario` queda listo para añadir canales.

### Migraciones

- `AgregarPreferenciasYProgramacion` (HabitTracker): `PreferenciasNotificacion` y `ProgramacionesEnvio` con índice `(Activa, ProximoEnvioUtc)`; `RegistrosDiarios.Origen` y `ClaveIdempotencia`.
- `IndiceCorreosPorEstado` (Control de Acceso).

### Frontend (b)

- Configuración › **Notificaciones**: aceptar o cancelar, horas, horario silencioso, zona, tipos y mensajes motivacionales.
- Interruptor por plan.
- **La invitación a activar los correos aparece al empezar el primer plan**, junto a la hora, con la casilla desmarcada. No va en el onboarding.
- Páginas de check-in y de baja.

### Pruebas

- **Token de check-in:** caduca a las 36 h; no sirve para otro usuario ni otro hábito; el GET no tiene efectos; el POST es idempotente.
- **`CalculadoraCadencia`:** 3, 7 y 21 días; nunca reactiva.
- **`CalculadoraProximoEnvio`:** `America/Santo_Domingo` y `America/New_York`, con su cambio de hora.
- Agrupación por franja y tope diario.
- Los correos no incluyen datos de salud.
- **Carrera del reclamo atómico** con SQLite.
- Recuperación de los correos que quedaron en `Procesando` sin duplicar un envío (RF-NOT-12).
- **Mutación:** la caducidad del token.

**Terminado:** con `SMTP_HOST=` y `CORREO_MODO=consola` se ve el correo con el botón, el check-in registra una sola vez y la baja corta los envíos.

**Producción:** para el piloto basta con el SMTP de Gmail (~500 correos al día). Para el lanzamiento hace falta un proveedor transaccional, y solo cambian las variables `SMTP_*`.

---

## Fase 6 — Producción del corte vertical, métricas y piloto

### Lista de producción (sobre lo construido en las Fases 1–5)

- **Cifrado en el contenedor:** un reinicio conserva las claves (RD-09), la columna en SQL Server no muestra datos en claro y queda documentado el respaldo del volumen de claves.
- `IAuditoriaService` en el cuestionario, en aceptar o cancelar los correos, en la baja por enlace y en las métricas.
- Límites de peticiones en todos los endpoints nuevos, incluido el check-in.
- Índices y consultas sin N+1.
- Healthcheck de `webapi` con el latido del worker.
- La imagen Docker con tzdata e `InvariantGlobalization=false`.
- Revisión de seguridad: `claude-security`, `ciberseguridad-pentester` (OWASP y ASVS) y mutación de las reglas clave.
- Documentación: README por pieza con la tabla funcionalidad → RF, `docs/maquina-de-estados.md` (con la máquina del plan), ESTADO y skill.

### E2E en Edge real (puppeteer-core)

1. Registro → onboarding de 3 toques (se cuentan) → "Mi día" → activar los correos → marcar → correo en `CorreosEnCola` → check-in en un clic → baja en un clic → descarga y borrado de la cuenta.
2. Cuestionario → plan Regular recomendado → plan no apto bloqueado con 400 `plan_no_apto`.

### Métricas de producto

**`IMetricasProductoService`** consulta de forma agregada `PlanUsuario`, `RegistroDiario` y `PreferenciasNotificacion`:
- no lee `CuestionarioSalud`, `RegistroPeso` ni `MedicionCorporal`;
- no devuelve identificadores;
- una cohorte de menos de 5 personas da **nulo**.

**Endpoint:** `GET /api/admin/metrics/adherence`, con el rol **`Administrador`** (el que existe en Control de Acceso), límite de peticiones y auditoría. Ver Riesgos, punto 5, sobre la ruta.

| Métrica | Definición | Umbral de partida |
|---|---|---|
| Primer hábito en 48 h | Inscritos con su primer avance dentro de las 48 h siguientes | ≥60 % |
| Retención día 7 | Inscritos con un avance entre los días 7 y 13 | ≥40 % |
| Retención día 30 | Inscritos con un avance entre los días 30 y 36 | ≥25 % |
| Planes completados | Planes con `FechaFinPrevista` vencida que terminan en Completado | Solo medir |
| Aceptación de correos | Personas con `CorreosActivos` después de su primer plan | Solo medir |
| Check-in por correo | Avances con `Origen=Correo` sobre el total | Solo medir |
| Regreso tras recaída | Personas con 3 o más días sin registro que vuelven en los 7 días siguientes | Solo medir |

Los umbrales son hipótesis: el informe del piloto los ajusta o los descarta.

**Frontend (en la misma rama):** la pantalla Resumen del Administrador muestra estas métricas.

### Piloto (hito de decisión)

- 10 a 20 personas (oficina, remoto, turno nocturno y trabajo físico) durante 3 a 4 semanas, con **consentimiento informado**.
- Entrevistas de 15 min en las semanas 2 y 4: qué las hizo volver, qué las hizo dejarlo y qué les sobró. Además se pregunta si un top voluntario de constancia (Fase 10) las motivaría o las presionaría.
- Entregable `docs/piloto.md`: guía de entrevistas, plantilla de resultados, formulario de consentimiento informado e informe de métricas y hallazgos con **la decisión sobre las Fases 7–10**.
- Requiere un entorno desplegado y accesible: ver Riesgos, punto 7.

### Pruebas y terminado

- Pruebas: las métricas no leen tablas de salud (por reflexión sobre las dependencias o los `DbSet` usados), no devuelven IDs y una cohorte menor de 5 da nulo.
- Mutación: la cohorte mínima.
- Terminado: los E2E 1 y 2 en verde, la producción desplegada y `docs/piloto.md` con el informe.

---

## Fase 7 ⚑ — Catálogo ampliado (hasta ~35 planes)

**Condicionada al piloto.** Solo agrega contenido: mismo modelo, mismo sembrador, misma prueba de contenido y mismas reglas de seguridad. Cada plan lleva versión mínima por tarea, ancla y contenido local, y se prioriza lo que pida el piloto.

**Planes nuevos:**
- **Básicos:** pausas activas en la oficina, 20-20-20, postura en el escritorio, dormir a tiempo, rutina de sueño para turno nocturno, meditación de 10 min, diario de gratitud, pomodoro, menos azúcar, desayuno saludable y estiramientos matutinos. Los tres primeros salen de "Mi día" como planes sueltos.
- **Regulares de peso y energía:** perder peso para oficina, control de peso en turno nocturno y perder peso después de los 50.
- **Regulares de mente y descanso:** estrés de 8 semanas, concentración profunda, estudiantes y reajuste del sueño en turnos rotativos.
- **Gimnasio** (todos con `RequiereCuestionario`): aumentar de peso saludable, full body para principiantes, glúteos y piernas (mujer), tren superior (hombre), abdomen y core, brazos y hombros, espalda y postura, recomposición corporal (mujer y hombre), y fuerza y equilibrio desde los 60.

**Migración:** ninguna (solo JSON).

**Pruebas:** la prueba de contenido existente, con más planes. Se amplía el E2E con un plan de gimnasio que exige el cuestionario.

**Terminado:**
- cada plan nuevo tiene la lectura de una persona del perfil laboral correspondiente, registrada en `docs/piloto.md` o en `docs/contenido/revisiones.md`;
- límites, índices y auditoría sin cambios, porque no hay endpoints nuevos.

## Fase 8 ⚑ — Planes personalizados con sugerencias

**Condicionada al piloto.** Se mantiene completa la antigua Fase 5:
- `POST /api/my-plans/custom`;
- `ValidadorPlanPersonal` con topes por unidad;
- `GET /api/plans/suggestions`;
- edición de las tareas del plan propio;
- límite de 10 planes propios;
- asistente de 3 pasos.

**Cambios:**
- cada tarea acepta `versionMinima?` y `ancla?`, y el asistente los pide como pasos opcionales;
- **empieza por "desde una plantilla y editarla"**, que es el camino con menos abandono; el plan desde cero llega después, dentro de la misma fase.

**Migración:** ninguna prevista: reutiliza `PlanUsuario` con origen Personal y las columnas del hábito de la Fase 4.

**Pruebas:**
- `ValidadorPlanPersonal` (topes y nombres);
- las sugerencias solo incluyen planes aptos;
- copiar una plantilla preserva la versión mínima y el ancla;
- mutación de los topes por unidad.

**Terminado:** límites de peticiones, auditoría de la creación y ampliación del E2E (crear desde una plantilla).

## Fase 9 ⚑ — Notas, apuntes y recordatorios

**Condicionada al piloto.** No resuelve el problema central y añade riesgo de datos sensibles: si el piloto no la pide, pasa al backlog.

**Se mantiene completa la antigua Fase 7:**
- `Nota` con el contenido cifrado por `ICifradorDatos`, el título en claro y la búsqueda en memoria (máximo 500);
- `DetectorDatosSensibles` → 400 `dato_sensible_detectado`;
- `Recordatorio` sobre `ProgramacionEnvio`, con el tipo nuevo RecordatorioNota; el correo solo lleva el título;
- límites, exportación y borrado de la cuenta;
- el aviso permanente de no guardar datos sensibles.

**Migración:** `AgregarNotasYRecordatorios`.

**Pruebas:**
- el detector: Luhn, contraseñas y cédula;
- el contenido cifrado;
- los recordatorios por zona horaria;
- mutación del detector.

**Terminado:** límites, auditoría, índices y ampliación del E2E (nota con una tarjeta rechazada).

## Fase 10 ⚑ — Muro de logros (top voluntario de constancia)

**Condicionada al piloto** (se pregunta en las entrevistas). Si el usuario lo prefiere, se puede adelantar justo después de la Fase 5 para probarlo dentro del piloto, porque solo depende de la Fase 4. Si se adelanta, conviene medir el regreso tras una recaída de quienes participan frente a quienes no.

**Por qué así:** un top motiva a quien va arriba y desanima a quien va abajo. Además, si muestra el plan que sigue cada persona, revela datos de salud (Ley 172-13). Se diseña para que cuente la **constancia**, nunca el cuerpo (P4), y para que nadie aparezca sin quererlo.

### Dominio

- **`ParticipacionMuro`** (clave `UsuarioId`): `Participa` (falso por defecto), `Alias`, `ActivadoEn` y `RetiradoEn`.
- **`ValidadorAlias`** (puro):
  - 3 a 20 caracteres, letras, números, espacios y guiones;
  - no puede contener el correo ni el nombre de la persona;
  - una lista cerrada de palabras ofensivas o de salud (por ejemplo «gordo» o «diabetes») lo rechaza con 400 `alias_no_valido`.
- **`CalculadoraMuro`** (pura) recibe solo los resúmenes de `CalculadoraProgreso` y `LogroUsuario` de quienes participan. Su firma no acepta ningún tipo de salud, plan o cuerpo, y lo comprueba una prueba por reflexión. Devuelve dos tops del **mes en curso**:
  - «Más días de constancia» (racha tolerante, completos y mínimos);
  - «Planes completados».
- **Reglas de la clasificación:**
  - **solo un top 10**, sin la posición de quien no entra;
  - empate por orden de llegada;
  - se reinicia cada mes, así quien empieza tarde tiene opción;
  - con **menos de 5 participantes** no se muestra el muro (misma regla de cohorte que P6).

### Privacidad y seguridad

- **Para participar** hacen falta las dos cosas: el consentimiento `compartirCumplimiento` (ya existe en Control de Acceso, por un puerto como el de la Fase 2) y la casilla «Quiero aparecer en el muro».
- **Al salir del muro o retirar el consentimiento,** la persona desaparece al instante. La baja de cuenta borra su participación (`ParticipanteHabitos`).
- **El muro solo muestra** alias, cifra e insignia. Nunca nombre, correo, foto, plan, área, peso ni medidas.
- **Moderación:** el Administrador puede ocultar un alias (`POST /api/admin/wall/{id}/hide`, auditado). Un alias oculto vuelve a pedir uno nuevo.
- **Anti-trampas:**
  - solo cuentan los avances de días pasados o del día actual (`fecha_futura` ya los bloquea);
  - el check-in por correo ya es idempotente;
  - hay un tope de un día cumplido por día.

### Endpoints (Estandar)

- `GET /api/community/wall`: los dos tops, cacheados 10 min; responde vacío con `motivo: "pocos_participantes"` si hay menos de 5.
- `GET/PUT /api/community/participation` `{participa, alias}`.
- `GET /api/community/wall/me`: «tu mes en números» y si estás en el top. Nunca dice en qué posición estás si no entras.

**Migración:** `AgregarMuroLogros` (tabla `ParticipacionesMuro`, con índice único por `Alias`).

### Frontend (b)

- Pantalla **Muro de logros**: dos tarjetas de top 10 con alias e insignias, y un texto positivo («Cada día cuenta, también la versión mínima»).
- Sin rojo ni flechas de bajada. Sin la posición de quien no está en el top.
- Configuración › Privacidad: casilla para aparecer y alias, explicando qué se muestra y qué no.
- Invitación suave al cumplir el primer logro de 7 días, nunca en el onboarding.

### Pruebas

- **Privacidad:** quien no participa o retiró el consentimiento nunca aparece. La respuesta no trae ningún campo de salud, plan ni identificador. Una cohorte de menos de 5 deja el muro vacío.
- **Por reflexión:** `CalculadoraMuro` no recibe tipos de salud.
- **Alias:** inválidos, repetidos o que contienen el correo.
- **Clasificación:** solo 10 posiciones, empates, reinicio mensual y moderación auditada.
- **Mutaciones:** mostrar a quien no participa, quitar la cohorte mínima e incluir el nombre del plan.

**Terminado:** límites de peticiones, auditoría de la moderación, índices, ampliación del E2E (activar el muro y verlo con 5 personas) y un texto revisado contra la lista de lo prohibido.

---

## Contenido local y guía de estilo (Fases 3, 4, 5 y 7; se copia a `referencias/producto.md`)

- **Tono:** español dominicano neutro, tuteo, frases cortas, unidades métricas. Sin moralizar sobre la comida ni hablar de "malos" alimentos.
- **Calor:** hidratación prudente (≤5 L o ≤20 vasos al día); caminar o correr temprano o por la tarde, sin el sol del mediodía; aviso médico por hipertensión, enfermedad cardíaca o renal.
- **Transporte:** bajarse una parada antes en la guagua o el carro público, usar las escaleras, estirarse en la espera, pausas para quien va en moto o conduce.
- **Comida local:** plátano, yuca, arroz, habichuelas, aguacate, mango, chinola y guineo. En "menos azúcar", refrescos y jugos azucarados sin culpar.
- **Jornadas reales:** call center, zona franca, turno nocturno, delivery en moto y trabajo de pie, con anclas y horas que tengan sentido para cada una.
- **Sin gimnasio ni gasto:** en casa sin equipo (una botella o un galón de agua como peso) y sin suplementos ni productos de pago.
- **Prudencia:** guías generales de salud pública (OMS, sobre actividad y sueño), aviso de que no sustituyen el consejo médico y sin metas calóricas por debajo de umbrales seguros.
- **Revisión humana** de cada plan de la Fase 7 por una persona del perfil correspondiente.

## Archivos clave

- **Dominio:** `Modules/HabitTracker/HabitTracker.Domain/Entities/Habito.cs`, `RegistroDiario.cs`, `TransicionesHabito.cs` y `Meta.cs`, y las carpetas nuevas `Perfil/`, `Salud/`, `Planes/`, `Progreso/`, `Motivacion/`, `Notificaciones/` y `Notas/`.
- **Application:** `Modules/HabitTracker/HabitTracker.Application/Services/HabitoService.cs` e `Interfaces/` (`IConsentimientoSalud`, `ICifradorDatos`, `IFirmadorEnlaces`, `INotificadorUsuario` e `IDirectorioUsuarios`).
- **Infrastructure:** `Modules/HabitTracker/HabitTracker.Infrastructure/Persistence/HabitTrackerDbContext.cs` y las carpetas nuevas `Configuraciones/`, `Contenido/catalogo-planes.json`, `Contenido/mensajes-motivacion.json`, `Seguridad/` (Data Protection) y `Workers/RecordatoriosWorker.cs`.
- **Host:**
  - `Host/WebApi/Extensions/HabitTrackerModuleExtensions.cs` (cableado);
  - `Host/WebApi/Program.cs` y `Host/WebApi/Adapters/`, con `PermisoServiceAdapter` y `ParticipanteHabitos` como modelo;
  - `Host/WebApi/Middlewares/GlobalExceptionMiddleware.cs` y `Host/WebApi/Extensions/InicializacionBaseDeDatos.cs` (siembra);
  - controllers nuevos: `ProfileController`, `ProgressController`, `PlansController`, `MyPlansController`, `TodayController`, `NotificationsController` y `AdminMetricsController` (más adelante, `NotesController` y `RemindersController`).
- **Control de Acceso (cambios mínimos):** `AccessControl.Application/Interfaces/IEmailQueue.cs`, `AccessControl.Infrastructure/Services/DatabaseEmailQueue.cs` y `Workers/EmailSenderWorker.cs` (recuperación de `Procesando`).
- **Frontend:** `frontend/src/features/{publico,perfil,progreso,habits}`, las features nuevas `planes`, `hoy`, `notificaciones` y `admin-metricas`, y `frontend/src/mocks/handlers/propuestas/`.
- **Skill:** `.claude/skills/contexto-habittracker/`, `scripts/contexto/` y `scripts/ci/verificar-contexto.sh`.

## Verificación (en cada fase)

1. `dotnet build HabitTrackerSystem.slnx` con 0 avisos y `dotnet test` en verde, con pruebas por capa: dominio puro, servicios con dobles y repositorios con SQLite.
2. Scripts del CI: `bash scripts/ci/verificar-reglas-rd.sh`, `verificar-commits.sh <base> HEAD`, `pruebas-verificar-commits.sh` y `verificar-contexto.sh`.
3. `cd frontend && npm test`, lint, tipos y build.
4. Prueba manual contra la API real (`dotnet run` + `VITE_API_URL=http://localhost:5246 npx vite --port 5174`) y en Docker, con `SMTP_HOST=` y `CORREO_MODO=consola`, revisando `CorreosEnCola`.
5. **Mutaciones obligatorias:** racha tolerante, tope de 2 extensiones, exclusión del peso en los logros, determinismo del selector, caducidad del token de check-in y cohorte mínima. Más las propias de cada fase.
6. Revisores:
   - `core-transversal-reviewer` antes de cada commit;
   - `frontend-reviewer` y `frontend-design-critic` en las PR b;
   - los textos nuevos se revisan contra la lista de lo prohibido.

## Riesgos y preguntas abiertas

1. **Choque: "Hoy extiende el plan del día existente".** `GET /api/habits/plan-del-dia` (P4, tag `practica-4`) depende de OpenWeatherMap y responde 503 si la API cae o falta la clave. Si Hoy colgara de él, el check-in diario fallaría con el clima.
   **Propuesta:** `GET /api/today` nuevo (`IHoyService`), que reutiliza el clima solo como consejo opcional; `plan-del-dia` y sus pruebas quedan intactos.
2. **Choque: el día UTC frente al día local.** `Habito.CumpleMetaFinal` usa el día UTC (regla documentada en `docs/maquina-de-estados.md`, de la P3). En República Dominicana (UTC−4), un registro después de las 20:00 cuenta para el día UTC siguiente.
   **Propuesta:** no tocar la regla de la P3 (solo afecta a la transición a Completado de un hábito) y calcular la adherencia, la racha y Hoy por el **día local** en `CalculadoraProgreso`, documentando la diferencia.
   **Decisión tuya:** mantenerlo así (recomendado) o migrar también `CumpleMetaFinal` al día local, lo que cambia el documento y las pruebas de la P3.
3. **Choque: la cantidad de la versión mínima.** `RegistroDiario` exige una cantidad mayor que 0 (invariante de la P3) y `VersionMinima` es texto.
   **Propuesta:** "Hoy solo lo mínimo" registra `Cantidad = 1` con `EsVersionMinima = true`; la constancia lo trata como un día cumplido, y en la regla de la P3 suma su cantidad real.
   **Alternativa:** añadir `CantidadMinima` numérica a `TareaPlantilla`.
4. **Ampliación:** peso y altura dentro de `PUT /api/profile` también son datos de salud, así que el 409 se aplica cuando el perfil los trae. Además, los campos del nivel 2 pasan a opcionales: es un cambio del contrato propuesto que el frontend adopta en la Fase 2b.
5. **Choque: `/api/admin/metrics`.** El frontend ya lo propuso con otra forma (`usuariosActivos30d`, `eliminacionesEnCurso`, `planesMasIniciados`…), y parte de esos datos son de Control de Acceso, que HabitTracker no puede leer (no se accede a tablas de otro módulo).
   **Propuesta:** `GET /api/admin/metrics/adherence` en HabitTracker. Los campos de Control de Acceso siguen "llegará pronto" hasta el módulo Reports, y la regla del frontend de "agrupar <5 como Otros" se unifica con el nulo para cohortes menores de 5.
6. **Añadido que no estaba en la revisión:** el check-in idempotente y la métrica "check-in por correo" necesitan `RegistroDiario.Origen` y `ClaveIdempotencia` (Fase 5). Además, la racha con pausa justificada necesita el historial `PeriodoPausa` (Fase 4), porque un solo `MotivoPausa` no basta para saber qué días excluir.
7. **Sin verificar / decisión tuya: dónde corre el piloto.** El CD sigue pendiente y Vercel no hospeda .NET de forma nativa. El piloto necesita la API, la base y el frontend accesibles desde fuera, con HTTPS, más el SMTP real (Gmail alcanza para 20 personas). Hay que elegir el hospedaje (por ejemplo, un VPS con `docker compose` o un servicio con contenedores) antes de la Fase 6. Esto choca con el orden acordado (CI → backend → front↔back → Docker → CD): el despliegue tendría que adelantarse al cierre de la Fase 6.
8. **Datos reales del piloto:** consentimiento informado conforme a la Ley 172-13 y borrado o anonimización al terminar, si así se acuerda con los participantes. La plantilla va en `docs/piloto.md`.
9. **Verificado en el código:**
   - la entidad de avance es **`RegistroDiario`**;
   - el rol administrativo es **`Administrador`**;
   - el cableado de HabitTracker está en `HabitTrackerModuleExtensions.cs`;
   - la cola solo tiene `EncolarCorreoRecuperacionAsync` y `EncolarCorreoActivacionAsync`.

   **Sin verificar:** si `HabitTracker.Infrastructure` ya tiene acceso a `Microsoft.AspNetCore.DataProtection(.Extensions)`. Si no, se añade la `FrameworkReference` o el paquete en la Fase 2.
10. **Fase 1 frente a la revisión:** se movieron `PlanUsuarioId` y `Descripcion` de la Fase 1 a la 4, por la misma regla YAGNI que la revisión aplica a la versión mínima y al ancla. Si prefieres dejarlos en la 1, no cambia nada más.

11. **Muro de logros (Fase 10):** va en contra de la regla original «sin rankings». Se incluye con las salvaguardas de su sección, pero si el piloto muestra que presiona o desanima, se descarta. Decisión tuya: dejarlo condicionado al piloto (recomendado) o adelantarlo tras la Fase 5 para probarlo en el piloto.

## Orden de recorte si hay que acortar el alcance

Nunca se recortan:
- `ReglasSeguridadPlanes` y el bloqueo `plan_no_apto`;
- consentimiento, cifrado y opt-in con baja en un clic;
- las pruebas y mutaciones de seguridad.

En orden, de lo primero a lo último que se recorta:

1. **Fase 10** (muro de logros) al backlog, si el piloto dice que presiona más de lo que motiva.
2. **Fase 9** (notas) al backlog.
3. **Fase 8** (planes propios); si se hace, solo "desde una plantilla".
4. **Fase 7** (catálogo ampliado), limitada a lo que pidió el piloto.
5. En la Fase 4: "tu mes en números", `TipExtra` con la celebración semanal y "Extender" (se queda el modo suave).
6. En la Fase 5:
   - el resumen semanal y el aviso "¿retomamos?" tras una pausa;
   - de la cadencia adaptativa queda solo el corte a los 21 días;
   - el check-in en un clic **no se recorta**, porque es el corazón de P5.
7. En la Fase 2: las medidas corporales (se quedan el peso y el cuestionario).
8. En la Fase 3: los Regulares bajan de 6 a 3 (perder grasa sostenible, reducir estrés y dormir mejor). Las 3 variantes de "Mi día" no se tocan.
9. En la Fase 6: el E2E 2 pasa a una prueba de API. El piloto se mantiene, aunque sea con 10 personas.
