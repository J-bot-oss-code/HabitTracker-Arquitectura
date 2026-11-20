using System.Reflection;
using Auditing.Application.DTOs;
using Auditing.Application.Interfaces;
using Auditing.Application.Services;
using Auditing.Domain;
using Auditing.Infrastructure.Persistence;
using Auditing.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Auditing.Infrastructure.Tests;

/// <summary>
/// Historial de auditoría (RF-AUD-01, 06 y 07) con el servicio y el repositorio reales sobre EF Core + SQLite en memoria:
/// sin API ni SQL Server (RD-12). Cada prueba prepara sus propios datos y no depende de las demás.
/// </summary>
public sealed class HistorialDeAuditoriaTests : IDisposable
{
    private static readonly Guid Admin = Guid.NewGuid();

    private readonly SqliteConnection _conexion = new("DataSource=:memory:");
    private readonly RelojManual _reloj = new(new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc));

    public HistorialDeAuditoriaTests()
    {
        _conexion.Open();
        using var contexto = NuevoContexto();
        contexto.Database.EnsureCreated();
    }

    public void Dispose() => _conexion.Dispose();

    private sealed class RelojManual(DateTime utc) : TimeProvider
    {
        public DateTime Ahora { get; set; } = utc;
        public override DateTimeOffset GetUtcNow() => new(Ahora, TimeSpan.Zero);
    }

    private AuditingDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<AuditingDbContext>().UseSqlite(_conexion).Options);

    private RegistroAuditoriaService Servicio(AuditingDbContext contexto) =>
        new(new RegistroAuditoriaRepository(contexto), _reloj);

    private async Task Registrar(
        DateTime cuando, string usuario, string accion, string entidad, string entidadId,
        string anterior = "antes", string nuevo = "después")
    {
        _reloj.Ahora = cuando;
        await using var contexto = NuevoContexto();
        await Servicio(contexto).RegistrarAsync(Admin, usuario, accion, entidad, entidadId, anterior, nuevo);
    }

    /// <summary>Cinco registros repartidos en varios días, usuarios, entidades y acciones.</summary>
    private async Task Sembrar()
    {
        await Registrar(new(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc), "beto@ejemplo.com", "Resolución de solicitud", "SolicitudPermiso", "s1");
        await Registrar(new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc), "admin@ejemplo.com", "Cambio de rol", "Usuario", "u1");
        await Registrar(new(2026, 11, 10, 23, 59, 59, DateTimeKind.Utc), "ana@ejemplo.com", "Subida de documento", "Documento", "d1");
        await Registrar(new(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc), "admin@ejemplo.com", "Cambio de estado", "Usuario", "u2");
        await Registrar(new(2026, 11, 12, 8, 0, 0, DateTimeKind.Utc), "ana@ejemplo.com", "Eliminación de documento", "Documento", "d1");
    }

    private async Task<PaginaAuditoriaDto> Consultar(ConsultaAuditoria consulta)
    {
        await using var contexto = NuevoContexto();
        return await Servicio(contexto).ConsultarAsync(consulta);
    }

    // ---------- RF-AUD-01: los siete datos ----------

    [Fact]
    [Trait("Requirement", "RF-AUD-01")]
    public async Task RF_AUD_01_UnRegistroGuardaQuienActuoQueHizoSobreQueCuandoYLosValoresAntesYDespues()
    {
        // Arrange
        var cuando = new DateTime(2026, 11, 10, 15, 30, 0, DateTimeKind.Utc);

        // Act
        await Registrar(cuando, "admin@ejemplo.com", "Cambio de rol", "Usuario", "u-77", "Estandar", "Administrador");
        var pagina = await Consultar(new ConsultaAuditoria());

        // Assert
        var r = Assert.Single(pagina.Registros);
        Assert.NotEqual(Guid.Empty, r.Id);
        Assert.Equal(Admin, r.UsuarioId);
        Assert.Equal("admin@ejemplo.com", r.Actor);
        Assert.Equal("Cambio de rol", r.Accion);
        Assert.Equal("Usuario", r.Entidad);
        Assert.Equal("u-77", r.EntidadId);
        Assert.Equal(cuando, r.Fecha);
        Assert.Equal(DateTimeKind.Utc, r.Fecha.Kind);
        Assert.Equal("Estandar", r.ValorAnterior);
        Assert.Equal("Administrador", r.ValorNuevo);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-01")]
    public void RF_AUD_01_UnRegistroAlQueLeFalteCualquieraDeLosDatos_NoSePuedeCrear()
    {
        // Arrange
        var ahora = DateTime.UtcNow;
        var sinUno = new Dictionary<string, Func<RegistroAuditoria>>
        {
            ["usuarioId"] = () => new(Guid.Empty, "a@ejemplo.com", "acción", "entidad", "id", "antes", "después", ahora),
            ["usuario"] = () => new(Admin, " ", "acción", "entidad", "id", "antes", "después", ahora),
            ["acción"] = () => new(Admin, "a@ejemplo.com", "", "entidad", "id", "antes", "después", ahora),
            ["entidad"] = () => new(Admin, "a@ejemplo.com", "acción", "", "id", "antes", "después", ahora),
            ["identificador"] = () => new(Admin, "a@ejemplo.com", "acción", "entidad", "", "antes", "después", ahora),
            ["fecha"] = () => new(Admin, "a@ejemplo.com", "acción", "entidad", "id", "antes", "después", default),
            ["valor anterior"] = () => new(Admin, "a@ejemplo.com", "acción", "entidad", "id", "", "después", ahora),
            ["valor nuevo"] = () => new(Admin, "a@ejemplo.com", "acción", "entidad", "id", "antes", null!, ahora),
        };

        // Act y Assert: ninguna combinación incompleta produce un registro
        foreach (var (falta, crear) in sinUno)
        {
            Assert.Throws<ArgumentException>(crear);
            Assert.True(falta.Length > 0);
        }
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-01")]
    public async Task RF_AUD_01_UnValorMasLargoQueLaColumnaSeRecortaSinPerderElRegistro()
    {
        // Arrange
        var largo = new string('x', RegistroAuditoria.LongitudMaximaValor + 300);

        // Act
        await Registrar(_reloj.Ahora, "admin@ejemplo.com", "Subida de documento", "Documento", "d1", "No existía", largo);
        var r = Assert.Single((await Consultar(new ConsultaAuditoria())).Registros);

        // Assert
        Assert.Equal(RegistroAuditoria.LongitudMaximaValor, r.ValorNuevo.Length);
    }

    // ---------- RF-AUD-07: filtros ----------

    [Fact]
    [Trait("Requirement", "RF-AUD-07")]
    public async Task RF_AUD_07_SinFiltrosDevuelveTodoDeLoMasRecienteALoMasAntiguo()
    {
        await Sembrar();

        var pagina = await Consultar(new ConsultaAuditoria());

        Assert.Equal(5, pagina.Total);
        Assert.Equal(["d1", "u2", "d1", "u1", "s1"], pagina.Registros.Select(r => r.EntidadId));
        Assert.Equal(pagina.Registros.OrderByDescending(r => r.Fecha).Select(r => r.Id), pagina.Registros.Select(r => r.Id));
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-07")]
    public async Task RF_AUD_07_FiltraPorUsuario_PorElCorreoQueContieneElTexto()
    {
        await Sembrar();

        var ana = await Consultar(new ConsultaAuditoria(Usuario: "ana@"));
        var todos = await Consultar(new ConsultaAuditoria(Usuario: "ejemplo.com"));
        var nadie = await Consultar(new ConsultaAuditoria(Usuario: "nadie@"));

        Assert.Equal(2, ana.Total);
        Assert.All(ana.Registros, r => Assert.Equal("ana@ejemplo.com", r.Actor));
        Assert.Equal(5, todos.Total);
        Assert.Equal(0, nadie.Total);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-07")]
    public async Task RF_AUD_07_FiltraPorEntidadYPorIdentificadorDeLaEntidad()
    {
        await Sembrar();

        var documentos = await Consultar(new ConsultaAuditoria(Entidad: "Documento"));
        var unaCuenta = await Consultar(new ConsultaAuditoria(Entidad: "Usuario", EntidadId: "u1"));
        var historiaDeUnDocumento = await Consultar(new ConsultaAuditoria(EntidadId: "d1"));

        Assert.Equal(2, documentos.Total);
        Assert.All(documentos.Registros, r => Assert.Equal("Documento", r.Entidad));
        Assert.Equal("Cambio de rol", Assert.Single(unaCuenta.Registros).Accion);
        Assert.Equal(2, historiaDeUnDocumento.Total);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-07")]
    public async Task RF_AUD_07_FiltraPorAccion()
    {
        await Sembrar();

        var pagina = await Consultar(new ConsultaAuditoria(Accion: "Cambio de rol"));

        Assert.Equal("u1", Assert.Single(pagina.Registros).EntidadId);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-07")]
    public async Task RF_AUD_07_ElRangoIncluyeElUltimoDiaCompletoYExcluyeElSiguiente()
    {
        await Sembrar();

        // El 10 de noviembre tiene un registro a las 12:00 y otro a las 23:59:59; el del 11 a las 00:00:00 ya es del día siguiente.
        var dia10 = await Consultar(new ConsultaAuditoria(Desde: new(2026, 11, 10), Hasta: new(2026, 11, 10)));

        Assert.Equal(2, dia10.Total);
        Assert.Equal(["Subida de documento", "Cambio de rol"], dia10.Registros.Select(r => r.Accion));
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-07")]
    public async Task RF_AUD_07_DosRangosDistintosDanResultadosDistintosSobreLosMismosDatos()
    {
        await Sembrar();

        var primera = await Consultar(new ConsultaAuditoria(Desde: new(2026, 11, 5), Hasta: new(2026, 11, 9)));
        var segunda = await Consultar(new ConsultaAuditoria(Desde: new(2026, 11, 10), Hasta: new(2026, 11, 12)));
        var soloDesde = await Consultar(new ConsultaAuditoria(Desde: new(2026, 11, 11)));
        var soloHasta = await Consultar(new ConsultaAuditoria(Hasta: new(2026, 11, 5)));

        Assert.Equal("s1", Assert.Single(primera.Registros).EntidadId);
        Assert.Equal(4, segunda.Total);
        Assert.Equal(2, soloDesde.Total);
        Assert.Equal(1, soloHasta.Total);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-07")]
    public async Task RF_AUD_07_UnRangoSinDatosDevuelveUnaListaVaciaNoUnError()
    {
        await Sembrar();

        var pagina = await Consultar(new ConsultaAuditoria(Desde: new(2020, 1, 1), Hasta: new(2020, 1, 31)));

        Assert.Equal(0, pagina.Total);
        Assert.Empty(pagina.Registros);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-07")]
    public async Task RF_AUD_07_LosFiltrosSeCombinan_UnRegistroTieneQueCumplirTodos()
    {
        await Sembrar();

        var pagina = await Consultar(new ConsultaAuditoria(
            Usuario: "ana@", Entidad: "Documento", Desde: new(2026, 11, 11), Hasta: new(2026, 11, 12)));

        Assert.Equal("Eliminación de documento", Assert.Single(pagina.Registros).Accion);
    }

    // ---------- RF-AUD-07 / RD-07: paginación y filtros inválidos ----------

    [Fact]
    [Trait("Requirement", "RF-AUD-07")]
    public async Task RF_AUD_07_PaginaElHistorial_ConElTotalCorrectoYSinRepetirNiSaltarRegistros()
    {
        await Sembrar();

        var p1 = await Consultar(new ConsultaAuditoria(Pagina: 1, Tamano: 2));
        var p2 = await Consultar(new ConsultaAuditoria(Pagina: 2, Tamano: 2));
        var p3 = await Consultar(new ConsultaAuditoria(Pagina: 3, Tamano: 2));
        var p4 = await Consultar(new ConsultaAuditoria(Pagina: 4, Tamano: 2));

        Assert.All(new[] { p1, p2, p3, p4 }, p => Assert.Equal(5, p.Total));
        Assert.Equal([2, 2, 1, 0], new[] { p1, p2, p3, p4 }.Select(p => p.Registros.Count));
        var ids = p1.Registros.Concat(p2.Registros).Concat(p3.Registros).Select(r => r.Id).ToList();
        Assert.Equal(5, ids.Distinct().Count());
        Assert.Equal(2, p2.Pagina);
        Assert.Equal(2, p2.Tamano);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_UnFiltroInvalidoSeRechazaConUnaExcepcionControlada_AntesDeConsultarLaBaseDeDatos()
    {
        var invalidos = new[]
        {
            new ConsultaAuditoria(Desde: new(2026, 11, 10), Hasta: new(2026, 11, 1)),           // hasta anterior a desde
            new ConsultaAuditoria(Desde: new(2025, 1, 1), Hasta: new(2026, 11, 1)),             // más de 366 días
            new ConsultaAuditoria(Desde: new(1990, 1, 1)),                                      // año imposible
            new ConsultaAuditoria(Hasta: DateOnly.MaxValue),                                    // fuera de rango
            new ConsultaAuditoria(Usuario: new string('a', RegistroAuditoria.LongitudMaximaUsuario + 1)),
            new ConsultaAuditoria(Entidad: new string('a', RegistroAuditoria.LongitudMaximaEntidad + 1)),
            new ConsultaAuditoria(Pagina: 0),
            new ConsultaAuditoria(Tamano: 0),
            new ConsultaAuditoria(Tamano: RegistroAuditoriaService.TamanoMaximo + 1),
        };

        foreach (var consulta in invalidos)
        {
            await Assert.ThrowsAsync<ArgumentException>(() => Consultar(consulta));
        }
    }

    // ---------- RF-AUD-06: el historial no se modifica ni se elimina ----------

    [Fact]
    [Trait("Requirement", "RF-AUD-06")]
    public async Task RF_AUD_06_ModificarUnRegistroGuardadoSeRechaza_YElRegistroQuedaIgual()
    {
        // Arrange
        await Registrar(_reloj.Ahora, "admin@ejemplo.com", "Cambio de rol", "Usuario", "u1", "Estandar", "Administrador");
        await using var contexto = NuevoContexto();
        var registro = await contexto.Registros.SingleAsync();
        contexto.Entry(registro).Property(r => r.ValorNuevo).CurrentValue = "Manipulado";

        // Act
        var async = await Assert.ThrowsAsync<InvalidOperationException>(() => contexto.SaveChangesAsync());
        var sync = Assert.Throws<InvalidOperationException>(() => contexto.SaveChanges());

        // Assert
        Assert.Equal(AuditingDbContext.MensajeInmutable, async.Message);
        Assert.Equal(AuditingDbContext.MensajeInmutable, sync.Message);
        Assert.Equal("Administrador", (await Consultar(new ConsultaAuditoria())).Registros.Single().ValorNuevo);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-06")]
    public async Task RF_AUD_06_EliminarUnRegistroGuardadoSeRechaza_YElRegistroSigueAhi()
    {
        // Arrange
        await Registrar(_reloj.Ahora, "admin@ejemplo.com", "Cambio de rol", "Usuario", "u1", "Estandar", "Administrador");
        await using var contexto = NuevoContexto();
        contexto.Registros.Remove(await contexto.Registros.SingleAsync());

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => contexto.SaveChangesAsync());
        Assert.Throws<InvalidOperationException>(() => contexto.SaveChanges());

        // Assert
        Assert.Equal(1, (await Consultar(new ConsultaAuditoria())).Total);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-06")]
    public void RF_AUD_06_NingunaOperacionExpuestaPorElServicioElRepositorioNiLaEntidadEditaOBorra()
    {
        // Arrange
        var prohibidas = new[] { "actualizar", "editar", "modificar", "eliminar", "borrar", "update", "delete", "remove", "set", "cambiar" };
        var tipos = new[]
        {
            typeof(IRegistroAuditoriaService), typeof(IRegistroAuditoriaRepository),
            typeof(RegistroAuditoriaService), typeof(RegistroAuditoriaRepository),
        };

        // Act
        var operaciones = tipos
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(m => m.Name)
            .ToList();
        var entidad = typeof(RegistroAuditoria);

        // Assert: solo se agrega, se registra y se consulta («Mi actividad» es otra consulta, de solo lectura)
        Assert.All(operaciones, nombre => Assert.Contains(nombre, new[] { "AgregarAsync", "ConsultarAsync", "RegistrarAsync", "MiActividadAsync" }));
        Assert.DoesNotContain(operaciones, n => prohibidas.Any(p => n.Contains(p, StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(entidad.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), m => !m.IsSpecialName);
        Assert.All(entidad.GetProperties(), p => Assert.True(p.SetMethod is null || !p.SetMethod.IsPublic));
    }

    // ---------- «Mi actividad»: solo lo propio y solo la ventana pedida ----------

    private async Task RegistrarDe(Guid usuarioId, DateTime cuando, string accion)
    {
        _reloj.Ahora = cuando;
        await using var contexto = NuevoContexto();
        await Servicio(contexto).RegistrarAsync(usuarioId, "persona@ejemplo.com", accion, "Usuario", usuarioId.ToString(), "antes", "después");
    }

    private async Task<PaginaAuditoriaDto> MiActividad(Guid usuarioId, int dias, int pagina = 1, int tamano = 50)
    {
        await using var contexto = NuevoContexto();
        return await Servicio(contexto).MiActividadAsync(usuarioId, dias, pagina, tamano);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-07")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_AUD_07_MiActividad_SoloDevuelveLoQueHizoLaPropiaPersona()
    {
        var ana = Guid.NewGuid();
        var beto = Guid.NewGuid();
        var hoy = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
        await RegistrarDe(ana, hoy.AddDays(-1), "Cambio de contraseña");
        await RegistrarDe(beto, hoy.AddDays(-1), "Subida de documento");
        _reloj.Ahora = hoy;

        var pagina = await MiActividad(ana, 7);

        var registro = Assert.Single(pagina.Registros);
        Assert.Equal((ana, "Cambio de contraseña"), (registro.UsuarioId, registro.Accion));
        Assert.Equal(1, pagina.Total);
    }

    [Theory]
    [InlineData(7, 2)]
    [InlineData(30, 3)]
    [Trait("Requirement", "RF-AUD-07")]
    public async Task RF_AUD_07_MiActividad_SoloLaVentanaPedida_SinBorrarLoDemas(int dias, int esperados)
    {
        var ana = Guid.NewGuid();
        var hoy = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
        await RegistrarDe(ana, hoy.AddDays(-1), "Cambio de contraseña");
        await RegistrarDe(ana, hoy.AddDays(-6), "Subida de documento");
        await RegistrarDe(ana, hoy.AddDays(-20), "Activación de cuenta");
        await RegistrarDe(ana, hoy.AddDays(-45), "Vinculación de Google");
        _reloj.Ahora = hoy;

        Assert.Equal(esperados, (await MiActividad(ana, dias)).Total);

        // RF-AUD-06: lo que queda fuera de la ventana sigue en el historial.
        await using var contexto = NuevoContexto();
        Assert.Equal(4, await contexto.Registros.CountAsync(r => r.UsuarioId == ana));
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-07")]
    public async Task RF_AUD_07_MiActividad_SinNadaEnLaVentana_DevuelveUnaPaginaVacia()
    {
        _reloj.Ahora = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

        var pagina = await MiActividad(Guid.NewGuid(), 30);

        Assert.Equal(0, pagina.Total);
        Assert.Empty(pagina.Registros);
    }

    [Theory]
    [InlineData(0, 1, 50)]
    [InlineData(14, 1, 50)]
    [InlineData(365, 1, 50)]
    [InlineData(7, 0, 50)]
    [InlineData(7, 1, 0)]
    [InlineData(7, 1, 201)]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_MiActividad_ValoresFueraDeRango_SeRechazanAntesDeConsultar(int dias, int pagina, int tamano)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => MiActividad(Guid.NewGuid(), dias, pagina, tamano));
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_MiActividad_SinIdentidad_NoConsultaNada()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => MiActividad(Guid.Empty, 7));
    }
}
