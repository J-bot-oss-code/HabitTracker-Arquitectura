using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Progreso;
using HabitTracker.Domain.Salud;
using HabitTracker.Infrastructure.Persistence;
using HabitTracker.Infrastructure.Persistence.Configuraciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HabitTracker.Infrastructure.Repositories;

public sealed class PerfilUsuarioRepository(HabitTrackerDbContext contexto) : IPerfilUsuarioRepository
{
    public Task<PerfilUsuario?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        contexto.PerfilesUsuario.FirstOrDefaultAsync(p => p.UsuarioId == usuarioId, cancellationToken);

    public async Task AgregarAsync(PerfilUsuario perfil, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        await contexto.PerfilesUsuario.AddAsync(perfil, cancellationToken);
    }

    public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        contexto.PerfilesUsuario.Where(p => p.UsuarioId == usuarioId).ExecuteDeleteAsync(cancellationToken);
}

/// <summary>
/// Guarda el cuestionario de salud cifrado por bloques (RD-05 no aplica: no es una contraseña; es cifrado reversible para
/// que su dueño lo lea y lo descargue). El texto en claro nunca llega a la base de datos.
/// </summary>
public sealed class CuestionarioSaludRepository(
    HabitTrackerDbContext contexto,
    ICifradorDatos cifrador,
    ILogger<CuestionarioSaludRepository>? logger = null) : ICuestionarioSaludRepository
{
    // Un propósito por bloque, versión y DUEÑO: un bloque no se descifra como otro, ni una fila copiada a otra persona.
    private const string PropositoObjetivos = "CuestionarioSalud.Objetivos.v1";
    private const string PropositoSeguridad = "CuestionarioSalud.Seguridad.v1";
    private const string PropositoCondiciones = "CuestionarioSalud.Condiciones.v1";

    private readonly ILogger _logger = logger ?? NullLogger<CuestionarioSaludRepository>.Instance;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<CuestionarioSalud?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var fila = await contexto.CuestionariosSalud.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId, cancellationToken);
        if (fila is null)
            return null;

        return CuestionarioSalud.Reconstruir(
            fila.UsuarioId, fila.Version,
            Leer<BloqueObjetivos>(fila.ObjetivosCifrado, PropositoObjetivos, usuarioId, out var objetivosLegible),
            objetivosLegible ? fila.ObjetivosRespondidoEn : null,
            Leer<BloqueSeguridad>(fila.SeguridadCifrado, PropositoSeguridad, usuarioId, out var seguridadLegible),
            seguridadLegible ? fila.SeguridadRespondidoEn : null,
            Leer<BloqueCondiciones>(fila.CondicionesCifrado, PropositoCondiciones, usuarioId, out var condicionesLegible),
            condicionesLegible ? fila.CondicionesRespondidoEn : null);
    }

    public async Task GuardarAsync(CuestionarioSalud cuestionario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cuestionario);

        var fila = await contexto.CuestionariosSalud.FirstOrDefaultAsync(c => c.UsuarioId == cuestionario.UsuarioId, cancellationToken);
        if (fila is null)
        {
            fila = new CuestionarioSaludFila { UsuarioId = cuestionario.UsuarioId };
            await contexto.CuestionariosSalud.AddAsync(fila, cancellationToken);
        }

        fila.Version = cuestionario.Version;
        fila.ObjetivosCifrado = Escribir(cuestionario.Objetivos, PropositoObjetivos, cuestionario.UsuarioId);
        fila.ObjetivosRespondidoEn = cuestionario.ObjetivosRespondidoEn;
        fila.SeguridadCifrado = Escribir(cuestionario.Seguridad, PropositoSeguridad, cuestionario.UsuarioId);
        fila.SeguridadRespondidoEn = cuestionario.SeguridadRespondidoEn;
        fila.CondicionesCifrado = Escribir(cuestionario.Condiciones, PropositoCondiciones, cuestionario.UsuarioId);
        fila.CondicionesRespondidoEn = cuestionario.CondicionesRespondidoEn;
    }

    public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        contexto.CuestionariosSalud.Where(c => c.UsuarioId == usuarioId).ExecuteDeleteAsync(cancellationToken);

    private static string PropositoDe(string bloque, Guid usuarioId) => $"{bloque}.{usuarioId:N}";

    private string? Escribir<T>(T? bloque, string proposito, Guid usuarioId) where T : class =>
        bloque is null ? null : cifrador.Cifrar(JsonSerializer.Serialize(bloque, Json), PropositoDe(proposito, usuarioId));

    /// <summary>
    /// Un bloque que no se puede descifrar (claves perdidas o rotadas sin conservar las anteriores, o una fila que no es de
    /// esta persona) se trata como NO respondido. Así la persona no queda bloqueada con un 500 ni se cae su descarga de
    /// datos: vuelve a responder el bloque y se reescribe con las claves vigentes. El aviso no lleva el contenido.
    /// </summary>
    private T? Leer<T>(string? cifrado, string proposito, Guid usuarioId, out bool legible) where T : class
    {
        legible = false;
        if (string.IsNullOrEmpty(cifrado))
            return null;
        try
        {
            var bloque = JsonSerializer.Deserialize<T>(cifrador.Descifrar(cifrado, PropositoDe(proposito, usuarioId)), Json);
            legible = bloque is not null;
            return bloque;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            _logger.LogWarning("Un bloque del cuestionario de salud ({Bloque}) no se pudo leer y se trata como no respondido.", proposito);
            return null;
        }
    }
}

public sealed class ProgresoCorporalRepository(HabitTrackerDbContext contexto) : IProgresoCorporalRepository
{
    public async Task<IReadOnlyList<RegistroPeso>> PesosAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        await contexto.RegistrosPeso.Where(r => r.UsuarioId == usuarioId).OrderBy(r => r.Fecha).ToListAsync(cancellationToken);

    public async Task AgregarPesoAsync(RegistroPeso registro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registro);
        await contexto.RegistrosPeso.AddAsync(registro, cancellationToken);
    }

    public async Task<IReadOnlyList<MedicionCorporal>> MedicionesAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        await contexto.MedicionesCorporales.Where(m => m.UsuarioId == usuarioId).OrderBy(m => m.Fecha).ToListAsync(cancellationToken);

    public Task<MedicionCorporal?> MedicionDelDiaAsync(Guid usuarioId, ZonaMedicion zona, DateTime diaUtc, CancellationToken cancellationToken = default)
    {
        var siguiente = diaUtc.AddDays(1);
        return contexto.MedicionesCorporales.FirstOrDefaultAsync(
            m => m.UsuarioId == usuarioId && m.Zona == zona && m.Fecha >= diaUtc && m.Fecha < siguiente, cancellationToken);
    }

    public async Task AgregarMedicionAsync(MedicionCorporal medicion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(medicion);
        await contexto.MedicionesCorporales.AddAsync(medicion, cancellationToken);
    }

    public async Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var pesos = await contexto.RegistrosPeso.Where(r => r.UsuarioId == usuarioId).ExecuteDeleteAsync(cancellationToken);
        var medidas = await contexto.MedicionesCorporales.Where(m => m.UsuarioId == usuarioId).ExecuteDeleteAsync(cancellationToken);
        return pesos + medidas;
    }
}
