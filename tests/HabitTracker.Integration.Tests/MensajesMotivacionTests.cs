using System.Globalization;
using System.Text;
using HabitTracker.Domain.Motivacion;
using HabitTracker.Infrastructure.Contenido;

namespace HabitTracker.Integration.Tests;

/// <summary>
/// Fase 4 de HabitTracker v2: prueba de contenido de <c>mensajes-motivacion.json</c>. Un mensaje que culpe, hable del cuerpo o
/// se pase de largo rompe el CI en lugar de llegar a la persona (P3 y P4).
/// </summary>
public class MensajesMotivacionTests
{
    // Lo prohibido en la motivación (referencias/producto.md): el cuerpo, los números de la báscula y los textos de culpa.
    private static readonly string[] Prohibidas =
    [
        "peso", "kg", "kilo", "caloria", "dieta", "imc", "gordo", "gorda", "flaco", "delgad", "cuerpo", "apariencia", "figura",
        "perdiste", "fallaste", "falla", "culpa", "castigo", "vergüenza", "vago", "flojo", "pereza", "deber", "obligaci",
        "racha perdida", "otra vez no", "no cumpliste", "incumpl",
    ];

    // Español dominicano neutro con tuteo: ni usted ni voseo.
    private static readonly string[] Formas = ["usted", "ustedes", " vos ", "tenés", "querés", "podés", "sos "];

    private static string Normalizar(string texto)
    {
        var descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string([.. descompuesto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);
    }

    private static Stream Json(string texto) => new MemoryStream(Encoding.UTF8.GetBytes(texto));

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Mensajes_CadaCategoriaTieneAlMenosDosYTodosCumplenElLargoMaximo()
    {
        var mensajes = LectorMensajesMotivacion.Leer();

        foreach (var categoria in Enum.GetValues<CategoriaMensaje>())
            Assert.True(mensajes.Count(m => m.Categoria == categoria) >= LectorMensajesMotivacion.MinimoPorCategoria, categoria.ToString());
        Assert.All(mensajes, m => Assert.InRange(m.Texto.Length, 1, SelectorMensajesMotivacion.LongitudMaxima));
        Assert.Equal(mensajes.Count, mensajes.Select(m => m.Texto).Distinct().Count());
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Mensajes_NingunoCulpaNiHablaDelCuerpoNiDelPesoComparandoSinAcentosNiMayusculas()
    {
        foreach (var mensaje in LectorMensajesMotivacion.Leer())
        {
            var texto = Normalizar(mensaje.Texto);
            foreach (var palabra in Prohibidas)
                Assert.False(texto.Contains(Normalizar(palabra), StringComparison.Ordinal), $"«{palabra}» en «{mensaje.Texto}»");
        }
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Mensajes_TuteoSinUstedNiVoseo()
    {
        foreach (var mensaje in LectorMensajesMotivacion.Leer())
        {
            var texto = $" {Normalizar(mensaje.Texto)} ";
            foreach (var forma in Formas)
                Assert.DoesNotContain(forma, texto, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Mensajes_ElRegresoNuncaEsUnReproche_SeInvitaYSeDejaLaPuertaAbierta()
    {
        var regreso = LectorMensajesMotivacion.Leer().Where(m => m.Categoria == CategoriaMensaje.Regreso).ToList();

        // Cada uno ofrece algo concreto y pequeño o dice que se espera, y ninguno recuerda lo que no se hizo.
        Assert.All(regreso, m =>
        {
            var t = Normalizar(m.Texto);
            Assert.True(
                new[] { "cuando", "minim", "pequen", "facil", "espera", "aqui", "sin prisa", "sin presion" }.Any(t.Contains),
                m.Texto);
            Assert.DoesNotContain("no hiciste", t, StringComparison.Ordinal);
            Assert.DoesNotContain("dias sin", t, StringComparison.Ordinal);
        });
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public void RD_08_Lector_CampoDesconocidoCategoriaInvalidaTextoLargoORepetido_Falla()
    {
        static string Archivo(params string[] mensajes) => $$"""{"version":1,"mensajes":[{{string.Join(",", mensajes)}}]}""";
        static string M(string categoria, string texto) => $$"""{"categoria":"{{categoria}}","texto":"{{texto}}"}""";
        var todasLasCategorias = Enum.GetValues<CategoriaMensaje>()
            .SelectMany(c => new[] { M(c.ToString(), $"{c} uno"), M(c.ToString(), $"{c} dos") }).ToArray();

        Assert.Equal(12, LectorMensajesMotivacion.Leer(Json(Archivo(todasLasCategorias))).Count);

        Assert.ThrowsAny<Exception>(() => LectorMensajesMotivacion.Leer(Json(Archivo([.. todasLasCategorias, M("Magia", "Algo")]))));
        Assert.ThrowsAny<Exception>(() => LectorMensajesMotivacion.Leer(Json(Archivo([.. todasLasCategorias, M("Inicio", new string('a', 161))]))));
        Assert.ThrowsAny<Exception>(() => LectorMensajesMotivacion.Leer(Json(Archivo([.. todasLasCategorias, M("Inicio", "Inicio uno")]))));
        Assert.ThrowsAny<Exception>(() => LectorMensajesMotivacion.Leer(Json(Archivo([.. todasLasCategorias, M("Inicio", "  ")]))));
        Assert.ThrowsAny<Exception>(() => LectorMensajesMotivacion.Leer(Json(
            Archivo([.. todasLasCategorias]).Replace("\"version\"", "\"extra\":1,\"version\"", StringComparison.Ordinal))));
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public void RD_08_Lector_UnaCategoriaConUnSoloMensaje_Falla_PorqueElMismoTextoSaldriaDosDiasSeguidos()
    {
        var sinSuficientes = Enum.GetValues<CategoriaMensaje>()
            .SelectMany(c => c == CategoriaMensaje.Cierre
                ? [$$"""{"categoria":"Cierre","texto":"Uno solo"}"""]
                : new[] { $$"""{"categoria":"{{c}}","texto":"{{c}} uno"}""", $$"""{"categoria":"{{c}}","texto":"{{c}} dos"}""" });

        var error = Assert.ThrowsAny<Exception>(() =>
            LectorMensajesMotivacion.Leer(Json($$"""{"version":1,"mensajes":[{{string.Join(",", sinSuficientes)}}]}""")));

        Assert.Contains("Cierre", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "RD-12")]
    public void RD_12_CatalogoMensajes_SeLeeUnaVezYLoEntregaTalCual()
    {
        var catalogo = new CatalogoMensajes();

        Assert.Same(catalogo.Todos, catalogo.Todos);
        Assert.Equal(LectorMensajesMotivacion.Leer().Count, catalogo.Todos.Count);
    }
}
