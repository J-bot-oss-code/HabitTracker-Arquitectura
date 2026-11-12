# Historial — HabitTracker v2 (plan en `docs/plan-habittracker-v2.md`)

## Fase 0: skill de contexto y ESTADO compacto (PR #75, 07-11-2026)

- Skill `contexto-habittracker` (`.claude/skills/`) con requisitos, mapas del backend y del frontend, recetas, principios de producto e inventario generado.
- `scripts/ci/verificar-contexto.sh` en el CI.
- ESTADO de 273 a 104 líneas, con la historia movida a `docs/historial/`.
- Medido: la puesta al día pasa de unos 550.000 tokens a unos 5.000–10.000.
- Después (PR #78/#79): `main` y `develop` con rulesets separados y el validador de commits que omite los ya publicados en `main` (ver `docs/historial/ci.md`).

## Fase 1: cimientos y deuda técnica (`refactor/habittracker-cimientos`, 08-11-2026)

### Backend

- `IHabitoService` (RD-01). `HabitsController` y `ParticipanteHabitos` dependen de la interfaz.
- **Excepciones con código** (`HabitTracker.Domain/Excepciones/HabitTrackerException.cs`):
  - `RecursoNoEncontradoException` → 404 `habito_no_encontrado`;
  - `ReglaNegocioException` → 400 `fecha_futura` y `meta_no_encontrada`.
  - Se traducen en `GlobalExceptionMiddleware` (antes de `ArgumentException` e `InvalidOperationException`), con lo que desaparece `EsNoEncontrado` de `HabitsController`.
  - Las transiciones prohibidas siguen siendo `InvalidOperationException` (400 genérico), para no tocar las suites de la P3 ni sus defectos.
- **Fecha futura:** se rechaza en el **servicio**, con un `TimeProvider` y 5 minutos de tolerancia, y no en el dominio. Las pruebas de la P3 registran fechas futuras a propósito para probar los límites del día UTC y de la semana ISO.
- **`GET /api/habits/{id}`.**
- **`Habito.MetaId` opcional** (`Guid?`):
  - si viene, debe existir;
  - el GUID vacío sigue siendo 400;
  - el FK Hábito→Meta pasa de `Cascade` a `SetNull`: borrar una meta ya no borra hábitos.
- **Modelo:**
  - una clase `IEntityTypeConfiguration` por entidad (`Persistence/Configuraciones/`);
  - `ConfigureConventions` lee todo `DateTime` con `Kind=Utc`, así que la API devuelve `Z`;
  - longitudes máximas, `HasPrecision(18, 2)` e índice `IX_Habitos_UsuarioId`.
- **Migración `HabitoMetaOpcionalYLongitudes`:** antes de acotar los `nvarchar(max)` comprueba que ningún texto exceda los límites. Si alguno los excede, se detiene con un mensaje claro (`THROW 50002`) y no trunca nada.

### Pruebas

- Nuevo `tests/HabitTracker.Api.Tests`: controller y middleware. `PlanDelDiaTests` se movió aquí desde `DocumentManager.Api.Tests`.
- Pruebas nuevas en Application y en `Integration` (`CimientosPersistenciaTests`, con SQLite).
- Se actualizó la prueba del hábito inexistente, que esperaba `InvalidOperationException`.

### Frontend

- El formulario ya no pide el GUID de meta.
- El detalle usa `GET /api/habits/{id}`: un 404 con código o un 403 se muestran como «Hábito no encontrado».
- El mock replica `metaId` opcional, `meta_no_encontrada`, `fecha_futura`, el 404 con código y las fechas con `Z`.
- Se quitó `VITE_META_ID_POR_DEFECTO`.

### Medido

- **Pruebas:** backend 863 (antes 841: +22 nuevas, 7 movidas de proyecto); frontend 634 (antes 623).
- **Mutación:** 8 reglas rotas a propósito y todas hacen fallar al menos una prueba. En el backend: sin rechazo de fecha futura, FK otra vez en `Cascade`, sin conversor UTC, 404 servido como 400 y meta inexistente aceptada. En el frontend: cualquier 404 tratado como «no encontrado», mock sin comprobar el GUID y mock sin tolerancia del reloj.
- **SQL Server real**, en la base aislada `HabitTracker_PruebaF1`, ya borrada:
  - la migración se aplica y revierte;
  - el chequeo detiene la migración con una `Categoria` de 150 caracteres;
  - el esquema final tiene `MetaId` anulable, `FK ... SET_NULL`, `nvarchar(200)/(50)` e `IX_Habitos_UsuarioId`.

### Revisiones (core-transversal-reviewer y frontend-reviewer)

Sin hallazgos críticos ni altos. Se corrigió en la misma fase:
- **La reversión de la migración** (`Down`) se detiene con `THROW 50003` si hay hábitos sin meta, en lugar de fallar al recrear la clave foránea.
- **El chequeo previo de `Up`** usa `DATALENGTH/2` (`LEN` ignora los espacios finales).
- **El conversor de fechas** pasa a UTC una fecha `Local` al escribirla (con su prueba).
- **El frontend:**
  - solo un 404 con `codigo` `habito_no_encontrado`, o un 403, se muestra como «no encontrado»; un 404 sin cuerpo sigue siendo un error de carga (`esNoDisponible`);
  - un id que no es GUID no se consulta;
  - el mock responde 404 sin cuerpo a un id que no es GUID y 403 con ProblemDetails para un hábito ajeno, y solo rechaza el GUID vacío;
  - pruebas nuevas: hábito ajeno, 500, 404 sin cuerpo y tolerancia de +2 min.

**Decisiones:**
- Un hábito ajeno sigue respondiendo **403**, no 404. Es la semántica de la P3 (RD-06) que fijan sus pruebas y defectos; el riesgo de enumeración es mínimo con GUID.

**Deuda menor anotada:**
- El dominio sigue usando `DateTime.UtcNow` y el servicio un `TimeProvider`: pasar `ahora` al dominio en una fase posterior.
- Un usuario autenticado sin `sub` válido recibe 403 y no 401: pasaba ya antes de esta fase.
- Las carpetas `Application/Exceptions` (clima) y `Domain/Excepciones` tienen nombres distintos.

### Para quien tenga una base anterior

```bash
dotnet ef database update --project Modules/HabitTracker/HabitTracker.Infrastructure --startup-project Host/WebApi --context HabitTrackerDbContext
```

Si se detiene con el `THROW 50002`, acorta los textos que indica antes de repetir.

## Fase 2a: perfil, salud cifrada y progreso corporal — backend (`feature/habittracker-perfil-salud`, 08-11-2026)

### Dominio (`HabitTracker.Domain/Perfil`, `Salud`, `Progreso`)

- **`PerfilUsuario`**, una fila por persona:
  - **Nivel 1, obligatorio:** `PerfilLaboral` (7 jornadas), zona horaria IANA validada con `TimeZoneInfo` y aviso de salud.
  - **Opcional:** horas de entrada, salida, despertar y dormir; edad 16–100, sexo, altura, peso, actividad, días, minutos y plan de interés.
  - `DatosPerfil.TraeDatosDeSalud` es verdadero si viene peso o altura.
  - El último peso de Progreso pasa al perfil.
- **`CuestionarioSalud`**, por bloques, cada uno con su `RespondidoEn`:
  - **(a)** hasta 3 objetivos, zonas solo con «definir/tonificar», experiencia y lugar;
  - **(b)** revisión de seguridad de 7 preguntas inspiradas en el PAR-Q+;
  - **(c)** condiciones (incluida la **enfermedad renal**) y alergias. «Ninguna» es exclusiva y no se admiten repetidas.
  - Solo listas cerradas: no hay texto libre.
- **`EvaluadorSalud`**, la única definición de las reglas:
  - **Nivel:** Normal, Precaución o ConsultarMédico (cualquier «sí» en la seguridad).
  - **Restricciones:**
    - `SoloIntensidadBaja`, `SinIntensidadAlta`;
    - `SinImpacto` (rodilla, osteoporosis);
    - `SinRestriccionCalorica` (embarazo, posparto, trastorno alimentario, menores de 18, IMC < 18,5);
    - zonas a evitar.
  - **Además:** alérgenos y completitud por bloque.
  - `BloquesFaltantesPara(RequisitosSaludPlan)`: un plan sin requisitos no pide ningún bloque (P1).
- **`RegistroPeso` y `MedicionCorporal`:** uno por día UTC (y por zona, en las medidas). Fechas desde el año 2000 y no futuras (`fecha_futura`). Límites del frontend.
- **Excepciones:** `ConsentimientoSaludRequeridoException` (409), `perfil_no_encontrado` (404) y `perfil_requerido` (400). Se llama así, y no `ConsentimientoRequeridoException`, porque Control de Acceso ya tiene una con ese nombre.

### Application, Infrastructure y Host

- **Puertos:**
  - `IConsentimientoSalud`, implementado por el adaptador `ConsentimientoSaludAdapter` con `IConsentimientoService` de Control de Acceso. Una cuenta sin consentimientos equivale a no consentir.
  - `ICifradorDatos`.
- **Servicios:**
  - `IPerfilService`: el nivel 1 no pide consentimiento; con peso o altura, sí.
  - `ICuestionarioSaludService`: cada bloque por separado, con consentimiento.
  - `IProgresoCorporalService`: un peso atrasado no pisa el del perfil; `objetivoKg` omitido se conserva y `null` lo quita.
  - `IDatosPerfilSaludService`: exportar y borrar.
- **Infrastructure:**
  - `CifradorDataProtection` (ASP.NET Core Data Protection, un propósito por bloque y versión);
  - `CuestionarioSaludRepository` guarda una fila de persistencia propia (`CuestionarioSaludFila`) con cada bloque como JSON cifrado; el dominio no sabe nada del cifrado;
  - `HabitTracker.Infrastructure` pasa a `FrameworkReference Microsoft.AspNetCore.App`, y sobran 6 paquetes `Microsoft.Extensions.*` (NU1510).
- **Host:**
  - `ProfileController` (`api/profile`, `api/profile/health/{objetivos|seguridad|condiciones}`) y `ProgressController` (`api/progress/weights|measurements`);
  - `objetivoKg` se lee como `JsonElement` para distinguir «omitido» de `null`;
  - el adaptador `ParticipantePerfilSalud` (sección `perfilYSalud`);
  - `AddProteccionDeDatos`: claves en `DATAPROTECTION_KEYS_PATH`, obligatoria fuera de Development;
  - el `catch` del 409 va antes de `HabitTrackerException`.
- **Docker:** directorio `/app/data/claves` y volumen con nombre `dataprotection-claves`.
- **Migración `AgregarPerfilYSalud`:** solo crea `PerfilesUsuario`, `CuestionariosSalud`, `RegistrosPeso` y `MedicionesCorporales` con sus índices. No toca las tablas existentes.

### Medido

- **Pruebas:** backend 946 (antes 863): dominio +41, Application +15, Integration +7, Api +16 y adaptadores +4.
- **Mutación:** 12 reglas rotas a propósito y todas hacen fallar al menos una prueba (8 antes de la revisión y 4 sobre sus correcciones):
  - perfil con peso sin consentimiento;
  - cuestionario sin cifrar;
  - un «sí» que no lleva a ConsultarMédico;
  - un Básico que exige la seguridad;
  - «Ninguna» combinable;
  - un peso atrasado que pisa el perfil;
  - una baja que borra perfiles ajenos;
  - trastorno alimentario sin restricción calórica;
  - `[Range(int, int)]` de vuelta sobre un decimal;
  - sin lectura tolerante del cuestionario;
  - el cifrado sin ligar a su dueño;
  - una evaluación que ignora el consentimiento retirado.
- **API real contra SQL Server**, base aislada `HabitTracker_PruebaF2`, ya borrada: **29 de 29**.
  - Registro, activación y login: 404 con código, nivel 1 sin consentimiento, 409 con peso, seguridad y peso sin consentimiento.
  - Con el consentimiento: 72,35 kg sin redondear, evaluación con `SinRestriccionCalorica`, 400 con «Ninguna + Asma» y con 4 objetivos.
  - **La columna de SQL Server no contiene `Hipertension` en claro** y el GET la descifra.
  - Pesos y medidas: objetivo conservado, un peso por día, `fecha_futura`, medida fuera de rango, descarga con `perfilYSalud` y 401 sin token.
  - Después de las correcciones de la revisión se repitió con una base nueva y volvió a dar **29 de 29**.
  - **La primera corrida dio 24 de 29.** Los 5 fallos eran del guion, no de la API: usaba las 12:00 UTC de un día que en ese momento iba por las 03:14 UTC, y la API respondió con razón `fecha_futura`. Antes, otro error del guion: el registro usa `nombre`, no `nombreCompleto`.

### Revisión RD (core-transversal-reviewer)

Sin hallazgos críticos. Se corrigió en la misma rama:
- **A1 (alto): un bloque que no se puede descifrar daba un 500.** Pasaba con claves perdidas o con otras claves, bloqueaba a la persona y tumbaba también su descarga de datos.
  - Ahora el repositorio lo trata como no respondido y registra un aviso sin contenido. La persona lo vuelve a responder y se reescribe con las claves vigentes.
- **A2 (alto): `[Range(30, 300)]` sobre un `decimal` daba un 500.** Lanzaba `OverflowException` con `1e20`, y `300.4` pasaba por redondeo.
  - Ahora se usa `Range(typeof(decimal), "30", "300", …)` en el peso y la altura.
- **M2: retirar el consentimiento `datosSalud` no tenía efecto.**
  - Ahora la evaluación deja de usar el cuestionario y el IMC (`usaDatosDeSalud: false`). La edad sigue protegiendo a los menores.
  - Los datos no se borran y su dueño los sigue viendo, por el derecho de acceso.
- **B1: una fila copiada a otra persona se podía descifrar.** Ahora el propósito del cifrado incluye el identificador del dueño.
- **B3:** se añadieron pruebas de `AddProteccionDeDatos`. Sin ruta, fuera de Development no arranca; con ruta, las claves sobreviven a un reinicio.
- **M1, documentado:** las claves quedan sin proteger en su volumen. El respaldo de las claves va **aparte** del de la base, y protegerlas con un certificado (`ProtectKeysWithCertificate`) queda para la Fase 6.
- **M3, decisión:** solo se cifra el cuestionario. El peso y las medidas quedan sin cifrar para poder ordenar y graficar en el servidor, y solo los ve su dueño.

**Deuda baja anotada:**
- «Uno por día» solo lo garantiza la aplicación: no hay índice único por día. Dos POST simultáneos podrían duplicar un peso.
- `IConsentimientoService.ObtenerAsync` no recibe un `CancellationToken`.
- `PUT /api/profile` sin `pesoKg` borra el peso del perfil, porque PUT reemplaza el perfil entero: el frontend debe reenviarlo.

### Pendiente (Fase 2b, frontend)

- Onboarding de 3 toques con el perfil real.
- Paso «Tu salud» bajo demanda.
- Configuración › Perfil y salud.
- Progreso conectado.
- Los contratos salen de `CONTRATOS_PROPUESTOS`.
- Los enums del backend son PascalCase (`Mujer`, `MuyAlta`, `TurnoNocturno`), y el contrato propuesto usaba minúsculas: el frontend los adopta.

## Fase 2b: perfil y salud en el frontend (`feature/frontend-perfil-salud`, 08-11-2026)

- **Contrato:** `features/perfil/types.ts` copia `ProfileController`: perfil en dos niveles, enums en PascalCase (`TurnoNocturno`, `Mujer`, `MuyAlta`) y cuestionario por bloques con su evaluación. Las etiquetas en español están en `features/perfil/etiquetas.ts`.
- **Onboarding de 3 toques (P1, P2):**
  - **Pasos:** jornada (tarjetas de radio), zona horaria (detectada con `Intl`, solo se confirma) y aviso de salud. No pide edad, peso ni el cuestionario.
  - **Consentimiento:** ya no da por hecho `datosSalud`. Si hay que volver a pedir los documentos, conserva el valor que tenía.
  - **Plan de la landing:** queda como interés (`metaSlug`).
- **Configuración › Perfil y salud** (`/ajustes/perfil-salud`, solo Estándar):
  - nivel 1 y datos opcionales (horarios, edad, sexo, altura, peso, actividad);
  - los tres bloques del cuestionario, cada uno con su botón y su fecha;
  - «Lo que tendremos en cuenta» (nivel de precaución y restricciones, en lenguaje llano);
  - un aviso para activar `datosSalud` aquí mismo.
  - «Ninguna» es exclusiva en las dos direcciones y los objetivos tienen un máximo de 3.
- **Progreso conectado:** el 409 muestra un mensaje común (`MENSAJE_CONSENTIMIENTO_SALUD`) que dice dónde dar el permiso.
- **Descarga de datos:** ahora es solo la del servidor (formato versión 3, con la sección `perfilYSalud`); el cliente ya no pide perfil ni progreso por su cuenta.
- **Mocks:** `src/mocks/handlers/perfil.ts` y `progreso.ts` salen de `CONTRATOS_PROPUESTOS` y replican el backend:
  - nivel 1 sin consentimiento y 409 solo con peso o altura;
  - bloques validados y evaluación replicada de `EvaluadorSalud`, que con el consentimiento retirado no se usa;
  - fechas en UTC con `Z`, `fecha_futura` y `perfil_requerido`;
  - una medida por día y zona, y el último peso que pasa al perfil.
- **Medido:**
  - **Pruebas:** frontend 651 (antes 634) y backend 947 (+1 por el ajuste del consentimiento), con lint, tipos y build en verde.
  - **Mutación:** 5 reglas rotas a propósito y todas hacen fallar al menos una prueba:
    - el onboarding vuelve a dar por hecho `datosSalud`;
    - «Ninguna» deja de ser exclusiva (sobrevivió al primer intento y se reforzó la prueba);
    - el 409 no explica dónde dar el permiso;
    - se exige el consentimiento aunque el peso no cambie, en el backend y en el mock.
  - **Contra la API real**, en una base aislada que ya se borró: **13 de 13**. El backend acepta tal cual los cuerpos que arma el frontend:
    - el perfil del onboarding, con todo en `null` salvo el nivel 1;
    - las horas `HH:mm` de un `input type=time`;
    - los tres bloques del cuestionario;
    - el peso con `toISOString()`;
    - y responde la descarga con `perfilYSalud`.
- **Revisión (`frontend-reviewer`):** sin bloqueantes. Se corrigió en esta rama:
  - **Alto: el registro guardaba en `localStorage` el peso, la altura, la edad y el sexo de la calculadora,** que el onboarding ya no usa. Ahora no se guardan (`datosCorporales: null`) y hay una prueba de ello.
  - **Alto: con el permiso retirado no se podía cambiar la jornada,** porque el formulario reenvía el peso guardado.
    - El backend (`PerfilService`) ahora exige el consentimiento solo si el peso o la altura **cambian** (con su prueba), y el mock hace lo mismo.
  - **Medio: valores por defecto peligrosos.**
    - El aviso «Permitir usar mis datos de salud» solo aparece con los consentimientos ya leídos y con versiones aceptadas: nunca acepta en nombre de la persona una versión legal que no vio.
    - El onboarding no envía consentimientos sin haberlos leído antes, así no retira uno que existía.
  - **Medio: fidelidad.**
    - Las fechas usan `formatearFecha` (RD-11).
    - El mock ordena las restricciones y los alérgenos como el backend (por el valor del enum).
  - **Bajo:**
    - el mock conserva el registro del mismo día al reemplazar un peso;
    - la descarga del mock deja el nombre vacío y la sincronización del peso actualiza `actualizadoEn`;
    - `replaceAll` en los nombres de las zonas;
    - `aria-describedby` en los errores de las opciones.
  - **Pruebas nuevas:**
    - el mensaje del 409;
    - `fecha_futura` y `perfil_requerido`;
    - el onboarding que conserva `datosSalud`;
    - el orden de la evaluación.
  - **Pendiente bajo:**
    - En el mock, el orden «validación → consentimiento» no es exacto en todos los casos.
    - El foco no va a los errores de guardado del cuestionario.

## Fase 3a: catálogo de planes y recomendador en el backend (`feature/habittracker-catalogo-planes`, 09-11-2026)

- **Dominio (`Domain/Planes/`):**
  - `PlanPlantilla` + `TareaPlantilla`, con `VersionMinima` y `AnclaSugerida` en cada tarea y `GuiaSemanal` con `TipExtra`.
  - Las invariantes de seguridad del contenido están en el constructor. Un plan se rechaza si:
    - usa datos de nivel 2 sin `RequiereCuestionario`;
    - es un Básico de peso o de intensidad alta;
    - sus semanas no encajan con su nivel;
    - es un Regular sin guía para cada semana;
    - tiene una tarea de agua por encima de 20 vasos o 5 litros al día.
  - `ReglasSeguridadPlanes` es la única definición de "no apto". `SituacionPersona.Crear` deja fuera el cuestionario sin el consentimiento, pero la edad sigue protegiendo.
  - `RecomendadorPlanes` y `VariantesMiDia`: "Mi día" de la jornada siempre primero; sin cuestionario, primero lo que no pide preguntas.
- **Contenido:** `catalogo-planes.json` con los 13 planes del plan, embebido. `LectorCatalogoPlanes` es estricto y `SembradorCatalogoPlanes` hace un upsert en cada arranque y nunca borra.
- **API:**
  - `GET /api/plans` y `GET /api/plans/{slug}`: anónimos, con caché pública de 5 minutos y la política `catalogo` (60 por minuto y por IP).
  - `GET /api/plans/recommended`: rol `Estandar`, con `no-store`.
- **Migración `AgregarCatalogoPlanes`:** `PlanesPlantilla` (listas como JSON con enums como texto) y `TareasPlantilla` (`Guid` propio).
  - Aplicada a la base local `HabitTracker` después del respaldo `HabitTracker_antes_catalogo_20261109.bak`.
- **Decisiones tomadas al implementar:**
  - **Un Regular dura de 3 a 12 semanas, no de 4 a 12:** `reducir-estres` (21 días) es Regular en el plan.
  - **Los días de la semana y la hora sugerida de cada tarea no se modelan:** el ancla los reemplaza y el `ResolvedorAncla` de la Fase 4 calcula la hora (YAGNI).
  - **Las variantes de "Mi día" de otras jornadas no se recomiendan,** pero siguen en el catálogo.
  - **Sin edad ni sexo en el perfil,** un plan que los restringe se recomienda con un aviso para completarlos; la inscripción (Fase 4) decidirá.
  - **La palabra «peso» se permite en los planes** («tu propio peso», «peso muerto»). Siguen prohibidas kg, calorías, dieta, IMC y las palabras de culpa.
- **Verificación:**
  - **Pruebas:** backend 1029 (antes 947), con build sin avisos.
  - **Prueba de contenido:**
    - los 13 slugs;
    - el cuestionario cuando hay datos de nivel 2;
    - los topes del agua, y el aviso médico con los riñones;
    - la versión mínima y el ancla;
    - las palabras prohibidas (encontró «Nada está prohibido» en el JSON y se reescribió).
  - **Mutación:** 6 reglas rotas a propósito, y todas hacen fallar al menos una prueba:
    - un plan de nivel 2 sin cuestionario;
    - un alérgeno del plan ignorado;
    - "Mi día" deja de ir primero;
    - se lee el cuestionario sin consentimiento;
    - el sembrador no retira;
    - el agua sin tope.
  - **Contra la API real**, en una base aislada que ya se borró: **33 de 33**, más un reinicio que confirma la siembra idempotente (0 nuevos, 13 planes y 33 tareas). Se comprobaron:
    - la caché y el 429;
    - el 404 con código;
    - "Mi día" de turno nocturno primero;
    - el no apto con embarazo;
    - el aviso de agua con hipertensión.
- **Revisión RD-01..RD-12 (`core-transversal-reviewer`):** sin bloqueantes ni altos. Se corrigió en esta rama:
  - **Medio: un error de un endpoint público quedaba en caché 5 minutos.** `[ResponseCache]` fija la cabecera antes de la acción, así que un 404 o un 500 salían con `public,max-age=300`. Ahora `GlobalExceptionMiddleware` responde todo error con `no-store`, con su prueba.
  - **Bajo:**
    - si falta la migración, la API no arranca; ahora el log dice qué hacer;
    - una `GuiaSemanal` NULL en la base ya no da un 500.
  - **Pendiente bajo:**
    - Detrás de un proxy, el límite por IP del catálogo es compartido (deuda 3 del ESTADO, Fase 6).
    - Un plan retirado que vuelve al JSON con una versión menor la baja; se acepta porque el JSON manda.
- **Lección:** un script de mutación que restaura el archivo original con `mv` le deja una fecha anterior a la de la DLL y la compilación incremental no lo recompila. Después de mutar, hay que compilar con `--no-incremental`.

## Fase 3b: catálogo de planes y recomendados en el frontend (`feature/frontend-catalogo-planes`, 12-11-2026)

- **Nueva feature `features/planes`:**
  - **Contrato y datos:**
    - `types.ts` copia los DTO de `PlansController`;
    - `etiquetas.ts` trae los textos en español;
    - `api.ts` ofrece `usePlanes`, `usePlan`, `usePlanResumen` y `useRecomendados`.
  - **Pantallas:**
    - `CatalogoPlanes` (filtros por nivel y área): lo usan la landing, `/metas` y `/planes`.
    - `PlanDetallePage` en `/metas/:slug` y `/planes/:slug`: aviso de seguridad, tareas con su versión mínima y guía semanal.
    - `PlanesPage` en `/planes`: «Tu día de trabajo saludable» primero, recomendados con sus motivos y «No recomendado para ti» con su razón.
  - **Hoy:** quien aún no tiene hábitos ve «Empezar mi día saludable» con la variante de su jornada.
  - **Menú:** «Planes» solo para el rol Estándar.
- **Catálogo estático eliminado** (`publico/catalogo.ts`, `CatalogoMetas`, `TarjetaMeta`). Sus consumidores usan ahora la API:
  - **Panel del alta y `ResumenAdminPage`:** `usePlanResumen`.
  - **`HoyPage` y `ProgresoPage`:** `usePlanResumen`, para mostrar la semana del plan.
  - **Slugs de la URL o de `localStorage`:** se validan por formato (`esSlugPlan`).
  - **Landing:** recibe el catálogo por una ranura (`LandingPage catalogo={…}`), porque una feature no importa componentes de otra.
- **«Elegir este plan»:** guarda el plan como interés (`metaSlug`) reenviando el perfil vigente, hasta que la Fase 4 traiga la inscripción real.
  - Antes de enviar se pide el perfil al servidor, para no pisar un peso registrado hace un momento.
  - El botón no se habilita mientras no se sepa si el plan es para la persona, porque el servidor aún no lo impone.
- **Mock de los planes:** replica `RecomendadorPlanes` y `ReglasSeguridadPlanes`. Sus datos se generaron del `catalogo-planes.json` y `mocks/planes.test.ts` los compara con el JSON del backend.
- **Verificación:**
  - **Pruebas:** frontend 692 (antes 651) con lint, tipos y build en verde. Pasan tres veces seguidas.
  - **Mutación:** 11 reglas rotas a propósito y todas hacen fallar al menos una prueba.
    - Tres sobrevivieron al primer intento y destaparon huecos: el slug con mayúsculas, la tarjeta de Hoy comprobada antes de que la consulta resolviera y el cuestionario leído sin consentimiento.
    - Se reforzaron las pruebas, no el código.
  - **Contra la API real**, en una base aislada que ya se borró: **27 de 27 respuestas idénticas** a las del mock.
    - 9 personas con distintos perfiles, cuestionarios y condiciones (embarazo, hipertensión, «sí» en la revisión de seguridad, IMC bajo, menor de edad…).
    - El catálogo, los 13 detalles y 4 filtros.
- **Revisiones (`frontend-reviewer` y `frontend-design-critic`):** sin bloqueantes. Se corrigió en esta rama:
  - **Medio (reviewer):**
    - elegir un plan podía pisar un peso recién registrado;
    - se podía elegir un plan sin saber si era apto;
    - la prueba de contenido fallaba si no estaba el JSON del backend.
  - **Alto (diseño):**
    - «no recomendado» sonaba a prohibición; ahora usa un tono de cuidado;
    - la acción principal de Hoy es un botón;
    - el catálogo completo marca los planes no recomendados.
  - **Medio (diseño):** nivel con etiqueta visible, aviso de cuántos planes se muestran y estados vacíos separados.
  - **Pendiente:**
    - filtros del catálogo en la URL;
    - unificar «meta» y «plan» en los textos;
    - confirmar antes de cambiar de plan, porque reinicia la semana del plan;
    - foco tras elegir un plan y título de pestaña con el nombre del plan.
- **Lección de entorno:** Vitest falla en todas las suites si la terminal abre `frontend/` con la unidad en minúscula (`c:\`), porque carga `vitest` dos veces. Se ejecuta desde `C:\` en mayúscula.

## Fase 4a: mis planes, check-in diario y adherencia en el backend (`feature/habittracker-mis-planes`, 12-11-2026)

- **Dominio:**
  - `Planes/PlanUsuario.cs`: el plan de una persona, su `PeriodoPausa` (inicio, fin y motivo) y `TransicionesPlanUsuario`, **el único punto de la máquina**: `Activo ⇄ Pausado`, y `Completado` (solo desde Activo y al llegar a su fin) o `Cancelado` como terminales. Extender y activar el modo suave no son transiciones.
  - `Planes/ResolvedorAncla.cs`: ancla + jornada → hora local sugerida (el turno nocturno tiene sus valores).
  - `Adherencia/CalculadoraProgreso.cs`, **la única definición de racha y cumplimiento**:
    - un plan con al menos una tarea diaria se mide en días; uno que solo tiene tareas semanales se mide en **semanas** (`UnidadSeguimiento.Semanas`): los días vacíos entre sesiones son lo esperado, no un fallo;
    - un día o una semana sin registro no corta la racha y dos seguidos sí; una pausa justificada no cuenta ni corta;
    - el modo suave convierte la versión mínima en un día completo.
  - `Adherencia/SugeridorAjuste.cs` (4 o más días sin registro en los últimos 7 → modo suave, extender o pausar; **nunca se aplica solo**), `CalculadoraLogros.cs` (su firma no recibe peso ni medidas) y `Motivacion/SelectorMensajesMotivacion.cs` (determinista con FNV-1a sobre el `Guid` del usuario y el día local; con 2 o más días sin registro, mensaje de regreso).
  - `Habito` gana `PlanUsuarioId`, `Descripcion`, `VersionMinima`, `Ancla` y `PausadoPorPlan`: al reanudar un plan solo vuelven los hábitos que el plan pausó. `RegistroDiario` gana `EsVersionMinima`.
- **Application:** `IPlanUsuarioService`, `IHoyService`, `IResumenProgresoService`, `ILogrosService` e `IDatosPlanesService` (exportar y borrar), con los puertos de `IPlanUsuarioPuertos.cs`. `ConflictoDeUnicidadException` es la excepción propia de Application para una violación de unicidad, de modo que el servicio no conoce a EF.
- **Infrastructure:** `mensajes-motivacion.json` (33 mensajes en 6 categorías, recurso embebido, ≤160 caracteres) y `CatalogoMensajes`; repositorios de planes y logros; `HabitTrackerUnitOfWork` traduce la violación de un índice único a `ConflictoDeUnicidadException`.
- **API (rol `Estandar`, identidad del token, 404 con cuerpo para lo ajeno):**
  - `POST /api/my-plans` (201), `GET /api/my-plans`, `GET /api/my-plans/{id}`, `POST .../transition`, `POST .../adjust` y `PUT .../reminder`;
  - `GET /api/today`, `GET /api/progress/summary` y `POST /api/progress/achievements/seen`;
  - `POST /api/habits/{id}/progress` acepta `esVersionMinima`.
  - `ParticipantePlanes` (sección `planes`) exporta y borra los planes, las pausas y los logros con la descarga y la baja de cuenta.
- **Migración `AgregarPlanesUsuario`:** `PlanesUsuario`, `PeriodosPausa` y `LogrosUsuario` (único por persona y clave), las columnas nuevas de `Habitos` (FK a `PlanesUsuario` en `SetNull`) y `RegistrosDiarios.EsVersionMinima`.
- **Decisiones tomadas al implementar:**
  - **La adherencia se mide por el día local,** con la zona del perfil. `Habito.CumpleMetaFinal` sigue usando el día UTC (regla de la P3, no se toca); la diferencia solo afecta a la transición del hábito a Completado y queda documentada.
  - **Las pausas se miden en días locales,** y `Reanudar` agrega al final del plan exactamente los mismos días que `CalculadoraProgreso` deja fuera.
  - **Un plan de N semanas dura N×7 días:** el último día es el fin previsto menos uno.
  - **Terminar un plan no borra nada.** Con `conservarHabitos`, los hábitos siguen activos y **conservan su vínculo con el plan**, para no perder su historial ni su récord; sin él, quedan en pausa.
  - **"Hoy solo lo mínimo"** registra `Cantidad = 1` con `EsVersionMinima = true`: la invariante `Cantidad > 0` de la P3 se mantiene.
  - **Un plan solo se puede tener una vez en curso:** un índice único filtrado (`UsuarioId`, `PlanPlantillaId` donde el estado es Activo o Pausado) lo garantiza en la base; un plan terminado no impide empezarlo de nuevo.
  - **Un logro se gana una sola vez:** inserción idempotente (`INSERT … WHERE NOT EXISTS`) más el índice único.
  - **Sin clima en `GET /api/today`:** el perfil no guarda ciudad. `plan-del-dia` y sus pruebas no cambian.
- **Verificación:**
  - **Pruebas:** backend 1214 (antes 1029), con build sin avisos.
  - **Mutación:** 28 reglas rotas a propósito y todas hacen fallar al menos una prueba: racha tolerante, tope de 2 extensiones, logros sin peso, determinismo del mensaje, umbral del sugeridor, plan no apto, límite de planes, aislamiento entre personas, pausas locales, índice único, conservar los hábitos, etc.
    - Dos sobrevivieron al principio y destaparon huecos: el día en que se reanuda una pausa y el resumen de un lunes sin sesiones. Se reforzaron las pruebas, no el código.
    - Un mutante del servicio de logros es equivalente a propósito: la inserción idempotente y el índice único se respaldan entre sí.
  - **Contra la API real**, en una base SQL Server aislada que ya se borró: **58 de 58** (planes, límites, pausas, ajustes, aislamiento entre personas, exportación y lo que queda en la base).
- **Lecciones:**
  - `sqlcmd` necesita `-I` (`QUOTED_IDENTIFIER ON`) para borrar o modificar filas de una tabla con un índice filtrado.
  - Las pruebas de dominio que fijaron la semántica diaria de un plan semanal tuvieron que reescribirse: el diseño nuevo mide por semanas.
- **Pendiente aceptado:**
  - el límite de 3 planes en curso se comprueba en el servicio y una carrera de dos peticiones podría pasarlo (solo el plan repetido lo impide la base);
  - `MarcarLogrosVistos` marca todos los logros;
  - una zona horaria desconocida cae a UTC sin avisar (tzdata en la imagen Docker, Fase 6).
