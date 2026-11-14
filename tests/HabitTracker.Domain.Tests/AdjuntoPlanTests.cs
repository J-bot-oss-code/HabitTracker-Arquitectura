using HabitTracker.Domain.Planes;

namespace HabitTracker.Domain.Tests;

/// <summary>Un archivo vinculado a un plan (RF-NEG-10) y la clave del permiso para eliminarlo.</summary>
public class AdjuntoPlanTests
{
    private static readonly DateTime Ahora = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Requirement", "RF-NEG-10")]
    public void RF_NEG_10_Adjunto_GuardaPlanPersonaYDocumento()
    {
        var plan = Guid.NewGuid();
        var persona = Guid.NewGuid();
        var documento = Guid.NewGuid();

        var adjunto = new AdjuntoPlan(plan, persona, documento, Ahora);

        Assert.NotEqual(Guid.Empty, adjunto.Id);
        Assert.Equal((plan, persona, documento, Ahora), (adjunto.PlanUsuarioId, adjunto.UsuarioId, adjunto.DocumentoId, adjunto.AdjuntadoEnUtc));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Adjunto_RechazaIdentificadoresVacios(bool planVacio, bool personaVacia, bool documentoVacio)
    {
        Guid Id(bool vacio) => vacio ? Guid.Empty : Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new AdjuntoPlan(Id(planVacio), Id(personaVacia), Id(documentoVacio), Ahora));
    }

    [Fact]
    [Trait("Requirement", "RD-11")]
    public void RD_11_Adjunto_ExigeFechaUtc()
    {
        Assert.Throws<ArgumentException>(() =>
            new AdjuntoPlan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Local)));
    }

    [Fact]
    [Trait("Requirement", "RF-GP-05")]
    public void RF_GP_05_ClaveDePermiso_EsUnaPorDocumento_ConElPrefijoYSinGuiones()
    {
        var id = Guid.Parse("0a1b2c3d-4e5f-6071-8293-a4b5c6d7e8f9");

        var clave = PermisosDocumentos.EliminarVinculado(id);

        Assert.Equal("Documents.DeleteLinked.0a1b2c3d4e5f60718293a4b5c6d7e8f9", clave);
        Assert.NotEqual(clave, PermisosDocumentos.EliminarVinculado(Guid.NewGuid()));
    }
}
