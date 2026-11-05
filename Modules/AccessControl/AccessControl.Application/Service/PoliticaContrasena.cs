using AccessControl.Application.Interfaces;
using AccessControl.Domain.Excepciones;

namespace AccessControl.Application.Service;

/// <summary>
/// Única definición de la política de contraseñas (RD-01): longitud de 12 a 1024 caracteres con al menos una letra y un
/// número, y que no aparezca en filtraciones conocidas. RF-CA-14 pide como mínimo 8 caracteres con letras y números: esta
/// política es más estricta, así que toda contraseña que la cumple también cumple ese mínimo.
/// Las cuentas existentes con contraseñas más cortas siguen entrando: la política solo se aplica al fijar una nueva.
/// </summary>
public sealed class PoliticaContrasena : IPoliticaContrasena
{
    public const int LongitudMinima = 12;
    public const int LongitudMaxima = 1024;

    private readonly IVerificadorContrasenasFiltradas _verificador;

    public PoliticaContrasena(IVerificadorContrasenasFiltradas verificador)
    {
        _verificador = verificador;
    }

    /// <summary>Política que solo comprueba el formato (sin consultar filtraciones). La usan las pruebas y la configuración que la desactiva.</summary>
    public static PoliticaContrasena SoloFormato { get; } = new(new VerificadorDesactivado());

    public void ValidarFormato(string? password)
    {
        if (string.IsNullOrWhiteSpace(password)
            || password.Length < LongitudMinima
            || password.Length > LongitudMaxima
            || !password.Any(char.IsLetter)
            || !password.Any(char.IsDigit))
        {
            throw new ContrasenaNoValidaException();
        }
    }

    public async Task ValidarNoFiltradaAsync(string password, CancellationToken cancellationToken = default)
    {
        if (await _verificador.EstaFiltradaAsync(password, cancellationToken))
        {
            throw new ContrasenaFiltradaException();
        }
    }
}

/// <summary>Verificador que nunca marca una contraseña como filtrada: se usa cuando la comprobación está desactivada.</summary>
public sealed class VerificadorDesactivado : IVerificadorContrasenasFiltradas
{
    public Task<bool> EstaFiltradaAsync(string password, CancellationToken cancellationToken = default) => Task.FromResult(false);
}
