using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace HabitTracker.Infrastructure.Persistence;

/// <summary>
/// Reconoce el error de "restricción única violada" de los proveedores que usa el módulo: SQL Server (2601 índice único y
/// 2627 restricción única) y SQLite (las pruebas). Lo demás se deja pasar: no se traga un error que no se entiende.
/// </summary>
internal static class ViolacionesDeUnicidad
{
    public static bool Es(Exception? excepcion)
    {
        for (var actual = excepcion; actual is not null; actual = actual.InnerException)
        {
            if (actual is SqlException sql && sql.Number is 2601 or 2627)
                return true;
            if (actual is DbException db && db.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
