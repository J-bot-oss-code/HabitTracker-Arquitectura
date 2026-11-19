using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Domain.Entities;
using AccessControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AccessControl.Infrastructure.Services;

public class ColaCorreosAdmin(ApplicationDbContext dbContext, ProcesadorColaCorreos procesador) : IColaCorreosAdmin
{
    public const int MaximoDeCorreos = 200;

    public async Task<IReadOnlyList<CorreoColaDto>> ListarAsync(string? estado, CancellationToken cancellationToken = default)
    {
        var consulta = dbContext.CorreosEnCola.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(estado))
        {
            // Solo valen los nombres de los estados (Enum.TryParse aceptaría también números sueltos).
            var nombre = Enum.GetNames<EstadoCorreo>().FirstOrDefault(n => n.Equals(estado.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException("El estado debe ser Pendiente, Procesando, Enviado o Fallido.", nameof(estado));
            var filtro = Enum.Parse<EstadoCorreo>(nombre);

            consulta = consulta.Where(c => c.Estado == filtro);
        }

        var correos = await consulta
            .OrderByDescending(c => c.FechaCreacion)
            .Take(MaximoDeCorreos)
            .ToListAsync(cancellationToken);

        return [.. correos.Select(c => new CorreoColaDto(
            c.Id, c.Destinatario, c.Asunto, c.Estado.ToString(), c.Intentos,
            c.FechaCreacion, c.FechaUltimoIntento, c.FechaEnvio, c.UltimoError))];
    }

    public async Task<ResultadoColaDto> ProcesarAsync(CancellationToken cancellationToken = default)
    {
        var r = await procesador.ProcesarAsync(cancellationToken);
        return new ResultadoColaDto(r.Enviados, r.Fallidos, r.Recuperados);
    }
}
