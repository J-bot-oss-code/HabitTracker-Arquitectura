using AccessControl.Application.Service;
using AccessControl.Domain.Entities;

namespace AccessControl.Application.Tests;

/// <summary>Reglas de la entidad de sesión y de la configuración de sesiones (RF-CA-12, RF-CA-18, RD-07).</summary>
public class SesionesDominioYOpcionesTests
{
    private static readonly string Hash = new('a', SesionUsuario.LongitudHash);
    private static readonly string OtroHash = new('b', SesionUsuario.LongitudHash);
    private static readonly TimeSpan Duracion = TimeSpan.FromDays(14);

    private static SesionUsuario Nueva(DateTime? ahora = null, string? dispositivo = "Firefox", string? ip = "10.0.0.1") =>
        new(Guid.NewGuid(), Hash, ahora ?? DateTime.UtcNow, Duracion, dispositivo, ip);

    // ---------------- Entidad ----------------

    [Fact]
    public void RF_CA_18_UnaSesionNuevaEstaActivaHastaQueVence()
    {
        var ahora = DateTime.UtcNow;
        var sesion = Nueva(ahora);

        Assert.True(sesion.EstaActiva(ahora));
        Assert.True(sesion.EstaActiva(ahora + Duracion - TimeSpan.FromSeconds(1)));
        Assert.False(sesion.EstaActiva(ahora + Duracion));
        Assert.Equal(ahora + Duracion, sesion.ExpiraEn);
        Assert.Null(sesion.RevocadaEn);
    }

    [Fact]
    public void RF_CA_18_UnaSesionRevocadaNoEstaActiva_YRevocarNoCambiaLaFechaOriginal()
    {
        var ahora = DateTime.UtcNow;
        var sesion = Nueva(ahora);

        sesion.Revocar(ahora.AddMinutes(1));
        sesion.Revocar(ahora.AddMinutes(5));

        Assert.False(sesion.EstaActiva(ahora.AddMinutes(2)));
        Assert.Equal(ahora.AddMinutes(1), sesion.RevocadaEn);
    }

    [Fact]
    public void RF_CA_12_LaSiguienteDeLaFamiliaConservaFamiliaDispositivoYOrigen_PeroEsOtraSesion()
    {
        var inicio = DateTime.UtcNow.AddDays(-3);
        var actual = Nueva(inicio, "Firefox en Windows", "10.0.0.1");
        var ahora = DateTime.UtcNow;

        var siguiente = actual.SiguienteEnLaFamilia(OtroHash, ahora, Duracion);

        Assert.NotEqual(actual.Id, siguiente.Id);
        Assert.Equal(actual.FamiliaId, siguiente.FamiliaId);
        Assert.Equal(actual.UsuarioId, siguiente.UsuarioId);
        Assert.Equal(OtroHash, siguiente.TokenHash);
        Assert.Equal(actual.CreadaEn, siguiente.CreadaEn);   // el inicio de la sesión no cambia al renovar
        Assert.Equal(ahora, siguiente.UltimoUsoEn);
        Assert.Equal(ahora + Duracion, siguiente.ExpiraEn);  // la vigencia vuelve a empezar
        Assert.Equal("Firefox en Windows", siguiente.Dispositivo);
        Assert.Equal("10.0.0.1", siguiente.IpCreacion);
        Assert.Null(siguiente.RevocadaEn);
    }

    [Fact]
    public void RD_07_CadaInicioDeSesionEsUnaFamiliaNueva()
    {
        Assert.NotEqual(Nueva().FamiliaId, Nueva().FamiliaId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u0007\u0001")]
    public void RD_07_UnDispositivoVacioOSoloConCaracteresDeControlSeMuestraComoDesconocido(string? dispositivo)
    {
        Assert.Equal(SesionUsuario.DispositivoDesconocido, Nueva(dispositivo: dispositivo).Dispositivo);
    }

    [Fact]
    public void RD_07_ElDispositivoSeLimpiaDeCaracteresDeControlYSeTrunca()
    {
        var sesion = Nueva(dispositivo: "Mozilla\r\n<b>x</b>" + new string('z', 500));

        Assert.DoesNotContain('\n', sesion.Dispositivo);
        Assert.DoesNotContain('\r', sesion.Dispositivo);
        Assert.Equal(SesionUsuario.LongitudMaximaDispositivo, sesion.Dispositivo.Length);
    }

    [Fact]
    public void RD_07_LaIpSeTruncaYUnaIpVaciaSeGuardaComoNula()
    {
        Assert.Equal(SesionUsuario.LongitudMaximaIp, Nueva(ip: new string('9', 200)).IpCreacion!.Length);
        Assert.Null(Nueva(ip: "  ").IpCreacion);
        Assert.Null(Nueva(ip: null).IpCreacion);
    }

    [Fact]
    public void RD_07_NoSeCreaUnaSesionConDatosInvalidos()
    {
        var ahora = DateTime.UtcNow;

        Assert.Throws<ArgumentException>(() => new SesionUsuario(Guid.Empty, Hash, ahora, Duracion, null, null));
        Assert.Throws<ArgumentException>(() => new SesionUsuario(Guid.NewGuid(), "", ahora, Duracion, null, null));
        Assert.Throws<ArgumentException>(() => new SesionUsuario(Guid.NewGuid(), "corto", ahora, Duracion, null, null));
        Assert.Throws<ArgumentException>(() => new SesionUsuario(Guid.NewGuid(), Hash, ahora, TimeSpan.Zero, null, null));
    }

    // ---------------- Opciones ----------------

    [Fact]
    public void RF_CA_18_LasOpcionesPorDefectoSonSegurasYValidas()
    {
        var opciones = new OpcionesSesion();

        opciones.Validar();

        Assert.Equal(14, opciones.DiasRefresh);
        Assert.True(opciones.CookieSegura);
        Assert.Equal("Strict", opciones.CookieSameSite);
        Assert.Equal(TimeSpan.FromDays(14), opciones.DuracionRefresh);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(91)]
    public void RD_07_UnaDuracionFueraDeRangoDetieneElArranque(int dias)
    {
        Assert.Throws<InvalidOperationException>(() => new OpcionesSesion { DiasRefresh = dias }.Validar());
    }

    [Fact]
    public void RD_07_UnSameSiteDesconocidoDetieneElArranque()
    {
        Assert.Throws<InvalidOperationException>(() => new OpcionesSesion { CookieSameSite = "Cualquiera" }.Validar());
    }

    [Fact]
    public void RD_07_SameSiteNoneExigeCookieSegura()
    {
        Assert.Throws<InvalidOperationException>(() => new OpcionesSesion { CookieSameSite = "None", CookieSegura = false }.Validar());

        new OpcionesSesion { CookieSameSite = "none", CookieSegura = true }.Validar();
        new OpcionesSesion { CookieSameSite = "Lax", CookieSegura = false }.Validar();
    }
}
