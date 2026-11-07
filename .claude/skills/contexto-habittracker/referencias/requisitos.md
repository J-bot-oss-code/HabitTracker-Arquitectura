# Requisitos (resumen de los PDF)

> Fuentes: `docs/requeriments/Requerimientos_Core_PIII.pdf` (Core) y `docs/requeriments/Practica1_ControlAcceso_PIII.pdf` (RF-CA-14 a 22). Una línea por requisito: «qué pide → cómo se comprueba». Para el texto exacto, `pdftotext -raw -enc UTF-8 <pdf> <salida>` (el lector de PDF no los renderiza).

## Cómo usar los IDs

- Cada commit y cada PR citan los IDs que tocan, con **2 dígitos exactos** (`RF-NEG-02`, nunca `RF-NEG-02.1`).
- **RD** = diseño (todo el sistema). **RF** = funcional, por pieza. Si no pasa el criterio, el requisito no está hecho.
- No se inventan IDs. Para funcionalidades nuevas del negocio: RF-NEG-01 (entidades), RF-NEG-02 (funcionalidades), RF-NEG-03/04/05 (máquina), RF-NEG-06 (acceso), RF-NEG-07 + RF-NOT-03 (notificación del dominio), RF-NEG-08 + RF-REP-02 (reporte).

## RD — diseño transversal

| ID | Pide → criterio |
|---|---|
| RD-01 | Cada pieza: responsabilidad única e interfaz explícita → se describe sin explicar el interior de otra |
| RD-02 | Lógica de negocio fuera de la presentación y del manejador HTTP → cambiar la UI no reescribe reglas |
| RD-03 | El Core no depende del negocio → sin el módulo de negocio, el Core compila y corre |
| RD-04 | Transiciones de estado en un solo punto → una transición nueva toca un único componente |
| RD-05 | Contraseñas con hash → leyendo la BD no se recupera ninguna |
| RD-06 | Autorización en el servidor en cada operación → llamar la operación a mano también se rechaza |
| RD-07 | Toda entrada externa validada antes de usarse → datos ausentes, mal tipados o fuera de rango dan rechazo controlado |
| RD-08 | Errores comprensibles sin detalles internos → sin trazas, rutas ni consultas |
| RD-09 | Datos fuera del proceso → reiniciar no pierde información |
| RD-10 | Credenciales en variables de entorno → nada en el repo ni en el historial |
| RD-11 | Mismo criterio de fecha y hora en todo el sistema → dos registros del mismo instante muestran la misma hora |
| RD-12 | Cada pieza se prueba sin levantar la app → al menos una prueba sin UI |

## RF-CA — Control de acceso (Pieza 1)

| ID | Pide → criterio |
|---|---|
| RF-CA-01 | Registro con correo único → el segundo registro con el mismo correo se rechaza |
| RF-CA-02 | Contraseña con hash → el valor guardado no coincide ni se revierte |
| RF-CA-03 | Login que entrega credencial → con datos incorrectos no revela cuál falló |
| RF-CA-04 | Dos roles, Administrador y Estándar → cada usuario tiene exactamente uno |
| RF-CA-05 | Cada operación declara su rol → hay un punto del código donde se lee |
| RF-CA-06 | Estándar que invoca operación de Administrador → rechazo explícito, también a mano |
| RF-CA-07 | Consulta del usuario autenticado y su rol → sin sesión se rechaza |
| RF-CA-08 | Cambio de rol solo por Administrador → un Estándar no cambia ninguno; auditado (RF-AUD-04) |
| RF-CA-09 | Recuperación con el correo → respuesta idéntica exista o no |
| RF-CA-10 | Código de un solo uso con vencimiento, por la cola → reutilizado o vencido se rechaza |
| RF-CA-11 | Con código válido, contraseña nueva con hash → la anterior deja de servir |
| RF-CA-12 | Sesiones previas al cambio de contraseña dejan de valer → la credencial vieja se rechaza |
| RF-CA-13 | Administrador fuerza el restablecimiento → la contraseña anterior no sirve; auditado (RF-AUD-05) |
| RF-CA-14 | Política mínima: 8+ caracteres con letras y números → rechazo controlado en registro y cambios |
| RF-CA-15 | El usuario nace inactivo y recibe un enlace/código de activación de un solo uso con vencimiento |
| RF-CA-16 | Activar habilita el login → usar el enlace dos veces o vencido se rechaza |
| RF-CA-17 | Reenvío de la activación por correo → respuesta idéntica exista o no |
| RF-CA-18 | Cierre de sesión → la credencial cerrada se rechaza |
| RF-CA-19 | 5 intentos fallidos seguidos bloquean 15 min → el sexto, aun correcto, se rechaza; un login correcto pone el contador en cero |
| RF-CA-20 | Administrador desactiva y reactiva usuarios → el desactivado no entra y pierde sus sesiones; no puede desactivarse a sí mismo |
| RF-CA-21 | Administrador lista usuarios con rol y estado → Estándar rechazado; sin hashes ni tokens |
| RF-CA-22 | Usuario con sesión cambia su contraseña dando la actual |

## RF-GP — Gestión de permisos (Pieza 2)

Máquina: `Pendiente → Aprobada → Aplicada` (automática), `Pendiente → Rechazada` (con motivo). Cualquier otra, prohibida.

