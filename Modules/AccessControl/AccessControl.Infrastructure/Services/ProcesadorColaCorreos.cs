using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccessControl.Infrastructure.Services;

/// <summary>Lo que hizo una pasada del procesador de la cola.</summary>
public sealed record ResultadoProcesamiento(int Enviados, int Fallidos);

/// <summary>
/// Un ciclo de la cola de correos (RF-NOT-09): toma los pendientes, los envía y registra el resultado. Lo llaman el worker
/// en segundo plano y, a pedido de un Administrador, el endpoint de la cola; ninguno repite la operación que creó el correo.
/// </summary>
public class ProcesadorColaCorreos(ApplicationDbContext dbContext, IEnviadorCorreo enviador, ILogger<ProcesadorColaCorreos> logger)
{
    /// <summary>
    /// Correos que se pueden intentar enviar: los Pendiente y los Fallido que aún tienen intentos. Los Fallido que
    /// agotaron sus intentos se quedan en la tabla para siempre: se excluyen en la consulta para no leerlos (ni filtrarlos
    /// en memoria) en cada vuelta. La espera entre reintentos (backoff) se decide después, en memoria.
    /// </summary>
    public static readonly System.Linq.Expressions.Expression<Func<CorreoEnCola, bool>> CandidatosDeEnvio =
        c => c.Estado == EstadoCorreo.Pendiente
          || (c.Estado == EstadoCorreo.Fallido && c.Intentos < CorreoEnCola.MaxIntentos);

    public async Task<ResultadoProcesamiento> ProcesarAsync(CancellationToken cancellationToken)
    {
        var ahora = DateTime.UtcNow;

        var candidatos = await dbContext.CorreosEnCola
            .Where(CandidatosDeEnvio)
            .ToListAsync(cancellationToken);

        var pendientes = candidatos.Where(c => c.PuedeReintentarse(ahora)).ToList();

        if (pendientes.Count == 0)
        {
            return new ResultadoProcesamiento(0, 0);
        }

        logger.LogInformation("Cola de correos: {Count} correos pendientes.", pendientes.Count);

        int enviados = 0, fallidos = 0;
        foreach (var correo in pendientes)
        {
            if (correo.Estado == EstadoCorreo.Fallido)
            {
                correo.Reintentar();
            }

            // RF-NOT-12: reclamar el correo ANTES de enviarlo para evitar duplicados si el proceso se reinicia.
            correo.MarcarProcesando();
            await dbContext.SaveChangesAsync(cancellationToken);

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

        return new ResultadoProcesamiento(enviados, fallidos);
    }
}
