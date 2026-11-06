namespace AccessControl.Application.Service;

internal static class FechasUtc
{
    /// <summary>SQL Server devuelve las fechas sin zona (Unspecified): se marcan como UTC para que el JSON lleve la "Z".</summary>
    public static DateTime? ComoUtc(DateTime? fecha) =>
        fecha is null ? null : DateTime.SpecifyKind(fecha.Value, DateTimeKind.Utc);

    public static DateTime ComoUtc(DateTime fecha) => DateTime.SpecifyKind(fecha, DateTimeKind.Utc);
}
