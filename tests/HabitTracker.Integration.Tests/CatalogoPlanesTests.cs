using System.Globalization;
using System.Text;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Planes;
using HabitTracker.Infrastructure.Contenido;
using HabitTracker.Infrastructure.Persistence;
using HabitTracker.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Integration.Tests;

/// <summary>
/// Fase 3 de HabitTracker v2: la prueba de contenido de <c>catalogo-planes.json</c> (un error de contenido rompe el CI) y
/// el sembrador y el repositorio contra EF Core + SQLite (RD-09, RD-12).
/// </summary>
public class CatalogoPlanesTests
{
    private static readonly string[] SlugsDeLanzamiento =
    [
        "perder-grasa-sostenible", "reducir-estres", "dormir-mejor", "primeros-5-km", "ganar-masa-muscular", "fuerza-en-casa",
        "mi-dia-oficina", "mi-dia-turno-nocturno", "mi-dia-trabajo-fisico", "hidratacion-con-calor", "caminar-para-empezar",
        "respiracion-anti-estres", "desconexion-digital-nocturna",
    ];

    // Lo prohibido en motivación (referencias/producto.md). "peso" se permite en los planes por "tu propio peso".
    private static readonly string[] PalabrasProhibidas =
        ["kg", "kilo", "caloria", "dieta", "imc", "gordo", "gorda", "perdiste", "fallaste", "culpa", "malos alimentos", "prohibid"];

    private static IReadOnlyList<PlanPlantilla> Catalogo() => [.. LectorCatalogoPlanes.Leer().Select(c => new PlanPlantilla(c))];

