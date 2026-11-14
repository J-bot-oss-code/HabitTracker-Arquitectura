using HabitTracker.Application.Exceptions;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Excepciones;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Planes;

namespace HabitTracker.Application.Tests;

/// <summary>Archivos vinculados a un plan (RF-NEG-10): reglas del servicio con dobles escritos a mano (RD-12).</summary>
public class AdjuntosPlanServiceTests
{
    private static readonly Guid Usuario = Guid.NewGuid();
    private static readonly Guid Otro = Guid.NewGuid();
    private const string Correo = "ana@ejemplo.com";
    private static readonly DateTime Ahora = new(2026, 11, 13, 14, 0, 0, DateTimeKind.Utc);

    private sealed class Planes : IPlanUsuarioRepository
    {
        public List<PlanUsuario> Lista { get; } = [];
        public Task<PlanUsuario?> ObtenerAsync(Guid planId, Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Lista.FirstOrDefault(p => p.Id == planId && p.UsuarioId == usuarioId));
        public Task<IReadOnlyList<PlanUsuario>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlanUsuario>>([.. Lista.Where(p => p.UsuarioId == usuarioId)]);
        public Task AgregarAsync(PlanUsuario plan, CancellationToken cancellationToken = default) { Lista.Add(plan); return Task.CompletedTask; }
        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class Adjuntos(Planes planes) : IAdjuntoPlanRepository
    {
        public List<AdjuntoPlan> Lista { get; } = [];
        public Task<IReadOnlyList<AdjuntoPlan>> ListarDePlanAsync(Guid planId, Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdjuntoPlan>>([.. Lista.Where(a => a.PlanUsuarioId == planId && a.UsuarioId == usuarioId)]);
        public Task<IReadOnlyList<AdjuntoConPlan>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdjuntoConPlan>>([.. Lista.Where(a => a.UsuarioId == usuarioId)
                .Select(a => (a, p: planes.Lista.First(p => p.Id == a.PlanUsuarioId))).Select(x => new AdjuntoConPlan(x.a, x.p.Nombre, x.p.Estado))]);
        public Task<AdjuntoPlan?> ObtenerAsync(Guid planId, Guid documentoId, Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Lista.FirstOrDefault(a => a.PlanUsuarioId == planId && a.DocumentoId == documentoId && a.UsuarioId == usuarioId));
        public Task AgregarAsync(AdjuntoPlan adjunto, CancellationToken cancellationToken = default) { Lista.Add(adjunto); return Task.CompletedTask; }
        public Task QuitarAsync(AdjuntoPlan adjunto, CancellationToken cancellationToken = default) { Lista.Remove(adjunto); return Task.CompletedTask; }
        public Task<bool> DocumentoEnPlanEnCursoAsync(Guid documentoId, Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Lista.Any(a => a.DocumentoId == documentoId && a.UsuarioId == usuarioId
                && planes.Lista.First(p => p.Id == a.PlanUsuarioId).EstaEnCurso));
    }

    private sealed class Documentos : IDocumentosUsuario
    {
        public Dictionary<Guid, (Guid Dueno, bool Eliminado)> Existentes { get; } = [];
        public Guid Nuevo(Guid dueno)
        {
            var id = Guid.NewGuid();
            Existentes[id] = (dueno, false);
            return id;
        }
        public Task<DocumentoResumen?> ObtenerAsync(Guid documentoId, Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Existentes.TryGetValue(documentoId, out var d) && d.Dueno == usuarioId && !d.Eliminado
                ? new DocumentoResumen(documentoId, "informe.pdf", "application/pdf", 1234, Ahora)
                : null);
    }

    private sealed class Permisos : IPermisoService
    {
        public HashSet<string> Aplicados { get; } = [];
        public Task<bool> TienePermisoEfectivoAsync(string solicitante, string permisoSolicitado, CancellationToken cancellationToken = default) =>
            Task.FromResult(solicitante == Correo && Aplicados.Contains(permisoSolicitado));
    }

    private sealed class UnidadDeTrabajo : IUnitOfWork
    {
        public bool Conflicto { get; set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Conflicto ? throw new ConflictoDeUnicidadException() : Task.FromResult(1);
        public void Dispose() { }
    }

    private static PlanPlantilla Plantilla() =>
        new(new ContenidoPlan(
            "plan", "Plan", "Resumen.", "Descripción.", AreaPlan.Estres, NivelPlan.Basico, IntensidadPlan.Baja, SexoPlan.Todos, 16, 100, 3, [], null,
            false, [], [], [], ["Uno", "Dos", "Tres"], "Aviso.", [], false, 1,
            [new TareaPlantilla(1, "Respirar", "Instrucciones.", Periodicidad.Diaria, 1, "veces", "Versión mínima.", Ancla.AMediaTarde)]));

    private sealed class Escenario
    {
        public Planes Planes { get; } = new();
        public Adjuntos Adjuntos { get; }
        public Documentos Documentos { get; } = new();
        public Permisos Permisos { get; } = new();
        public UnidadDeTrabajo Unidad { get; } = new();
        public PlanUsuario Plan { get; }
        public AdjuntosPlanService Servicio { get; }

        public Escenario()
        {
            Adjuntos = new Adjuntos(Planes);
            Plan = PlanUsuario.Iniciar(Usuario, Plantilla(), null, Ahora);
            Planes.Lista.Add(Plan);
            Servicio = new AdjuntosPlanService(Planes, Adjuntos, Documentos, Permisos, Unidad, new RelojFijo(Ahora));
        }
    }

    private sealed class RelojFijo(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }

    private static async Task<string> CodigoAsync(Func<Task> accion)
    {
        var ex = await Assert.ThrowsAnyAsync<HabitTrackerException>(accion);
        return ex.Codigo;
    }

    // ---------- Adjuntar ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-10")]
    public async Task RF_NEG_10_Adjuntar_UnArchivoPropio_AUnPlanEnCurso_QuedaListado()
    {
        var e = new Escenario();
        var doc = e.Documentos.Nuevo(Usuario);

        await e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, doc);

        var lista = await e.Servicio.ListarDePlanAsync(Usuario, e.Plan.Id);
        Assert.Equal(doc, Assert.Single(lista).DocumentoId);
        var vinculo = Assert.Single(await e.Servicio.ListarVinculosAsync(Usuario));
        Assert.True(vinculo.EnCurso);
        Assert.Equal(e.Plan.Nombre, vinculo.PlanNombre);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-10")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_10_Adjuntar_ElArchivoDeOtraPersonaOUnoEliminado_NoSeEncuentra()
    {
        var e = new Escenario();
        var ajeno = e.Documentos.Nuevo(Otro);
        var eliminado = e.Documentos.Nuevo(Usuario);
        e.Documentos.Existentes[eliminado] = (Usuario, true);

        Assert.Equal(RecursoNoEncontradoException.DocumentoNoEncontrado, await CodigoAsync(() => e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, ajeno)));
        Assert.Equal(RecursoNoEncontradoException.DocumentoNoEncontrado, await CodigoAsync(() => e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, eliminado)));
        Assert.Equal(RecursoNoEncontradoException.DocumentoNoEncontrado, await CodigoAsync(() => e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, Guid.NewGuid())));
        Assert.Empty(e.Adjuntos.Lista);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_Adjuntar_AUnPlanAjeno_NoSeEncuentra()
    {
        var e = new Escenario();
        var doc = e.Documentos.Nuevo(Otro);

        Assert.Equal(RecursoNoEncontradoException.PlanUsuarioNoEncontrado, await CodigoAsync(() => e.Servicio.AdjuntarAsync(Otro, e.Plan.Id, doc)));
        Assert.Equal(RecursoNoEncontradoException.PlanUsuarioNoEncontrado, await CodigoAsync(() => e.Servicio.ListarDePlanAsync(Otro, e.Plan.Id)));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-10")]
    public async Task RF_NEG_10_Adjuntar_AUnPlanTerminado_SeRechaza()
    {
        var e = new Escenario();
        var doc = e.Documentos.Nuevo(Usuario);
        e.Plan.Cancelar(Ahora);

        Assert.Equal(ReglaNegocioException.PlanNoEnCurso, await CodigoAsync(() => e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, doc)));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-10")]
    public async Task RF_NEG_10_Adjuntar_ElMismoArchivoDosVeces_EsDuplicado_AunSiLaBaseSeAdelanta()
    {
        var e = new Escenario();
        var doc = e.Documentos.Nuevo(Usuario);
        await e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, doc);

        Assert.Equal(ReglaNegocioException.AdjuntoDuplicado, await CodigoAsync(() => e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, doc)));

