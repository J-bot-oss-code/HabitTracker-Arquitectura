# Control de Acceso y Gestión de Hábitos (Práctica 1)

## Requisitos Previos y Variables de Entorno
Para ejecutar este proyecto, necesitas configurar User Secrets o variables de entorno locales. **Nunca incluyas valores reales en el código fuente.**

*   `JwtSettings:Clave`: (String) Clave secreta larga para firmar los tokens JWT.
*   `JwtSettings:Issuer`: (String) Emisor del token (ej. `HabitTrackerApi`).
*   `JwtSettings:Audience`: (String) Audiencia del token (ej. `HabitTrackerUsers`).

## Instrucciones de Ejecución
1. Restaura las herramientas y paquetes: `dotnet restore`
2. Aplica las migraciones a tu base de datos local (SQL Server):
   `dotnet ef database update --project Infrastructure --startup-project WebApi`
3. Levanta la API:
   `dotnet run --project Host/WebApi`

## Guía de Pruebas (Criterios de Aceptación)
Sigue estas instrucciones desde Swagger o Postman para validar la rúbrica:

*   **Registro y rechazo controlado:** Usa `POST /api/auth/register` con una clave menor a 8 caracteres o sin números. Recibirás un error 400 controlado.
*   **Encolado de correos (Sin SMTP):** Si el servidor SMTP no está configurado o falla, el registro (201 Created) y la recuperación de clave terminan con éxito. Los correos quedan pendientes en la tabla/cola interna.
*   **Activación y un solo uso:** Tras registrarte, usa el token simulado en `POST /api/auth/activate`. Si lo envías dos veces, la segunda vez será rechazado.
*   **Seguridad de enumeración (Recuperación):** Llama a `POST /api/auth/recuperar-password` con un correo falso y luego con el verdadero. El sistema devolverá un `200 OK` idéntico y en el mismo tiempo.
*   **Bloqueo de fuerza bruta (15 min):** Llama a `POST /api/auth/login` con clave incorrecta 5 veces seguidas. Al 6to intento, recibirás un error de bloqueo temporal.
*   **Invalidación de sesión (Logout / Cambio de Rol/Clave):** Inicia sesión, guarda el JWT. Llama a `POST /api/auth/logout`. Vuelve a intentar usar el JWT en `GET /api/auth/me`; recibirás un `401 Unauthorized`.
*   **Protección por Roles:** Como usuario Estándar, intenta invocar `GET /api/admin/usuarios` (construyendo la petición manualmente si es necesario). El sistema devolverá `403 Forbidden`.
*   **Desactivación y Autodesactivación:** Como Administrador, intenta desactivarte a ti mismo en `PATCH /api/admin/usuarios/{tu_id}/estado`. El sistema lo rechazará. Desactiva a otro usuario y sus sesiones abiertas morirán instantáneamente.

---
*Documentación de la Máquina de Estados disponible en `docs/maquina-de-estados.md`.*
