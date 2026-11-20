PROGRAMACIÓN III · TDS-007 · ITLA · 2026-C-3

# Asignación 4 — Historial de auditoría: quién cambió qué y cuándo

Bloque 5 | Semana 12 | 5 puntos | Individual

| Plazo | Entrega tardía |
|---|---|
| El que indica la plataforma virtual. | Hasta 24 horas después, con 50 % de penalidad. Luego no se acepta. |

Cuarta y última asignación. Construyes la **Pieza 6 del Core: el historial de auditoría**. Es la última pieza que le falta a tu proyecto, y hace lo mismo que `git log` con tu repositorio: guardar quién cambió qué, cuándo, y qué había antes. Aquí lo haces para los datos de tu aplicación.

> **En resumen:** entregas en el tag `asignacion-4` el historial de auditoría funcionando (registro de los eventos exigidos y consulta solo para el Administrador), sus pruebas y la bitácora con el agente.

## 1. Qué entregas

### 1.1 Historial de auditoría funcionando

| ID | Requisito | Criterio de aceptación |
|---|---|---|
| RF-AUD-01 | Cada registro guarda usuario que actuó, acción, entidad afectada, identificador, fecha y hora, valor anterior y valor nuevo. | Un registro al que le falte cualquiera de esos datos no cumple. |
| RF-AUD-02 | Se audita toda resolución de solicitud de permiso. | Queda registrado quién aprobó o rechazó qué y cuándo. |
| RF-AUD-03 | Se audita toda subida y eliminación de documento (RF-DOC-06). | Ambas acciones generan registro. |
| RF-AUD-04 | Se audita todo cambio de rol y toda desactivación o reactivación de usuario (RF-CA-08, RF-CA-20). | El registro muestra el valor anterior y el nuevo. |
| RF-AUD-05 | Se audita todo restablecimiento de contraseña (RF-CA-13). | Identifica si lo inició el propio usuario o un Administrador. Nunca guarda la contraseña ni el código. |
| RF-AUD-06 | El registro de auditoría no se modifica ni se elimina desde la aplicación. | No existe ninguna operación de edición ni de borrado sobre él. |
| RF-AUD-07 | Consulta filtrable por usuario, por entidad y por rango de fechas. | Reservada al Administrador; un usuario Estándar recibe rechazo. |

Aplican además RD-06 (la consulta se rechaza también si se invoca directamente), RD-07 (un filtro inválido produce un rechazo controlado), RD-09 (el historial sobrevive a reiniciar la aplicación y a `docker compose down`) y RD-11 (la fecha y hora se registra con el mismo criterio que en el resto del sistema).

Algunas decisiones de diseño son tuyas y las defiendes en el proyecto final: dónde se escribe el registro para que no pueda quedar un cambio sin su registro, cómo representas el valor anterior y el nuevo, y cómo evitas que el Core dependa de tu módulo de negocio (RD-03) al auditar. Tu README, en una sección `Auditoría`, dice cómo provocar cada evento y cómo consultar el historial con cada filtro.

### 1.2 Pruebas sobre la auditoría

- Una prueba por cada tipo de evento auditado (RF-AUD-02 a 05) que verifica los campos del registro, incluidos el valor anterior y el nuevo.
- Una prueba que verifica que un usuario Estándar no consulta el historial (RF-AUD-07).
- Una prueba que verifica que ninguna operación expuesta modifica o borra registros (RF-AUD-06).
- Pruebas sin levantar la aplicación completa (RD-12), con tres fases identificables, independientes entre sí y con nombre que dice el comportamiento.
- Un único comando, en la sección `Pruebas` de tu README, ejecuta toda la suite desde un clon limpio. La suite pasa.

### 1.3 Bitácora con el agente y paralelo con git

Archivo `docs/bitacora-asignacion-4.md`, incluido en el tag. Documenta al menos una tarea de esta asignación que le delegaste al agente, con qué le pediste, qué te devolvió y al menos un caso en que se equivocó, cómo lo detectaste y cómo lo corregiste. Una bitácora donde todo salió bien a la primera está incompleta y se devuelve.

