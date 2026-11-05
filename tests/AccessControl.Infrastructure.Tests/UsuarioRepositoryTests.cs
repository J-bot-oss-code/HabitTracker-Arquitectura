using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Excepciones;
using AccessControl.Infrastructure.Persistence;
using AccessControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AccessControl.Infrastructure.Tests;

/// <summary>
/// Persistencia real del usuario con EF Core sobre SQLite en memoria (no necesita SQL Server ni la API).
/// Cada prueba usa contextos distintos sobre la MISMA base, como harían dos peticiones simultáneas.
/// </summary>
public sealed class UsuarioRepositoryTests : IDisposable
{
    private readonly SqliteConnection _conexion = new("DataSource=:memory:");

    public UsuarioRepositoryTests()
    {
        _conexion.Open();
        using var contexto = NuevoContexto();
        contexto.Database.EnsureCreated();
    }

    public void Dispose() => _conexion.Dispose();

    private ApplicationDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conexion).Options);

    private static Usuario NuevoUsuario(string correo = "persona@ejemplo.com") =>
        new("Persona de prueba", new Email(correo), "hash-guardado", Rol.Estandar);

    private long? LeerIntentosActivacion()
    {
        using var comando = _conexion.CreateCommand();
        comando.CommandText = "SELECT IntentosActivacion FROM usuarios LIMIT 1";
        var valor = comando.ExecuteScalar();
        return valor is null or DBNull ? null : Convert.ToInt64(valor);
    }

    // ---------- RF-CA-01: correo único también en la base de datos ----------

    [Fact]
    public async Task RF_CA_01_DosRegistrosConElMismoCorreo_ElSegundoSeRechazaAunqueLaComprobacionPreviaPase()
    {
        // Simula la carrera: ambos contextos comprueban "no existe" y los dos insertan.
        using var contextoA = NuevoContexto();
        using var contextoB = NuevoContexto();
        var repoA = new UsuarioRepository(contextoA);
        var repoB = new UsuarioRepository(contextoB);

        Assert.False(await repoA.CorreoExistsAsync(new Email("persona@ejemplo.com")));
        Assert.False(await repoB.CorreoExistsAsync(new Email("persona@ejemplo.com")));

        await repoA.SaveAsync(NuevoUsuario());
        await Assert.ThrowsAsync<CorreoYaRegistradoException>(() => repoB.SaveAsync(NuevoUsuario()));

        using var verificacion = NuevoContexto();
        Assert.Equal(1, await verificacion.usuarios.CountAsync());
    }

    [Fact]
    public async Task RF_CA_01_ElCorreoSeNormalizaAMinusculas_AsiQueMayusculasTampocoDuplican()
    {
        using var contexto = NuevoContexto();
        var repo = new UsuarioRepository(contexto);
        await repo.SaveAsync(NuevoUsuario("persona@ejemplo.com"));

        using var otroContexto = NuevoContexto();
        await Assert.ThrowsAsync<CorreoYaRegistradoException>(
            () => new UsuarioRepository(otroContexto).SaveAsync(NuevoUsuario("PERSONA@Ejemplo.COM")));
    }

    [Fact]
    public async Task RF_CA_01_UnCorreoDistinto_SeGuardaSinProblema()
    {
        using var contexto = NuevoContexto();
        var repo = new UsuarioRepository(contexto);
        await repo.SaveAsync(NuevoUsuario("una@ejemplo.com"));
        await repo.SaveAsync(NuevoUsuario("otra@ejemplo.com"));

        using var verificacion = NuevoContexto();
        Assert.Equal(2, await verificacion.usuarios.CountAsync());
    }

    // ---------- RD-09: lo que se guarda sobrevive y se lee en otro contexto ----------

    [Fact]
    public async Task RD_09_ElUsuarioGuardadoSeLeeIgualEnUnContextoNuevo()
    {
        var usuario = NuevoUsuario();
        usuario.EstablecerTokenActivacion("123456", DateTime.UtcNow.AddHours(24));
        using (var contexto = NuevoContexto())
        {
            await new UsuarioRepository(contexto).SaveAsync(usuario);
        }

        using var lectura = NuevoContexto();
        var leido = await new UsuarioRepository(lectura).GetByCorreoAsync(new Email("persona@ejemplo.com"));

        Assert.NotNull(leido);
        Assert.Equal(usuario.Id, leido!.Id);
        Assert.False(leido.Activo);
        Assert.True(leido.PendienteDeActivacion);
        Assert.Equal("123456", leido.TokenAcceso!.Valor);
        Assert.Equal("hash-guardado", leido.PasswordHash);
    }

    // ---------- UpdateAsync: solo escribe lo que cambió ----------

    [Fact]
    public async Task UpdateAsync_PersisteLosObjetosDeValorReemplazados()
    {
        using (var contexto = NuevoContexto())
        {
            await new UsuarioRepository(contexto).SaveAsync(NuevoUsuario());
        }

        // Cada paso carga y guarda en un contexto propio, como una petición HTTP nueva.
        using (var contexto = NuevoContexto())
        {
            var repo = new UsuarioRepository(contexto);
            var usuario = (await repo.GetByCorreoAsync(new Email("persona@ejemplo.com")))!;
            usuario.GenerarCodigoRecuperacion("654321", 1);
            usuario.RegistrarIntentoFallido();
            await repo.UpdateAsync(usuario);
        }

        using var lectura = NuevoContexto();
        var guardado = (await new UsuarioRepository(lectura).GetByCorreoAsync(new Email("persona@ejemplo.com")))!;
        Assert.Equal("654321", guardado.Recuperacion!.Valor);
        Assert.Equal(1, guardado.Bloqueo.IntentosFallidos);
    }

    [Fact]
    public async Task UpdateAsync_AlCompletarLaActivacion_ElCodigoPendienteSeBorra()
    {
        using (var contexto = NuevoContexto())
        {
            var usuario = NuevoUsuario();
            usuario.EstablecerTokenActivacion("123456", DateTime.UtcNow.AddHours(24));
            await new UsuarioRepository(contexto).SaveAsync(usuario);
        }

        using (var contexto = NuevoContexto())
        {
            var repo = new UsuarioRepository(contexto);
            var usuario = (await repo.GetByCorreoAsync(new Email("persona@ejemplo.com")))!;
            usuario.CompletarActivacion("123456");
            await repo.UpdateAsync(usuario);
        }

        using var lectura = NuevoContexto();
        var guardado = (await new UsuarioRepository(lectura).GetByCorreoAsync(new Email("persona@ejemplo.com")))!;
        Assert.True(guardado.Activo);
        Assert.Null(guardado.TokenAcceso);
    }

    [Fact]
    public async Task RD_06_UpdateAsync_ConservaElContadorDeIntentosDeActivacionQueOtraPeticionReservo()
    {
        var usuario = NuevoUsuario();
        usuario.EstablecerTokenActivacion("123456", DateTime.UtcNow.AddHours(24));
        using (var contexto = NuevoContexto())
        {
            await new UsuarioRepository(contexto).SaveAsync(usuario);
        }

        // La petición A carga al usuario (contador en 0 o nulo)...
        using var contextoA = NuevoContexto();
        var repoA = new UsuarioRepository(contextoA);
        var enA = (await repoA.GetByCorreoAsync(new Email("persona@ejemplo.com")))!;

        // ...mientras otra petición reserva 3 intentos de activación directamente en la base de datos...
        using (var comando = _conexion.CreateCommand())
        {
            comando.CommandText = "UPDATE usuarios SET IntentosActivacion = 3";
            comando.ExecuteNonQuery();
        }

        // ...y la petición A guarda un cambio ajeno al código de activación (un fallo de login).
        enA.RegistrarIntentoFallido();
        await repoA.UpdateAsync(enA);

        // Protección contra regresiones: si un guardado reescribiera todas las columnas, el contador volvería al valor
        // que esta petición leyó y el límite de 5 intentos se podría saltar. (Comprobado: con el Update() anterior este
        // escenario tampoco pisaba el contador; la prueba fija ese comportamiento para que no cambie.)
        Assert.Equal(3, LeerIntentosActivacion());
    }

    [Fact]
    public async Task UpdateAsync_ConUnaEntidadNoRastreada_LaGuardaCompleta()
    {
        var usuario = NuevoUsuario();
        using (var contexto = NuevoContexto())
        {
            await new UsuarioRepository(contexto).SaveAsync(usuario);
        }

        // Una entidad creada fuera del contexto actual (sin rastrear) también se puede actualizar.
        usuario.EstablecerTokenActivacion("999999", DateTime.UtcNow.AddHours(24));
        using (var contexto = NuevoContexto())
        {
            await new UsuarioRepository(contexto).UpdateAsync(usuario);
        }

        using var lectura = NuevoContexto();
        var guardado = (await new UsuarioRepository(lectura).GetByIdAsync(usuario.Id))!;
        Assert.Equal("999999", guardado.TokenAcceso!.Valor);
    }
}
