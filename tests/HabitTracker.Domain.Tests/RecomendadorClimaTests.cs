using HabitTracker.Domain.Servicios;

namespace HabitTracker.Domain.Tests;

public class RecomendadorClimaTests
{
    private static CondicionClimatica Clima(decimal temperatura, bool precipitacion = false) =>
        new(temperatura, precipitacion, "descripción");

    [Theory]
    [InlineData(20, false, LugarSugerido.AireLibre)]
    [InlineData(5, false, LugarSugerido.AireLibre)]
    [InlineData(35, false, LugarSugerido.AireLibre)]
    [InlineData(4.9, false, LugarSugerido.BajoTecho)]
    [InlineData(35.1, false, LugarSugerido.BajoTecho)]
    [InlineData(20, true, LugarSugerido.BajoTecho)]
    [InlineData(-3, true, LugarSugerido.BajoTecho)]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_SugerirLugar_SegunTemperaturaYPrecipitacion(double temperatura, bool precipitacion, LugarSugerido esperado)
    {
        Assert.Equal(esperado, RecomendadorClima.SugerirLugar(Clima((decimal)temperatura, precipitacion)));
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(24.9, 0)]
    [InlineData(25, 250)]
    [InlineData(29.9, 250)]
    [InlineData(30, 500)]
    [InlineData(35, 500)]
    [InlineData(35.1, 1000)]
    [InlineData(42, 1000)]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_AguaAdicional_AumentaConElCalor(double temperatura, int esperadoMl)
    {
        Assert.Equal(esperadoMl, RecomendadorClima.AguaAdicionalMl(Clima((decimal)temperatura)));
    }

    [Theory]
    [InlineData(20, true, "precipitación")]
    [InlineData(40, false, "calor")]
    [InlineData(0, false, "frío")]
    [InlineData(20, false, "favorable")]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_ExplicarSugerencia_MencionaLaCausa(double temperatura, bool precipitacion, string fragmento)
    {
        var texto = RecomendadorClima.ExplicarSugerencia(Clima((decimal)temperatura, precipitacion));

        Assert.Contains(fragmento, texto);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_ConClimaNulo_LanzaArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => RecomendadorClima.SugerirLugar(null!));
        Assert.Throws<ArgumentNullException>(() => RecomendadorClima.AguaAdicionalMl(null!));
        Assert.Throws<ArgumentNullException>(() => RecomendadorClima.ExplicarSugerencia(null!));
    }
}
