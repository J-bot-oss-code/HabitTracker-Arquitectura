// Copia de dos contenidos del backend que GET /api/plans/{slug} no entrega y que Mis planes sí usa:
// los mensajes de motivación (mensajes-motivacion.json) y el tip extra de cada semana de la guía
// (catalogo-planes.json). La prueba features/mis-planes/misPlanes.test.ts los compara con los JSON del
// backend: si el contenido cambia allí, este archivo se regenera (no se edita a mano).

// CategoriaMensaje del backend: el tono depende del momento de la persona en su plan.
export type CategoriaMensaje = 'Inicio' | 'Constancia' | 'Recaida' | 'Hito' | 'Cierre' | 'Regreso'

export const MENSAJES_MOTIVACION: { categoria: CategoriaMensaje; texto: string }[] = [
  {
    categoria: 'Inicio',
    texto: 'Empezar es lo más difícil, y ya lo hiciste. Hoy basta con un primer paso pequeño.',
  },
  {
    categoria: 'Inicio',
    texto: 'Un hábito nuevo se construye de a poco. Hoy escoge lo más fácil y hazlo.',
  },
  {
    categoria: 'Inicio',
    texto: 'No tiene que ser perfecto. Que sea posible: eso es lo que cuenta al principio.',
  },
  {
    categoria: 'Inicio',
    texto: 'Tu plan está listo y tú también. Empieza con la tarea que más te guste.',
  },
  {
    categoria: 'Inicio',
    texto: 'Los primeros días son para probar. Ajustamos lo que haga falta, sin prisa.',
  },
  {
    categoria: 'Inicio',
    texto: 'Hoy empieza algo bueno para ti. Con la versión mínima también cuenta.',
  },
  {
    categoria: 'Constancia',
    texto: 'Cada día que cumples suma, aunque sea poquito. Sigue así, a tu ritmo.',
  },
  {
    categoria: 'Constancia',
    texto: 'Lo pequeño que repites cada día vale más que lo grande que haces una sola vez.',
  },
  {
    categoria: 'Constancia',
    texto: 'Ya le estás agarrando el ritmo. Un día a la vez, como vas.',
  },
  {
    categoria: 'Constancia',
    texto: 'Hoy toca otro pasito. Si el día está complicado, la versión mínima cuenta igual.',
  },
  {
    categoria: 'Constancia',
    texto: 'Tu constancia se nota. Respira, toma agua y sigue con lo tuyo.',
  },
  {
    categoria: 'Constancia',
    texto: 'Lo importante es volver cada día, y tú vuelves. Buen trabajo.',
  },
  {
    categoria: 'Recaida',
    texto: 'Qué bueno verte de vuelta. Retomar es parte del camino, y hoy lo estás haciendo.',
  },
  {
    categoria: 'Recaida',
    texto: 'Volver cuenta, y mucho. Hoy empezamos de nuevo, sin cuentas pendientes.',
  },
  {
    categoria: 'Recaida',
    texto: 'Un par de días de pausa no borran lo que ya avanzaste. Sigue desde aquí.',
  },
  {
    categoria: 'Recaida',
    texto: 'Retomar es de valientes. Hoy basta con lo mínimo para volver al ritmo.',
  },
  {
    categoria: 'Recaida',
    texto: 'Aquí estás otra vez, y eso es lo que importa. Poco a poco.',
  },
  {
    categoria: 'Hito',
    texto: '¡Llegaste a un hito! Detente un momento a reconocer cuánto has avanzado.',
  },
  {
    categoria: 'Hito',
    texto: 'Mira cuántos días has cumplido desde que empezaste. Eso lo hiciste tú.',
  },
  {
    categoria: 'Hito',
    texto: 'Otro tramo del camino cumplido. Celébralo a tu manera.',
  },
  {
    categoria: 'Hito',
    texto: 'Hoy puedes darte un buen descanso: la constancia también se cuida.',
  },
  {
    categoria: 'Hito',
    texto: 'Llevas un buen tramo recorrido. Lo que sigue es seguir, y ya sabes cómo.',
  },
  {
    categoria: 'Cierre',
    texto: 'Terminaste tu plan. Lo que construiste en estas semanas es tuyo y se queda contigo.',
  },
  {
    categoria: 'Cierre',
    texto: 'Llegaste al final. Mira atrás: cada día que cumpliste hizo posible este momento.',
  },
  {
    categoria: 'Cierre',
    texto: 'Plan completado. Descansa, celebra y, cuando quieras, escoge tu próximo paso.',
  },
  {
    categoria: 'Cierre',
    texto: 'Cerraste un ciclo con constancia. Gracias por dedicarte este tiempo.',
  },
  {
    categoria: 'Cierre',
    texto: 'Lo lograste: cumpliste el plan. Los hábitos que armaste ya son parte de ti.',
  },
  {
    categoria: 'Regreso',
    texto: 'Aquí seguimos cuando quieras. Hoy puedes retomar con la versión mínima, sin presión.',
  },
  {
    categoria: 'Regreso',
    texto: 'Pasa que la vida se complica. Cuando puedas, vuelve con un solo gesto pequeño.',
  },
  {
    categoria: 'Regreso',
    texto: 'Cuando quieras retomar, empieza por lo más fácil y ya verás cómo arranca el día.',
  },
  {
    categoria: 'Regreso',
    texto:
      'No hay prisa ni cuentas pendientes. Retomar hoy, con lo mínimo, ya es un buen comienzo.',
  },
  {
    categoria: 'Regreso',
    texto: 'Tu plan te espera tal como lo dejaste. Un paso pequeño hoy es suficiente.',
  },
  {
    categoria: 'Regreso',
    texto: 'Un mal rato no es el final. Cuando te sientas con ganas, aquí estamos.',
  },
]

