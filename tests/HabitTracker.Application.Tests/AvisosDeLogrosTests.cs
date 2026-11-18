using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Adherencia;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Planes;
using Microsoft.Extensions.Logging.Abstractions;

namespace HabitTracker.Application.Tests;

/// <summary>
/// Un logro nuevo se avisa una sola vez (RF-NOT-03) por un punto de extensión opcional, sin que un fallo del aviso rompa la
/// consulta de Hoy. Dobles escritos a mano (RD-12).
/// </summary>
public class AvisosDeLogrosTests
{
    private static readonly Guid Usuario = Guid.NewGuid();
    private static readonly DateTime Ahora = new(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc);

    private sealed class Logros : ILogroUsuarioRepository
    {
        public List<LogroUsuario> Lista { get; } = [];
        public Task<IReadOnlyList<LogroUsuario>> ListarAsync(Guid usuarioId, CancellationToken c = default) =>
            Task.FromResult<IReadOnlyList<LogroUsuario>>([.. Lista.Where(l => l.UsuarioId == usuarioId)]);
        public Task<bool> AgregarSiNoExisteAsync(LogroUsuario logro, CancellationToken c = default)
        {
            if (Lista.Any(l => l.UsuarioId == logro.UsuarioId && l.Clave == logro.Clave)) return Task.FromResult(false);
            Lista.Add(logro);
            return Task.FromResult(true);
        }
        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken c = default) => Task.FromResult(Lista.RemoveAll(l => l.UsuarioId == usuarioId));
    }

    private sealed class Notificador : INotificadorUsuario
    {
        public List<ClaveLogro> Avisos { get; } = [];
        public bool Falla { get; set; }
        public Task LogroObtenidoAsync(Guid usuarioId, ClaveLogro clave, CancellationToken c = default)
        {
            if (Falla) throw new InvalidOperationException("Notificaciones no responde.");
            Avisos.Add(clave);
            return Task.CompletedTask;
        }
    }

    // Una persona con un primer avance registrado: gana el logro PrimerAvance.
    private static Habito ConUnAvance()
    {
        var habito = new Habito(Usuario, "Respirar", Periodicidad.Diaria, 1, "min");
        habito.Activar(Usuario);
        habito.RegistrarAvance(Usuario, 1, Ahora);
        return habito;
    }

    private static Task<IReadOnlyList<LogroUsuario>> Sincronizar(LogrosService servicio) =>
        servicio.SincronizarAsync(Usuario, [], [ConUnAvance()]);

    [Fact]
    [Trait("Requirement", "RF-NOT-03")]
    public async Task RF_NOT_03_ElPrimerLogroSeAvisaUnaSolaVez_AunqueSeConsulteOtraVez()
    {
        var notificador = new Notificador();
        var servicio = new LogrosService(new Logros(), notificador: notificador);

        await Sincronizar(servicio);
        await Sincronizar(servicio);

        Assert.Equal([ClaveLogro.PrimerAvance], notificador.Avisos);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-03")]
    public async Task RF_NOT_03_UnLogroQueOtraConsultaYaGuardo_NoSeAvisaDeNuevo()
    {
        var logros = new Logros();
        logros.Lista.Add(new LogroUsuario(Usuario, ClaveLogro.PrimerAvance, Ahora)); // la consulta simultánea lo guardó antes
        var notificador = new Notificador();

        await Sincronizar(new LogrosService(logros, notificador: notificador));

        Assert.Empty(notificador.Avisos);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-08")]
    public async Task RF_NOT_08_SiElAvisoFalla_ElLogroQuedaGuardadoYLaConsultaNoFalla()
    {
        var logros = new Logros();
        var servicio = new LogrosService(logros, notificador: new Notificador { Falla = true }, logger: NullLogger<LogrosService>.Instance);

        var resultado = await Sincronizar(servicio);

        Assert.Contains(resultado, l => l.Clave == ClaveLogro.PrimerAvance);
        Assert.Single(logros.Lista);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_SinNotificadorLosLogrosSeGananIgualQueAntes()
    {
        var logros = new Logros();

        await Sincronizar(new LogrosService(logros));

        Assert.Single(logros.Lista);
    }
}
