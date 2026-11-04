using DocumentManager.Infrastructure.Storage;

namespace DocumentManager.Infrastructure.Tests;

public sealed class AlmacenamientoArchivosLocalTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "p4-docs-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
        {
            Directory.Delete(_raiz, recursive: true);
        }
    }

    private AlmacenamientoArchivosLocal Crear() => new(_raiz);

    [Fact]
    [Trait("Requirement", "RF-DOC-05")]
    public void RF_DOC_05_Constructor_CreaLaCarpetaRaizSiNoExiste()
    {
        Assert.False(Directory.Exists(_raiz));

        _ = Crear();

        Assert.True(Directory.Exists(_raiz));
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-05")]
    public async Task RF_DOC_05_Guardar_YAbrir_DevuelvenLosMismosBytes()
    {
        var almacen = Crear();
        byte[] original = [10, 20, 30, 40, 50];

        await almacen.GuardarAsync("abc123", new MemoryStream(original));
        await using var flujo = await almacen.AbrirLecturaAsync("abc123");

        using var copia = new MemoryStream();
        await flujo.CopyToAsync(copia);
        Assert.Equal(original, copia.ToArray());
        Assert.True(File.Exists(Path.Combine(_raiz, "abc123")));
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-05")]
    public async Task RF_DOC_05_Guardar_NoSobrescribeUnArchivoExistente()
    {
        var almacen = Crear();
        await almacen.GuardarAsync("abc123", new MemoryStream([1]));

        await Assert.ThrowsAsync<IOException>(() => almacen.GuardarAsync("abc123", new MemoryStream([2])));

        await using var flujo = await almacen.AbrirLecturaAsync("abc123");
        Assert.Equal(1, flujo.ReadByte());
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-03")]
    public async Task RF_DOC_03_Abrir_ArchivoInexistente_LanzaFileNotFound()
    {
        var almacen = Crear();

        await Assert.ThrowsAsync<FileNotFoundException>(() => almacen.AbrirLecturaAsync("noexiste"));
    }

    [Theory]
    [InlineData("../fuera.txt")]
    [InlineData("..\\fuera.txt")]
    [InlineData("sub/archivo")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_Guardar_NombreConRutaOVacio_SeRechazaYNoEscribeFueraDeLaRaiz(string nombre)
    {
        var almacen = Crear();

        await Assert.ThrowsAsync<ArgumentException>(() => almacen.GuardarAsync(nombre, new MemoryStream([1])));

        Assert.Empty(Directory.GetFiles(_raiz));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_raiz)!, "fuera.txt")));
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-05")]
    public async Task RF_DOC_05_RaizConSeparadorFinal_FuncionaIgual()
    {
        // En Docker la ruta suele escribirse con barra final: "/app/data/documentos/".
        var almacen = new AlmacenamientoArchivosLocal(_raiz + Path.DirectorySeparatorChar);

        await almacen.GuardarAsync("abc123", new MemoryStream([7]));
        await using var flujo = await almacen.AbrirLecturaAsync("abc123");

        Assert.Equal(7, flujo.ReadByte());
    }

    [Theory]
    [InlineData("../fuera.txt")]
    [InlineData("..\\fuera.txt")]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_AbrirYDescartar_NombreConRuta_SeRechaza(string nombre)
    {
        var almacen = Crear();

        await Assert.ThrowsAsync<ArgumentException>(() => almacen.AbrirLecturaAsync(nombre));
        await Assert.ThrowsAsync<ArgumentException>(() => almacen.DescartarAsync(nombre));
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-05")]
    public async Task RF_DOC_05_Guardar_CopiaInterrumpida_NoDejaArchivoParcial()
    {
        var almacen = Crear();

        await Assert.ThrowsAsync<IOException>(() => almacen.GuardarAsync("parcial", new FlujoQueFalla()));

        Assert.False(File.Exists(Path.Combine(_raiz, "parcial")));
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-05")]
    public async Task RF_DOC_05_Guardar_SobreArchivoExistente_NoBorraElOriginalAlFallar()
    {
        var almacen = Crear();
        await almacen.GuardarAsync("abc123", new MemoryStream([1]));

        await Assert.ThrowsAsync<IOException>(() => almacen.GuardarAsync("abc123", new MemoryStream([2])));

        Assert.True(File.Exists(Path.Combine(_raiz, "abc123")));
    }

    /// <summary>Entrega unos bytes y después falla, como un disco que se llena a mitad de la copia.</summary>
    private sealed class FlujoQueFalla : Stream
    {
        private bool _entregado;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_entregado)
            {
                throw new IOException("Fallo simulado.");
            }

            _entregado = true;
            buffer[offset] = 1;
            return 1;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    [Trait("Requirement", "RF-DOC-05")]
    public async Task RF_DOC_05_Descartar_BorraElArchivoYNoFallaSiNoExiste()
    {
        var almacen = Crear();
        await almacen.GuardarAsync("abc123", new MemoryStream([1]));

        await almacen.DescartarAsync("abc123");
        await almacen.DescartarAsync("abc123");

        Assert.False(File.Exists(Path.Combine(_raiz, "abc123")));
    }
}
