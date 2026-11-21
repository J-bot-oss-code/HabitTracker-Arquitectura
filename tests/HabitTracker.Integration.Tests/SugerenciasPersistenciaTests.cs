using HabitTracker.Application.Sugerencias;
using HabitTracker.Domain.Excepciones;
using HabitTracker.Domain.Sugerencias;
using HabitTracker.Infrastructure.Persistence;
using HabitTracker.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Integration.Tests;

/// <summary>
/// Quejas y mejoras con EF Core y SQLite: el servicio completo contra la base (límite diario, bandeja filtrada y paginada, cambio de
/// estado, descarga y borrado de la persona) sin la API (RD-09, RD-12).
/// </summary>
public class SugerenciasPersistenciaTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Beto = Guid.NewGuid();

    private sealed class RelojManual(DateTime utc) : TimeProvider
    {
        public DateTime Ahora { get; set; } = utc;
        public override DateTimeOffset GetUtcNow() => new(Ahora, TimeSpan.Zero);
    }

    private readonly RelojManual _reloj = new(new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc));

    private async Task<(SqliteConnection Conexion, DbContextOptions<HabitTrackerDbContext> Opciones)> PrepararAsync()
    {
        var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<HabitTrackerDbContext>().UseSqlite(conexion).Options;
        await using var contexto = new HabitTrackerDbContext(opciones);
        await contexto.Database.EnsureCreatedAsync();
        return (conexion, opciones);
    }

    // Cada operación con su propio contexto, como en cada petición HTTP.
    private SugerenciasService Servicio(HabitTrackerDbContext contexto) =>
        new(new SugerenciaRepository(contexto), new HabitTrackerUnitOfWork(contexto), _reloj);

    private async Task<T> ConServicio<T>(DbContextOptions<HabitTrackerDbContext> opciones, Func<SugerenciasService, Task<T>> operacion)
    {
        await using var contexto = new HabitTrackerDbContext(opciones);
        return await operacion(Servicio(contexto));
    }

    private Task<SugerenciaDto> Enviar(DbContextOptions<HabitTrackerDbContext> o, Guid usuario, string texto = "Un texto suficientemente largo.") =>
        ConServicio(o, s => s.EnviarAsync(usuario, TipoSugerencia.Mejora, texto));

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_UnaSugerenciaEnviadaPersisteConSuEstadoYSuFechaUtc()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;

        var enviada = await Enviar(opciones, Ana, "Me gustaría un modo más sencillo.");

        var mias = await ConServicio(opciones, s => s.ListarMiasAsync(Ana));
        var guardada = Assert.Single(mias);
        Assert.Equal((enviada.Id, EstadoSugerencia.Nueva, "Me gustaría un modo más sencillo."), (guardada.Id, guardada.Estado, guardada.Texto));
        Assert.Equal(DateTimeKind.Utc, guardada.CreadaEn.Kind);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_UnaPersonaSoloVeLasSuyas()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;
        await Enviar(opciones, Ana);
        await Enviar(opciones, Beto);

        Assert.Single(await ConServicio(opciones, s => s.ListarMiasAsync(Ana)));
        Assert.Empty(await ConServicio(opciones, s => s.ListarMiasAsync(Guid.NewGuid())));
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_AlLlegarAlTopeDiario_ElSextoSeRechazaConUnCodigoYNoSeGuarda()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;
        for (var i = 0; i < SugerenciasService.MaximoPorDia; i++)
            await Enviar(opciones, Ana);

        var error = await Assert.ThrowsAsync<ReglaNegocioException>(() => Enviar(opciones, Ana));

        Assert.Equal(SugerenciasService.CodigoLimite, error.Codigo);
        Assert.Equal(SugerenciasService.MaximoPorDia, (await ConServicio(opciones, s => s.ListarMiasAsync(Ana))).Count);
        await Enviar(opciones, Beto); // el tope es por persona
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_PasadasLas24Horas_LaPersonaPuedeVolverAEscribir()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;
        for (var i = 0; i < SugerenciasService.MaximoPorDia; i++)
            await Enviar(opciones, Ana);

        _reloj.Ahora = _reloj.Ahora.AddHours(24).AddMinutes(1);

        await Enviar(opciones, Ana);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_UnTextoNoValidoNoToca_NiCuentaParaElTope()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;

        await Assert.ThrowsAsync<ArgumentException>(() => Enviar(opciones, Ana, "corto"));

        Assert.Empty(await ConServicio(opciones, s => s.ListarMiasAsync(Ana)));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_ElTextoConHtmlSeGuardaTalCual_NoSeInterpretaNiSeAltera()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;
        const string texto = "<script>alert(1)</script> el botón no responde";

        await Enviar(opciones, Ana, texto);

        Assert.Equal(texto, (await ConServicio(opciones, s => s.ListarMiasAsync(Ana))).Single().Texto);
    }

    // ---------- Bandeja del Administrador ----------

    private async Task SembrarBandejaAsync(DbContextOptions<HabitTrackerDbContext> opciones)
    {
        // 7 sugerencias de 3 personas en días distintos (el tope diario es por persona).
        var personas = new[] { Ana, Beto, Guid.NewGuid() };
        for (var i = 0; i < 7; i++)
        {
            _reloj.Ahora = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc).AddDays(i);
            await Enviar(opciones, personas[i % 3], $"Sugerencia número {i} con texto suficiente.");
        }
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-07")]
    public async Task RF_AUD_07_LaBandeja_PaginaDeLaMasRecienteALaMasAntiguaYCuentaLasNuevas()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;
        await SembrarBandejaAsync(opciones);

        var primera = await ConServicio(opciones, s => s.ListarAsync(null, 1, 3));
        var tercera = await ConServicio(opciones, s => s.ListarAsync(null, 3, 3));

        Assert.Equal((7, 7), (primera.Total, primera.Nuevas));
        Assert.Equal(3, primera.Sugerencias.Count);
        Assert.Contains("número 6", primera.Sugerencias[0].Texto); // la más reciente primero
        Assert.Contains("número 0", Assert.Single(tercera.Sugerencias).Texto);
        Assert.Empty((await ConServicio(opciones, s => s.ListarAsync(null, 4, 3))).Sugerencias);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-07")]
    public async Task RF_AUD_07_LaBandeja_FiltraPorEstado_YElNumeroDeNuevasIgnoraElFiltro()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;
        await SembrarBandejaAsync(opciones);
        var todas = await ConServicio(opciones, s => s.ListarAsync(null, 1, 10));
        await ConServicio(opciones, s => s.CambiarEstadoAsync(todas.Sugerencias[0].Id, EstadoSugerencia.Leida));
        await ConServicio(opciones, s => s.CambiarEstadoAsync(todas.Sugerencias[1].Id, EstadoSugerencia.Resuelta));

        var leidas = await ConServicio(opciones, s => s.ListarAsync(EstadoSugerencia.Leida, 1, 10));
        var nuevas = await ConServicio(opciones, s => s.ListarAsync(EstadoSugerencia.Nueva, 1, 10));
        var resueltas = await ConServicio(opciones, s => s.ListarAsync(EstadoSugerencia.Resuelta, 1, 10));

        Assert.Equal((1, 5), (leidas.Total, leidas.Nuevas));
        Assert.Equal((5, 5), (nuevas.Total, nuevas.Nuevas));
        Assert.Equal(1, resueltas.Total);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-07")]
    public async Task RF_AUD_07_LaBandejaVacia_DevuelveUnaPaginaVaciaConCeroNuevas()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;

        var pagina = await ConServicio(opciones, s => s.ListarAsync(null, 1, 20));

        Assert.Equal((0, 0), (pagina.Total, pagina.Nuevas));
        Assert.Empty(pagina.Sugerencias);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, SugerenciasService.TamanoMaximo + 1)]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_LaBandeja_RechazaPaginasYTamanosFueraDeRango(int pagina, int tamano)
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;

        await Assert.ThrowsAsync<ArgumentException>(() => ConServicio(opciones, s => s.ListarAsync(null, pagina, tamano)));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_LaBandeja_RechazaUnEstadoInexistente()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;

        await Assert.ThrowsAsync<ArgumentException>(() => ConServicio(opciones, s => s.ListarAsync((EstadoSugerencia)99, 1, 20)));
    }

    [Fact]
    [Trait("Requirement", "RD-04")]
    public async Task RD_04_CambiarElEstado_Persiste_YUnaNuevaNoVuelveAlEstadoNueva()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;
        var enviada = await Enviar(opciones, Ana);
        _reloj.Ahora = _reloj.Ahora.AddHours(2);

        var leida = await ConServicio(opciones, s => s.CambiarEstadoAsync(enviada.Id, EstadoSugerencia.Leida));

        Assert.Equal((EstadoSugerencia.Leida, Ana), (leida.Estado, leida.UsuarioId));
        Assert.Equal(EstadoSugerencia.Leida, (await ConServicio(opciones, s => s.ListarMiasAsync(Ana))).Single().Estado);
        await Assert.ThrowsAsync<ArgumentException>(() => ConServicio(opciones, s => s.CambiarEstadoAsync(enviada.Id, EstadoSugerencia.Nueva)));
    }

    [Fact]
    [Trait("Requirement", "RD-04")]
    public async Task RD_04_DosAdministradoresMarcandoLoMismoAlaVez_NoRompenNada()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;
        var enviada = await Enviar(opciones, Ana);

        await Task.WhenAll(
            ConServicio(opciones, s => s.CambiarEstadoAsync(enviada.Id, EstadoSugerencia.Leida)),
            ConServicio(opciones, s => s.CambiarEstadoAsync(enviada.Id, EstadoSugerencia.Leida)));

        Assert.Equal(EstadoSugerencia.Leida, (await ConServicio(opciones, s => s.ListarMiasAsync(Ana))).Single().Estado);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_MarcarUnaSugerenciaQueNoExiste_Da404ConCodigo()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;

        var error = await Assert.ThrowsAsync<RecursoNoEncontradoException>(
            () => ConServicio(opciones, s => s.CambiarEstadoAsync(Guid.NewGuid(), EstadoSugerencia.Leida)));

        Assert.Equal(SugerenciasService.CodigoNoEncontrada, error.Codigo);
    }

    // ---------- Descarga de datos y baja de cuenta ----------

    [Fact]
    [Trait("Requirement", "RF-CA-18")]
    public async Task RF_CA_18_LaBajaDeCuenta_BorraLasSugerenciasDeLaPersonaYSoloLasSuyas_ConLaDescargaIncluyendolas()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;
        await Enviar(opciones, Ana);
        await Enviar(opciones, Ana);
        await Enviar(opciones, Beto);

        Assert.Equal(2, (await ConServicio(opciones, s => s.ExportarAsync(Ana))).Count);

        await ConServicio(opciones, async s => { await s.EliminarAsync(Ana); return 0; });
        await ConServicio(opciones, async s => { await s.EliminarAsync(Ana); return 0; }); // idempotente

        Assert.Empty(await ConServicio(opciones, s => s.ListarMiasAsync(Ana)));
        Assert.Single(await ConServicio(opciones, s => s.ListarMiasAsync(Beto)));
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_SinIdentidad_NadaSeEnviaNiSeLista()
    {
        var (conexion, opciones) = await PrepararAsync();
        await using var _ = conexion;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Enviar(opciones, Guid.Empty));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ConServicio(opciones, s => s.ListarMiasAsync(Guid.Empty)));
    }
}
