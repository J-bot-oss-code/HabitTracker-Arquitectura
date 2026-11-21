using AccessControl.Application.Service;

namespace AccessControl.Application.Tests;

/// <summary>La sesión guarda «navegador en sistema» y no el User-Agent completo (privacidad; se muestra en Sesiones activas).</summary>
public class DescripcionDispositivoTests
{
    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36", "Chrome en Windows")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0", "Edge en Windows")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Mobile/15E148 Safari/604.1", "Safari en iPhone")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/131.0 Mobile/15E148 Safari/604.1", "Chrome en iPhone")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/26.0 Chrome/122.0.0.0 Mobile Safari/537.36", "Samsung Internet en Android")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Safari/605.1.15", "Safari en Mac")]
    [InlineData("Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:133.0) Gecko/20100101 Firefox/133.0", "Firefox en Linux")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 OPR/115.0.0.0", "Opera en Windows")]
    public void RD_07_ElUserAgentSeResumeEnNavegadorYSistema(string userAgent, string esperado) =>
        Assert.Equal(esperado, DescripcionDispositivo.De(userAgent));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("curl/8.5.0")]
    public void RD_07_SinUserAgentReconocibleSeGuardaComoDesconocido(string? userAgent) =>
        Assert.Equal(DescripcionDispositivo.Desconocido, DescripcionDispositivo.De(userAgent));

    [Fact]
    public void RD_07_NoGuardaVersionesNiModeloDelEquipo()
    {
        var resultado = DescripcionDispositivo.De(
            "Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Mobile Safari/537.36");

        Assert.Equal("Chrome en Android", resultado);
        Assert.DoesNotContain("SM-S918B", resultado);
        Assert.DoesNotContain("122", resultado);
    }
}
