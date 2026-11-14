using HabitTracker.Application.DTOs;
using HabitTracker.Application.Exceptions;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Adherencia;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Excepciones;
using HabitTracker.Domain.Motivacion;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Planes;
using HabitTracker.Domain.Salud;

namespace HabitTracker.Application.Tests;

/// <summary>Fase 4 de HabitTracker v2: inscripción, estados, ajustes, Hoy, resumen y datos (dobles escritos a mano, RD-12).</summary>
public class MisPlanesServiceTests
{
    private static readonly Guid Usuario = Guid.NewGuid();
    private static readonly Guid Otro = Guid.NewGuid();
    // 08:00 local en Santo Domingo (UTC-4), un lunes.
    private static readonly DateTime Lunes = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    // ---------- Dobles ----------

    private sealed class RelojFijo(DateTime utc) : TimeProvider
    {
        public DateTime Ahora { get; set; } = utc;
        public override DateTimeOffset GetUtcNow() => new(Ahora, TimeSpan.Zero);
    }

    private sealed class AdjuntosFalsos : IAdjuntoPlanRepository
    {
        public List<AdjuntoPlan> Lista { get; } = [];
        public Func<Guid, PlanUsuario?> PlanDe { get; set; } = _ => null;
        public Task<IReadOnlyList<AdjuntoPlan>> ListarDePlanAsync(Guid planId, Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdjuntoPlan>>([.. Lista.Where(a => a.PlanUsuarioId == planId && a.UsuarioId == usuarioId)]);
        public Task<IReadOnlyList<AdjuntoConPlan>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdjuntoConPlan>>([.. Lista.Where(a => a.UsuarioId == usuarioId)
                .Select(a => new AdjuntoConPlan(a, PlanDe(a.PlanUsuarioId)!.Nombre, PlanDe(a.PlanUsuarioId)!.Estado))]);
        public Task<AdjuntoPlan?> ObtenerAsync(Guid planId, Guid documentoId, Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Lista.FirstOrDefault(a => a.PlanUsuarioId == planId && a.DocumentoId == documentoId && a.UsuarioId == usuarioId));
        public Task AgregarAsync(AdjuntoPlan adjunto, CancellationToken cancellationToken = default) { Lista.Add(adjunto); return Task.CompletedTask; }
        public Task QuitarAsync(AdjuntoPlan adjunto, CancellationToken cancellationToken = default) { Lista.Remove(adjunto); return Task.CompletedTask; }
        public Task<bool> DocumentoEnPlanEnCursoAsync(Guid documentoId, Guid usuarioId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class Planes : IPlanUsuarioRepository
    {
        public List<PlanUsuario> Lista { get; } = [];
        public int Eliminados { get; private set; }

        public Task<PlanUsuario?> ObtenerAsync(Guid planId, Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Lista.FirstOrDefault(p => p.Id == planId && p.UsuarioId == usuarioId));

        public Task<IReadOnlyList<PlanUsuario>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlanUsuario>>([.. Lista.Where(p => p.UsuarioId == usuarioId).OrderByDescending(p => p.FechaInicio)]);

        public Task AgregarAsync(PlanUsuario plan, CancellationToken cancellationToken = default)
        {
            Lista.Add(plan);
            return Task.CompletedTask;
        }

        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            Eliminados += Lista.RemoveAll(p => p.UsuarioId == usuarioId);
            return Task.FromResult(Eliminados);
        }
    }

    private sealed class Habitos : IHabitoRepository
    {
        public List<Habito> Lista { get; } = [];

        public Task<Habito?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Lista.FirstOrDefault(h => h.Id == id));

        public Task<IReadOnlyList<Habito>> GetByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Habito>>([.. Lista.Where(h => h.UsuarioId == usuarioId)]);

        public Task<IReadOnlyList<Habito>> GetByPlanUsuarioIdAsync(Guid usuarioId, Guid planUsuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Habito>>([.. Lista.Where(h => h.UsuarioId == usuarioId && h.PlanUsuarioId == planUsuarioId)]);

        public Task<IReadOnlyList<Habito>> GetByUsuarioIdSoloLecturaAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            GetByUsuarioIdAsync(usuarioId, cancellationToken);

        public Task AddAsync(Habito habito, CancellationToken cancellationToken = default)
        {
            Lista.Add(habito);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Habito habito, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class Plantillas(List<PlanPlantilla> plantillas) : IPlanPlantillaRepository
    {
        public Task<IReadOnlyList<PlanPlantilla>> ListarActivosAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlanPlantilla>>([.. plantillas.Where(p => p.Activo)]);

        public Task<PlanPlantilla?> ObtenerActivoPorSlugAsync(string slug, CancellationToken cancellationToken = default) =>
            Task.FromResult(plantillas.FirstOrDefault(p => p.Activo && p.Slug == slug));

        public Task<IReadOnlyList<PlanPlantilla>> ObtenerPorSlugsAsync(IReadOnlyCollection<string> slugs, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlanPlantilla>>([.. plantillas.Where(p => slugs.Contains(p.Slug))]);
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
        public Task<CuestionarioSalud?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default) => Task.FromResult(cuestionario);
        public Task GuardarAsync(CuestionarioSalud cuestionario, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Consentimiento(bool consiente) : IConsentimientoSalud
    {
        public Task<bool> TieneConsentimientoDatosSaludAsync(Guid usuarioId, CancellationToken cancellationToken = default) => Task.FromResult(consiente);
    }

    private sealed class UnidadDeTrabajo : IUnitOfWork
    {
        public int Guardados { get; private set; }
        /// <summary>Simula que otra petición se adelantó y la base rechazó un duplicado.</summary>
        public bool ConflictoDeUnicidad { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ConflictoDeUnicidad)
                throw new ConflictoDeUnicidadException();
            Guardados++;
            return Task.FromResult(0);
        }

        public void Dispose() { }
    }

    private sealed class Logros : ILogroUsuarioRepository
    {
        public List<LogroUsuario> Lista { get; } = [];

        public Task<IReadOnlyList<LogroUsuario>> ListarAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LogroUsuario>>([.. Lista.Where(l => l.UsuarioId == usuarioId)]);

        public Task<bool> AgregarSiNoExisteAsync(LogroUsuario logro, CancellationToken cancellationToken = default)
        {
            if (Lista.Any(l => l.UsuarioId == logro.UsuarioId && l.Clave == logro.Clave))
                return Task.FromResult(false);
            Lista.Add(logro);
            return Task.FromResult(true);
        }

        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Lista.RemoveAll(l => l.UsuarioId == usuarioId));
    }

    private sealed class Mensajes : ICatalogoMensajes
    {
        public IReadOnlyList<MensajeMotivacion> Todos { get; } =
            [.. Enum.GetValues<CategoriaMensaje>().SelectMany(c => Enumerable.Range(1, 3).Select(i => new MensajeMotivacion(c, $"{c} {i}")))];
    }

    // ---------- Contenido ----------

    private static TareaPlantilla Tarea(int orden, string nombre, Periodicidad periodicidad = Periodicidad.Diaria, decimal cantidad = 1,
        string unidad = "veces", Ancla ancla = Ancla.AMediaTarde) =>
        new(orden, nombre, "Instrucciones.", periodicidad, cantidad, unidad, "Versión mínima.", ancla);

    private static PlanPlantilla Plantilla(string slug, NivelPlan nivel = NivelPlan.Basico, AreaPlan area = AreaPlan.Estres,
        int semanas = 3, IReadOnlyList<TareaPlantilla>? tareas = null)
    {
        var requiere = area is AreaPlan.ControlDePeso or AreaPlan.Gimnasio;
        return new PlanPlantilla(new ContenidoPlan(
            slug, slug, "Resumen.", "Descripción.", area, nivel, IntensidadPlan.Baja, SexoPlan.Todos, 16, 100, semanas, [], null,
            false, [], [], [], ["Uno", "Dos", "Tres"], "Aviso.",
            nivel == NivelPlan.Basico ? [] : [.. Enumerable.Range(1, semanas).Select(s => new SemanaGuia(s, $"Semana {s}.", $"Tip {s}."))],
            requiere, 1, tareas ?? [Tarea(1, "Respirar"), Tarea(2, "Estirar", ancla: Ancla.AlDespertar)]));
    }

    private static PerfilUsuario Perfil(Guid? usuario = null, int? edad = null) => PerfilUsuario.Crear(
        usuario ?? Usuario,
        new DatosPerfil(PerfilLaboral.OficinaDiurna, "America/Santo_Domingo", true, Edad: edad),
        Lunes);

    private static CuestionarioSalud Cuestionario(params CondicionSalud[] condiciones)
    {
        var c = new CuestionarioSalud(Usuario);
        c.GuardarObjetivos(new BloqueObjetivos([Objetivo.ReducirEstres], [], Experiencia.Principiante, Lugar.CasaSinEquipo), Lunes);
        c.GuardarSeguridad(new BloqueSeguridad(false, false, false, false, false, false, false), Lunes);
        c.GuardarCondiciones(new BloqueCondiciones(condiciones.Length == 0 ? [CondicionSalud.Ninguna] : condiciones, [Alergia.Ninguna]), Lunes);
        return c;
    }

    private sealed class Escenario
    {
        public Planes Planes { get; } = new();
        public Habitos Habitos { get; } = new();
        public Logros Logros { get; } = new();
        public UnidadDeTrabajo Unidad { get; } = new();
        public RelojFijo Reloj { get; } = new(Lunes);
        public List<PlanPlantilla> Catalogo { get; }
        public PlanUsuarioService Servicio { get; }
        public HoyService Hoy { get; }
        public ResumenProgresoService Resumen { get; }
        public DatosPlanesService Datos { get; }
        public AdjuntosFalsos Adjuntos { get; } = new();

        public Escenario(bool consiente = false, PerfilUsuario? perfil = null, CuestionarioSalud? cuestionario = null,
            List<PlanPlantilla>? catalogo = null, bool sinPerfil = false)
        {
            Catalogo = catalogo ?? [Plantilla("basico"), Plantilla("otro-basico"), Plantilla("tercero"), Plantilla("cuarto")];
            var perfiles = new Perfiles(sinPerfil ? null : perfil ?? Perfil());
            var plantillas = new Plantillas(Catalogo);
            Servicio = new PlanUsuarioService(Planes, plantillas, Habitos, perfiles, new Cuestionarios(cuestionario),
                new Consentimiento(consiente), Unidad, Reloj);
            var logrosServicio = new LogrosService(Logros, Reloj);
            Hoy = new HoyService(Planes, plantillas, Habitos, perfiles, logrosServicio, new Mensajes(), Reloj);
            Resumen = new ResumenProgresoService(Planes, Habitos, perfiles, logrosServicio, Logros, new Mensajes(), Unidad, Reloj);
            Datos = new DatosPlanesService(Planes, Logros, Adjuntos);
        }

        public Task<PlanUsuarioDto> Inscribir(string slug = "basico", Ancla? ancla = null, TimeOnly? hora = null, Guid? usuario = null) =>
            Servicio.InscribirseAsync(usuario ?? Usuario, new InscribirseCommand(slug, ancla, hora));

        public Habito Habito(Guid planId, string nombre) =>
            Habitos.Lista.Single(h => h.PlanUsuarioId == planId && h.Nombre == nombre);
    }

    private static async Task<string> CodigoDe(Task tarea) =>
        (await Assert.ThrowsAsync<ReglaNegocioException>(() => tarea)).Codigo;

    // ---------- Inscribirse ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Inscribirse_UnBasicoSinPermisoNiCuestionario_CreaElPlanYSusHabitosActivos()
    {
        var e = new Escenario(consiente: false);

        var dto = await e.Inscribir();

        Assert.Equal(EstadoPlanUsuario.Activo, dto.Estado);
        Assert.Equal(2, dto.Habitos.Count);
        Assert.All(e.Habitos.Lista, h =>
        {
            Assert.Equal(dto.Id, h.PlanUsuarioId);
            Assert.Equal(EstadoHabito.Activo, h.Estado);
        });
        Assert.Equal(1, e.Unidad.Guardados);
        Assert.Equal(new TimeOnly(14, 45), TimeOnly.Parse(dto.HoraRecordatorio!)); // ancla de la primera tarea
        Assert.Equal("14:45", dto.Habitos.Single(h => h.Nombre == "Respirar").HoraSugerida);
        Assert.Equal("06:30", dto.Habitos.Single(h => h.Nombre == "Estirar").HoraSugerida);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Inscribirse_LaHoraDeLaPersonaOSuAnclaMandanSobreLaDeLaPrimeraTarea()
    {
        var conAncla = await new Escenario().Inscribir(ancla: Ancla.AlDespertar);
        var conHora = await new Escenario().Inscribir(ancla: Ancla.AlDespertar, hora: new TimeOnly(5, 15));

        Assert.Equal("06:30", conAncla.HoraRecordatorio);
        Assert.Equal("05:15", conHora.HoraRecordatorio);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-06")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_06_Inscribirse_UnPlanQuePideElCuestionarioSinPermiso_Da409YNoGuardaNada()
    {
        var e = new Escenario(consiente: false, catalogo: [Plantilla("peso", NivelPlan.Regular, AreaPlan.ControlDePeso, 4)]);

        await Assert.ThrowsAsync<ConsentimientoSaludRequeridoException>(() => e.Inscribir("peso"));

        Assert.Empty(e.Planes.Lista);
        Assert.Empty(e.Habitos.Lista);
        Assert.Equal(0, e.Unidad.Guardados);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Inscribirse_ConPermisoPeroSinRespuestas_PideCompletarElCuestionario()
    {
        var e = new Escenario(consiente: true, cuestionario: null, catalogo: [Plantilla("peso", NivelPlan.Regular, AreaPlan.ControlDePeso, 4)]);

        Assert.Equal(ReglaNegocioException.CuestionarioIncompleto, await CodigoDe(e.Inscribir("peso")));
        Assert.Empty(e.Planes.Lista);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Inscribirse_UnPlanNoApto_SeRechazaConPlanNoAptoYSinHabitos()
    {
        var e = new Escenario(consiente: true, cuestionario: Cuestionario(CondicionSalud.Embarazo),
            catalogo: [Plantilla("peso", NivelPlan.Regular, AreaPlan.ControlDePeso, 4)]);

        Assert.Equal(ReglaNegocioException.PlanNoApto, await CodigoDe(e.Inscribir("peso")));

        Assert.Empty(e.Planes.Lista);
        Assert.Empty(e.Habitos.Lista);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Inscribirse_UnPlanAptoQuePideElCuestionario_SeInscribeConLosRequisitosCumplidos()
    {
        var e = new Escenario(consiente: true, cuestionario: Cuestionario(),
            catalogo: [Plantilla("peso", NivelPlan.Regular, AreaPlan.ControlDePeso, 4)]);

        var dto = await e.Inscribir("peso");

        Assert.Equal(4, dto.Semanas);
        Assert.Equal(4, dto.Guia.Count);
        // El tip extra no viaja hasta celebrar la semana.
        Assert.All(dto.Guia, g => Assert.Null(g.TipExtra));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Inscribirse_MaximoTresPlanesEnCursoYSinRepetirPlantilla()
    {
        var e = new Escenario();
        await e.Inscribir("basico");
        await e.Inscribir("otro-basico");
        await e.Inscribir("tercero");

        Assert.Equal(ReglaNegocioException.PlanYaActivo, await CodigoDe(e.Inscribir("basico")));
        Assert.Equal(ReglaNegocioException.LimitePlanes, await CodigoDe(e.Inscribir("cuarto")));
        Assert.Equal(3, e.Planes.Lista.Count);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Inscribirse_UnPlanTerminadoNoCuentaParaElLimiteNiParaRepetir()
    {
        var e = new Escenario();
        var primero = await e.Inscribir("basico");
        await e.Servicio.CambiarEstadoAsync(Usuario, primero.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Cancelado, null, false));

        var otra = await e.Inscribir("basico");

        Assert.Equal(EstadoPlanUsuario.Activo, otra.Estado);
        Assert.Equal(2, e.Planes.Lista.Count);
    }

    [Theory]
    [InlineData("no-existe")]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_Inscribirse_SlugInexistenteOVacio_Es404PlanNoEncontrado(string slug)
    {
        var e = new Escenario();

        var error = await Assert.ThrowsAsync<RecursoNoEncontradoException>(() => e.Inscribir(slug));

        Assert.Equal(RecursoNoEncontradoException.PlanNoEncontrado, error.Codigo);
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_Inscribirse_SinPerfil_Es404PerfilNoEncontrado()
    {
        var error = await Assert.ThrowsAsync<RecursoNoEncontradoException>(() => new Escenario(sinPerfil: true).Inscribir());

        Assert.Equal(RecursoNoEncontradoException.PerfilNoEncontrado, error.Codigo);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-06")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_06_Inscribirse_SinIdentidad_SeRechaza()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new Escenario().Inscribir(usuario: Guid.Empty));
    }

    // ---------- Estados ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Estado_PausarYReanudar_AcompananLosHabitosDelPlan()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();

        var pausado = await e.Servicio.CambiarEstadoAsync(Usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Pausado, MotivoPausa.Viaje, false));

        Assert.Equal(EstadoPlanUsuario.Pausado, pausado.Estado);
        Assert.Equal(MotivoPausa.Viaje, pausado.MotivoPausa);
        Assert.All(e.Habitos.Lista, h => Assert.Equal(EstadoHabito.Pausado, h.Estado));

        var reanudado = await e.Servicio.CambiarEstadoAsync(Usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Activo, null, false));

        Assert.Equal(EstadoPlanUsuario.Activo, reanudado.Estado);
        Assert.All(e.Habitos.Lista, h => Assert.Equal(EstadoHabito.Activo, h.Estado));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_Estado_PausarSinMotivo_SeRechazaYNadaCambia()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();
        var guardados = e.Unidad.Guardados;

        Assert.Equal(ReglaNegocioException.MotivoPausaRequerido, await CodigoDe(
            e.Servicio.CambiarEstadoAsync(Usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Pausado, null, false))));

        Assert.Equal(EstadoPlanUsuario.Activo, e.Planes.Lista[0].Estado);
        Assert.All(e.Habitos.Lista, h => Assert.Equal(EstadoHabito.Activo, h.Estado));
        Assert.Equal(guardados, e.Unidad.Guardados);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-04")]
    public async Task RF_NEG_04_Estado_CompletarAntesDelFin_SeRechazaYLosHabitosNoSeTocan()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();
        var guardados = e.Unidad.Guardados;

        var codigo = await CodigoDe(e.Servicio.CambiarEstadoAsync(Usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Completado, null, true)));

        Assert.Equal(ReglaNegocioException.PlanAunNoTermina, codigo);
        Assert.All(e.Habitos.Lista, h => Assert.Equal(plan.Id, h.PlanUsuarioId));
        Assert.Equal(guardados, e.Unidad.Guardados);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-05")]
    public async Task RF_NEG_05_Estado_PausadoACompletado_EstaProhibido()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();
        await e.Servicio.CambiarEstadoAsync(Usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Pausado, MotivoPausa.Otro, false));
        e.Reloj.Ahora = Lunes.AddDays(40);

        var codigo = await CodigoDe(e.Servicio.CambiarEstadoAsync(Usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Completado, null, false)));

        Assert.Equal(ReglaNegocioException.TransicionPlanInvalida, codigo);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Estado_CompletarAlFinal_ConservarOPausarLosHabitos(bool conservar)
    {
        var e = new Escenario();
        var plan = await e.Inscribir();
        e.Reloj.Ahora = Lunes.AddDays(21);

        var dto = await e.Servicio.CambiarEstadoAsync(Usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Completado, null, conservar));

        Assert.Equal(EstadoPlanUsuario.Completado, dto.Estado);
        // Nunca se desvincula: el historial del plan y su récord se conservan, con o sin hábitos activos.
        Assert.All(e.Habitos.Lista, h => Assert.Equal(plan.Id, h.PlanUsuarioId));
        Assert.All(e.Habitos.Lista, h => Assert.Equal(conservar ? EstadoHabito.Activo : EstadoHabito.Pausado, h.Estado));
        Assert.Equal(2, dto.Habitos.Count);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Estado_CancelarUnPlanEnPausaConservandoLosHabitos_LosDejaActivos()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();
        e.Habito(plan.Id, "Estirar").Pausar(Usuario); // pausado a propósito: no vuelve
        await e.Servicio.CambiarEstadoAsync(Usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Pausado, MotivoPausa.Viaje, false));

        await e.Servicio.CambiarEstadoAsync(Usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Cancelado, null, true));

        Assert.Equal(EstadoHabito.Activo, e.Habito(plan.Id, "Respirar").Estado);
        Assert.Equal(EstadoHabito.Pausado, e.Habito(plan.Id, "Estirar").Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Estado_Reanudar_SoloReactivaLosHabitosQueElPlanPauso()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();
        // La persona deja en pausa a propósito una de las tareas; después pausa y reanuda el plan.
        e.Habito(plan.Id, "Estirar").Pausar(Usuario);
        await e.Servicio.CambiarEstadoAsync(Usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Pausado, MotivoPausa.Viaje, false));

        await e.Servicio.CambiarEstadoAsync(Usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Activo, null, false));

        Assert.Equal(EstadoHabito.Activo, e.Habito(plan.Id, "Respirar").Estado);
        Assert.Equal(EstadoHabito.Pausado, e.Habito(plan.Id, "Estirar").Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Estado_UnPlanYaCompletadoConserva_SusHabitosYElProgresoEnElDetalle()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();
        Avanzar(e, plan.Id, "Respirar", 0);
        Avanzar(e, plan.Id, "Estirar", 0);
        e.Reloj.Ahora = Lunes.AddDays(21);

        var completado = await e.Servicio.CambiarEstadoAsync(Usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Completado, null, true));
        var resumen = await e.Resumen.ObtenerResumenAsync(Usuario);

        Assert.Equal(2, completado.Habitos.Count);
        Assert.True(completado.Progreso.DiasCumplidos >= 1);
        Assert.True(resumen.RachaMasLarga >= 1); // el récord no se pierde al terminar el plan
        Assert.Contains(resumen.Logros, l => l.Clave == ClaveLogro.PlanCompletado);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Inscribirse_DosPeticionesALaVez_LaSegundaSeRechazaComoPlanYaActivo()
    {
        var e = new Escenario();
        e.Unidad.ConflictoDeUnicidad = true; // el índice único de la base de datos dejó pasar solo a la primera

        Assert.Equal(ReglaNegocioException.PlanYaActivo, await CodigoDe(e.Inscribir()));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Estado_Cancelar_NoBorraNadaYElPlanQuedaTerminal()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();

        var dto = await e.Servicio.CambiarEstadoAsync(Usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Cancelado, null, false));

        Assert.Equal(EstadoPlanUsuario.Cancelado, dto.Estado);
        Assert.Equal(2, e.Habitos.Lista.Count);
        Assert.Equal(ReglaNegocioException.TransicionPlanInvalida,
            await CodigoDe(e.Servicio.CambiarEstadoAsync(Usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Activo, null, false))));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-06")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_06_UnPlanAjeno_EsComoUnoQueNoExiste_EnTodasLasOperaciones()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();

