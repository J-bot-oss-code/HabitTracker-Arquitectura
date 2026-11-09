using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Excepciones;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Planes;
using HabitTracker.Domain.Salud;

namespace HabitTracker.Application.Tests;

/// <summary>Fase 3 de HabitTracker v2: catálogo, detalle y recomendados con dobles escritos a mano (RD-12).</summary>
public class PlanesServiceTests
{
    private static readonly Guid Usuario = Guid.NewGuid();
    private static readonly DateTime Ahora = new(2026, 11, 9, 15, 0, 0, DateTimeKind.Utc);

    private static PlanPlantilla Plan(string slug, NivelPlan nivel = NivelPlan.Basico, AreaPlan area = AreaPlan.Estres,
        IReadOnlyList<PerfilLaboral>? perfiles = null, IReadOnlyList<Alergia>? alergenos = null, string? nombre = null)
    {
        var semanas = nivel == NivelPlan.Basico ? 2 : 4;
        var requiere = area is AreaPlan.ControlDePeso or AreaPlan.Gimnasio || (alergenos?.Count ?? 0) > 0;
        return new PlanPlantilla(new ContenidoPlan(
            slug, nombre ?? slug, "Resumen.", "Descripción.", area, nivel, IntensidadPlan.Baja, SexoPlan.Todos, 16, 100, semanas,
            perfiles ?? [], null, false, [], [], alergenos ?? [], ["Uno", "Dos", "Tres"], "No sustituye a tu médico.",
            nivel == NivelPlan.Basico ? [] : [.. Enumerable.Range(1, semanas).Select(s => new SemanaGuia(s, $"Semana {s}.", "Tip."))],
            requiere, 1,
            [new TareaPlantilla(1, "Respirar", "Inhala y exhala.", Periodicidad.Diaria, 5, "minutos", "3 respiraciones.", Ancla.AMediaTarde)]));
    }

    private static List<PlanPlantilla> Catalogo() =>
    [
        Plan(VariantesMiDia.Oficina, perfiles: [PerfilLaboral.OficinaDiurna, PerfilLaboral.Remoto]),
        Plan(VariantesMiDia.TurnoNocturno, perfiles: [PerfilLaboral.TurnoNocturno]),
        Plan(VariantesMiDia.TrabajoFisico, perfiles: [PerfilLaboral.TrabajoFisico]),
        Plan("respirar", nombre: "Respirar"),
        Plan("dormir", NivelPlan.Regular, AreaPlan.Descanso, nombre: "Dormir"),
        Plan("con-mani", NivelPlan.Regular, AreaPlan.Nutricion, alergenos: [Alergia.Mani], nombre: "Con maní"),
    ];

    private sealed class Planes(List<PlanPlantilla> planes) : IPlanPlantillaRepository
    {
        public Task<IReadOnlyList<PlanPlantilla>> ListarActivosAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlanPlantilla>>([.. planes.Where(p => p.Activo)]);

        public Task<PlanPlantilla?> ObtenerActivoPorSlugAsync(string slug, CancellationToken cancellationToken = default) =>
            Task.FromResult(planes.FirstOrDefault(p => p.Activo && p.Slug == slug));
    }

