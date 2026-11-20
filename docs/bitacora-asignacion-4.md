# Bitácora — Asignación 4: historial de auditoría

Base: `develop` en `baba1ac`. Rama `feature/auditoria-asignacion-4`.

**Cómo trabajé.** Le di el enunciado al agente y le pedí un plan antes de escribir código. El agente exploró el repositorio, propuso el plan y lo ajusté con dos mensajes; después lo implementó por fases (un commit por fase, con sus pruebas) y comprobó el sistema en Docker. Yo decidí el alcance y el agente escribió y ejecutó.

## Qué le pedí (literal)

1. «ya llego la asignacion 4» (con `docs/asignacion-4.md`).
2. «Recuerda incluir filtrado en la auditorias, ya que pueden ser muchos datos. una pregunta: vas a quitar lo que tiene el modulo de acceso y lo vas a trasladar al modulo de auditoria ?»
3. Aprobé el plan. Mi pregunta 2 cambió dos cosas: el filtrado y la **paginación** pasaron a ser requisito (en la base de datos, con tres índices), y el plan quedó diciendo que **no se traslada nada**: Control de Acceso sigue registrando donde ocurre el cambio y solo se quita su auditor provisional, que escribía en el log, para conectarlo con el historial real.

## Tareas delegadas

| # | Tarea | Qué devolvió | Commit |
|---|---|---|---|
| 1 | Módulo Auditing: registro con los siete datos, consulta filtrada y paginada solo Administrador, inmutabilidad, migración | `RegistroAuditoria`, `AuditingDbContext` que rechaza modificar o borrar, `GET api/audit`; 20 pruebas | `656685b` |
| 2 | Eventos de Control de Acceso: rol, desactivar, reactivar, restablecimiento propio y forzado | Puerto existente conectado al historial; el restablecimiento propio, que no se auditaba, ahora sí; 6 pruebas + adaptador | `b136b6c` |
| 3 | Permisos: aprobar y rechazar | Puerto opcional `IAuditorPermisos`; 4 pruebas + adaptador | `3f9c27e` |
| 4 | Documentos: subir y eliminar | Puerto opcional `IAuditorDocumentos`; 5 pruebas + adaptador | `2212a71` |

## Errores del agente (detectados y corregidos)

**1. Dedujo «quién inició el restablecimiento» de un dato que no lo dice.** La exploración propuso saberlo por el prefijo `INVALIDADO_` de la contraseña. Al revisarlo vi que falla si la persona pide su propio código después de que un Administrador forzó el restablecimiento: el prefijo seguiría ahí y el registro diría «Administrador» sin serlo. Se cambió por un campo en el código de recuperación (`IniciadoPorAdministrador`, migración `AgregarOrigenDelCodigoDeRecuperacion`). La prueba `RF_AUD_05_SiLaPersonaPideSuPropioCodigoDespuesDeUnForzado_ElRegistroDiceQueLoInicioElla` fija el caso, y comprobé que no es vacía: con la marca desactivada, `RF_AUD_05_UnRestablecimientoForzado…` falla.

**2. La migración generada habría roto a quien ya tuviera un código vigente.** `dotnet ef` añadió la columna anulable y las filas existentes quedaban en `NULL`; EF intenta leerlo en un `bool` no anulable y falla al cargar esa cuenta. Lo vi al leer la migración antes de commitear y se añadió el `UPDATE` que rellena `false` a los códigos vigentes (queda comentado en `20261119235755_AgregarOrigenDelCodigoDeRecuperacion.cs`).

**3. Su script de comprobación leyó el correo equivocado y produjo fallos falsos.** Al ejecutar en Docker salieron 7 `FAIL`: el restablecimiento propio respondía 400 y el historial tenía 14 registros en vez de 15 (`FAIL ana restablece (propio) 200 (esperado 200, obtenido 400)`). No lo di por un defecto del sistema: el código de recuperación salió vacío porque el correo más reciente de Ana era una notificación («Solicitud rechazada») enviada en el mismo segundo, y el script tomaba siempre el último. Se corrigió para elegir el correo por asunto y se repitió desde una base limpia: 15 de 15 y cero fallos. Seis de los siete eran consecuencia de ese mismo código vacío y el séptimo era otro error del script («True» frente a «true»).

