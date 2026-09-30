# Máquina de Estados: Hábito (Módulo de Negocio)

La entidad central del negocio es el `Habito`. Sus estados y transiciones se gestionan de manera centralizada en el dominio mediante la clase `TransicionesHabito`.

## Tabla de Transiciones

| Desde | Hacia | Quién la ejecuta | Condición |
| :--- | :--- | :--- | :--- |
| Pendiente | Activo | Usuario | El usuario decide iniciar el seguimiento del hábito. |
| Activo | Pausado | Usuario | El usuario suspende temporalmente el hábito. |
| Pausado | Activo | Usuario | El usuario retoma el hábito. |
| Activo | Completado | Usuario | Se alcanza la meta y se finaliza con éxito (Estado Terminal). |
| Activo | Abandonado | Usuario | El usuario desiste de continuar (Estado Terminal). |
| Pausado | Abandonado | Usuario | El usuario decide no retomarlo y desiste (Estado Terminal). |
| **Completado** | *Cualquiera* | *Nadie* | **PROHIBIDA:** Un hábito completado es un estado terminal inmutable. |
| **Abandonado** | *Cualquiera* | *Nadie* | **PROHIBIDA:** Un hábito abandonado es un estado terminal inmutable. |