// Por slug, el tip extra de cada semana (índice 0 = semana 1). Solo los planes con guía semanal.
export const TIPS_EXTRA: Record<string, (string | null)[]> = {
  'caminar-para-empezar': [
    'Deja los tenis junto a la puerta desde la noche anterior.',
    'Bájate una parada antes de la guagua o del carro público.',
    'Invita a alguien: caminar acompañado se hace más corto.',
    'Elige una ruta nueva para que no se vuelva aburrido.',
  ],
  'perder-grasa-sostenible': [
    'Ten una jarra de agua fría en la nevera: se toma más.',
    'Una ensalada de aguacate y tomate cuenta, y mucho.',
    'La chinola sin azúcar también refresca.',
    'Prepara la cena temprano los días que llegas tarde.',
    'Escribe qué te ayudó esta semana: te servirá después.',
    'Los guineítos verdes y la auyama llenan y rinden.',
    'Usa las escaleras siempre que puedas.',
    'Almorzar lejos de la pantalla ayuda a notar cuándo estás satisfecho.',
    'Deja la fruta a la vista en la cocina.',
    'Una caminata en familia el fin de semana suma.',
    'Elige el hábito que más te costó y dale una versión más fácil.',
    'Piensa cuál será tu siguiente plan.',
  ],
  'reducir-estres': [
    'Ponla después de algo que ya haces, como el primer café.',
    'Una pausa al aire libre, aunque sea en el parqueo, descansa más.',
    'Escribir la preocupación ayuda a soltarla hasta mañana.',
  ],
  'dormir-mejor': [
    'Pon una alarma para acostarte, no solo para levantarte.',
    'Toma el café en el balcón o en la galería.',
    'Un té de tilo o de manzanilla de noche reemplaza el ritual.',
    'Un cuarto fresco y oscuro ayuda tanto como el horario.',
  ],
  'primeros-5-km': [
    'Si puedes hablar mientras trotas, vas al ritmo correcto.',
    'Unos tenis cómodos importan más que unos caros.',
    'Toma agua antes de salir y al volver.',
    'Estira las piernas al terminar, nunca en frío.',
    'Busca una ruta plana y con sombra.',
    'Correr acompañado ayuda en las semanas difíciles.',
    'Si una salida no se pudo, repite la semana sin prisa.',
    'Celebra tu primera carrera de 5 km con alguien.',
  ],
  'ganar-masa-muscular': [
    'Pide a alguien del gimnasio que revise tu técnica.',
    'Anota la carga de cada ejercicio.',
    'Una botella de agua fría a mano ayuda a no saltar los descansos.',
    'Descansar también construye músculo.',
    'Duerme de 7 a 9 horas: ahí se recupera el músculo.',
    'Habichuelas con arroz son una buena fuente de proteína.',
    'Calienta 5 minutos antes de la primera serie.',
    'Revisa tus anotaciones: mira cuánto has subido.',
    'Respira al hacer la fuerza; no aguantes el aire.',
    'La técnica correcta vale más que una carga mayor.',
    'Huevo, pollo o queso en cada comida suman proteína.',
    'Elige tu siguiente plan para no perder el ritmo.',
  ],
  'fuerza-en-casa': [
    'Hazlo frente a un espejo para revisar la postura.',
    'Una toalla en el piso hace de colchoneta.',
    'Pon tu música favorita: 20 minutos son 5 canciones.',
    'Un galón de agua sirve de pesa para las sentadillas.',
    'Anota tus repeticiones para ver cuánto avanzaste.',
    'Repite la semana 1 y compara: notarás la diferencia.',
  ],
}
