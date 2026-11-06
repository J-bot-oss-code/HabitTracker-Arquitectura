namespace AccessControl.Application.Interfaces;

public interface IServicioBajaCuentas
{
    /// <summary>
    /// Borra definitivamente las cuentas cuyo periodo de gracia terminó: primero los datos de cada módulo y, solo si todos lo
    /// lograron, la cuenta. Si algo falla, esa cuenta se reintenta en la siguiente pasada. Devuelve cuántas se borraron.
    /// </summary>
    Task<int> EjecutarAsync(DateTime ahora, CancellationToken cancelacion = default);
}
