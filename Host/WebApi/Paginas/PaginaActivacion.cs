namespace WebApi.Paginas;

/// <summary>
/// Página a la que lleva el enlace del correo de activación. NO activa nada por sí sola: un escáner de correos o un
/// navegador que precarga enlaces abre las URL con GET, y si eso activara la cuenta (o gastara intentos del código)
/// lo haría sin que la persona lo pidiera. La activación ocurre al pulsar el botón, que hace un POST.
/// La página es fija: el correo y el código los lee el navegador de la propia URL y el servidor nunca los refleja
/// en el HTML (sin riesgo de inyección). Se permite solo su script, identificado con un nonce distinto en cada respuesta.
/// </summary>
public static class PaginaActivacion
{
    public static string Html(string nonce) => $$"""
        <!DOCTYPE html>
        <html lang="es">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="referrer" content="no-referrer">
        <title>Activar cuenta · HabitTracker Nexus</title>
        <style nonce="{{nonce}}">
        body{font-family:Arial,Helvetica,sans-serif;background:#f4f6f8;color:#1f2937;margin:0;padding:48px 16px;text-align:center}
        main{max-width:440px;margin:0 auto;background:#fff;border-radius:8px;padding:32px}
        h1{font-size:20px;color:#0f766e;margin:0 0 12px}
        button{background:#0f766e;color:#fff;border:0;border-radius:8px;padding:12px 28px;font-size:16px;font-weight:bold;cursor:pointer}
        button:disabled{opacity:.5;cursor:default}
        p{line-height:1.5}
        </style>
        </head>
        <body>
        <main>
        <h1>Activa tu cuenta</h1>
        <p id="mensaje">Pulsa el botón para activar tu cuenta en HabitTracker Nexus.</p>
        <button id="activar" type="button">Activar mi cuenta</button>
        </main>
        <script nonce="{{nonce}}">
        const params = new URLSearchParams(location.search);
        const correo = params.get('correo') || '';
        const codigo = params.get('codigo') || '';
        const mensaje = document.getElementById('mensaje');
        const boton = document.getElementById('activar');
        // El correo y el código no deben quedarse en la barra de direcciones ni en el historial.
        history.replaceState(null, '', location.pathname);
        if (!correo || !/^[0-9]{6}$/.test(codigo)) {
          mensaje.textContent = 'El enlace está incompleto. Pide un código nuevo desde la pantalla de activación.';
          boton.disabled = true;
        }
        boton.addEventListener('click', async () => {
          boton.disabled = true;
          try {
            const respuesta = await fetch('/api/auth/activate', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ correo, codigo })
            });
            if (respuesta.ok) {
              mensaje.textContent = 'Cuenta activada correctamente. Ya puedes iniciar sesión en HabitTracker Nexus.';
              boton.hidden = true;
            } else if (respuesta.status === 429) {
              mensaje.textContent = 'Demasiados intentos. Espera un minuto e inténtalo de nuevo.';
              boton.disabled = false;
            } else {
              mensaje.textContent = 'El enlace no es válido, ya se usó o venció. Pide un código nuevo.';
              boton.hidden = true;
            }
          } catch {
            mensaje.textContent = 'No se pudo conectar. Inténtalo de nuevo.';
            boton.disabled = false;
          }
        });
        </script>
        </body>
        </html>
        """;
}
