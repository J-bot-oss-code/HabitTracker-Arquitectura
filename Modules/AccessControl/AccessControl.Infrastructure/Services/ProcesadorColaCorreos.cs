using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AccessControl.Infrastructure.Services;

/// <summary>Lo que hizo una pasada del procesador de la cola.</summary>
public sealed record ResultadoProcesamiento(int Enviados, int Fallidos, int Recuperados);

/// <summary>
/// Un ciclo de la cola de correos (RF-NOT-09): toma los pendientes, los envía y registra el resultado. Lo llaman el worker
/// en segundo plano y, a pedido de un Administrador, el endpoint de la cola; ninguno repite la operación que creó el correo.
/// Configuración (opcional): <c>Correo:EsperaReintentoSegundos</c> (60) y <c>Correo:MinutosParaRecuperar</c> (10).
/// </summary>
public class ProcesadorColaCorreos(
    ApplicationDbContext dbContext,
    IEnviadorCorreo enviador,
    IConfiguration configuration,
    ILogger<ProcesadorColaCorreos> logger)
{
    /// <summary>
    /// Correos que se pueden intentar enviar: los Pendiente y los Fallido que aún tienen intentos. Los Fallido que
    /// agotaron sus intentos se quedan en la tabla hasta la purga (14 días): se excluyen en la consulta para no leerlos (ni filtrarlos
    /// en memoria) en cada vuelta. La espera entre reintentos (backoff) se decide después, en memoria.
    /// </summary>
    public static readonly System.Linq.Expressions.Expression<Func<CorreoEnCola, bool>> CandidatosDeEnvio =
        c => c.Estado == EstadoCorreo.Pendiente
          || (c.Estado == EstadoCorreo.Fallido && c.Intentos < CorreoEnCola.MaxIntentos);

    public const int DiasDeConservacion = 14;

    public async Task<ResultadoProcesamiento> ProcesarAsync(CancellationToken cancellationToken)
    {
        var ahora = DateTime.UtcNow;
        var espera = TimeSpan.FromSeconds(Math.Max(0, configuration.GetValue<int?>("Correo:EsperaReintentoSegundos") ?? 60));

        var recuperados = await RecuperarAtascadosAsync(ahora, cancellationToken);

        // Minimización (SEG-12): los terminados guardan códigos y enlaces firmados; pasados 14 días ya no sirven a nadie.
        await dbContext.CorreosEnCola
            .Where(c => c.FechaCreacion < ahora.AddDays(-DiasDeConservacion)
                && (c.Estado == EstadoCorreo.Enviado || (c.Estado == EstadoCorreo.Fallido && c.Intentos >= CorreoEnCola.MaxIntentos)))
            .ExecuteDeleteAsync(cancellationToken);

        var candidatos = await dbContext.CorreosEnCola
            .Where(CandidatosDeEnvio)
            .ToListAsync(cancellationToken);

        var pendientes = candidatos.Where(c => c.PuedeReintentarse(ahora, espera)).ToList();

        if (pendientes.Count > 0)
        {
            logger.LogInformation("Cola de correos: {Count} correos pendientes.", pendientes.Count);
        }

        int enviados = 0, fallidos = 0;
        foreach (var correo in pendientes)
        {
            // RF-NOT-12: reclamar el correo ANTES de enviarlo. Si otro proceso (u otra pasada) ya lo tomó, se omite.
            if (!await ReclamarAsync(correo, ahora, cancellationToken))
            {
                continue;
            }

            try
            {
                await enviador.EnviarAsync(correo.Destinatario, correo.Asunto, correo.Cuerpo, cancellationToken);

                correo.MarcarEnviado();
                await dbContext.SaveChangesAsync(cancellationToken);
                enviados++;

                logger.LogInformation("Correo {Id} enviado a {Destino}.", correo.Id, Email.Enmascarar(correo.Destinatario));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                correo.RegistrarFallo(ex.Message);
                await dbContext.SaveChangesAsync(cancellationToken);
                fallidos++;

                logger.LogWarning(ex, "Fallo el envío del correo {Id} a {Destino} (intento {Intentos}).", correo.Id, Email.Enmascarar(correo.Destinatario), correo.Intentos);
            }
        }

        return new ResultadoProcesamiento(enviados, fallidos, recuperados);
    }

    /// <summary>
    /// Pasa el correo a <c>Procesando</c> con un solo UPDATE condicionado al estado (reclamo atómico): de dos pasadas
    /// simultáneas solo una afecta la fila y envía. Devuelve <c>false</c> si ya no estaba disponible.
    /// </summary>
    public async Task<bool> ReclamarAsync(CorreoEnCola correo, DateTime ahora, CancellationToken cancellationToken)
    {
        var id = correo.Id;
        var filas = await dbContext.CorreosEnCola
            .Where(c => c.Id == id
                && (c.Estado == EstadoCorreo.Pendiente
                    || (c.Estado == EstadoCorreo.Fallido && c.Intentos < CorreoEnCola.MaxIntentos)))
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.Estado, EstadoCorreo.Procesando)
                      .SetProperty(c => c.FechaUltimoIntento, ahora),
                cancellationToken);

        if (filas == 0)
        {
            return false;
        }

        // El UPDATE no pasa por el rastreador: se recarga para que la entidad refleje el estado reclamado.
        await dbContext.Entry(correo).ReloadAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Un correo que quedó en <c>Procesando</c> (el proceso cayó entre reclamarlo y enviarlo) no se reintentaría nunca:
    /// pasado el plazo se registra como un intento fallido y vuelve al ciclo normal de reintentos (RF-NOT-10).
    /// </summary>
    private async Task<int> RecuperarAtascadosAsync(DateTime ahora, CancellationToken cancellationToken)
    {
        var minutos = Math.Max(1, configuration.GetValue<int?>("Correo:MinutosParaRecuperar") ?? 10);
        var limite = ahora.AddMinutes(-minutos);

        var atascados = await dbContext.CorreosEnCola
            .Where(c => c.Estado == EstadoCorreo.Procesando && (c.FechaUltimoIntento ?? c.FechaCreacion) < limite)
            .ToListAsync(cancellationToken);

        foreach (var correo in atascados)
        {
            correo.RegistrarFallo("El correo quedó en Procesando (el proceso se detuvo antes de enviarlo); se recupera.");
            logger.LogWarning("Correo {Id} recuperado de Procesando.", correo.Id);
        }

        if (atascados.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return atascados.Count;
    }
}
