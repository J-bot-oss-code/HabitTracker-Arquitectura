namespace HabitTracker.Application.Exceptions;

/// <summary>
/// El enlace del correo no sirve: es inválido, está alterado, caducó, o la cuenta, el plan o el hábito ya no están activos.
/// El mensaje es siempre el mismo para no decir cuál de las razones fue (RD-08); la API responde 400 <c>enlace_no_valido</c>.
/// </summary>
public sealed class EnlaceNoValidoException() : Exception("El enlace no es válido o ya caducó.");