    private sealed class Perfiles(PerfilUsuario? perfil) : IPerfilUsuarioRepository
    {
        public Task<PerfilUsuario?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(perfil is not null && perfil.UsuarioId == usuarioId ? perfil : null);
        public Task AgregarAsync(PerfilUsuario perfil, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Cuestionarios(CuestionarioSalud? cuestionario) : ICuestionarioSaludRepository
    {
        public int Lecturas { get; private set; }

        public Task<CuestionarioSalud?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            Lecturas++;
            return Task.FromResult(cuestionario);
        }

        public Task GuardarAsync(CuestionarioSalud cuestionario, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Consentimiento(bool consiente) : IConsentimientoSalud
    {
        public Task<bool> TieneConsentimientoDatosSaludAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(consiente);
    }

    private static PerfilUsuario Perfil(PerfilLaboral laboral = PerfilLaboral.OficinaDiurna) =>
        PerfilUsuario.Crear(Usuario, new DatosPerfil(laboral, "America/Santo_Domingo", true), Ahora);

    private static CuestionarioSalud ConAlergiaAlMani()
    {
        var c = new CuestionarioSalud(Usuario);
        c.GuardarObjetivos(new BloqueObjetivos([Objetivo.DormirMejor], [], Experiencia.Principiante, Lugar.CasaSinEquipo), Ahora);
        c.GuardarSeguridad(new BloqueSeguridad(false, false, false, false, false, false, false), Ahora);
        c.GuardarCondiciones(new BloqueCondiciones([CondicionSalud.Ninguna], [Alergia.Mani]), Ahora);
        return c;
    }

    private static PlanesService Servicio(List<PlanPlantilla>? planes = null, PerfilUsuario? perfil = null,
        Cuestionarios? cuestionarios = null, bool consiente = true) =>
        new(new Planes(planes ?? Catalogo()), new Perfiles(perfil), cuestionarios ?? new Cuestionarios(null), new Consentimiento(consiente));

    // ---------- Catálogo público ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Listar_SinFiltros_DevuelveLosActivosConBasicosPrimero()
    {
        var planes = Catalogo();
        planes.Single(p => p.Slug == "respirar").Retirar();

        var lista = await Servicio(planes).ListarAsync(new FiltrosPlanes(null, null, null));

        Assert.Equal(5, lista.Count);
        Assert.DoesNotContain(lista, p => p.Slug == "respirar");
        Assert.All(lista.TakeWhile(p => p.Nivel == NivelPlan.Basico), p => Assert.Equal(NivelPlan.Basico, p.Nivel));
        Assert.Equal(NivelPlan.Regular, lista[^1].Nivel);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Listar_ConFiltros_AplicaAreaNivelYJornada()
    {
        var servicio = Servicio();

        var descanso = await servicio.ListarAsync(new FiltrosPlanes(AreaPlan.Descanso, null, null));
        var regulares = await servicio.ListarAsync(new FiltrosPlanes(null, NivelPlan.Regular, null));
        var remoto = await servicio.ListarAsync(new FiltrosPlanes(null, null, PerfilLaboral.Remoto));

        Assert.Equal(["dormir"], descanso.Select(p => p.Slug));
        Assert.All(regulares, p => Assert.Equal(NivelPlan.Regular, p.Nivel));
        // Un plan sin jornadas es para todas; uno con jornadas solo aparece en las suyas.
        Assert.Contains(remoto, p => p.Slug == VariantesMiDia.Oficina);
        Assert.Contains(remoto, p => p.Slug == "respirar");
        Assert.DoesNotContain(remoto, p => p.Slug == VariantesMiDia.TurnoNocturno);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Obtener_DetalleConTareasYGuiaSinElTipExtra()
    {
        var detalle = await Servicio().ObtenerAsync("dormir");

        Assert.Equal("dormir", detalle.Slug);
        Assert.Single(detalle.Tareas);
        Assert.Equal("3 respiraciones.", detalle.Tareas[0].VersionMinima);
        Assert.Equal(4, detalle.GuiaSemanal.Count);
        Assert.DoesNotContain("TipExtra", typeof(SemanaGuiaDto).GetProperties().Select(p => p.Name));
    }

    [Theory]
    [InlineData("no-existe")]
    [InlineData("")]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_Obtener_SlugInexistenteORetirado_Lanza404ConCodigo(string slug)
    {
        var planes = Catalogo();
        planes.Single(p => p.Slug == "dormir").Retirar();
        var servicio = Servicio(planes);

        var error = await Assert.ThrowsAsync<RecursoNoEncontradoException>(() => servicio.ObtenerAsync(slug));
        var retirado = await Assert.ThrowsAsync<RecursoNoEncontradoException>(() => servicio.ObtenerAsync("dormir"));

        Assert.Equal(RecursoNoEncontradoException.PlanNoEncontrado, error.Codigo);
        Assert.Equal(RecursoNoEncontradoException.PlanNoEncontrado, retirado.Codigo);
    }

    // ---------- Recomendados ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Recomendar_SinPerfil_Lanza404PerfilNoEncontrado()
    {
        var error = await Assert.ThrowsAsync<RecursoNoEncontradoException>(() => Servicio(perfil: null).RecomendarAsync(Usuario));

        Assert.Equal(RecursoNoEncontradoException.PerfilNoEncontrado, error.Codigo);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-06")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_06_Recomendar_SinIdentidad_SeRechaza()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Servicio(perfil: Perfil()).RecomendarAsync(Guid.Empty));
    }

    [Theory]
    [InlineData(PerfilLaboral.OficinaDiurna, VariantesMiDia.Oficina)]
    [InlineData(PerfilLaboral.TurnoNocturno, VariantesMiDia.TurnoNocturno)]
    [InlineData(PerfilLaboral.TrabajoFisico, VariantesMiDia.TrabajoFisico)]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Recomendar_PerfilNuevo_VeSuMiDiaPrimero(PerfilLaboral laboral, string esperado)
    {
        var resultado = await Servicio(perfil: Perfil(laboral)).RecomendarAsync(Usuario);

        Assert.Equal(esperado, resultado.Recomendados[0].Plan.Slug);
        Assert.False(resultado.PersonalizadoConSalud);
        Assert.NotEmpty(resultado.Recomendados[0].Motivos);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Recomendar_ConConsentimiento_UsaElCuestionarioYMuestraLosNoAptos()
    {
        var resultado = await Servicio(perfil: Perfil(), cuestionarios: new Cuestionarios(ConAlergiaAlMani())).RecomendarAsync(Usuario);

        Assert.True(resultado.PersonalizadoConSalud);
        var noApto = Assert.Single(resultado.NoAptos);
        Assert.Equal("con-mani", noApto.Plan.Slug);
        Assert.Equal(MotivoNoApto.ContieneAlergenos, Assert.Single(noApto.Razones).Motivo);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_02_Recomendar_SinConsentimiento_NiSiquieraLeeElCuestionario()
    {
        var cuestionarios = new Cuestionarios(ConAlergiaAlMani());

        var resultado = await Servicio(perfil: Perfil(), cuestionarios: cuestionarios, consiente: false).RecomendarAsync(Usuario);

        Assert.Equal(0, cuestionarios.Lecturas);
        Assert.False(resultado.PersonalizadoConSalud);
        Assert.Empty(resultado.NoAptos);
        Assert.Contains(resultado.Recomendados, r => r.Plan.Slug == "con-mani"
            && r.Advertencias.Contains(RecomendadorPlanes.AvisoPreguntasDeSalud));
    }
}
