# Requisitos funcionales — Práctica 3: Negocio e integración

## Fuente, alcance y prioridad

Este documento concreta la Práctica 3 a partir de **Practica3_NegocioIntegracion_PIII.pdf** y de la definición funcional acordada para Habit Tracker. La rúbrica oficial del PDF es la autoridad para la evaluación.

La práctica lleva a funcionar y prueba la etapa actual del módulo de negocio. El alcance es RF-NEG-02, RF-NEG-03, RF-NEG-04, RF-NEG-05, RF-NEG-06 y RF-NEG-09; aplican también RD-04, RD-07, RD-09 y RD-12. RF-NEG-07, RF-NEG-08 y RF-NEG-10 quedan fuera de esta entrega, tal como indica el PDF.

## 1. Máquina de estados

La entidad central es `Habito`. El enum `EstadoHabito` declara en un solo lugar exactamente cuatro estados:

- `Planificado`: estado inicial.
- `Activo`.
- `Pausado`.
- `Completado`: único estado terminal.

La máquina de Habit Tracker es independiente de la máquina de solicitudes de Gestión de Permisos. Las transiciones válidas se deciden en un solo punto del dominio. Toda transición que no figure en la tabla permitida se rechaza sin cambiar el estado.

### Transiciones permitidas

| Desde | Hacia | Quién la ejecuta | Condición de aceptación |
|---|---|---|---|
| `Planificado` | `Activo` | Usuario propietario | El usuario autenticado es el propietario del hábito (`Habito.UsuarioId == usuarioActualId`). |
| `Activo` | `Pausado` | Usuario propietario | El usuario autenticado es el propietario. |
| `Pausado` | `Activo` | Usuario propietario | El usuario autenticado es el propietario. |
| `Activo` | `Completado` | Usuario propietario | El usuario autenticado es el propietario y `CumpleMetaFinal == true`. |

### Transiciones prohibidas explícitas

El orden de esta tabla es intencional. La primera fila es el defecto 1; la salida del estado terminal es el defecto 2. Así ambos mutantes tienen efectos distintos.

| Desde | Hacia | Rechazo esperado |
|---|---|---|
| `Planificado` | `Completado` | No se puede saltar el estado `Activo`; permanece `Planificado`. |
| `Pausado` | `Completado` | Primero debe volver a `Activo`; permanece `Pausado`. |
| `Completado` | `Activo` | `Completado` es terminal; permanece `Completado`. |

Cualquier otro par no enumerado también se rechaza y conserva el estado actual. No existe transición de salida desde `Completado`.

### Propietario, rol e identidad

- Las cinco operaciones REST de Habit Tracker requieren JWT válido y autorización en el servidor; ninguna se protege solo en la interfaz (**RF-NEG-06, RD-06**).
- Para estas rutas, el rol requerido es `Estándar`. Un Administrador aprueba solicitudes en el módulo de Permisos; ser Administrador no permite modificar hábitos ajenos.
- La identidad y `usuarioActualId` se obtienen de los claims autenticados configurados por Control de Acceso, nunca de un `UsuarioId` confiado en el cuerpo de la petición.
- El servicio valida además que el usuario autenticado sea dueño del hábito antes de leerlo o modificarlo.

## 2. Las cinco funcionalidades separables (RF-NEG-02)

Cada operación se puede provocar y verificar por separado. Los contratos HTTP y los ejemplos ejecutables se documentarán en `README.md`.

| ID | Funcionalidad y ruta | Comportamiento |
|---|---|---|
| RF-NEG-02.1 | Crear hábito — `POST /api/habits` | Crea para el usuario autenticado un hábito con nombre, periodicidad, cantidad objetivo y unidad. Nace en `Planificado`. |
| RF-NEG-02.2 | Consultar mis hábitos — `GET /api/habits` | Devuelve únicamente hábitos del usuario autenticado; nunca datos de otro propietario. |
| RF-NEG-02.3 | Cambiar estado — `POST /api/habits/{id}/transition` | Acepta solo las transiciones de la tabla y las condiciones de propietario/objetivo. Rechaza las demás sin cambiar el estado. |
| RF-NEG-02.4 | Registrar avance diario — `POST /api/habits/{id}/progress` | Registra una cantidad positiva en la unidad configurada para el hábito. Solo el propietario puede registrar avance y el hábito debe estar `Activo`. |
| RF-NEG-02.5 | Reconfigurar/resetear — `POST /api/habits/{id}/advanced-reconfig` | Cambia la periodicidad, cantidad objetivo o unidad del hábito. Requiere el permiso aplicado `Habits.AdvancedReconfig` para el mismo usuario. |

### Medición del avance y significado de completar

