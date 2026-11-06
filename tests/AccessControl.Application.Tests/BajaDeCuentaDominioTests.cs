using AccessControl.Application.Service;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;

namespace AccessControl.Application.Tests;

/// <summary>Estados de la cuenta ante una desactivación o una baja pedida por su dueño (RF-CA-20, RD-04).</summary>
public class BajaDeCuentaDominioTests
{
    private static readonly DateTime Ahora = DateTime.UtcNow;

    private static Usuario Activo()
    {
        var usuario = new Usuario("Persona", new Email("persona@ejemplo.com"), "hash-guardado");
        usuario.Activar();
        return usuario;
    }

    // ---------------- Desactivar ----------------

    [Fact]
    public void RF_CA_20_QuienDesactivaSuCuentaQuedaInactivoYMarcadoComoSuDueno()
    {
        var usuario = Activo();

        usuario.SolicitarDesactivacion(Ahora);

        Assert.False(usuario.Activo);
        Assert.True(usuario.DesactivadaPorElUsuario);
        Assert.False(usuario.PendienteDeActivacion);
        Assert.Equal(Ahora, usuario.DesactivadaPorUsuarioEn);
    }

    [Fact]
    public void RF_CA_20_UnaCuentaDesactivadaPorUnAdministradorNoEsDeLaPersona()
    {
        var usuario = Activo();

        usuario.Desactivar();

        Assert.False(usuario.Activo);
        Assert.False(usuario.DesactivadaPorElUsuario);   // no se reactiva sola al iniciar sesión
    }

    [Fact]
    public void RF_CA_20_NoSePuedeDesactivarUnaCuentaQueYaEstaInactiva()
    {
        var usuario = Activo();
        usuario.SolicitarDesactivacion(Ahora);

        Assert.Throws<InvalidOperationException>(() => usuario.SolicitarDesactivacion(Ahora));
    }

    [Fact]
    public void RF_CA_20_IniciarSesionReactivaLaCuentaQueSuDuenoHabiaDesactivado()
    {
        var usuario = Activo();
        usuario.SolicitarDesactivacion(Ahora);

        usuario.ReactivarTrasDesactivacion();

        Assert.True(usuario.Activo);
        Assert.False(usuario.DesactivadaPorElUsuario);
        Assert.Null(usuario.DesactivadaPorUsuarioEn);
    }

    [Fact]
    public void RF_CA_20_NoSeReactivaPorLoginUnaCuentaDesactivadaPorUnAdministrador()
    {
        var usuario = Activo();
        usuario.Desactivar();

        Assert.Throws<InvalidOperationException>(usuario.ReactivarTrasDesactivacion);
        Assert.False(usuario.Activo);
    }

    [Fact]
    public void RF_CA_20_UnAdministradorQueActivaUnaCuentaLaLimpiaDeLaMarcaDelDueno()
    {
        var usuario = Activo();
        usuario.SolicitarDesactivacion(Ahora);

        usuario.Activar();

        Assert.True(usuario.Activo);
        Assert.Null(usuario.DesactivadaPorUsuarioEn);
    }

    // ---------------- Baja con periodo de gracia ----------------

    [Fact]
    public void RF_CA_20_ProgramarLaBajaFijaLaFechaDeBorradoYLaCuentaSigueActiva()
    {
        var usuario = Activo();

        usuario.ProgramarBaja(Ahora, 30);

        Assert.True(usuario.TieneBajaProgramada);
        Assert.Equal(Ahora, usuario.BajaSolicitadaEn);
        Assert.Equal(Ahora.AddDays(30), usuario.BajaProgramadaPara);
        Assert.True(usuario.Activo);   // durante la gracia el login funciona para poder cancelar
    }

    [Fact]
    public void RF_CA_20_PedirLaBajaOtraVezNoMueveLaFechaOriginal()
    {
        var usuario = Activo();
        usuario.ProgramarBaja(Ahora, 30);

        usuario.ProgramarBaja(Ahora.AddDays(10), 30);

        Assert.Equal(Ahora, usuario.BajaSolicitadaEn);
        Assert.Equal(Ahora.AddDays(30), usuario.BajaProgramadaPara);
    }

    [Fact]
    public void RD_07_UnPeriodoDeGraciaNegativoSeRechaza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Activo().ProgramarBaja(Ahora, -1));
    }

    [Fact]
    public void RF_CA_20_CancelarLaBajaLaQuitaYAvisaSiNoHabiaNinguna()
    {
        var usuario = Activo();
        Assert.False(usuario.CancelarBaja());

        usuario.ProgramarBaja(Ahora, 30);

        Assert.True(usuario.CancelarBaja());
        Assert.False(usuario.TieneBajaProgramada);
        Assert.Null(usuario.BajaProgramadaPara);
        Assert.Null(usuario.BajaSolicitadaEn);
    }

    [Fact]
    public void RF_CA_20_LaBajaVenceSoloCuandoTerminaElPeriodoDeGracia()
    {
        var usuario = Activo();
        Assert.False(usuario.BajaVencida(Ahora.AddYears(1)));   // sin baja programada nunca vence

        usuario.ProgramarBaja(Ahora, 30);

        Assert.False(usuario.BajaVencida(Ahora.AddDays(30).AddSeconds(-1)));
        Assert.True(usuario.BajaVencida(Ahora.AddDays(30)));
        Assert.True(usuario.BajaVencida(Ahora.AddDays(31)));
    }

    // ---------------- Opciones ----------------

    [Fact]
    public void RF_CA_20_LasOpcionesPorDefectoSon30DiasDeGraciaYUnaPasadaAlDia()
    {
        var opciones = new OpcionesPrivacidad();

        opciones.Validar();

        Assert.Equal(30, opciones.DiasGraciaBaja);
        Assert.Equal(1440, opciones.IntervaloEjecucionMinutos);
    }

    [Theory]
    [InlineData(-1, 60)]
    [InlineData(366, 60)]
    [InlineData(30, 0)]
    [InlineData(30, 10081)]
    public void RD_07_UnaConfiguracionFueraDeRangoDetieneElArranque(int dias, int minutos)
    {
        var opciones = new OpcionesPrivacidad { DiasGraciaBaja = dias, IntervaloEjecucionMinutos = minutos };

        Assert.Throws<InvalidOperationException>(opciones.Validar);
    }

    [Fact]
    public void RF_CA_20_UnPeriodoDeGraciaDeCeroDiasEsValido()
    {
        new OpcionesPrivacidad { DiasGraciaBaja = 0 }.Validar();
    }
}
