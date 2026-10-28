# 📊 Estado del Flujo de Trabajo (Core PIII)

Este archivo sirve como un tablero Kanban en texto para trackear el progreso del desarrollo.

## ✅ Pieza 1 (detalle)
* **Pieza 1: Control de Acceso (Semanas 2 a 4)**
  * [x] Definir entidad `Usuario` y `Rol` (Dominio).
  * [x] Implementar hasheo de contraseñas (Aplicación / Seguridad).
  * [x] Lógica de registro de usuario con correo único (RF-CA-01, RF-CA-02).
  * [x] Lógica de Login y generación de Token JWT (RF-CA-03).
  * [x] Restricciones de sesión (Activación, Bloqueo de 15 min, Blacklist de tokens) (RF-CA-07, RF-CA-15, RF-CA-18, RF-CA-19).
  * [x] Restricciones por roles (Administrador/Estándar) (RF-CA-04, RF-CA-05).
  * [x] Administración de usuarios: listar, cambiar rol/estado y revocación de sesiones (RF-CA-06, RF-CA-08, RF-CA-20, RF-CA-21).
  * [x] Solicitud de recuperación de contraseña sin revelar correos registrados (RF-CA-09).
  * [x] Generación de código de un solo uso con vencimiento y encolado de correo (RF-CA-10).
  * [x] Restablecimiento de contraseña y validación de política (RF-CA-11, RF-CA-14).
  * [x] Invalidación automática de sesiones previas al cambiar la clave (RF-CA-12).
  * [x] Restablecimiento forzado de contraseña por parte del Administrador (RF-CA-13).
  * [x] Cambio de contraseña propia con sesión activa exigiendo la contraseña actual (RF-CA-22).

## ⏳ Pendiente (Backlog)
* Pieza 3: Manejador de Documentos.
* Pieza 4: Notificaciones (la cola de correos ya existe dentro de Control de Acceso).
* Pieza 5: Reportes con Agregación.
* Pieza 6: Auditoría.
* Módulo de negocio: RF-NEG-07, RF-NEG-08 y RF-NEG-10 (fuera del alcance de la Práctica 3).

## ✅ Terminado
* Estructura base del Monolito Modular (Clean Architecture).
* Pieza 1: Control de Acceso (tag `practica-1`).
* Pieza 2: Gestión de Permisos con máquina de estados y 6 defectos plantados (tags `practica-2`, `practica-2-defecto-1..6`).
* Módulo de negocio Habit Tracker: 5 funcionalidades, máquina de estados, integración con Permisos y 4 defectos plantados (tags `practica-3`, `practica-3-defecto-1..4`).