        // Carrera: el índice único responde antes que la comprobación.
        var otro = e.Documentos.Nuevo(Usuario);
        e.Unidad.Conflicto = true;
        Assert.Equal(ReglaNegocioException.AdjuntoDuplicado, await CodigoAsync(() => e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, otro)));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-10")]
    public async Task RF_NEG_10_Adjuntar_TopeDeDiez_YLosEliminadosNoOcupanLugar()
    {
        var e = new Escenario();
        var docs = Enumerable.Range(0, AdjuntoPlan.MaximoPorPlan).Select(_ => e.Documentos.Nuevo(Usuario)).ToList();
        foreach (var d in docs)
            await e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, d);
        var extra = e.Documentos.Nuevo(Usuario);

        Assert.Equal(ReglaNegocioException.LimiteAdjuntos, await CodigoAsync(() => e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, extra)));

        // Uno se eliminó desde «Mis archivos»: su vínculo viejo se limpia y deja lugar.
        e.Documentos.Existentes[docs[0]] = (Usuario, true);
        await e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, extra);
        Assert.Equal(AdjuntoPlan.MaximoPorPlan, e.Adjuntos.Lista.Count);
        Assert.DoesNotContain(e.Adjuntos.Lista, a => a.DocumentoId == docs[0]);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-10")]
    public async Task RF_NEG_10_Listar_NoMuestraLosArchivosYaEliminados()
    {
        var e = new Escenario();
        var vivo = e.Documentos.Nuevo(Usuario);
        var muerto = e.Documentos.Nuevo(Usuario);
        await e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, vivo);
        await e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, muerto);
        e.Documentos.Existentes[muerto] = (Usuario, true);

        var lista = await e.Servicio.ListarDePlanAsync(Usuario, e.Plan.Id);

        Assert.Equal(vivo, Assert.Single(lista).DocumentoId);
    }

    // ---------- Quitar (con el permiso por documento) ----------

    [Fact]
    [Trait("Requirement", "RF-GP-05")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_GP_05_Quitar_ConElPlanEnCurso_SinPermiso_PideElPermiso_YNoQuitaNada()
    {
        var e = new Escenario();
        var doc = e.Documentos.Nuevo(Usuario);
        await e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, doc);

        Assert.Equal(PermisoRequeridoException.DocumentoRequierePermiso, await CodigoAsync(() => e.Servicio.QuitarAsync(Usuario, Correo, e.Plan.Id, doc)));
        Assert.Single(e.Adjuntos.Lista);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-05")]
    public async Task RF_GP_05_Quitar_ConElPermisoDeESEdocumento_Quita_PeroNoConElDeOtro()
    {
        var e = new Escenario();
        var doc = e.Documentos.Nuevo(Usuario);
        var otro = e.Documentos.Nuevo(Usuario);
        await e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, doc);
        await e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, otro);
        e.Permisos.Aplicados.Add(PermisosDocumentos.EliminarVinculado(doc));

        await Assert.ThrowsAsync<PermisoRequeridoException>(() => e.Servicio.QuitarAsync(Usuario, Correo, e.Plan.Id, otro));
        await e.Servicio.QuitarAsync(Usuario, Correo, e.Plan.Id, doc);

        Assert.Equal(otro, Assert.Single(e.Adjuntos.Lista).DocumentoId);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-10")]
    public async Task RF_NEG_10_Quitar_ConElPlanTerminado_EsLibre_SinPermiso()
    {
        var e = new Escenario();
        var doc = e.Documentos.Nuevo(Usuario);
        await e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, doc);
        e.Plan.Cancelar(Ahora); // un plan terminado ya no protege a sus archivos

        await e.Servicio.QuitarAsync(Usuario, Correo, e.Plan.Id, doc);

        Assert.Empty(e.Adjuntos.Lista);
        Assert.False(await e.Adjuntos.DocumentoEnPlanEnCursoAsync(doc, Usuario));
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_Quitar_UnVinculoQueNoExiste_NoSeEncuentra()
    {
        var e = new Escenario();

        Assert.Equal(RecursoNoEncontradoException.DocumentoNoEncontrado, await CodigoAsync(() => e.Servicio.QuitarAsync(Usuario, Correo, e.Plan.Id, Guid.NewGuid())));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-10")]
    public async Task RF_NEG_10_EnCurso_SeguirPausadoCuenta_YCompletadoOCanceladoNo()
    {
        var e = new Escenario();
        var doc = e.Documentos.Nuevo(Usuario);
        await e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, doc);

        Assert.True(await e.Adjuntos.DocumentoEnPlanEnCursoAsync(doc, Usuario));
        e.Plan.Pausar(MotivoPausa.Viaje, Ahora);
        Assert.True(await e.Adjuntos.DocumentoEnPlanEnCursoAsync(doc, Usuario));
        Assert.True(Assert.Single(await e.Servicio.ListarVinculosAsync(Usuario)).EnCurso);
        e.Plan.Reanudar(Ahora, TimeZoneInfo.Utc);
        e.Plan.Cancelar(Ahora);
        Assert.False(await e.Adjuntos.DocumentoEnPlanEnCursoAsync(doc, Usuario));
        Assert.False(Assert.Single(await e.Servicio.ListarVinculosAsync(Usuario)).EnCurso);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-10")]
    public async Task RF_NEG_10_Vinculos_UnArchivoEliminado_YaNoApareceNiPidePermiso()
    {
        var e = new Escenario();
        var doc = e.Documentos.Nuevo(Usuario);
        await e.Servicio.AdjuntarAsync(Usuario, e.Plan.Id, doc);

        e.Documentos.Existentes[doc] = (Usuario, true);

        Assert.Empty(await e.Servicio.ListarVinculosAsync(Usuario));
    }
}