- Un hábito tiene periodicidad `Diaria` o `Semanal`, una cantidad objetivo positiva y una unidad (por ejemplo, páginas o minutos). La meta operativa del hábito no sustituye la entidad de largo plazo `Meta`.
- Cada registro de avance contiene una cantidad positiva y una fecha UTC; sus unidades deben coincidir con las del hábito.
- `CumpleMetaFinal` es verdadero cuando la suma de los avances del propietario para el período vigente alcanza o supera la cantidad objetivo. Para `Diaria`, el período es el día UTC; para `Semanal`, la semana ISO en UTC, de lunes a lunes.
- Solo se registra avance mientras el hábito está `Activo`. Solo se completa desde `Activo`, siendo su propietario y después de alcanzar la meta del período.

### Significado de reconfigurar/resetear

`advanced-reconfig` reemplaza la periodicidad, cantidad objetivo y/o unidad con valores válidos y empieza un nuevo ciclo de medición desde la fecha UTC de reconfiguración. El progreso histórico se conserva y puede consultarse; los registros anteriores al nuevo ciclo no cuentan para la meta nueva. La operación no cambia el estado de la máquina y se rechaza si el hábito está `Completado`. Esta semántica evita borrar el historial y respeta el estado terminal.

## 3. Integración real con Gestión de Permisos

El código del permiso es exactamente `Habits.AdvancedReconfig`.

1. Un usuario Estándar crea en Gestión de Permisos una solicitud para ese código. La solicitud nace `Pendiente`.
2. Mientras esté `Pendiente` o `Rechazada`, el propietario que invoque `POST /api/habits/{id}/advanced-reconfig` recibe un rechazo y el hábito no se reconfigura.
3. Un Administrador aprueba la solicitud. Gestión de Permisos la aplica automáticamente y queda efectiva para el solicitante.
4. Al reintentar la operación, Habit Tracker consulta el permiso efectivo para la misma identidad del solicitante y ejecuta la reconfiguración.

La prueba de integración usa el dominio, servicios y repositorios reales de ambos módulos, sin mocks/dobles que reemplacen ninguno y sin iniciar la API/servidor. Usa una base relacional de prueba si hace falta. Verifica tanto el rechazo antes de la aprobación como el éxito después. Debe fallar con el defecto 4.

Habit Tracker no acopla su máquina de estados ni su dominio a las clases internas de Permisos. La consulta de autorización cruza una interfaz/puerto del módulo y se conecta mediante la composición de dependencias.

## 4. Suite de pruebas

- Usar el framework del lenguaje y ejecutar sin levantar la aplicación completa (**RD-12**).
- Cada prueba es independiente, tiene fases Preparar–Actuar–Verificar (AAA), verifica un comportamiento y nombra lo que comprueba.
- Cubrir cada fila permitida y prohibida de las tablas anteriores. Las prohibidas verifican rechazo y estado intacto.
- Probar el rechazo de un no propietario en `Planificado → Activo`, el cumplimiento/incumplimiento de la meta en `Activo → Completado`, y que no se sale de `Completado`.
- Un solo comando documentado bajo `## Pruebas` en README debe ejecutar toda la suite desde un clon limpio; el comando previsto es `dotnet test`. Pasa sobre el tag base `practica-3`.

## 5. Defectos, commits y tags

En cada mutante cambia solo código de producción: ningún archivo de pruebas. Cada commit tiene como único padre el commit etiquetado `practica-3`; su mensaje identifica el archivo y la línea cambiados. Sobre el tag de cada defecto, el comando documentado falla al menos una prueba asociada. Sobre la base, todas pasan.

| Tag | Mutación de producción | Prueba que debe detectar el defecto |
|---|---|---|
| `practica-3-defecto-1` | Añadir a las transiciones permitidas la primera fila prohibida: `Planificado → Completado`. | La prueba de esa transición detecta que ahora se permite y que el estado ya no queda `Planificado`. |
| `practica-3-defecto-2` | Añadir una salida desde `Completado`, por ejemplo `Completado → Activo`. | La prueba del estado terminal detecta la salida. |
| `practica-3-defecto-3` | Quitar en la activación la comprobación de que `Habito.UsuarioId == usuarioActualId`. | La prueba intenta activar el hábito con un usuario distinto del propietario y debe fallar con la mutación. |
| `practica-3-defecto-4` | Omitir la consulta del permiso efectivo en `advanced-reconfig`. | La prueba de integración detecta que la acción se ejecuta sin permiso aplicado. |

El README incluye `## Defectos`, una fila por tag, requisito/archivo afectado, nombre de la prueba y comportamiento verificado. Si un mutante no hace fallar una prueba, se completa la suite primero y se vuelve a crear la base antes de plantar los tags.

## 6. Persistencia y compatibilidad del modelo existente

