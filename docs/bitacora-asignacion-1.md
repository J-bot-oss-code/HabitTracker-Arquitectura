# Bitácora - Asignación 1

## Datos de la entrega

- Proyecto: HabitTracker Nexus
- Asignatura: Programación III
- Entrega: Asignación 1, punto 1.4
- Fecha: 22 de septiembre de 2026

## Propósito

Registrar las decisiones y el avance inicial del proyecto, dejando evidencia de la estructura acordada antes de implementar las funcionalidades del Core.

## Actividades realizadas

### 1. Definición de la arquitectura

Se organizó la solución como un monolito modular basado en Clean Architecture y .NET 10. La separación busca que la lógica del dominio permanezca independiente de los detalles de infraestructura y de la interfaz web.

La solución incluye un host web (`Host/WebApi`) y módulos para Control de Acceso, Gestión de Hábitos, Permisos, Documentos, Notificaciones, Reportes y Auditoría. Cada módulo dispone de proyectos para Domain, Application e Infrastructure según corresponda.

### 2. Modelado inicial del dominio de negocio

Se creó la base del módulo HabitTracker con las entidades `Categoria`, `Etiqueta`, `Habito`, `Meta`, `Recompensa` y `RegistroDiario`. Estas entidades representan el vocabulario principal que utilizará el rastreador de hábitos.

### 3. Documentación del diseño

Se documentó la estructura y el progreso del proyecto en los archivos principales del repositorio. La documentación describe la arquitectura modular, el flujo de dependencias y el estado de los requisitos funcionales y transversales.

## Decisiones técnicas

| Decisión | Justificación |
| --- | --- |
| Usar .NET 10 | Mantener el proyecto sobre la versión de plataforma definida para la asignatura. |
| Aplicar Clean Architecture | Separar reglas de negocio, casos de uso, infraestructura y presentación. |
| Usar un monolito modular | Mantener un despliegue simple sin perder límites claros entre responsabilidades. |
| Mantener dependencias hacia el interior | Evitar que el dominio dependa de EF Core, ASP.NET u otros detalles externos. |

## Estado actual

La estructura base y el modelo inicial del dominio están disponibles. La implementación de los requisitos del Core continúa por módulos, empezando por Control de Acceso, y debe validarse contra los requisitos de diseño transversales antes de cada entrega.

## Próximos pasos

1. Completar los casos de uso de Control de Acceso.
2. Incorporar persistencia e infraestructura mediante contratos definidos en Application.
3. Añadir pruebas para cada pieza implementada.
4. Mantener actualizada esta bitácora con decisiones, avances y dificultades relevantes.
