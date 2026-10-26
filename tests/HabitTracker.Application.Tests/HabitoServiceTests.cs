using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Entities;

namespace HabitTracker.Application.Tests;

public class HabitoServiceTests
{
    private readonly Guid propietario = Guid.NewGuid();
    private readonly Guid noPropietario = Guid.NewGuid();

    private sealed class InMemoryHabitoRepository : IHabitoRepository
    {
        private readonly List<Habito> _habitos = new();

        public Task<Habito?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_habitos.SingleOrDefault(h => h.Id == id));

        public Task<IReadOnlyList<Habito>> GetByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Habito>>(_habitos.Where(h => h.UsuarioId == usuarioId).ToList());

        public Task AddAsync(Habito habito, CancellationToken cancellationToken = default)
        {
            _habitos.Add(habito);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Habito habito, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCallCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.FromResult(1);
        }

        public void Dispose() { }
    }

    private sealed class InMemoryMetaRepository : IMetaRepository
    {
        public HashSet<Guid> MetasExistentes { get; } = new();

        public Task<bool> ExistsAsync(Guid metaId, CancellationToken cancellationToken = default) =>
            Task.FromResult(metaId != Guid.Empty);
    }

    private sealed class FakePermisoService : IPermisoService
    {
        public HashSet<(string solicitante, string permiso)> PermisosEfectivos { get; } = new();

        public Task<bool> TienePermisoEfectivoAsync(
            string solicitante,
            string permisoSolicitado,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PermisosEfectivos.Contains((solicitante, permisoSolicitado)));
    }

    private static Habito CrearHabitoEnEstado(
        InMemoryHabitoRepository repository,
        Guid usuarioId,
        EstadoHabito estado)
    {
        var habito = new Habito(usuarioId, "Leer", Periodicidad.Diaria, 10, "paginas", Guid.NewGuid());

        var campoEstado = typeof(Habito).GetProperty("Estado", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        campoEstado!.SetValue(habito, estado);

        repository.AddAsync(habito).Wait();
        return habito;
    }

    private static DateTime HoyUtc() => DateTime.UtcNow.Date;

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_CrearHabito_CreaconExitosoYGuarda()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var command = new CrearHabitoCommand("Leer 20 paginas", Periodicidad.Diaria, 20, "paginas", Guid.NewGuid());

        // Act
        var id = await service.CrearHabitoAsync(command, propietario);

        // Assert
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
        var habitos = await repository.GetByUsuarioIdAsync(propietario);
        Assert.Single(habitos);
        Assert.Equal(EstadoHabito.Planificado, habitos.Single().Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_CrearHabito_NombreVacio_Rechaza()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var command = new CrearHabitoCommand("", Periodicidad.Diaria, 20, "paginas", Guid.NewGuid());

        // Act
        var excepcion = await Record.ExceptionAsync(() => service.CrearHabitoAsync(command, propietario));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<ArgumentException>(excepcion);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_CrearHabito_CantidadObjetivoCero_Rechaza()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var command = new CrearHabitoCommand("Leer", Periodicidad.Diaria, 0, "paginas", Guid.NewGuid());

        // Act
        var excepcion = await Record.ExceptionAsync(() => service.CrearHabitoAsync(command, propietario));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<ArgumentException>(excepcion);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_ConsultarHabitos_SoloPropios()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());

        var habitoPropio = new Habito(propietario, "Leer", Periodicidad.Diaria, 10, "paginas", Guid.NewGuid());
        var habitoAjeno = new Habito(noPropietario, "Correr", Periodicidad.Diaria, 5, "km", Guid.NewGuid());
        await repository.AddAsync(habitoPropio);
        await repository.AddAsync(habitoAjeno);

        // Act
        var resultado = await service.ConsultarHabitosAsync(propietario);

        // Assert
        Assert.Single(resultado);
        Assert.Equal(habitoPropio.Id, resultado.Single().Id);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-04")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_04_CambiarEstado_PlanificadoAAActivo_Propietario_CambiaAActivo()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var habito = CrearHabitoEnEstado(repository, propietario, EstadoHabito.Planificado);

        // Act
        await service.CambiarEstadoAsync(habito.Id, EstadoHabito.Activo, propietario);

        // Assert
        var actualizado = await repository.GetByIdAsync(habito.Id);
        Assert.Equal(EstadoHabito.Activo, actualizado!.Estado);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-04")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_04_CambiarEstado_NoPropietario_Rechaza()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var habito = CrearHabitoEnEstado(repository, propietario, EstadoHabito.Planificado);

        // Act
        var excepcion = await Record.ExceptionAsync(() => service.CambiarEstadoAsync(habito.Id, EstadoHabito.Activo, noPropietario));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<UnauthorizedAccessException>(excepcion);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-04")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_04_CambiarEstado_HabitoInexistente_Rechaza()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());

        // Act
        var excepcion = await Record.ExceptionAsync(() => service.CambiarEstadoAsync(Guid.NewGuid(), EstadoHabito.Activo, propietario));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<InvalidOperationException>(excepcion);
        Assert.Contains("no existe", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-05")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_05_CambiarEstado_ActivoAACompletado_SinMetaCumplida_Rechaza()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var habito = CrearHabitoEnEstado(repository, propietario, EstadoHabito.Activo);

        // Act
        var excepcion = await Record.ExceptionAsync(() => service.CambiarEstadoAsync(habito.Id, EstadoHabito.Completado, propietario));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<InvalidOperationException>(excepcion);
        Assert.Contains("No se alcanzó la meta", excepcion.Message);
        Assert.Equal(EstadoHabito.Activo, habito.Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_02_RegistrarAvance_ActivoYPropietario_AgregaRegistro()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var habito = CrearHabitoEnEstado(repository, propietario, EstadoHabito.Activo);
        var fechaUtc = HoyUtc();

        // Act
        await service.RegistrarAvanceAsync(habito.Id, 5, fechaUtc, propietario);

        // Assert
        var actualizado = await repository.GetByIdAsync(habito.Id);
        Assert.Single(actualizado!.Registros);
        Assert.Equal(5, actualizado.Registros.Single().Cantidad);
        Assert.Equal(DateTimeKind.Utc, actualizado.Registros.Single().Fecha.Kind);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_02_RegistrarAvance_NoPropietario_Rechaza()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var habito = CrearHabitoEnEstado(repository, propietario, EstadoHabito.Activo);

        // Act
        var excepcion = await Record.ExceptionAsync(() => service.RegistrarAvanceAsync(habito.Id, 5, HoyUtc(), noPropietario));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<UnauthorizedAccessException>(excepcion);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_RegistrarAvance_CantidadCero_Rechaza()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var habito = CrearHabitoEnEstado(repository, propietario, EstadoHabito.Activo);

        // Act
        var excepcion = await Record.ExceptionAsync(() => service.RegistrarAvanceAsync(habito.Id, 0, HoyUtc(), propietario));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<ArgumentException>(excepcion);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-11")]
    public async Task RF_NEG_02_RegistrarAvance_FechaNoUtc_Rechaza()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var habito = CrearHabitoEnEstado(repository, propietario, EstadoHabito.Activo);
        var fechaLocal = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local);

        // Act
        var excepcion = await Record.ExceptionAsync(() => service.RegistrarAvanceAsync(habito.Id, 5, fechaLocal, propietario));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<ArgumentException>(excepcion);
        Assert.Contains("UTC", excepcion.Message);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RF-NEG-06")]
    public async Task RF_NEG_02_AdvancedReconfig_SinPermisoEfectivo_Rechaza()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var habito = CrearHabitoEnEstado(repository, propietario, EstadoHabito.Activo);

        // Act
        var excepcion = await Record.ExceptionAsync(() => service.ReconfigurarHabitoAsync(
            habito.Id, Periodicidad.Semanal, 30, "minutos", propietario, "persona@ejemplo.com"));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<UnauthorizedAccessException>(excepcion);
        Assert.Contains("Habits.AdvancedReconfig", excepcion.Message);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RF-NEG-06")]
    public async Task RF_NEG_02_AdvancedReconfig_ConPermisoEfectivo_Ejecutase()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        permisoService.PermisosEfectivos.Add(("persona@ejemplo.com", "Habits.AdvancedReconfig"));
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var habito = CrearHabitoEnEstado(repository, propietario, EstadoHabito.Activo);

        // Act
        await service.ReconfigurarHabitoAsync(habito.Id, Periodicidad.Semanal, 30, "minutos", propietario, "persona@ejemplo.com");

        // Assert
        var actualizado = await repository.GetByIdAsync(habito.Id);
        Assert.Equal(Periodicidad.Semanal, actualizado!.Periodicidad);
        Assert.Equal(30m, actualizado.CantidadObjetivo);
        Assert.Equal("minutos", actualizado.Unidad);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RF-NEG-06")]
    public async Task RF_NEG_02_AdvancedReconfig_HabitoCompletado_Rechaza()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        permisoService.PermisosEfectivos.Add((propietario.ToString(), "Habits.AdvancedReconfig"));
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var habito = CrearHabitoEnEstado(repository, propietario, EstadoHabito.Completado);

        // Act
        var excepcion = await Record.ExceptionAsync(() => service.ReconfigurarHabitoAsync(
            habito.Id, Periodicidad.Semanal, 30, "minutos", propietario, "persona@ejemplo.com"));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<InvalidOperationException>(excepcion);
        Assert.Contains("completado", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RF-NEG-06")]
    public async Task RF_NEG_02_AdvancedReconfig_NoPropietario_Rechaza()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        permisoService.PermisosEfectivos.Add(("persona@ejemplo.com", "Habits.AdvancedReconfig"));
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var habito = CrearHabitoEnEstado(repository, propietario, EstadoHabito.Activo);

        // Act
        var excepcion = await Record.ExceptionAsync(() => service.ReconfigurarHabitoAsync(
            habito.Id, Periodicidad.Semanal, 30, "minutos", noPropietario, "persona@ejemplo.com"));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<UnauthorizedAccessException>(excepcion);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RF-NEG-06")]
    public async Task RF_NEG_02_AdvancedReconfig_IdentidadNoEsUsuarioIdDelCliente()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        permisoService.PermisosEfectivos.Add(("persona@ejemplo.com", "Habits.AdvancedReconfig"));
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var habito = CrearHabitoEnEstado(repository, propietario, EstadoHabito.Activo);

        // Act - Usar identidad diferente a la del propietario
        var excepcion = await Record.ExceptionAsync(() => service.ReconfigurarHabitoAsync(
            habito.Id, Periodicidad.Semanal, 30, "minutos", noPropietario, "persona@ejemplo.com"));

        // Assert - Debe rechazar porque el propietario es diferente
        Assert.NotNull(excepcion);
        Assert.IsType<UnauthorizedAccessException>(excepcion);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RF-NEG-06")]
    public async Task RF_NEG_02_AdvancedReconfig_ConsultaPermisoConCorreoCorrecto()
    {
        // Arrange
        var permisoService = new FakePermisoService();
        permisoService.PermisosEfectivos.Add(("persona@ejemplo.com", "Habits.AdvancedReconfig"));

        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var habito = CrearHabitoEnEstado(repository, propietario, EstadoHabito.Activo);

        // Act - Usar correo correcto
        await service.ReconfigurarHabitoAsync(
            habito.Id, Periodicidad.Semanal, 30, "minutos", propietario, "persona@ejemplo.com");

        // Assert - El permiso se consultó con el correo, no con el GUID
        Assert.Contains(("persona@ejemplo.com", "Habits.AdvancedReconfig"), permisoService.PermisosEfectivos);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_CrearHabito_PeriodicidadNoDefinida_Rechaza()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var periodicidadInvalida = (Periodicidad)999;
        var command = new CrearHabitoCommand("Leer", periodicidadInvalida, 20, "paginas", Guid.NewGuid());

        // Act
        var excepcion = await Record.ExceptionAsync(() => service.CrearHabitoAsync(command, propietario));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<ArgumentException>(excepcion);
        Assert.Contains("periodicidad", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Reconfigurar_PeriodicidadNoDefinida_Rechaza()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        permisoService.PermisosEfectivos.Add(("persona@ejemplo.com", "Habits.AdvancedReconfig"));
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var habito = CrearHabitoEnEstado(repository, propietario, EstadoHabito.Activo);
        var periodicidadInvalida = (Periodicidad)999;

        // Act
        var excepcion = await Record.ExceptionAsync(() => service.ReconfigurarHabitoAsync(
            habito.Id, periodicidadInvalida, 30, "minutos", propietario, "persona@ejemplo.com"));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<ArgumentException>(excepcion);
        Assert.Contains("periodicidad", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_CrearHabito_MetaIdVacio_Rechaza()
    {
        // Arrange
        var repository = new InMemoryHabitoRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var permisoService = new FakePermisoService();
        var service = new HabitoService(repository, unitOfWork, permisoService, new InMemoryMetaRepository());
        var command = new CrearHabitoCommand("Leer", Periodicidad.Diaria, 20, "paginas", Guid.Empty);

        // Act
        var excepcion = await Record.ExceptionAsync(() => service.CrearHabitoAsync(command, propietario));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<ArgumentException>(excepcion);
        Assert.Contains("meta", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }
}
