# 📊 Seguimiento de Proyecto — HabitTracker Nexus

## Estado General

**Proyecto:** Programación III · ITLA · 2026-C-3  
**Fecha de inicio:** Semana 2  
**Arquitectura:** Monolito Modular · Clean Architecture · .NET 10  
**Rama activa:** `develop`  

---

## Checklist de Requisitos del Core (Transversales)

| RD | Requisito | Estado | Evidencia |
|----|-----------|--------|-----------|
| RD-01 | Componentes con responsabilidad única | 🔲 Pendiente | — |
| RD-02 | Lógica de negocio en capa de servicio/dominio | 🔲 Pendiente | — |
| RD-03 | Core no depende del módulo de negocio | 🔲 Pendiente | — |
| RD-04 | Transiciones de estado en un único punto | 🔲 Pendiente | — |
| RD-05 | Contraseñas con hash (bcrypt/argon2) | 🔲 Pendiente | — |
| RD-06 | Autorización verificada en servidor | 🔲 Pendiente | — |
| RD-07 | Entradas validadas antes de BD/lógica | 🔲 Pendiente | — |
| RD-08 | Errores genéricos sin trazas de pila | 🔲 Pendiente | — |
| RD-09 | Datos persisten en almacenamiento externo | 🔲 Pendiente | — |
| RD-10 | Credenciales en variables de entorno | 🔲 Pendiente | — |
| RD-11 | Fechas con DateTime.UtcNow | 🔲 Pendiente | — |
| RD-12 | Cada pieza con al menos una prueba unitaria | 🔲 Pendiente | — |

---

## Piezas del Core — Progreso

### Pieza 1 — Control de Acceso (Semanas 2-4)
- [ ] RF-CA-01: Registro con correo único
- [ ] RF-CA-02: Contraseña con hash
- [ ] RF-CA-03: Inicio de sesión
- [ ] RF-CA-04: Dos roles (Administrador/Estándar)
- [ ] RF-CA-05: Cada operación declara rol requerido
- [ ] RF-CA-06: Rechazo para operaciones de Administrador
- [ ] RF-CA-07: Consulta de usuario autenticado
- [ ] RF-CA-08: Cambio de rol reservado a Administrador
- [ ] RF-CA-09: Recuperación de contraseña
- [ ] RF-CA-10: Código de un solo uso con vencimiento
- [ ] RF-CA-11: Restablecimiento con nueva contraseña
- [ ] RF-CA-12: Sesiones anteriores inválidas
- [ ] RF-CA-13: Restablecimiento forzado por Administrador
- [ ] **Módulo AccessControl creado** (Domain, Application, Infrastructure) ✅
- [ ] **Agente:** `core-transversal-reviewer` ✅
- [ ] **Agente:** `net-clean-architecture-reviewer` ✅

### Pieza 2 — Gestión de Permisos (Semanas 6-8)
- [ ] RF-GP-01: Solicitud de permiso
- [ ] RF-GP-02: Consulta de solicitudes pendientes
- [ ] RF-GP-03: Aprobación de solicitud
- [ ] RF-GP-04: Rechazo con motivo escrito
- [ ] RF-GP-05: Aplicación automática de cambio de permiso
- [ ] RF-GP-06: Solo transiciones permitidas
- [ ] RF-GP-07: Nadie resuelve su propia solicitud
- [ ] **Módulo Permissions creado** ✅

### Pieza 3 — Manejador de Documentos (Semana 9)
- [ ] RF-DOC-01: Subir documento con metadatos
- [ ] RF-DOC-02: Listar documentos permitidos
- [ ] RF-DOC-03: Descargar (solo dueño o Admin)
- [ ] RF-DOC-04: Eliminación lógica
- [ ] RF-DOC-05: Almacenamiento persistente fuera del contenedor
- [ ] RF-DOC-06: Auditoría de subidas/eliminaciones
- [ ] **Módulo DocumentManager creado** ✅

### Pieza 4 — Notificaciones (Semanas 11-12)
- [ ] RF-NOT-01: Notificar a Administradores al crear solicitud
- [ ] RF-NOT-02: Notificar resultado al solicitante
- [ ] RF-NOT-03: Evento propio del módulo de negocio
- [ ] RF-NOT-04: Consultar notificaciones propias
- [ ] RF-NOT-05: Marcar como leída
- [ ] RF-NOT-06: Dos canales (interno + correo)
- [ ] RF-NOT-07: Desactivar canal de correo
- [ ] RF-NOT-08: Correos encolados
- [ ] RF-NOT-09: Procesador independiente
- [ ] RF-NOT-10: Reintento limitado
- [ ] RF-NOT-11: Estado del correo con intentos y errores
- [ ] RF-NOT-12: Sin duplicación de envíos
- [ ] RF-NOT-13: Credenciales en variables de entorno
- [ ] **Módulo Notifications creado** ✅