    private static string Normalizar(string texto)
    {
        var descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string([.. descompuesto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);
    }

    private static IEnumerable<string> Textos(PlanPlantilla p) =>
        new[] { p.Nombre, p.Resumen, p.Descripcion, p.AvisoSeguridad }
            .Concat(p.Beneficios)
            .Concat(p.GuiaSemanal.SelectMany(g => new[] { g.Texto, g.TipExtra ?? string.Empty }))
            .Concat(p.Tareas.SelectMany(t => new[] { t.Nombre, t.Instrucciones, t.VersionMinima, t.Unidad }));

    // ---------- Prueba de contenido ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-01")]
    public void RF_NEG_01_Catalogo_TraeLos13PlanesDeLanzamientoConSlugsUnicos()
    {
        var catalogo = Catalogo();

        Assert.Equal(SlugsDeLanzamiento.Order(StringComparer.Ordinal), catalogo.Select(p => p.Slug).Order(StringComparer.Ordinal));
        Assert.Equal(7, catalogo.Count(p => p.Nivel == NivelPlan.Basico));
        Assert.Equal(6, catalogo.Count(p => p.Nivel == NivelPlan.Regular));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Catalogo_CadaPerfilLaboralTieneSuMiDiaBasicoYSinPreguntas()
    {
        var catalogo = Catalogo().ToDictionary(p => p.Slug);

        foreach (var perfil in Enum.GetValues<PerfilLaboral>())
        {
            var plan = catalogo[VariantesMiDia.SlugPara(perfil)];
            Assert.Equal(NivelPlan.Basico, plan.Nivel);
            Assert.False(plan.RequiereCuestionario);
            Assert.Contains(perfil, plan.PerfilesLaborales);
        }
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-01")]
    [Trait("Requirement", "RD-07")]
    public void RF_NEG_01_Catalogo_ExigeElCuestionarioCuandoUsaDatosDeNivel2()
    {
        foreach (var plan in Catalogo())
        {
            var usaNivel2 = plan.Area is AreaPlan.ControlDePeso or AreaPlan.Gimnasio
                || plan.Intensidad != IntensidadPlan.Baja
                || plan.Contraindicaciones.Count > 0
                || plan.Alergenos.Count > 0
                || plan.EdadMinima > PlanPlantilla.EdadMinimaPermitida
                || plan.EdadMaxima < PlanPlantilla.EdadMaximaPermitida
                || plan.Sexo != SexoPlan.Todos;
            if (usaNivel2)
                Assert.True(plan.RequiereCuestionario, plan.Slug);
            if (plan.Nivel == NivelPlan.Basico)
                Assert.False(plan.Area == AreaPlan.ControlDePeso || plan.Intensidad == IntensidadPlan.Alta, plan.Slug);
        }
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Catalogo_LasTareasDeAguaRespetanLosTopesYLlevanAvisoMedico()
    {
        foreach (var plan in Catalogo())
        {
            var vasos = plan.Tareas.Where(t => t.Periodicidad == Periodicidad.Diaria && t.Unidad == TareaPlantilla.UnidadVasos).Sum(t => t.Cantidad);
            var litros = plan.Tareas.Where(t => t.Periodicidad == Periodicidad.Diaria && t.Unidad == TareaPlantilla.UnidadLitros).Sum(t => t.Cantidad);
            Assert.True(vasos <= TareaPlantilla.MaximoVasosAlDia && litros <= TareaPlantilla.MaximoLitrosAlDia, plan.Slug);

            // Una tarea que habla de agua usa una unidad de agua, para que el tope y el aviso se apliquen.
            foreach (var tarea in plan.Tareas.Where(t => Normalizar(t.Nombre).Contains("agua", StringComparison.Ordinal)))
                Assert.True(tarea.EsDeAgua, $"{plan.Slug}: {tarea.Nombre}");

            if (plan.IncluyeAgua)
            {
                var aviso = Normalizar(plan.AvisoSeguridad);
                Assert.Contains("medico", aviso, StringComparison.Ordinal);
                Assert.Contains("rinones", aviso, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Catalogo_CadaTareaTieneVersionMinimaYAnclaDeLaLista()
    {
        foreach (var tarea in Catalogo().SelectMany(p => p.Tareas))
        {
            Assert.False(string.IsNullOrWhiteSpace(tarea.VersionMinima));
            Assert.True(Enum.IsDefined(tarea.AnclaSugerida));
        }
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Catalogo_NingunTextoUsaPalabrasProhibidas()
    {
        foreach (var plan in Catalogo())
            foreach (var texto in Textos(plan).Select(Normalizar))
                foreach (var palabra in PalabrasProhibidas)
                    Assert.False(texto.Contains(palabra, StringComparison.Ordinal), $"{plan.Slug}: «{palabra}» en «{texto}»");
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public void RD_08_Lector_CampoDesconocidoOSlugRepetido_Falla()
    {
        static Stream Json(string texto) => new MemoryStream(Encoding.UTF8.GetBytes(texto));
        const string plan = """
            {"slug":"a","nombre":"A","resumen":"R","descripcion":"D","area":"Estres","nivel":"Basico","intensidad":"Baja",
             "semanas":2,"beneficios":["1","2","3"],"avisoSeguridad":"Aviso","requiereCuestionario":false,
             "tareas":[{"orden":1,"nombre":"T","instrucciones":"I","periodicidad":"Diaria","cantidad":1,"unidad":"veces",
                        "versionMinima":"M","anclaSugerida":"AlDespertar"}]}
            """;

        Assert.Single(LectorCatalogoPlanes.Leer(Json($$"""{"version":1,"planes":[{{plan}}]}""")));
        Assert.ThrowsAny<Exception>(() => LectorCatalogoPlanes.Leer(Json($$"""{"version":1,"planes":[{{plan}},{{plan}}]}""")));
        Assert.ThrowsAny<Exception>(() => LectorCatalogoPlanes.Leer(Json($$"""{"version":1,"planes":[{{plan.Replace("\"area\"", "\"zona\"", StringComparison.Ordinal)}}]}""")));
        Assert.ThrowsAny<Exception>(() => LectorCatalogoPlanes.Leer(Json($$"""{"version":1,"planes":[{{plan.Replace("Estres", "Magia", StringComparison.Ordinal)}}]}""")));
    }

    // ---------- Sembrador y repositorio (SQLite) ----------

    private static async Task<(SqliteConnection Conexion, DbContextOptions<HabitTrackerDbContext> Opciones)> PrepararBaseAsync()
    {
        var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<HabitTrackerDbContext>().UseSqlite(conexion).Options;
        await using var contexto = new HabitTrackerDbContext(opciones);
        await contexto.Database.EnsureCreatedAsync();
        return (conexion, opciones);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-01")]
    [Trait("Requirement", "RD-09")]
    public async Task RF_NEG_01_Sembrador_PrimeraVezYRepetido_DejaLos13PlanesSinDuplicar()
    {
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;

        await using (var contexto = new HabitTrackerDbContext(opciones))
            Assert.Equal(new ResultadoSiembraCatalogo(13, 0, 0), await new SembradorCatalogoPlanes(contexto).SembrarAsync());
        await using (var contexto = new HabitTrackerDbContext(opciones))
            Assert.Equal(new ResultadoSiembraCatalogo(0, 0, 0), await new SembradorCatalogoPlanes(contexto).SembrarAsync());

        await using var lectura = new HabitTrackerDbContext(opciones);
        var planes = await new PlanPlantillaRepository(lectura).ListarActivosAsync();
        Assert.Equal(13, planes.Count);

        // Las listas en JSON y las tareas vuelven completas de la base.
        var gimnasio = planes.Single(p => p.Slug == "ganar-masa-muscular");
        Assert.Equal(4, gimnasio.ZonasCorporales.Count);
        Assert.Equal(12, gimnasio.GuiaSemanal.Count);
        Assert.False(string.IsNullOrEmpty(gimnasio.GuiaSemanal[0].TipExtra));
        Assert.Equal(3, gimnasio.Beneficios.Count);
        Assert.Equal([1, 2, 3], gimnasio.Tareas.Select(t => t.Orden));
        var oficina = planes.Single(p => p.Slug == VariantesMiDia.Oficina);
        Assert.Contains(PerfilLaboral.Remoto, oficina.PerfilesLaborales);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-01")]
    [Trait("Requirement", "RD-09")]
    public async Task RF_NEG_01_Sembrador_VersionNuevaActualizaYElPlanQueSaleSeRetiraSinBorrarse()
    {
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        var catalogo = LectorCatalogoPlanes.Leer();
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await new SembradorCatalogoPlanes(contexto).SembrarAsync(catalogo);

        var oficina = catalogo.Single(c => c.Slug == VariantesMiDia.Oficina);
        var nuevaVersion = oficina with
        {
            Nombre = "Mi día de oficina (v2)",
            VersionContenido = 2,
            Tareas = [oficina.Tareas[0]],
        };
        var sinDormir = catalogo.Where(c => c.Slug != "dormir-mejor" && c.Slug != VariantesMiDia.Oficina).Append(nuevaVersion).ToList();
        await using (var contexto = new HabitTrackerDbContext(opciones))
            Assert.Equal(new ResultadoSiembraCatalogo(0, 1, 1), await new SembradorCatalogoPlanes(contexto).SembrarAsync(sinDormir));

        await using (var lectura = new HabitTrackerDbContext(opciones))
        {
            var repositorio = new PlanPlantillaRepository(lectura);
            var actualizado = await repositorio.ObtenerActivoPorSlugAsync(VariantesMiDia.Oficina);
            Assert.Equal("Mi día de oficina (v2)", actualizado!.Nombre);
            Assert.Single(actualizado.Tareas);
            Assert.Null(await repositorio.ObtenerActivoPorSlugAsync("dormir-mejor"));
            Assert.Equal(12, (await repositorio.ListarActivosAsync()).Count);
            Assert.True(await lectura.PlanesPlantilla.AnyAsync(p => p.Slug == "dormir-mejor" && !p.Activo));
            Assert.Equal(1, await lectura.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM TareasPlantilla WHERE PlanPlantillaId = {0}", actualizado.Id).SingleAsync());
        }

        // Si vuelve al JSON, el plan retirado se reactiva.
        await using (var contexto = new HabitTrackerDbContext(opciones))
            Assert.Equal(new ResultadoSiembraCatalogo(0, 1, 0), await new SembradorCatalogoPlanes(contexto).SembrarAsync([.. sinDormir, catalogo.Single(c => c.Slug == "dormir-mejor")]));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-01")]
    public async Task RF_NEG_01_Sembrador_VersionIgualOMenor_NoPisaLoGuardado()
    {
        var (conexion, opciones) = await PrepararBaseAsync();
        await using var _ = conexion;
        var catalogo = LectorCatalogoPlanes.Leer();
        await using (var contexto = new HabitTrackerDbContext(opciones))
            await new SembradorCatalogoPlanes(contexto).SembrarAsync(catalogo);

        var cambiadoSinVersion = catalogo.Select(c => c.Slug == "reducir-estres" ? c with { Nombre = "Otro nombre" } : c).ToList();
        await using (var contexto = new HabitTrackerDbContext(opciones))
            Assert.Equal(new ResultadoSiembraCatalogo(0, 0, 0), await new SembradorCatalogoPlanes(contexto).SembrarAsync(cambiadoSinVersion));

        await using var lectura = new HabitTrackerDbContext(opciones);
        Assert.Equal("Reducir el estrés", (await new PlanPlantillaRepository(lectura).ObtenerActivoPorSlugAsync("reducir-estres"))!.Nombre);
    }
}
