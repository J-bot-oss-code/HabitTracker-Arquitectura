using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Notifications.Domain;
using Notifications.Infrastructure.Persistence;
using Notifications.Infrastructure.Repositories;

namespace Notifications.Infrastructure.Tests;

/// <summary>Persistencia de notificaciones y preferencias con EF Core sobre SQLite en memoria (sin SQL Server, RD-12).</summary>
public sealed class NotificacionRepositoryTests : IDisposable
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Beto = Guid.NewGuid();
    private static readonly DateTime Ahora = new(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _conexion = new("DataSource=:memory:");

    public NotificacionRepositoryTests()
    {
        _conexion.Open();
        using var contexto = NuevoContexto();
        contexto.Database.EnsureCreated();
    }

    public void Dispose() => _conexion.Dispose();

    private NotificationsDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<NotificationsDbContext>().UseSqlite(_conexion).Options);

    private async Task Agregar(Guid usuario, string titulo, int minutos = 0)
    {
        using var contexto = NuevoContexto();
        await new NotificacionRepository(contexto).AgregarAsync(
            new Notificacion(usuario, TipoNotificacion.LogroObtenido, titulo, "mensaje", Ahora.AddMinutes(minutos)));
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-04")]
    public async Task RF_NOT_04_ListarDevuelveSoloLasDeLaPersona_DeLaMasRecienteALaMasAntigua()
    {
        await Agregar(Ana, "vieja", 0);
        await Agregar(Ana, "nueva", 10);
        await Agregar(Beto, "de Beto", 5);

        using var contexto = NuevoContexto();
        var lista = await new NotificacionRepository(contexto).ListarDeUsuarioAsync(Ana, soloNoLeidas: false);

        Assert.Equal(["nueva", "vieja"], lista.Select(n => n.Titulo));
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-05")]
    public async Task RF_NOT_05_MarcarComoLeidaSePersisteYElFiltroDeNoLeidasLaExcluye()
    {
        await Agregar(Ana, "una");
        await Agregar(Ana, "dos", 1);
        Guid id;
        using (var contexto = NuevoContexto())
        {
            var repo = new NotificacionRepository(contexto);
            var primera = (await repo.ListarDeUsuarioAsync(Ana, false)).Last();
            id = primera.Id;
            var seguida = (await repo.ObtenerAsync(id))!;
            seguida.MarcarLeida(Ana, Ahora.AddHours(1));
            await repo.GuardarCambiosAsync();
        }

        using var lectura = NuevoContexto();
        var repoLectura = new NotificacionRepository(lectura);
        Assert.Single(await repoLectura.ListarDeUsuarioAsync(Ana, soloNoLeidas: true));
        Assert.Equal(2, (await repoLectura.ListarDeUsuarioAsync(Ana, soloNoLeidas: false)).Count);
        Assert.True((await repoLectura.ObtenerAsync(id))!.Leida);
    }

    [Fact]
    [Trait("Requirement", "RF-CA-19")]
    public async Task RF_CA_19_EliminarDeUsuarioBorraSoloLasDeEsaPersonaYEsIdempotente()
    {
        await Agregar(Ana, "a1");
        await Agregar(Ana, "a2");
        await Agregar(Beto, "b1");

        using var contexto = NuevoContexto();
        var repo = new NotificacionRepository(contexto);
        Assert.Equal(2, await repo.EliminarDeUsuarioAsync(Ana));
        Assert.Equal(0, await repo.EliminarDeUsuarioAsync(Ana));

        Assert.Empty(await repo.ListarDeUsuarioAsync(Ana, false));
        Assert.Single(await repo.ListarDeUsuarioAsync(Beto, false));
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-07")]
    public async Task RF_NOT_07_LaPreferenciaSeGuardaSeActualizaYSeBorra()
    {
        using (var contexto = NuevoContexto())
            await new PreferenciaNotificacionesRepository(contexto).AgregarAsync(new PreferenciaNotificaciones(Ana, correoActivo: false));

        using (var contexto = NuevoContexto())
        {
            var repo = new PreferenciaNotificacionesRepository(contexto);
            var guardada = (await repo.ObtenerAsync(Ana))!;
            Assert.False(guardada.CorreoActivo);
            guardada.CambiarCorreo(true);
            await repo.GuardarCambiosAsync();
        }

        using (var contexto = NuevoContexto())
        {
            var repo = new PreferenciaNotificacionesRepository(contexto);
            Assert.True((await repo.ObtenerAsync(Ana))!.CorreoActivo);
            Assert.Null(await repo.ObtenerAsync(Beto));
            await repo.EliminarDeUsuarioAsync(Ana);
            Assert.Null(await repo.ObtenerAsync(Ana));
        }
    }
}
