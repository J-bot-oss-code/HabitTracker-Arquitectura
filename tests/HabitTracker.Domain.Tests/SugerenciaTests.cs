using HabitTracker.Domain.Sugerencias;

namespace HabitTracker.Domain.Tests;

/// <summary>Quejas y mejoras: el texto y el estado se validan en un solo punto, sin base de datos (RD-04, RD-07, RD-12).</summary>
public class SugerenciaTests
{
    private static readonly DateTime Ahora = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Ana = Guid.NewGuid();

    private static Sugerencia Nueva(string texto = "Me gustaría ver mis rachas en un calendario.") =>
        new(Ana, TipoSugerencia.Mejora, texto, Ahora);

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_UnaSugerenciaValida_NaceNuevaYConElTextoSinEspaciosSobrantes()
    {
        var s = Nueva("   Me gustaría ver mis rachas.   ");

        Assert.Equal("Me gustaría ver mis rachas.", s.Texto);
        Assert.Equal(EstadoSugerencia.Nueva, s.Estado);
        Assert.Equal((Ana, Ahora), (s.UsuarioId, s.CreadaEnUtc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("         ")]
    [InlineData("corto")]
    [Trait("Requirement", "RD-07")]
    public void RD_07_UnTextoVacioOMuyCorto_SeRechaza(string? texto) =>
        Assert.Throws<ArgumentException>(() => Nueva(texto!));

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_ElLimiteDeLongitudEsExacto()
    {
        Assert.Equal(Sugerencia.LongitudMaxima, Nueva(new string('a', Sugerencia.LongitudMaxima)).Texto.Length);
        Assert.Throws<ArgumentException>(() => Nueva(new string('a', Sugerencia.LongitudMaxima + 1)));
        Assert.Equal(Sugerencia.LongitudMinima, Nueva(new string('a', Sugerencia.LongitudMinima)).Texto.Length);
        Assert.Throws<ArgumentException>(() => Nueva(new string('a', Sugerencia.LongitudMinima - 1)));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_LaPersonaElTipoYLaFechaUtcSonObligatorios()
    {
        Assert.Throws<ArgumentException>(() => new Sugerencia(Guid.Empty, TipoSugerencia.Queja, "Un texto suficientemente largo", Ahora));
        Assert.Throws<ArgumentException>(() => new Sugerencia(Ana, (TipoSugerencia)99, "Un texto suficientemente largo", Ahora));
        Assert.Throws<ArgumentException>(() => new Sugerencia(Ana, TipoSugerencia.Queja, "Un texto suficientemente largo", DateTime.Now));
    }

    [Fact]
    [Trait("Requirement", "RD-04")]
    public void RD_04_Estado_NuevaLeidaResuelta_YReabrirComoLeida()
    {
        var s = Nueva();

        s.CambiarEstado(EstadoSugerencia.Leida, Ahora.AddHours(1));
        Assert.Equal((EstadoSugerencia.Leida, Ahora.AddHours(1)), (s.Estado, s.EstadoCambiadoEnUtc));

        s.CambiarEstado(EstadoSugerencia.Resuelta, Ahora.AddHours(2));
        Assert.Equal(EstadoSugerencia.Resuelta, s.Estado);

        s.CambiarEstado(EstadoSugerencia.Leida, Ahora.AddHours(3));
        Assert.Equal(EstadoSugerencia.Leida, s.Estado);
    }

    [Fact]
    [Trait("Requirement", "RD-04")]
    public void RD_04_SePuedeResolverSinPasarPorLeida()
    {
        var s = Nueva();

        s.CambiarEstado(EstadoSugerencia.Resuelta, Ahora.AddHours(1));

        Assert.Equal(EstadoSugerencia.Resuelta, s.Estado);
    }

    [Fact]
    [Trait("Requirement", "RD-04")]
    public void RD_04_NadaVuelveANueva_YRepetirElMismoEstadoNoCambiaLaFecha()
    {
        var s = Nueva();
        s.CambiarEstado(EstadoSugerencia.Leida, Ahora.AddHours(1));

        Assert.Throws<ArgumentException>(() => s.CambiarEstado(EstadoSugerencia.Nueva, Ahora.AddHours(2)));
        Assert.Throws<ArgumentException>(() => s.CambiarEstado((EstadoSugerencia)99, Ahora.AddHours(2)));

        s.CambiarEstado(EstadoSugerencia.Leida, Ahora.AddHours(5)); // el mismo estado: nada cambia
        Assert.Equal(Ahora.AddHours(1), s.EstadoCambiadoEnUtc);
    }

    [Fact]
    [Trait("Requirement", "RD-04")]
    public void RD_04_UnaNuevaSePuedeMarcarNuevaSinEfecto()
    {
        var s = Nueva();

        s.CambiarEstado(EstadoSugerencia.Nueva, Ahora.AddHours(1));

        Assert.Equal((EstadoSugerencia.Nueva, (DateTime?)null), (s.Estado, s.EstadoCambiadoEnUtc));
    }

    [Fact]
    [Trait("Requirement", "RD-11")]
    public void RD_11_CambiarElEstadoExigeUnaFechaUtc() =>
        Assert.Throws<ArgumentException>(() => Nueva().CambiarEstado(EstadoSugerencia.Leida, DateTime.Now));
}
