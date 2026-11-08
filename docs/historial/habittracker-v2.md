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
