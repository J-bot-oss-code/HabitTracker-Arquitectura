using Auditing.Application.DTOs;
using Auditing.Application.Interfaces;
using Auditing.Domain;

namespace Auditing.Application.Services;

public sealed class RegistroAuditoriaService(IRegistroAuditoriaRepository repositorio, TimeProvider? reloj = null) : IRegistroAuditoriaService
{
    /// <summary>Tope del rango: un historial de más de un año no es un período y carga la base de datos.</summary>
    public const int MaximoDiasDelRango = 366;
    public const int TamanoMaximo = 200;

    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public Task RegistrarAsync(
        Guid usuarioId, string usuario, string accion, string entidad, string entidadId,
        string valorAnterior, string valorNuevo, CancellationToken cancellationToken = default) =>
        repositorio.AgregarAsync(
            new RegistroAuditoria(usuarioId, usuario, accion, entidad, entidadId, valorAnterior, valorNuevo, _reloj.GetUtcNow().UtcDateTime),
            cancellationToken);

    public async Task<PaginaAuditoriaDto> ConsultarAsync(ConsultaAuditoria consulta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        // RD-07: todo se valida antes de tocar la base de datos.
        var filtro = new FiltroAuditoria(
            Texto(consulta.Usuario, "usuario", RegistroAuditoria.LongitudMaximaUsuario),
            Texto(consulta.Entidad, "entidad", RegistroAuditoria.LongitudMaximaEntidad),
            Texto(consulta.EntidadId, "entidadId", RegistroAuditoria.LongitudMaximaEntidadId),
            Texto(consulta.Accion, "accion", RegistroAuditoria.LongitudMaximaAccion),
            consulta.Desde is { } d ? Dia(d).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : null,
            consulta.Hasta is { } h ? Dia(h).AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : null); // «hasta» se incluye: el límite es el día siguiente

        if (consulta.Desde is { } desde && consulta.Hasta is { } hasta)
        {
            if (hasta < desde)
                throw new ArgumentException("«hasta» no puede ser anterior a «desde».");
            if (hasta.DayNumber - desde.DayNumber + 1 > MaximoDiasDelRango)
                throw new ArgumentException($"El rango no puede pasar de {MaximoDiasDelRango} días.");
        }

        if (consulta.Pagina < 1)
            throw new ArgumentException("La página empieza en 1.");
        if (consulta.Tamano < 1 || consulta.Tamano > TamanoMaximo)
            throw new ArgumentException($"El tamaño de la página va de 1 a {TamanoMaximo}.");

        return await PaginaAsync(filtro, consulta.Pagina, consulta.Tamano, cancellationToken);
    }

    /// <summary>Las ventanas de «Mi actividad»: una semana o un mes. El historial completo sigue intacto (RF-AUD-06).</summary>
    public static readonly int[] DiasDeMiActividad = [7, 30];

    public async Task<PaginaAuditoriaDto> MiActividadAsync(
        Guid usuarioId, int dias, int pagina, int tamano, CancellationToken cancellationToken = default)
    {
        // RD-07: todo se valida antes de tocar la base de datos.
        if (usuarioId == Guid.Empty)
            throw new UnauthorizedAccessException("Sesión inválida.");
        if (!DiasDeMiActividad.Contains(dias))
            throw new ArgumentException("«dias» debe ser 7 o 30.");
        if (pagina < 1)
            throw new ArgumentException("La página empieza en 1.");
        if (tamano < 1 || tamano > TamanoMaximo)
            throw new ArgumentException($"El tamaño de la página va de 1 a {TamanoMaximo}.");

        var desde = _reloj.GetUtcNow().UtcDateTime.AddDays(-dias);
        return await PaginaAsync(new FiltroAuditoria(null, null, null, null, desde, null, usuarioId), pagina, tamano, cancellationToken);
    }

    private async Task<PaginaAuditoriaDto> PaginaAsync(FiltroAuditoria filtro, int numero, int tamano, CancellationToken cancellationToken)
    {
        var pagina = await repositorio.ConsultarAsync(filtro, numero, tamano, cancellationToken);

        return new PaginaAuditoriaDto(
            pagina.Total, numero, tamano,
            [.. pagina.Registros.Select(r => new RegistroAuditoriaDto(
                r.Id, DateTime.SpecifyKind(r.FechaUtc, DateTimeKind.Utc), r.UsuarioId, r.Usuario, r.Accion, r.Entidad, r.EntidadId,
                r.ValorAnterior, r.ValorNuevo))]);
    }

    private static string? Texto(string? valor, string campo, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;
        var limpio = valor.Trim();
        if (limpio.Length > maximo)
            throw new ArgumentException($"El filtro «{campo}» supera los {maximo} caracteres.");
        return limpio;
    }

    private static DateOnly Dia(DateOnly dia)
    {
        if (dia.Year < 2000 || dia.Year > 9000)
            throw new ArgumentException("Las fechas del rango no son válidas.");
        return dia;
    }
}