### Pieza 5 — Reportes (Semana 12)
- [ ] RF-REP-01: Reporte sobre el Core (agregación)
- [ ] RF-REP-02: Reporte sobre el módulo de negocio
- [ ] RF-REP-03: Agregaciones reales
- [ ] RF-REP-04: Respeto de rol en consultas
- [ ] RF-REP-05: Filtro por rango de fechas
- [ ] **Módulo Reports creado** ✅

### Pieza 6 — Auditoría (Semana 14)
- [ ] RF-AUD-01: Registro completo de auditoría
- [ ] RF-AUD-02: Auditoría de resoluciones de permisos
- [ ] RF-AUD-03: Auditoría de documentos
- [ ] RF-AUD-04: Auditoría de cambios de rol
- [ ] RF-AUD-05: Auditoría de restablecimiento de contraseña
- [ ] RF-AUD-06: Registro inmutable
- [ ] RF-AUD-07: Consulta filtrada (Admin)
- [ ] **Módulo Auditing creado** ✅

### Módulo de Negocio — Rastreador de Hábitos
- [ ] RF-NEG-01: Al menos 4 entidades relacionadas ✅ (6 creadas)
- [ ] RF-NEG-02: Al menos 5 funcionalidades separables
- [ ] RF-NEG-03: Máquina de estados propia (3-5 estados)
- [ ] RF-NEG-04: Al menos una transición prohibida explícita
- [ ] RF-NEG-05: Al menos un estado terminal
- [ ] RF-NEG-06: Operaciones protegidas por Control de Acceso
- [ ] RF-NEG-07: Al menos una notificación propia del dominio
- [ ] RF-NEG-08: Alimenta al menos un reporte con agregación
- [ ] RF-NEG-09: Máquina de estados independiente
- [ ] RF-NEG-10: Adjuntar documentos (opcional)
- [ ] **Entidades creadas:** Categoria, Meta, Habito, RegistroDiario, Etiqueta, Recompensa ✅
- [ ] **Módulo HabitTracker creado** ✅

---

## Cronograma

| Semana | Actividad | Estado |
|--------|-----------|--------|
| 2-4 | Pieza 1: Control de Acceso | 🔲 Pendiente |
| 4 | Máquina de estados del negocio (estructura) | 🔲 Pendiente |
| 6-8 | Pieza 2: Gestión de Permisos | 🔲 Pendiente |
| 8 | Máquina de estados del negocio (pruebas) | 🔲 Pendiente |
| 9 | Pieza 3: Manejador de Documentos | 🔲 Pendiente |
| 11-12 | Pieza 4: Notificaciones | 🔲 Pendiente |
| 12 | Pieza 5: Reportes | 🔲 Pendiente |
| 14 | Pieza 6: Auditoría | 🔲 Pendiente |
| 15 | Proyecto Final | 🔲 Pendiente |

---

## Agentes Disponibles

| Agente | Descripción | Archivo |
|--------|-------------|---------|
| `core-transversal-reviewer` | Auditor de RD-01 a RD-12 | `.github/copilot/agents/core-transversal-reviewer.agent.md` |
| `net-clean-architecture-reviewer` | Revisor de bugs y buenas prácticas | `.github/copilot/agents/net-clean-architecture-reviewer.agent.md` |
| `net-domain-usecase-expert` | Experto en dominios y casos de uso | `.github/copilot/agents/net-domain-usecase-expert.agent.md` |
| `net-infrastructure-expert` | Experto en infraestructura | `.github/copilot/agents/net-infrastructure-expert.agent.md` |
| `net-security-api-expert` | Experto en seguridad y API | `.github/copilot/agents/net-security-api-expert.agent.md` |
| `junior-code-tutor` | Mentor pedagógico para código | `.github/copilot/agents/junior-code-tutor.agent.md` ✅ |
| `implementation-flow-guide` | Guía de flujo de implementación | `.github/copilot/agents/implementation-flow-guide.agent.md` ✅ |
