using System.Security.Cryptography;
using System.Text.Json;
using HabitTracker.Application.Interfaces;

namespace HabitTracker.Application.Notificaciones;

/// <summary>Lo que dice un enlace de «Ya lo hice»: de quién es, qué hábito y qué día local. No abre sesión.</summary>
public sealed record DatosEnlaceCheckin(Guid UsuarioId, Guid HabitoId, DateOnly FechaLocal);

/// <summary>
/// Firma y lee los enlaces del correo. El token es opaco, caduca solo y está atado a su propósito: uno de check-in no sirve para
/// nada más y uno alterado, caducado o de otro propósito se lee como <c>null</c>, sin decir por qué (RD-08).
/// </summary>
public interface IFirmadorEnlaces
{
    string FirmarCheckin(DatosEnlaceCheckin datos);

    /// <summary>Los datos del enlace, o <c>null</c> si no es válido, fue alterado o ya caducó.</summary>
    DatosEnlaceCheckin? LeerCheckin(string token);
}

/// <summary>
/// <see cref="IFirmadorEnlaces"/> sobre <see cref="ICifradorDatos"/> (Data Protection, mismas claves, propósito propio). La
/// caducidad va dentro del token y se compara con el <see cref="TimeProvider"/>, así se prueba sin esperar 36 horas (RD-12).
/// </summary>
public sealed class FirmadorEnlaces(ICifradorDatos cifrador, TimeProvider? reloj = null) : IFirmadorEnlaces
{
    public static readonly TimeSpan VigenciaCheckin = TimeSpan.FromHours(36);
    private const string Proposito = "Enlace.Checkin.v1";
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    private sealed record Carga(Guid U, Guid H, string F, long E);

    public string FirmarCheckin(DatosEnlaceCheckin datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var expira = _reloj.GetUtcNow().Add(VigenciaCheckin).ToUnixTimeSeconds();
        var carga = new Carga(datos.UsuarioId, datos.HabitoId, datos.FechaLocal.ToString("yyyy-MM-dd"), expira);
        return cifrador.Cifrar(JsonSerializer.Serialize(carga), Proposito);
    }

    public DatosEnlaceCheckin? LeerCheckin(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048)
            return null;
        try
        {
            var carga = JsonSerializer.Deserialize<Carga>(cifrador.Descifrar(token, Proposito));
            if (carga is null || carga.E <= _reloj.GetUtcNow().ToUnixTimeSeconds()
                || carga.U == Guid.Empty || carga.H == Guid.Empty
                || !DateOnly.TryParseExact(carga.F, "yyyy-MM-dd", out var fecha))
                return null;
            return new DatosEnlaceCheckin(carga.U, carga.H, fecha);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException or ArgumentException)
        {
            return null;
        }
    }
}
