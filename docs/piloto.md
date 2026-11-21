# Piloto de HabitTracker Nexus

Plan del piloto (Fase 6 del plan v2). Su informe y la decisión que salga de él son la condición para empezar las Fases 7 a 10 (`docs/plan-habittracker-v2.md`).

## Alcance

- **Quién:** entre 10 y 20 personas conocidas, invitadas una a una.
- **Qué prueban:** el flujo completo: registro, activación desde el correo, contar su jornada, elegir un plan, el check-in diario y los recordatorios por correo (con «Ya lo hice»).
- **Cuánto dura:** 4 semanas (suficiente para ver la retención a 30 días).
- **Dónde:** el VPS de `docs/produccion.md`, con el correo real y los secretos propios.

## Antes de invitar a nadie

1. El **consentimiento** informado de cada persona, por escrito (Ley 172-13 de protección de datos): qué se guarda, que los datos de salud van cifrados y que pueden descargar o borrar todo desde Configuración › Privacidad y datos.
2. Los respaldos programados y **una restauración probada** (`scripts/despliegue/restaurar.sh`).
3. El monitor externo por palabra clave `Healthy` sobre `/health`.
4. Una prueba de navegador con Edge contra el sistema real, y el escaneo de seguridad automático (`docs/informe-produccion.md`).

## Qué se mira (y qué no)

Todo sale del Resumen del Administrador (`GET /api/admin/metrics`): cifras agregadas, sin nombres ni datos de salud.

| Pregunta | Dónde se ve |
|---|---|
| ¿Entra gente y se queda? | Personas activas a 30 días y **retención a 30 días** |
| ¿Usan el correo? | Registros por correo de los últimos 7 días |
| ¿Qué planes les interesan? | Planes más iniciados |
| ¿Qué les molesta o qué mejorarían? | **Quejas y mejoras** (bandeja del Administrador) |
| ¿Algo se rompió? | Cola de correos (fallidos) y `/health` |

**Importante:** por privacidad, lo que cuenta personas **se oculta con menos de 5**. Con un piloto de 10 a 20 personas, algunas cifras (retención, personas activas) aparecerán como «—» al principio; es la regla, no un error. Las quejas y mejoras, las cuentas nuevas y los correos fallidos siempre se ven.

**No se mide el cuerpo de nadie**, no se compara a las personas entre sí y no hay rankings.

## Cómo se decide

Al final de las 4 semanas se escribe un informe corto (qué pasó, qué dijeron las personas, qué cifras salieron) y se decide con estos criterios:

- **Seguir a las Fases 7 a 10 si:** la mayoría de las personas hizo check-in más de la mitad de las semanas, el correo no generó quejas de ruido y no hubo incidentes de seguridad ni de datos.
- **Corregir primero si:** las quejas apuntan a un problema de uso (entender el plan, el onboarding, la hora de los recordatorios) o la retención cae mucho tras la primera semana.
- **Detener si:** hubo cualquier fuga o pérdida de datos. Se avisa a las personas, se corrige y se documenta antes de continuar.

## Si algo sale mal

- **Se cae la API:** `docker compose -f docker-compose.prod.yml ps`, revisar los logs y reiniciar (`docs/produccion.md`).
- **Los recordatorios se detienen:** `/health` responde `Degraded`; reiniciar la API y revisar el log del proceso de recordatorios. Se pueden apagar con `RECORDATORIOS_HABILITADO=false` en el `.env` (y `docker compose up -d`).
- **Una persona quiere salir:** Configuración › Privacidad y datos (descarga y baja, con 30 días de gracia).
