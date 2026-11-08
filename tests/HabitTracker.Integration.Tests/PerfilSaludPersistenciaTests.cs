using System.Security.Cryptography;
using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Salud;
using HabitTracker.Infrastructure.Persistence;
using HabitTracker.Infrastructure.Repositories;
using HabitTracker.Infrastructure.Seguridad;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Integration.Tests;

/// <summary>
/// Perfil, cuestionario de salud cifrado y progreso corporal contra EF Core + SQLite y Data Protection real (claves
/// efímeras en memoria). Comprueba que el texto en claro nunca llega a la base de datos (RD-09, datos de salud).
/// </summary>
public class PerfilSaludPersistenciaTests
{
    private static readonly DateTime Ahora = DateTime.UtcNow;
    private readonly Guid usuario = Guid.NewGuid();
    private readonly IDataProtectionProvider claves = new EphemeralDataProtectionProvider();

    private sealed class ConConsentimiento : IConsentimientoSalud
    {
        public Task<bool> TieneConsentimientoDatosSaludAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private static async Task<(SqliteConnection Conexion, DbContextOptions<HabitTrackerDbContext> Opciones)> PrepararBaseAsync()
    {
        var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<HabitTrackerDbContext>().UseSqlite(conexion).Options;
        await using var contexto = new HabitTrackerDbContext(opciones);
        await contexto.Database.EnsureCreatedAsync();
        return (conexion, opciones);
    }

    private CuestionarioSaludService Cuestionarios(HabitTrackerDbContext contexto, IDataProtectionProvider? proveedor = null) =>
        new(new CuestionarioSaludRepository(contexto, new CifradorDataProtection(proveedor ?? claves)),
            new PerfilUsuarioRepository(contexto), new ConConsentimiento(), new HabitTrackerUnitOfWork(contexto));