Incluye además una tabla que compara **un registro de auditoría de tu aplicación con un commit de tu repositorio**, campo por campo: qué campo del registro corresponde a autor, mensaje, fecha, identificador del commit y diff, y qué información guarda uno que el otro no.

## 2. Cómo se entrega

```bash
git tag asignacion-4
git push origin --tags
```

En Moodle, en la tarea «Asignación 4»: la URL de tu repositorio y el nombre de la etiqueta, `asignacion-4`. Se evalúa el punto etiquetado; puedes seguir trabajando en `main` después. Es la última asignación del cuatrimestre.

## 3. Rúbrica

| Criterio | No logrado (0 %) | Parcialmente logrado (50 %) | Logrado (100 %) |
|---|---|---|---|
| Eventos auditados RF-AUD-01 a 05 (1.5 pts) | No existe el registro de auditoría, o no se audita ninguno de los eventos exigidos. | Se auditan algunos eventos, o a algún registro le falta un dato de RF-AUD-01, o el valor anterior y el nuevo no se guardan, o un registro guarda la contraseña o el código de recuperación. | Se auditan resolución de solicitudes, subida y eliminación de documentos, cambio de rol, restablecimiento de contraseña (propio y forzado por un Administrador), desactivación y reactivación de usuarios; cada registro trae usuario, acción, entidad, identificador, fecha y hora, valor anterior y valor nuevo, y ninguno guarda contraseñas ni códigos. |
| Consulta e inmutabilidad RF-AUD-06 y 07 (1.0 pt) | El historial se puede editar o borrar desde la aplicación, o cualquier usuario lo consulta. | La consulta funciona para el Administrador y rechaza al Estándar, pero falta algún filtro (usuario, entidad o rango de fechas), o el rango de fechas incluye o excluye mal el último día, o existe una operación que modifica o elimina registros pero está oculta. | El Administrador consulta filtrando por usuario, por entidad y por rango de fechas, con resultados distintos y correctos; el Estándar recibe rechazo; no existe ninguna operación de edición ni de borrado sobre el historial. |
| Pruebas sobre la auditoría (1.0 pt) | No hay pruebas de la auditoría, o no corren. | Hay pruebas, pero solo cubren un evento, o verifican que se creó un registro sin comprobar sus campos, o dependen del orden de ejecución. | Hay una prueba por tipo de evento auditado que verifica los campos del registro (incluidos valor anterior y nuevo), una que verifica que un Estándar no consulta el historial y una que verifica que ninguna operación expuesta modifica o borra registros. Un comando documentado en el README las ejecuta y la suite pasa. |
| Bitácora y paralelo con tu historial de git (1.5 pts) | No hay bitácora, o no corresponde a esta asignación, o no muestra evidencia de la sesión real. | Documenta qué pediste y qué devolvió el agente, pero no hay ningún error del agente detectado y corregido, o falta el paralelo con el historial de git. | Qué pediste, qué devolvió, al menos un error del agente detectado y corregido con evidencia de la sesión, y el paralelo entre un registro de auditoría de tu aplicación y un commit de tu repositorio, campo por campo. |
| Total | 5.0 | | |

## 4. Qué se revisa exactamente

- `git clone` del repositorio y `git checkout asignacion-4`.
- El README: cómo provocar cada evento y cómo consultar con cada filtro, seguido al pie de la letra.
- Aprobar y rechazar una solicitud, subir y eliminar un documento, cambiar un rol, desactivar y reactivar un usuario, y restablecer una contraseña como el propio usuario y como Administrador; en cada caso, el registro resultante con todos sus campos.
- Que ningún registro contiene una contraseña ni un código de recuperación.
- La consulta como Administrador con cada filtro y con un rango de fechas, y como Estándar (rechazo).
- Que no hay en el código ninguna operación que edite o borre registros del historial.
- `docker compose down` (sin `-v`) y volver a levantar: el historial sigue ahí.
- El comando de la sección «Pruebas» del README, ejecutado tal como está escrito.
- `docs/bitacora-asignacion-4.md`, incluida la tabla de paralelo con el historial de git.

Programación III · ITLA · 2026-C-3