El proyecto actual persiste el enum como texto y usa `Pendiente`/`Abandonado`; la definición de esta práctica requiere `Planificado` y elimina `Abandonado`. Antes de la migración se consulta si existen hábitos persistidos. Se renombra `Pendiente` a `Planificado`. No se convierte `Abandonado` a `Completado` ni a otro estado porque no son semánticamente equivalentes: si hay filas heredadas, se conservan y se presenta una estrategia compatible antes de retirar ese valor del almacenamiento. No se descartan datos silenciosamente.

## 7. Revisión acumulada (2.0 puntos)

Siguiendo el README desde el punto etiquetado, comprobar los cinco frentes que pide el PDF:

1. Registro con activación por correo.
2. Inicio y cierre de sesión.
3. Rechazo a un usuario Estándar que invoca una operación de Administrador.
4. Recuperación de contraseña.
5. Gestión de Permisos: crear, aprobar y rechazar solicitudes, y rechazar transiciones fuera de tabla.

## 8. Entrega y revisión limpia

- Tag base: `practica-3`; toda la suite pasa.
- Tags de defectos: `practica-3-defecto-1` a `practica-3-defecto-4`.
- Entregar en Moodle la URL del repositorio y el nombre del tag base `practica-3`.
- En un clon limpio, ejecutar el README y `git checkout practica-3`; verificar el comando único.
- Para cada mutante, confirmar padre directo, comando fallido, mensaje de commit con archivo/línea y `git diff practica-3 practica-3-defecto-N --stat` sin pruebas.
- Comparar la tabla, el código, las pruebas, la sección `## Defectos` del README y la prueba real de integración.

## Rúbrica oficial — 8.0 puntos

Aplicar estos descriptores y puntajes del PDF oficial:

### 1. Módulo de negocio implementado RF-NEG-02 a RF-NEG-06 y RF-NEG-09 — 1.5 puntos

- **No logrado (0 %):** No hay operaciones del negocio funcionando, o se ejecutan sin sesión válida.
- **Parcialmente logrado (50 %):** Hay menos de cinco funcionalidades, alguna operación no exige el rol que le corresponde, o las transiciones no se resuelven en un solo punto.
- **Logrado (100 %):** Al menos cinco funcionalidades separables, protegidas por Control de Acceso, con estados y transiciones en un solo lugar, la prohibida y el estado terminal rechazados, y el README explica cómo provocar cada una.

### 2. Suite de la máquina de estados — 1.0 punto

- **No logrado (0 %):** No hay suite, no corre, requiere levantar la aplicación completa o falla en el tag.
- **Parcialmente logrado (50 %):** La suite corre y pasa, pero falta la prueba de alguna fila de las tablas o del estado terminal, alguna prueba depende del orden o no tiene tres fases identificables.
- **Logrado (100 %):** Un comando documentado ejecuta la suite desde un clon limpio y pasa; hay al menos una prueba por cada fila de las tablas y por el estado terminal.

### 3. Detección de los defectos 1 a 3 y su forma — 1.5 puntos

- **No logrado (0 %):** Con ningún defecto plantado falla una prueba, o no hay etiquetas de defecto.
- **Parcialmente logrado (50 %):** Falla una prueba con uno o dos de los tres defectos, o con los tres pero la forma no cumple: algún commit no cuelga de `practica-3`, toca pruebas, su mensaje no dice la línea o el README no identifica la prueba que lo detecta.
- **Logrado (100 %):** Con cada uno de los tres defectos falla al menos una prueba; sobre `practica-3` todas pasan; los commits cuelgan de `practica-3`, no tocan pruebas, sus mensajes dicen archivo y línea, y el README identifica la prueba que detecta cada uno.

### 4. Prueba de integración con Gestión de Permisos — 2.0 puntos

- **No logrado (0 %):** No hay prueba de integración, usa dobles en lugar de los componentes reales o requiere la aplicación levantada.
- **Parcialmente logrado (50 %):** Hay prueba real con las dos caras, pero con el defecto 4 plantado todas las pruebas pasan, o solo se verifica una de las dos caras.
- **Logrado (100 %):** La prueba usa los componentes reales, verifica el rechazo antes de aprobar y la ejecución después, y falla con el defecto 4 plantado (`practica-3-defecto-4`) con la forma pedida.

### 5. Revisión acumulada: Control de Acceso y Gestión de Permisos — 2.0 puntos

- **No logrado (0 %):** El proyecto no arranca siguiendo el README, o funciona uno de los cinco frentes o ninguno.
- **Parcialmente logrado (50 %):** Funcionan de dos a cuatro de los cinco frentes.
- **Logrado (100 %):** Funcionan los cinco frentes.

**Total: 8.0 puntos.**
