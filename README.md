# 🎯 HabitTracker Nexus - Sistema Académico Distribuido

Plataforma de alta fidelidad para el registro de hábitos diarios, organización de metas y trazabilidad del progreso a lo largo del tiempo. Proyecto desarrollado siguiendo los principios de **Clean Architecture** y **Domain-Driven Design (DDD)**.

## 🏗️ Arquitectura del Sistema

El sistema está dividido en dos grandes bloques con un acoplamiento estrictamente controlado:

1. **CORE (Servicios Transversales y Fundación)**
   * Control de Acceso (Autenticación y Roles).
   * Gestión de Permisos (Máquina de estados de aprobación).
   * Manejador de Documentos (Soft delete y persistencia física).
   * Reportes, Notificaciones y Auditoría.

2. **MÓDULO DE NEGOCIO (Rastreador de Hábitos)**
   * Gestión de Metas (Máquina de estados: Planificada, Activa, Pausada, Alcanzada).
   * Gestión de Categorías y Hábitos.
   * Registro transaccional diario de actividades.

## 💻 Stack Tecnológico
* **Framework:** .NET 10 (Web API)
* **Lenguaje:** C#
* **Arquitectura:** Clean Architecture
* **ORM:** Entity Framework Core
* **Seguridad:** JWT (JSON Web Tokens) & Hashing