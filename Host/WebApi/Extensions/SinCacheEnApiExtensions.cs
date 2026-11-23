namespace WebApi.Extensions;

/// <summary>
/// SEG-10: ninguna respuesta de la API (perfil, salud, tokens…) debe quedar guardada en el navegador ni en un proxy. Se pone
/// <c>Cache-Control: no-store</c> a toda respuesta de <c>/api</c> que no haya fijado la suya (el catálogo público sí la fija).
/// </summary>
public static class SinCacheEnApiExtensions
{
    public static IApplicationBuilder UseSinCacheEnApi(this IApplicationBuilder app) =>
        app.Use(async (contexto, siguiente) =>
        {
            contexto.Response.OnStarting(() =>
            {
                AplicarSinCache(contexto);
                return Task.CompletedTask;
            });
            await siguiente();
        });

    public static void AplicarSinCache(HttpContext contexto)
    {
        if (contexto.Request.Path.StartsWithSegments("/api") && !contexto.Response.Headers.ContainsKey("Cache-Control"))
        {
            contexto.Response.Headers.CacheControl = "no-store";
        }
    }
}