        async Task Comprobar(Func<Task> operacion)
        {
            var error = await Assert.ThrowsAsync<RecursoNoEncontradoException>(operacion);
            Assert.Equal(RecursoNoEncontradoException.PlanUsuarioNoEncontrado, error.Codigo);
        }

        await Comprobar(() => e.Servicio.ObtenerAsync(Otro, plan.Id));
        await Comprobar(() => e.Servicio.CambiarEstadoAsync(Otro, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Cancelado, null, false)));
        await Comprobar(() => e.Servicio.AjustarAsync(Otro, plan.Id, new AjustarPlanCommand(TipoAjuste.Extender)));
        await Comprobar(() => e.Servicio.ConfigurarRecordatorioAsync(Otro, plan.Id, new ConfigurarRecordatorioCommand(new TimeOnly(7, 0), true)));
        Assert.Equal(EstadoPlanUsuario.Activo, e.Planes.Lista[0].Estado);
    }

    // ---------- Ajustes y recordatorio ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Ajustar_ModoSuaveYExtender_ConElTopeDeDosExtensiones()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();

        var suave = await e.Servicio.AjustarAsync(Usuario, plan.Id, new AjustarPlanCommand(TipoAjuste.ModoSuave));
        Assert.Equal(Lunes.AddDays(7), suave.ModoSuaveHasta);

        await e.Servicio.AjustarAsync(Usuario, plan.Id, new AjustarPlanCommand(TipoAjuste.Extender));
        var extendido = await e.Servicio.AjustarAsync(Usuario, plan.Id, new AjustarPlanCommand(TipoAjuste.Extender));
        Assert.Equal(5, extendido.SemanasTotales);
        Assert.Equal(ReglaNegocioException.LimiteExtensiones,
            await CodigoDe(e.Servicio.AjustarAsync(Usuario, plan.Id, new AjustarPlanCommand(TipoAjuste.Extender))));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_Ajustar_PausarNoEsUnAjuste_EsUnaTransicion()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();

        Assert.Equal(ReglaNegocioException.AjusteNoDisponible,
            await CodigoDe(e.Servicio.AjustarAsync(Usuario, plan.Id, new AjustarPlanCommand(TipoAjuste.Pausar))));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Recordatorio_ActivarGuardaLaHoraYLaPreferencia()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();

        var dto = await e.Servicio.ConfigurarRecordatorioAsync(Usuario, plan.Id, new ConfigurarRecordatorioCommand(new TimeOnly(7, 5), true));

        Assert.True(dto.RecordatoriosActivos);
        Assert.Equal("07:05", dto.HoraRecordatorio);
    }

    // ---------- Listar ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-06")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_06_Listar_SoloDevuelveLosPlanesDeLaPersona()
    {
        var e = new Escenario();
        await e.Inscribir("basico");
        await e.Inscribir("otro-basico");
        var ajeno = new Escenario();
        e.Planes.Lista.AddRange(ajeno.Planes.Lista);

        Assert.Equal(2, (await e.Servicio.ListarAsync(Usuario)).Count);
        Assert.Empty(await e.Servicio.ListarAsync(Otro));
    }

    // ---------- Hoy ----------

    private static void Avanzar(Escenario e, Guid planId, string nombre, int diasDespuesDelLunes, decimal cantidad = 1, bool minima = false, int horaUtc = 14)
    {
        var fecha = Lunes.Date.AddDays(diasDespuesDelLunes).AddHours(horaUtc);
        e.Habito(planId, nombre).RegistrarAvance(Usuario, cantidad, fecha, minima);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Hoy_ListaLasTareasDeLosPlanesActivosConSuEstado()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();
        Avanzar(e, plan.Id, "Respirar", 0);                                // hecha
        Avanzar(e, plan.Id, "Estirar", 0, minima: true, cantidad: 0.5m);   // solo lo mínimo, sin llegar a la meta
        e.Reloj.Ahora = Lunes.AddHours(8);

        var hoy = await e.Hoy.ObtenerHoyAsync(Usuario);

        Assert.Equal(new DateOnly(2026, 11, 9), hoy.Fecha);
        Assert.Equal(EstadoTareaHoy.Hecho, hoy.Tareas.Single(t => t.Nombre == "Respirar").Estado);
        var estirar = hoy.Tareas.Single(t => t.Nombre == "Estirar");
        Assert.Equal(EstadoTareaHoy.Minimo, estirar.Estado);
        Assert.Equal(0.5m, estirar.Restante);
        Assert.Equal("Versión mínima.", estirar.VersionMinima);
        Assert.Equal(plan.Id, Assert.Single(hoy.Planes).PlanId);
        Assert.False(string.IsNullOrEmpty(hoy.Mensaje));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Hoy_UnPlanEnPausaNoTraeTareasNiMensaje()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();
        await e.Servicio.CambiarEstadoAsync(Usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Pausado, MotivoPausa.Lesion, false));

        var hoy = await e.Hoy.ObtenerHoyAsync(Usuario);

        Assert.Empty(hoy.Tareas);
        Assert.Empty(hoy.Planes);
        Assert.Null(hoy.Mensaje);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Hoy_SinPlanes_EstaVacioYNoFalla()
    {
        var hoy = await new Escenario().Hoy.ObtenerHoyAsync(Usuario);

        Assert.Empty(hoy.Tareas);
        Assert.Null(hoy.Mensaje);
        Assert.Empty(hoy.LogrosNuevos);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Hoy_UnaTareaSemanalSeMideEnLaSemanaLocal()
    {
        var semanal = Plantilla("correr", NivelPlan.Regular, AreaPlan.Energia, 4,
            [Tarea(1, "Salida", Periodicidad.Semanal, 3, "salidas")]);
        var e = new Escenario(catalogo: [semanal]);
        var plan = await e.Inscribir("correr");
        Avanzar(e, plan.Id, "Salida", 0, 1);
        Avanzar(e, plan.Id, "Salida", 2, 1);
        e.Reloj.Ahora = Lunes.AddDays(3).AddHours(8);

        var tarea = Assert.Single((await e.Hoy.ObtenerHoyAsync(Usuario)).Tareas);

        Assert.Equal(2, tarea.CantidadPeriodo);
        Assert.Equal(1, tarea.Restante);
        Assert.Equal(EstadoTareaHoy.Pendiente, tarea.Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-11")]
    public async Task RD_11_Hoy_UnAvanceDeLaNocheDelDomingoNoCuentaParaElLunesLocal_NiParaLaSemanaNueva()
    {
        // 02:00 UTC del lunes 16 son las 22:00 del domingo 15 en Santo Domingo: todavía es la semana (y el día) anterior.
        var domingoPorLaNoche = new DateTime(2026, 11, 16, 2, 0, 0, DateTimeKind.Utc);
        var lunesLocal = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

        var diario = new Escenario();
        var planDiario = await diario.Inscribir();
        diario.Habito(planDiario.Id, "Respirar").RegistrarAvance(Usuario, 1, domingoPorLaNoche);
        diario.Reloj.Ahora = lunesLocal;

        var semanal = new Escenario(catalogo: [Plantilla("correr", NivelPlan.Regular, AreaPlan.Energia, 4, [Tarea(1, "Salida", Periodicidad.Semanal, 3, "salidas")])]);
        var planSemanal = await semanal.Inscribir("correr");
        semanal.Habito(planSemanal.Id, "Salida").RegistrarAvance(Usuario, 1, domingoPorLaNoche);
        semanal.Reloj.Ahora = lunesLocal;

        var hoyDiario = await diario.Hoy.ObtenerHoyAsync(Usuario);
        var hoySemanal = await semanal.Hoy.ObtenerHoyAsync(Usuario);

        Assert.Equal(new DateOnly(2026, 11, 16), hoyDiario.Fecha);
        Assert.Equal(EstadoTareaHoy.Pendiente, hoyDiario.Tareas.Single(t => t.Nombre == "Respirar").Estado);
        Assert.Equal(0m, hoyDiario.Tareas.Single(t => t.Nombre == "Respirar").CantidadPeriodo);
        Assert.Equal(0m, hoySemanal.Tareas.Single().CantidadPeriodo);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Hoy_LaSemanaCelebradaDesbloqueaElTipExtraSoloEnLaSemanaSiguiente()
    {
        var regular = Plantilla("regular", NivelPlan.Regular, AreaPlan.Energia, 4, [Tarea(1, "Caminar")]);
        var e = new Escenario(catalogo: [regular]);
        var plan = await e.Inscribir("regular");
        for (var d = 0; d < 5; d++)
            Avanzar(e, plan.Id, "Caminar", d);
        e.Reloj.Ahora = Lunes.AddDays(8).AddHours(8); // martes de la segunda semana

        var hoy = await e.Hoy.ObtenerHoyAsync(Usuario);
        var enElDetalle = await e.Servicio.ObtenerAsync(Usuario, plan.Id);

        Assert.Equal(1, hoy.CelebracionSemana);
        Assert.Equal("Tip 1.", hoy.TipExtra);
        Assert.Equal("Tip 1.", enElDetalle.Guia.Single(g => g.Semana == 1).TipExtra);
        Assert.Null(enElDetalle.Guia.Single(g => g.Semana == 2).TipExtra);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Hoy_UnPlanSoloSemanal_NoSugiereAjustesNiHablaDeRegresoCuandoVaAlRitmoEsperado()
    {
        var semanal = Plantilla("correr", NivelPlan.Regular, AreaPlan.Energia, 4, [Tarea(1, "Salida", Periodicidad.Semanal, 3, "salidas")]);
        var e = new Escenario(catalogo: [semanal]);
        var plan = await e.Inscribir("correr");
        // Tres salidas por semana (lunes, miércoles y viernes): entre una y otra hay días vacíos, y eso es lo esperado.
        foreach (var dia in new[] { 0, 2, 4, 7, 9 })
            Avanzar(e, plan.Id, "Salida", dia);
        e.Reloj.Ahora = Lunes.AddDays(10).AddHours(8); // jueves de la segunda semana

        var hoy = await e.Hoy.ObtenerHoyAsync(Usuario);

        var plan0 = Assert.Single(hoy.Planes);
        Assert.Null(plan0.Sugerencia);
        Assert.Equal(2, plan0.Racha); // semanas seguidas, no días
        Assert.NotEqual(CategoriaMensaje.Regreso.ToString(), hoy.Mensaje!.Split(' ')[0]);
        Assert.Equal(UnidadSeguimiento.Semanas, (await e.Servicio.ObtenerAsync(Usuario, plan.Id)).Progreso.Unidad);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Hoy_ElMensajeDeRegresoApareceConDosDiasSinRegistro_YLaSugerenciaConCuatro()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();
        Avanzar(e, plan.Id, "Respirar", 0);
        Avanzar(e, plan.Id, "Estirar", 0);
        e.Reloj.Ahora = Lunes.AddDays(5).AddHours(8); // sábado: 4 días sin registro (martes a viernes)

        var hoy = await e.Hoy.ObtenerHoyAsync(Usuario);

        Assert.StartsWith(nameof(CategoriaMensaje.Regreso), hoy.Mensaje);
        var sugerencia = Assert.Single(hoy.Planes).Sugerencia;
        Assert.NotNull(sugerencia);
        Assert.Contains(TipoAjuste.ModoSuave, sugerencia.Opciones);
        Assert.Contains(TipoAjuste.Pausar, sugerencia.Opciones);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Hoy_ElPrimerAvanceGanaSuLogroUnaSolaVezYAlVerloYaNoSale()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();
        Avanzar(e, plan.Id, "Respirar", 0);
        e.Reloj.Ahora = Lunes.AddHours(8);

        var primera = await e.Hoy.ObtenerHoyAsync(Usuario);
        var segunda = await e.Hoy.ObtenerHoyAsync(Usuario);

        Assert.Equal([ClaveLogro.PrimerAvance], primera.LogrosNuevos);
        Assert.Equal([ClaveLogro.PrimerAvance], segunda.LogrosNuevos);
        Assert.Single(e.Logros.Lista);

        await e.Resumen.MarcarLogrosVistosAsync(Usuario);

        Assert.Empty((await e.Hoy.ObtenerHoyAsync(Usuario)).LogrosNuevos);
    }

    // ---------- Resumen ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Resumen_RachaLogrosYTuMesEnNumeros()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();
        foreach (var dia in new[] { 0, 1, 2, 4 })
        {
            Avanzar(e, plan.Id, "Respirar", dia);
            Avanzar(e, plan.Id, "Estirar", dia);
        }

        e.Reloj.Ahora = Lunes.AddDays(4).AddHours(8);

        var resumen = await e.Resumen.ObtenerResumenAsync(Usuario);

        Assert.Equal(4, resumen.Racha); // un día sin registro no corta
        Assert.Equal(4, resumen.RachaMasLarga);
        Assert.Contains(resumen.Logros, l => l.Clave == ClaveLogro.PrimerAvance && !l.Visto);
        Assert.Equal(4, resumen.Mes.DiasCumplidos);
        Assert.Equal(2, resumen.Mes.HabitosMantenidos);
        Assert.Equal(0, resumen.Mes.Regresos);
        Assert.Equal(1, resumen.PlanesEnCurso);
        Assert.False(string.IsNullOrEmpty(resumen.Mensaje));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Resumen_SinPlanes_TodoEnCeroYSinMensaje()
    {
        var resumen = await new Escenario().Resumen.ObtenerResumenAsync(Usuario);

        Assert.Equal(0, resumen.Racha);
        Assert.Empty(resumen.Logros);
        Assert.Null(resumen.Mensaje);
        Assert.Equal(0, resumen.PlanesEnCurso);
    }

    // ---------- Descarga y baja de cuenta ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-06")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_06_Datos_ExportarYEliminar_SoloTocanALaPersona()
    {
        var e = new Escenario();
        var plan = await e.Inscribir();
        Avanzar(e, plan.Id, "Respirar", 0);
        await e.Hoy.ObtenerHoyAsync(Usuario);
        await e.Servicio.CambiarEstadoAsync(Usuario, plan.Id, new CambiarEstadoPlanCommand(EstadoPlanUsuario.Pausado, MotivoPausa.Trabajo, false));
        var documento = Guid.NewGuid();
        e.Adjuntos.PlanDe = _ => e.Planes.Lista[0];
        e.Adjuntos.Lista.Add(new AdjuntoPlan(plan.Id, Usuario, documento, Lunes));

        var exportado = await e.Datos.ExportarAsync(Usuario);

        var exportadoPlan = Assert.Single(exportado.Planes);
        Assert.Equal(MotivoPausa.Trabajo, Assert.Single(exportadoPlan.Pausas).Motivo);
        Assert.Equal(documento, Assert.Single(exportadoPlan.ArchivosVinculados));
        Assert.NotEmpty(exportado.Logros);
        Assert.Empty((await e.Datos.ExportarAsync(Otro)).Planes);

        await e.Datos.EliminarAsync(Otro);
        Assert.Single(e.Planes.Lista);
        await e.Datos.EliminarAsync(Usuario);
        Assert.Empty(e.Planes.Lista);
        Assert.Empty(e.Logros.Lista);
    }
}
