using System.Security.Cryptography;
using System.Text;
using AccessControl.Application.Interfaces;
using AccessControl.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AccessControl.Application.Service;

public sealed class ServicioBajaCuentas : IServicioBajaCuentas
{
    // Tantas cuentas por pasada como máximo: si hubiera más, las demás se borran en la siguiente.
    private const int CuentasPorPasada = 50;

    private readonly IUsuarioRepository _usuarios;
    private readonly IEnumerable<IParticipanteDatosUsuario> _participantes;
    private readonly ILogger<ServicioBajaCuentas> _logger;

    public ServicioBajaCuentas(
        IUsuarioRepository usuarios,
        IEnumerable<IParticipanteDatosUsuario> participantes,
        ILogger<ServicioBajaCuentas> logger)
    {
        _usuarios = usuarios;
        _participantes = participantes;
        _logger = logger;
    }

    public async Task<int> EjecutarAsync(DateTime ahora, CancellationToken cancelacion = default)
    {
        var vencidas = await _usuarios.ObtenerConBajaVencidaAsync(ahora, CuentasPorPasada);
        var borradas = 0;

        foreach (var usuario in vencidas)
        {
            cancelacion.ThrowIfCancellationRequested();

            // Solo el identificador anonimizado queda en el log: nunca el correo ni el nombre de quien se dio de baja.
            var anonimo = Anonimizar(usuario.Id);
            var correo = usuario.Correo.Valor;

            try
            {
                // Primero los datos de cada módulo; la cuenta se borra solo si todos lo lograron. Si algo falla, el plazo
                // sigue vencido y la siguiente pasada lo reintenta (los borrados son idempotentes).
                foreach (var participante in _participantes)
                {
                    await participante.EliminarAsync(usuario.Id, correo, cancelacion);
                }

                await _usuarios.EliminarDefinitivamenteAsync(usuario.Id, correo);
                borradas++;

                _logger.LogInformation("Cuenta {CuentaAnonima} eliminada definitivamente al terminar su periodo de gracia.", anonimo);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "No se pudo completar la baja de la cuenta {CuentaAnonima}; se reintentará.", anonimo);
            }
        }

        return borradas;
    }

    /// <summary>Primeros 12 caracteres del SHA-256 del identificador: sirve para seguir un caso sin identificar a nadie.</summary>
    public static string Anonimizar(Guid usuarioId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(usuarioId.ToString())))[..12];
}
