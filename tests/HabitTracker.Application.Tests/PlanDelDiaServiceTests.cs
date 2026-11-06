using HabitTracker.Application.Exceptions;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Servicios;

namespace HabitTracker.Application.Tests;

public class PlanDelDiaServiceTests
{
    private readonly Guid usuario = Guid.NewGuid();
    private readonly Guid otroUsuario = Guid.NewGuid();
    private readonly Guid metaId = Guid.NewGuid();

    private Habito Crear(Guid dueno, string nombre, EstadoHabito estado)
    {
        var habito = new Habito(dueno, nombre, Periodicidad.Diaria, 1, "veces", metaId);
        if (estado is EstadoHabito.Activo or EstadoHabito.Pausado or EstadoHabito.Completado)
        {
            habito.Activar(dueno);
        }

        if (estado == EstadoHabito.Pausado)
        {
            habito.Pausar(dueno);
        }

        return habito;
    }

    private static PlanDelDiaService CrearServicio(IEnumerable<Habito> habitos, ClimaFalso clima) =>
        new(new RepositorioFalso(habitos), clima);

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_PlanDelDia_SoloIncluyeHabitosActivosDelUsuario()
    {
        var activo = Crear(usuario, "Correr", EstadoHabito.Activo);
        var pausado = Crear(usuario, "Leer", EstadoHabito.Pausado);
        var planificado = Crear(usuario, "Meditar", EstadoHabito.Planificado);
        var ajeno = Crear(otroUsuario, "Nadar", EstadoHabito.Activo);
        var servicio = CrearServicio([activo, pausado, planificado, ajeno], new ClimaFalso(new CondicionClimatica(20, false, "cielo claro")));

        var plan = await servicio.ObtenerAsync(usuario, "Santo Domingo");

        var unico = Assert.Single(plan.Habitos);
        Assert.Equal(activo.Id, unico.HabitoId);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_PlanDelDia_UsaLosDatosDelClimaParaRecomendar()
    {
        var habito = Crear(usuario, "Correr", EstadoHabito.Activo);

        var soleado = await CrearServicio([habito], new ClimaFalso(new CondicionClimatica(22, false, "cielo claro")))
            .ObtenerAsync(usuario, "Santo Domingo");
        var lluvioso = await CrearServicio([habito], new ClimaFalso(new CondicionClimatica(22, true, "lluvia")))
            .ObtenerAsync(usuario, "Santo Domingo");
        var caluroso = await CrearServicio([habito], new ClimaFalso(new CondicionClimatica(36, false, "despejado")))
            .ObtenerAsync(usuario, "Santo Domingo");

        Assert.Equal("AireLibre", soleado.Habitos.Single().LugarSugerido);
        Assert.Equal(0, soleado.AguaAdicionalMl);
        Assert.Equal("BajoTecho", lluvioso.Habitos.Single().LugarSugerido);
        Assert.Equal("BajoTecho", caluroso.Habitos.Single().LugarSugerido);
        Assert.Equal(1000, caluroso.AguaAdicionalMl);
        Assert.Equal(36, caluroso.Clima.TemperaturaC);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_PlanDelDia_SinHabitosActivos_DevuelveElClimaYUnaListaVacia()
    {
        var servicio = CrearServicio([], new ClimaFalso(new CondicionClimatica(20, false, "cielo claro")));

        var plan = await servicio.ObtenerAsync(usuario, "Santo Domingo");

        Assert.Empty(plan.Habitos);
        Assert.Equal("cielo claro", plan.Clima.Descripcion);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_PlanDelDia_LaCiudadSeRecortaAntesDeConsultarAlProveedor()
    {
        var clima = new ClimaFalso(new CondicionClimatica(20, false, "ok"));
        var servicio = CrearServicio([], clima);

        var plan = await servicio.ObtenerAsync(usuario, "  Santo Domingo  ");

        Assert.Equal("Santo Domingo", clima.UltimaCiudad);
        Assert.Equal("Santo Domingo", plan.Ciudad);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Santo Domingo; DROP TABLE")]
    [InlineData("a&appid=otraClave")]
    [InlineData("<script>")]
    [InlineData("Ciudad123")]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_PlanDelDia_CiudadInvalida_SeRechazaSinLlamarAlProveedor(string? ciudad)
    {
        var clima = new ClimaFalso(new CondicionClimatica(20, false, "ok"));
        var servicio = CrearServicio([], clima);

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.ObtenerAsync(usuario, ciudad!));

        Assert.Equal(0, clima.Llamadas);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_PlanDelDia_CiudadMasLargaQueElMaximo_SeRechaza()
    {
        var clima = new ClimaFalso(new CondicionClimatica(20, false, "ok"));
        var servicio = CrearServicio([], clima);
        var larga = new string('a', PlanDelDiaService.LongitudMaximaCiudad + 1);

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.ObtenerAsync(usuario, larga));
    }

    [Theory]
    [InlineData("São Paulo")]
    [InlineData("Saint-Denis")]
    [InlineData("Santo Domingo, DO")]
    [InlineData("L'Aquila")]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_PlanDelDia_NombresDeCiudadLegitimos_SeAceptan(string ciudad)
    {
        var servicio = CrearServicio([], new ClimaFalso(new CondicionClimatica(20, false, "ok")));

        var plan = await servicio.ObtenerAsync(usuario, ciudad);

        Assert.Equal(ciudad, plan.Ciudad);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_PlanDelDia_UsuarioVacio_NoAutorizado()
    {
        var servicio = CrearServicio([], new ClimaFalso(new CondicionClimatica(20, false, "ok")));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => servicio.ObtenerAsync(Guid.Empty, "Santo Domingo"));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_PlanDelDia_ClimaNoDisponible_PropagaUnaExcepcionControlada()
    {
        var servicio = CrearServicio(
            [Crear(usuario, "Correr", EstadoHabito.Activo)],
            new ClimaFalso(new ServicioClimaNoDisponibleException()));

        await Assert.ThrowsAsync<ServicioClimaNoDisponibleException>(() => servicio.ObtenerAsync(usuario, "Santo Domingo"));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_PlanDelDia_CiudadInexistenteEnElProveedor_PropagaCiudadNoEncontrada()
    {
        var servicio = CrearServicio([], new ClimaFalso(new CiudadNoEncontradaException()));

        await Assert.ThrowsAsync<CiudadNoEncontradaException>(() => servicio.ObtenerAsync(usuario, "Narnia"));
    }

    private sealed class ClimaFalso : IServicioClima
    {
        private readonly CondicionClimatica? _condicion;
        private readonly Exception? _error;

        public ClimaFalso(CondicionClimatica condicion) => _condicion = condicion;
        public ClimaFalso(Exception error) => _error = error;

        public int Llamadas { get; private set; }
        public string? UltimaCiudad { get; private set; }

        public Task<CondicionClimatica> ObtenerAsync(string ciudad, CancellationToken cancellationToken = default)
        {
            Llamadas++;
            UltimaCiudad = ciudad;

            return _error is not null
                ? Task.FromException<CondicionClimatica>(_error)
                : Task.FromResult(_condicion!);
        }
    }

    private sealed class RepositorioFalso(IEnumerable<Habito> habitos) : IHabitoRepository
    {
        private readonly List<Habito> _habitos = habitos.ToList();

        public Task<Habito?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_habitos.SingleOrDefault(h => h.Id == id));

        public Task<IReadOnlyList<Habito>> GetByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Habito>>(_habitos.Where(h => h.UsuarioId == usuarioId).ToList());

        public Task AddAsync(Habito habito, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(Habito habito, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_habitos.RemoveAll(h => h.UsuarioId == usuarioId));
    }
}
