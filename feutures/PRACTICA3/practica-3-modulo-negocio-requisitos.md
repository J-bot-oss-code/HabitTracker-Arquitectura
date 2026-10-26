### 🎯 Objetivo General de la Práctica 3

En esta práctica vas a poner a funcionar la máquina de estados de tu **módulo de negocio propio** (en tu caso, Habit Tracker), crear la **suite de pruebas unitarias que protege su máquina de estados**, plantar **4 defectos en tu propio código**, construir la **prueba de integración entre tu negocio y Gestión de Permisos**, y mantener la **revisión acumulada** de Control de Acceso y Permisos[1][2].

---

### 📋 Los 5 Componentes de la Práctica 3

#### 1\. Tu Módulo de Negocio Funcionando (RF-NEG-02 a 06 y 09 — 1.5 pts)[3][4]

* **RF-NEG-02**: Al menos **5 funcionalidades separables**. Cada una debe ser probable por separado y el `README.md` debe explicar cómo provocarla[3].
* **RF-NEG-03**: Máquina de estados propia con **entre 3 y 5 estados**, declarados en un solo lugar del código y reflejados en `docs/maquina-de-estados.md`[3].
* **RF-NEG-04**: Al menos una **transición prohibida explícita** (intentarla se rechaza y el estado no cambia)[3].
* **RF-NEG-05**: Al menos un **estado terminal** del que no se puede salir (ninguna transición parte de él)[3][5].
* **RF-NEG-06**: Todas sus operaciones protegidas por Control de Acceso (requieren sesión válida y verificación de rol)[5].
* **RF-NEG-09**: Máquina de estados **independiente** de la de Gestión de Permisos[5].
* **Conexión obligatoria con Permisos**: Al menos una operación de tu negocio debe requerir un permiso concedido por Gestión de Permisos (un usuario Estándar solicita el permiso, un Administrador lo aprueba y solo entonces se puede ejecutar la acción en el negocio)[6].

---

#### 2\. Suite de Pruebas de tu Máquina de Estados (1.0 pt)[6]

* Pruebas unitarias sin levantar la aplicación completa (`RD-12`), independientes entre sí y estructuradas en las tres fases AAA (Preparar–Actuar–Verificar)[6][7].
* **Cobertura completa**: Al menos una prueba por cada fila de las tablas de transiciones permitidas y prohibidas de tu `docs/maquina-de-estados.md`[7].
* Prueba explícita que compruebe que **del estado terminal no se puede salir**[7].
* Ejecutable mediante un **único comando** documentado en la sección `## Pruebas` del `README.md` (ej. `dotnet test`)[7].

---

#### 3\. Cuatro Defectos Plantados en tu Propio Código (1.5 pts)[9]

A diferencia de la Práctica 2 (donde los defectos venían prefijados por tu matrícula), en la Práctica 3 **los defectos se diseñan sobre tus propias tablas de negocio**[9]:

1. **Defecto 1**: Agregar a las transiciones permitidas la primera transición de la tabla de prohibidas de tu `docs/maquina-de-estados.md` *(Efecto: Una transición prohibida pasa a ser permitida)*[10].
2. **Defecto 2**: Agregar una transición que parta de tu estado terminal *(Efecto: El estado terminal deja de ser terminal)*[10].
3. **Defecto 3**: Eliminar la verificación de la condición de la primera transición que tenga condición (ej. quién la ejecuta) *(Efecto: La transición se ejecuta sin cumplir su condición)*[10].
4. **Defecto 4**: En la operación de tu negocio que depende de un permiso de Gestión de Permisos, eliminar la consulta a Gestión de Permisos *(Efecto: La operación se ejecuta aunque el permiso no esté aprobado.* **Este defecto lo detecta tu prueba de integración** *)*[10].

**Reglas para los 4 tags (** **practica-3-defecto-1** **a** **practica-3-defecto-4** **)**[11][13]:

* Cada commit debe colgar directamente de `practica-3` como su padre[11].
* Modifica **únicamente código de producción** (ningún archivo de pruebas)[11].
* El mensaje del commit debe indicar el **archivo y línea modificada**[11].
* El `README.md` debe incluir la sección `## Defectos` asociando cada tag con la prueba que lo detecta[13].

---

#### 4\. La Prueba de Integración con Gestión de Permisos (2.0 pts)[13]

* Una prueba unitaria que conecte **tu módulo de negocio real con Gestión de Permisos real** (sin dobles/mocks y sin levantar la API/servidor)[13][15].
* **Verifica las dos caras**:
  1. La operación del negocio **se rechaza** mientras la solicitud de permiso NO está aprobada[14][15].
  2. La operación del negocio **se permite** después de que un Administrador aprueba la solicitud[14][15].
* Debe **fallar cuando se planta el Defecto 4** (`practica-3-defecto-4`)[13][15].

---

#### 5\. Revisión Acumulada (2.0 pts)[14]

Al arrancar tu API desde el `README.md`, el profesor comprobará los **5 frentes activos**[14]:

1. Control de Acceso: Registro con activación por correo.
2. Control de Acceso: Inicio y cierre de sesión.
3. Control de Acceso: Rechazo a usuario Estándar que invoca operación de Administrador (403 Forbidden).
4. Control de Acceso: Recuperación de contraseña.
5. Gestión de Permisos: Creación, aprobación, rechazo de solicitudes y rechazo de transiciones prohibidas.

---

### 📦 Entregables y Tags Git[16]

* **Tag base**: `practica-3` (donde **100% de las pruebas pasa**).
* **Tags de defectos**: `practica-3-defecto-1`, `practica-3-defecto-2`, `practica-3-defecto-3`, `practica-3-defecto-4`.
* **Moodle**: Entregar la URL del repositorio y la etiqueta `practica-3`.