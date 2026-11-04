using DocumentManager.Domain.Entities;
using DocumentManager.Domain.Results;

namespace DocumentManager.Domain.Tests;

public class DocumentoTests
{
    private static readonly Guid Dueno = Guid.NewGuid();

    private static Documento CrearDocumento() =>
        new("informe.pdf", "application/pdf", 2048, Dueno, "dueno@ejemplo.com");

    [Fact]
    [Trait("Requirement", "RF-DOC-01")]
    public void RF_DOC_01_Constructor_RegistraNombreTipoTamanoFechaYUsuario()
    {
        var antes = DateTime.UtcNow;

        var documento = CrearDocumento();

        Assert.NotEqual(Guid.Empty, documento.Id);
        Assert.Equal("informe.pdf", documento.NombreOriginal);
        Assert.Equal("application/pdf", documento.TipoContenido);
        Assert.Equal(2048, documento.TamanoBytes);
        Assert.Equal(Dueno, documento.UsuarioId);
        Assert.Equal("dueno@ejemplo.com", documento.CorreoUsuario);
        Assert.Equal(DateTimeKind.Utc, documento.FechaSubidaUtc.Kind);
        Assert.True(documento.FechaSubidaUtc >= antes);
        Assert.False(documento.Eliminado);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-01")]
    public void RF_DOC_01_Constructor_NombreAlmacenadoEsUnGuidIndependienteDelNombreOriginal()
    {
        var documento = new Documento("../../appsettings.json", "text/plain", 10, Dueno, "dueno@ejemplo.com");

        Assert.True(Guid.TryParseExact(documento.NombreAlmacenado, "N", out _));
        Assert.DoesNotContain("..", documento.NombreAlmacenado);
        Assert.DoesNotContain("/", documento.NombreAlmacenado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Constructor_NombreVacio_Lanza(string nombre)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new Documento(nombre, "text/plain", 10, Dueno, "dueno@ejemplo.com"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Constructor_TamanoNoPositivo_Lanza(long tamano)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Documento("a.txt", "text/plain", tamano, Dueno, "dueno@ejemplo.com"));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Constructor_UsuarioVacio_Lanza()
    {
        Assert.Throws<ArgumentException>(() =>
            new Documento("a.txt", "text/plain", 10, Guid.Empty, "dueno@ejemplo.com"));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Constructor_NombreMasLargoQueElMaximo_Lanza()
    {
        var nombre = new string('a', Documento.LongitudMaximaNombre + 1);

        Assert.Throws<ArgumentException>(() =>
            new Documento(nombre, "text/plain", 10, Dueno, "dueno@ejemplo.com"));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Constructor_TipoOCorreoVacios_Lanza()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new Documento("a.txt", " ", 10, Dueno, "dueno@ejemplo.com"));
        Assert.ThrowsAny<ArgumentException>(() =>
            new Documento("a.txt", "text/plain", 10, Dueno, " "));
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-01")]
    public void RF_DOC_01_Constructor_RecortaEspaciosDeLosCampos()
    {
        var documento = new Documento("  a.txt  ", " text/plain ", 10, Dueno, " dueno@ejemplo.com ");

        Assert.Equal("a.txt", documento.NombreOriginal);
        Assert.Equal("text/plain", documento.TipoContenido);
        Assert.Equal("dueno@ejemplo.com", documento.CorreoUsuario);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-01")]
    public void RF_DOC_01_Constructor_DosDocumentosMismoNombre_TienenNombreAlmacenadoDistinto()
    {
        var a = CrearDocumento();
        var b = CrearDocumento();

        Assert.NotEqual(a.NombreAlmacenado, b.NombreAlmacenado);
    }

    [Fact]
    [Trait("Requirement", "RD-11")]
    public void RD_11_Eliminar_FechaUnspecified_Falla()
    {
        var documento = CrearDocumento();

        var resultado = documento.Eliminar(DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified));

        Assert.Equal(DomainError.FechaEliminacionDebeSerUtc, resultado.Error);
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public void RD_08_DomainResult_MensajesGenericosYFailureNingunoLanza()
    {
        Assert.Throws<ArgumentException>(() => DomainResult.Failure(DomainError.Ninguno));
        Assert.Equal("El documento ya fue eliminado.", DomainResult.Failure(DomainError.DocumentoYaEliminado).ErrorMessage);
        Assert.Null(DomainResult.Success().ErrorMessage);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-03")]
    public void RF_DOC_03_PuedeAcceder_Dueno_EsVerdadero()
    {
        Assert.True(CrearDocumento().PuedeAcceder(Dueno, esAdministrador: false));
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-03")]
    public void RF_DOC_03_PuedeAcceder_OtroUsuarioEstandar_EsFalso()
    {
        Assert.False(CrearDocumento().PuedeAcceder(Guid.NewGuid(), esAdministrador: false));
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-03")]
    public void RF_DOC_03_PuedeAcceder_AdministradorAjeno_EsVerdadero()
    {
        Assert.True(CrearDocumento().PuedeAcceder(Guid.NewGuid(), esAdministrador: true));
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-04")]
    public void RF_DOC_04_Eliminar_MarcaEliminadoConFechaUtcYConservaElRegistro()
    {
        var documento = CrearDocumento();
        var fecha = DateTime.UtcNow;

        var resultado = documento.Eliminar(fecha);

        Assert.True(resultado.IsSuccess);
        Assert.True(documento.Eliminado);
        Assert.Equal(fecha, documento.FechaEliminacionUtc);
        Assert.Equal("informe.pdf", documento.NombreOriginal);
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-04")]
    public void RF_DOC_04_Eliminar_YaEliminado_Falla()
    {
        var documento = CrearDocumento();
        documento.Eliminar(DateTime.UtcNow);

        var resultado = documento.Eliminar(DateTime.UtcNow);

        Assert.False(resultado.IsSuccess);
        Assert.Equal(DomainError.DocumentoYaEliminado, resultado.Error);
    }

    [Fact]
    [Trait("Requirement", "RD-11")]
    public void RD_11_Eliminar_FechaNoUtc_FallaYNoMarcaEliminado()
    {
        var documento = CrearDocumento();

        var resultado = documento.Eliminar(DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local));

        Assert.False(resultado.IsSuccess);
        Assert.Equal(DomainError.FechaEliminacionDebeSerUtc, resultado.Error);
        Assert.False(documento.Eliminado);
    }
}