| ID | Pide → criterio |
|---|---|
| RF-GP-01 | Estándar crea solicitud → nace Pendiente |
| RF-GP-02 | Administrador consulta pendientes → un Estándar no ve las de otros |
| RF-GP-03 | Administrador aprueba → queda quién y cuándo |
| RF-GP-04 | Administrador rechaza con motivo → sin motivo no se completa |
| RF-GP-05 | Al aprobar se aplica el permiso solo → el solicitante ya puede ejecutar la acción |
| RF-GP-06 | Solo las transiciones de la tabla → aprobar dos veces o Rechazada→Aplicada se rechaza |
| RF-GP-07 | Nadie resuelve su propia solicitud |

## RF-DOC — Manejador de documentos (Pieza 3)

| ID | Pide → criterio |
|---|---|
| RF-DOC-01 | Subir con nombre, tipo, tamaño, fecha y autor → consultable con esos datos |
| RF-DOC-02 | Listar los permitidos → sin eliminados ni ajenos (salvo Administrador) |
| RF-DOC-03 | Descargar → solo el dueño o un Administrador |
| RF-DOC-04 | Eliminar de forma lógica → no aparece, pero el registro sigue |
| RF-DOC-05 | Archivo en volumen persistente → recrear el contenedor no lo pierde |
| RF-DOC-06 | Subidas y eliminaciones auditadas (RF-AUD-03) |

## RF-NOT — Notificaciones y cola de correos (Pieza 4)

| ID | Pide → criterio |
|---|---|
| RF-NOT-01 | Crear una solicitud de permiso notifica a todos los Administradores y a nadie más |
| RF-NOT-02 | Resolver notifica al solicitante (aprobada o rechazada, con motivo) |
| RF-NOT-03 | Un evento propio del negocio dispara una notificación → descrito en la documentación y provocable en la demo |
| RF-NOT-04 | El usuario consulta sus notificaciones (leídas/no leídas), nunca las ajenas |
| RF-NOT-05 | Marcar como leída; marcar una ajena se rechaza |
| RF-NOT-06 | Toda notificación por dos canales: interno y correo |
| RF-NOT-07 | Desactivar el correo sin perder el canal interno → sigue apareciendo y no se encola correo |
| RF-NOT-08 | Los correos se encolan, no se envían en la operación → la operación funciona con el servidor caído |
| RF-NOT-09 | Un proceso independiente envía los pendientes |
| RF-NOT-10 | Reintentos limitados y luego fallido; nunca infinitos ni perdidos sin rastro |
| RF-NOT-11 | Cada correo guarda estado, intentos, fechas y último error; el Administrador consulta la cola |
| RF-NOT-12 | Un correo enviado no se reenvía; procesar dos veces no duplica |
| RF-NOT-13 | Credenciales SMTP en variables de entorno (RD-10) |

## RF-REP — Reportes (Pieza 5)

| ID | Pide → criterio |
|---|---|
| RF-REP-01 | Reporte del Core (permisos por estado y período, o documentos por usuario) → valores agrupados |
| RF-REP-02 | Reporte del negocio → responde una pregunta real del dominio |
| RF-REP-03 | Agregación real → menos filas que los registros resumidos |
| RF-REP-04 | Respetan el rol → un Estándar no ve datos ajenos |
| RF-REP-05 | Filtro por rango de fechas → rangos distintos dan resultados distintos |

## RF-AUD — Auditoría (Pieza 6)

| ID | Pide → criterio |
|---|---|
| RF-AUD-01 | Cada registro: usuario, acción, entidad, identificador, fecha y hora, valor anterior y nuevo |
| RF-AUD-02 | Se audita toda resolución de permiso |
| RF-AUD-03 | Se audita toda subida y eliminación de documento |
| RF-AUD-04 | Se audita todo cambio de rol (anterior y nuevo) |
| RF-AUD-05 | Se audita todo restablecimiento de contraseña (propio o forzado), sin la contraseña ni el código |
| RF-AUD-06 | La auditoría no se edita ni se borra desde la aplicación |
| RF-AUD-07 | Consulta filtrable por usuario, entidad y fechas; solo Administrador |

## RF-NEG — Módulo de negocio

| ID | Pide → criterio |
|---|---|
| RF-NEG-01 | Al menos 4 entidades relacionadas → las relaciones existen en el modelo de datos |
| RF-NEG-02 | Al menos 5 funcionalidades separables → cada una se prueba por separado |
| RF-NEG-03 | Máquina propia de 3 a 5 estados → declarados en un solo lugar |
| RF-NEG-04 | Al menos una transición prohibida explícita → se rechaza y el estado no cambia |
| RF-NEG-05 | Al menos un estado terminal → ninguna transición sale de él |
| RF-NEG-06 | Operaciones protegidas por Control de acceso → sin sesión y rol no se ejecutan |
| RF-NEG-07 | Dispara al menos una notificación propia (cumple RF-NOT-03) |
| RF-NEG-08 | Alimenta al menos un reporte con agregación (cumple RF-REP-02) |
| RF-NEG-09 | Su máquina es independiente de la de Permisos → cambiar una no obliga a cambiar la otra |
| RF-NEG-10 | Adjuntar documentos a una entidad del negocio es opcional; si se hace, cumple la Pieza 3 |

Estado: RF-NEG-01..06 y 09 hechos (P3). RF-NEG-07, 08 y 10 pendientes.
