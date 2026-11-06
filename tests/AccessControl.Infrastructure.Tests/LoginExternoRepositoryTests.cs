using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Excepciones;
using AccessControl.Infrastructure.Persistence;
using AccessControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AccessControl.Infrastructure.Tests;

/// <summary>Persistencia real de los vínculos con Google y de las cuentas sin contraseña, con EF Core sobre SQLite en memoria.</summary>
public sealed class LoginExternoRepositoryTests : IDisposable
{
    private readonly SqliteConnection _conexion = new("DataSource=:memory:");

    public LoginExternoRepositoryTests()
    {
        _conexion.Open();
        using var contexto = NuevoContexto();
        contexto.Database.EnsureCreated();
    }

    public void Dispose() => _conexion.Dispose();

    private ApplicationDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conexion).Options);

    private static Usuario CuentaDeGoogle(string correo = "persona@ejemplo.com") =>
        Usuario.CrearConProveedorExterno("Persona de Google", new Email(correo));

    private static ConsentimientoUsuario Consentimiento(Usuario u) =>
        ConsentimientoUsuario.DelRegistro(u.Id, "v2.1", "v1.4", false, DateTime.UtcNow, ConsentimientoUsuario.OrigenGoogle);

    private static LoginExterno Vinculo(Usuario u, string subject = "sub-1") =>
        new(u.Id, LoginExterno.ProveedorGoogle, subject, DateTime.UtcNow);

    private async Task<Usuario> GuardarCuentaConPassword(string correo = "otra@ejemplo.com")
    {
        var usuario = new Usuario("Otra", new Email(correo), "hash-guardado");
        using var contexto = NuevoContexto();
        await new UsuarioRepository(contexto).SaveAsync(usuario);
        return usuario;
    }

    private int Contar<T>(Func<ApplicationDbContext, IQueryable<T>> consulta)
    {
        using var contexto = NuevoContexto();
        return consulta(contexto).Count();
    }

    // ---------- Cuenta, consentimiento y vínculo en una sola operación ----------

    [Fact]
    public async Task RF_CA_01_ElAltaConGoogleGuardaCuentaConsentimientoYVinculoJuntos_YLaCuentaNoTieneContrasena()
    {
        var usuario = CuentaDeGoogle();

        using (var contexto = NuevoContexto())
        {
            await new UsuarioRepository(contexto).SaveAsync(usuario, Consentimiento(usuario), Vinculo(usuario));
        }

        using var lectura = NuevoContexto();
        var guardado = await new UsuarioRepository(lectura).GetByIdAsync(usuario.Id);
        Assert.NotNull(guardado);
        Assert.Null(guardado!.PasswordHash);          // la columna admite nulos
        Assert.False(guardado.TienePassword);
        Assert.True(guardado.Activo);
        Assert.Equal(1, Contar(c => c.ConsentimientosUsuario.Where(x => x.UsuarioId == usuario.Id && x.Origen == "google")));
        Assert.Equal(1, Contar(c => c.LoginsExternos.Where(x => x.UsuarioId == usuario.Id)));
    }

    [Fact]
    public async Task RF_CA_01_SiElCorreoYaExisteNoQuedaNingunConsentimientoNiVinculoHuerfano()
    {
        await GuardarCuentaConPassword("persona@ejemplo.com");
        var duplicada = CuentaDeGoogle("persona@ejemplo.com");

        using (var contexto = NuevoContexto())
        {
            await Assert.ThrowsAsync<CorreoYaRegistradoException>(
                () => new UsuarioRepository(contexto).SaveAsync(duplicada, Consentimiento(duplicada), Vinculo(duplicada)));
        }

        Assert.Equal(0, Contar(c => c.LoginsExternos));
        Assert.Equal(0, Contar(c => c.ConsentimientosUsuario));
        Assert.Equal(1, Contar(c => c.usuarios));
    }

    // ---------- Búsquedas ----------

    [Fact]
    public async Task RF_CA_03_SeBuscaElVinculoPorProveedorYSubject_NoPorCorreo()
    {
        var usuario = CuentaDeGoogle();
        using (var contexto = NuevoContexto())
        {
            await new UsuarioRepository(contexto).SaveAsync(usuario, Consentimiento(usuario), Vinculo(usuario, "sub-1"));
        }

        using var lectura = NuevoContexto();
        var repositorio = new LoginExternoRepository(lectura);

        Assert.Equal(usuario.Id, (await repositorio.ObtenerPorSubjectAsync("google", "sub-1"))!.UsuarioId);
        Assert.Null(await repositorio.ObtenerPorSubjectAsync("google", "otro-sub"));
        Assert.Equal("sub-1", (await repositorio.ObtenerDeUsuarioAsync(usuario.Id, "google"))!.Subject);
        Assert.Null(await repositorio.ObtenerDeUsuarioAsync(Guid.NewGuid(), "google"));
        Assert.Single(await repositorio.ListarDeUsuarioAsync(usuario.Id));
        Assert.Empty(await repositorio.ListarDeUsuarioAsync(Guid.NewGuid()));
    }

    // ---------- Unicidad ----------

    [Fact]
    public async Task RD_09_UnaIdentidadDeGoogleNoPuedeVincularseADosCuentas()
    {
        var a = await GuardarCuentaConPassword("a@ejemplo.com");
        var b = await GuardarCuentaConPassword("b@ejemplo.com");
        using (var contexto = NuevoContexto())
        {
            await new LoginExternoRepository(contexto).AgregarAsync(Vinculo(a, "mismo-sub"));
        }

        using var segundo = NuevoContexto();
        await Assert.ThrowsAsync<DbUpdateException>(() => new LoginExternoRepository(segundo).AgregarAsync(Vinculo(b, "mismo-sub")));
    }

    [Fact]
    public async Task RD_09_UnaCuentaNoPuedeTenerDosIdentidadesDeGoogle()
    {
        var a = await GuardarCuentaConPassword("a@ejemplo.com");
        using (var contexto = NuevoContexto())
        {
            await new LoginExternoRepository(contexto).AgregarAsync(Vinculo(a, "sub-1"));
        }

        using var segundo = NuevoContexto();
        await Assert.ThrowsAsync<DbUpdateException>(() => new LoginExternoRepository(segundo).AgregarAsync(Vinculo(a, "sub-2")));
    }

    // ---------- Desvincular y baja ----------

    [Fact]
    public async Task RF_CA_03_DesvincularQuitaSoloElVinculoDeEsaCuenta()
    {
        var a = await GuardarCuentaConPassword("a@ejemplo.com");
        var b = await GuardarCuentaConPassword("b@ejemplo.com");
        using (var contexto = NuevoContexto())
        {
            var repositorio = new LoginExternoRepository(contexto);
            await repositorio.AgregarAsync(Vinculo(a, "sub-a"));
            await repositorio.AgregarAsync(Vinculo(b, "sub-b"));
        }

        using var accion = NuevoContexto();
        var repo = new LoginExternoRepository(accion);

        Assert.True(await repo.EliminarAsync(a.Id, "google"));
        Assert.False(await repo.EliminarAsync(a.Id, "google"));   // ya no existe
        Assert.Equal(1, Contar(c => c.LoginsExternos));
        Assert.NotNull(await repo.ObtenerPorSubjectAsync("google", "sub-b"));
    }

    [Fact]
    public async Task RD_09_AlEliminarLaCuentaSeEliminanSusVinculos()
    {
        var usuario = CuentaDeGoogle();
        using (var contexto = NuevoContexto())
        {
            await new UsuarioRepository(contexto).SaveAsync(usuario, Consentimiento(usuario), Vinculo(usuario));
        }

        using (var contexto = NuevoContexto())
        {
            contexto.usuarios.Remove(contexto.usuarios.Single(u => u.Id == usuario.Id));
            await contexto.SaveChangesAsync();
        }

        Assert.Equal(0, Contar(c => c.LoginsExternos));
    }

    // ---------- Cuenta de Google que crea su contraseña ----------

    [Fact]
    public async Task RF_CA_11_UnaCuentaSinContrasenaPuedeGuardarSuPrimeraContrasena()
    {
        var usuario = CuentaDeGoogle();
        using (var contexto = NuevoContexto())
        {
            await new UsuarioRepository(contexto).SaveAsync(usuario, Consentimiento(usuario), Vinculo(usuario));
        }

        using (var contexto = NuevoContexto())
        {
            var repositorio = new UsuarioRepository(contexto);
            var cargado = (await repositorio.GetByIdAsync(usuario.Id))!;
            cargado.GenerarCodigoRecuperacion("123456", 1);
            cargado.RestablecerPassword("hash-nuevo", "123456");
            await repositorio.UpdateAsync(cargado);
        }

        using var lectura = NuevoContexto();
        var final = (await new UsuarioRepository(lectura).GetByIdAsync(usuario.Id))!;
        Assert.Equal("hash-nuevo", final.PasswordHash);
        Assert.True(final.TienePassword);
    }
}