**4. Presentó como real un dato inventado.** En el ejemplo del README escribió el `id` del registro («f1a5e0c4-…») sin haberlo capturado de la ejecución. Lo detecté al volver a leer la sección y se sustituyó por `<GUID del registro>`; el registro completo de abajo sí es el capturado.

## Paralelo: un registro de auditoría y un commit de git

Registro real, capturado de la API (`GET /api/audit?accion=Cambio de rol`), y el commit real `701aa7a` de este repositorio:

| Concepto | Campo del registro de auditoría | Valor en el registro | Valor en el commit |
|---|---|---|---|
| **Autor** | `usuarioId` y `actor` | `f4ecd579-…` · `admin@ejemplo.com` | `Juan Ramon De Los Santos Ramirez` (nombre y correo) |
| **Mensaje** | `accion` + `entidad` | `Cambio de rol` sobre `Usuario` | `fix: dos guardados simultáneos de la preferencia de correo ya no dan 500 (hallazgo 3) [RF-NOT-07, RD-07]` |
| **Fecha** | `fecha` | `2026-11-20T00:16:51.676251Z` (UTC) | `2026-11-19T14:34:04-04:00` (con su zona) |
| **Identificador** | `id` (GUID) | `c9e5fd50-24d9-44cd-9986-8fdd58984022` | `701aa7a34f412ef3998b493ea75687d93696199a` (hash SHA-1) |
| **Diff** | `valorAnterior` → `valorNuevo` | `Estandar` → `Administrador` | `NotificacionRepository.cs` +19 −1, `NotificacionRepositoryTests.cs` +17 |
| **A qué se aplicó** | `entidad` + `entidadId` | `Usuario` · `e39d050d-…` (la cuenta cambiada) | los archivos del diff |
| **Padre** | — (no existe) | el orden sale de `fecha` | `28cf6a4`, el commit anterior |

**Qué guarda uno que el otro no:**
- **El commit** guarda *todas* las líneas que cambiaron, no un resumen de un valor; el **encadenamiento** con su padre (si alguien altera un commit antiguo, cambia su hash y el de todos los siguientes: se nota); la distinción entre autor y quien lo aplicó; y el estado completo del proyecto en ese punto.
- **El registro de auditoría** guarda **quién** actuó con su identidad dentro de la aplicación (el rol y el id de la cuenta, no un nombre de git), **sobre qué entidad de negocio** (`Usuario`, `Documento`) y su identificador, y se puede **consultar por esos campos** (por usuario, entidad y rango) con una pregunta, cosa que `git log` solo hace con búsquedas de texto.
- **La inmutabilidad no es igual.** La del commit es criptográfica (el hash lo delata); la del registro la imponen las reglas de la aplicación (no hay operación de edición ni borrado y el contexto rechaza modificar), pero **no es tamper-evident**: quien tenga acceso directo a la base de datos podría alterarlo sin dejar rastro. Es un límite que declaro; cubrirlo exigiría encadenar los registros con hashes.

## Cómo verificarlo

La sección `Auditoría` del README tiene cada evento con su comando y cada filtro con su `curl`. Con Docker (`docker compose up --build`): 15 registros tras provocar los 9 tipos de evento, cada filtro devuelve lo esperado, un Estándar recibe `403`, `PUT`/`PATCH`/`DELETE`/`POST` sobre `/api/audit` responden `405`, ningún registro contiene contraseñas ni códigos, y el total sigue siendo 15 tras `docker compose down` (sin `-v`) y volver a levantar. Pruebas: `dotnet test --filter "FullyQualifiedName~RF_AUD"`.