    private static BloqueCondiciones CondicionesSensibles() =>
        new([CondicionSalud.Hipertension, CondicionSalud.HistorialTrastornoAlimentario], [Alergia.Mani]);

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_Cuestionario_LaFilaNoGuardaElTextoEnClaro()
    {
        // Arrange
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;

        // Act
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await Cuestionarios(contexto).GuardarCondicionesAsync(usuario, CondicionesSensibles());

        // Assert: se lee la columna cruda, como lo haría alguien con acceso directo a la base de datos.
        await using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT CondicionesCifrado FROM CuestionariosSalud";
        var crudo = (string?)await comando.ExecuteScalarAsync();
        Assert.False(string.IsNullOrEmpty(crudo));
        Assert.DoesNotContain("Hipertension", crudo);
        Assert.DoesNotContain("Trastorno", crudo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mani", crudo);
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_Cuestionario_IdaYVuelta_DevuelveLasMismasRespuestas()
    {
        // Arrange
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        await using (var contexto = new HabitTrackerDbContext(opciones))
        {
            var servicio = Cuestionarios(contexto);
            await servicio.GuardarCondicionesAsync(usuario, CondicionesSensibles());
            await servicio.GuardarSeguridadAsync(usuario, new BloqueSeguridad(false, false, true, false, false, false, false));
        }

        // Act: un DbContext nuevo, como otra petición HTTP.
        CuestionarioSaludDto dto;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            dto = await Cuestionarios(contexto).ObtenerAsync(usuario);

        // Assert
        Assert.Equal([CondicionSalud.Hipertension, CondicionSalud.HistorialTrastornoAlimentario], dto.Condiciones!.Condiciones);
        Assert.Equal([Alergia.Mani], dto.Condiciones.Alergias);
        Assert.True(dto.Seguridad!.MareosOPerdidaDeConocimiento);
        Assert.Equal(NivelPrecaucion.ConsultarMedico, dto.Evaluacion.Nivel);
        Assert.Contains(RestriccionSalud.SinRestriccionCalorica, dto.Evaluacion.Restricciones);
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_Cuestionario_ConOtrasClaves_NoSeLeeYSeTrataComoNoRespondido()
    {
        // Arrange
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await Cuestionarios(contexto).GuardarCondicionesAsync(usuario, CondicionesSensibles());

        // Act: otra instalación (claves perdidas o distintas) intenta leerlo.
        CuestionarioSaludDto dto;
        await using (var otro = new HabitTrackerDbContext(opciones))
            dto = await Cuestionarios(otro, new EphemeralDataProtectionProvider()).ObtenerAsync(usuario);

        // Assert: no hay datos en claro ni un 500 que bloquee a la persona (ni su descarga de datos); vuelve a responder.
        Assert.Null(dto.Condiciones);
        Assert.Null(dto.CondicionesRespondidoEn);
        Assert.False(dto.Evaluacion.Completitud.Condiciones);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    [Trait("Requirement", "RD-09")]
    public async Task RD_06_Cuestionario_UnaFilaCopiadaAOtraPersona_NoSeDescifra()
    {
        // Arrange: el cifrado está ligado a su dueño.
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        var otra = Guid.NewGuid();
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await Cuestionarios(contexto).GuardarCondicionesAsync(usuario, CondicionesSensibles());

        // Act: alguien con acceso a la base copia el texto cifrado a la fila de otra persona.
        await using (var comando = conexion.CreateCommand())
        {
            comando.CommandText = "INSERT INTO CuestionariosSalud (UsuarioId, Version, CondicionesCifrado, CondicionesRespondidoEn) " +
                                  "SELECT $otra, Version, CondicionesCifrado, CondicionesRespondidoEn FROM CuestionariosSalud";
            comando.Parameters.AddWithValue("$otra", otra.ToString().ToUpperInvariant());
            await comando.ExecuteNonQueryAsync();
        }
        CuestionarioSaludDto dto;
        await using (var contexto = new HabitTrackerDbContext(opciones))
            dto = await Cuestionarios(contexto).ObtenerAsync(otra);

        // Assert
        Assert.Null(dto.Condiciones);
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    public void RD_09_Cifrador_UnPropositoNoDescifraLoDeOtro()
    {
        var cifrador = new CifradorDataProtection(claves);
        var cifrado = cifrador.Cifrar("dato de salud", "CuestionarioSalud.Condiciones.v1");

        Assert.Equal("dato de salud", cifrador.Descifrar(cifrado, "CuestionarioSalud.Condiciones.v1"));
        Assert.ThrowsAny<CryptographicException>(() => cifrador.Descifrar(cifrado, "CuestionarioSalud.Objetivos.v1"));
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RD_09_Perfil_SePersisteConHorasYDecimales()
    {
        // Arrange
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        var datos = new DatosPerfil(PerfilLaboral.TurnoNocturno, "America/Santo_Domingo", true,
            HoraEntradaTrabajo: new TimeOnly(22, 0), HoraSalidaTrabajo: new TimeOnly(6, 0), PesoKg: 72.35m, AlturaCm: 168.5m);

        // Act
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await new PerfilService(new PerfilUsuarioRepository(contexto), new ConConsentimiento(), new HabitTrackerUnitOfWork(contexto))
                .GuardarAsync(usuario, datos);

        // Assert
        await using var verificacion = new HabitTrackerDbContext(opciones);
        var perfil = await verificacion.PerfilesUsuario.SingleAsync(p => p.UsuarioId == usuario);
        Assert.Equal(new TimeOnly(22, 0), perfil.HoraEntradaTrabajo);
        Assert.Equal(72.35m, perfil.PesoKg);
        Assert.Equal(DateTimeKind.Utc, perfil.ActualizadoEn.Kind);
    }

    [Fact]
    [Trait("Requirement", "RD-09")]
    [Trait("Requirement", "RD-06")]
    public async Task RD_09_BajaDeCuenta_BorraSoloLosDatosDeEsaPersona()
    {
        // Arrange: dos personas con perfil, cuestionario, peso y medida.
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        var otra = Guid.NewGuid();
        foreach (var persona in new[] { usuario, otra })
        {
            await using var contexto = new HabitTrackerDbContext(opciones);
            var unidad = new HabitTrackerUnitOfWork(contexto);
            var perfiles = new PerfilUsuarioRepository(contexto);
            await new PerfilService(perfiles, new ConConsentimiento(), unidad).GuardarAsync(persona,
                new DatosPerfil(PerfilLaboral.Remoto, "America/Santo_Domingo", true));
            await Cuestionarios(contexto).GuardarCondicionesAsync(persona, CondicionesSensibles());
            var progreso = new ProgresoCorporalService(new ProgresoCorporalRepository(contexto), perfiles, new ConConsentimiento(), unidad);
            await progreso.RegistrarPesoAsync(persona, new RegistrarPesoCommand(Ahora, 70));
            await progreso.RegistrarMedicionAsync(persona, new RegistrarMedicionCommand(Ahora, Domain.Progreso.ZonaMedicion.Cintura, 80));
        }

        // Act
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await new DatosPerfilSaludService(new PerfilUsuarioRepository(contexto),
                new CuestionarioSaludRepository(contexto, new CifradorDataProtection(claves)),
                new ProgresoCorporalRepository(contexto)).EliminarAsync(usuario);

        // Assert
        await using var verificacion = new HabitTrackerDbContext(opciones);
        Assert.False(await verificacion.PerfilesUsuario.AnyAsync(p => p.UsuarioId == usuario));
        Assert.False(await verificacion.RegistrosPeso.AnyAsync(p => p.UsuarioId == usuario));
        Assert.False(await verificacion.MedicionesCorporales.AnyAsync(m => m.UsuarioId == usuario));
        Assert.True(await verificacion.PerfilesUsuario.AnyAsync(p => p.UsuarioId == otra));
        Assert.True(await verificacion.RegistrosPeso.AnyAsync(p => p.UsuarioId == otra));
        await using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM CuestionariosSalud";
        Assert.Equal(1L, (long)(await comando.ExecuteScalarAsync())!);
    }
}
