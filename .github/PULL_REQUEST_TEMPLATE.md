## Qué cambia

- Se actualizó el archivo `.gitignore` para hacerlo más robusto, multiplataforma y amigable con múltiples entornos de desarrollo (IDEs). Específicamente, se añadieron exclusiones para:
  - **JetBrains Rider** (carpeta `.idea/` y archivos `*.sln.iml`).
  - **Metadatos de Sistemas Operativos** (`.DS_Store` de macOS y `Thumbs.db` de Windows).
  - **Configuraciones locales de usuario** (`appsettings.local.json`, `appsettings.*.user`, y variables de entorno extendidas como `*.env.local`, `*.env.development.local`, `*.env.*.local`).
- Se creó el archivo de plantilla para Pull Requests `.github/PULL_REQUEST_TEMPLATE.md`.

No se realizaron cambios en `README.md` en este Pull Request. El `README.md` actual es excelente y describe perfectamente la arquitectura (diagrama Mermaid C4), el modelo de dominio y las estrictas reglas de colaboración del monolito modular.

## Por qué

1. **Optimización de `.gitignore`**: Aunque la configuración inicial de `.gitignore` era muy buena para Visual Studio estándar, el desarrollo colaborativo moderno a menudo incluye miembros que utilizan JetBrains Rider o sistemas operativos basados en macOS. Sin estas exclusiones, se corre el riesgo de subir accidentalmente metadatos locales y archivos de configuración que provoquen conflictos entre desarrolladores.
2. **Plantilla de Pull Request**: Para estandarizar las contribuciones en el repositorio y asegurar que se respeten los principios de Clean Architecture y Monolito Modular definidos en el proyecto.

## Cómo probarlo

1. **Verificar `.gitignore`**:
   - Inspeccionar el archivo `.gitignore` para comprobar la correcta inclusión de las secciones de Rider, metadatos de OS y configuraciones locales al final del archivo.
   - Ejecutar `git status` y verificar que ningún archivo de configuración local o metadato aparezca en la lista de cambios no ignorados.
2. **Verificar la Plantilla de PR**:
   - Visualizar este archivo `.github/PULL_REQUEST_TEMPLATE.md` en formato Markdown para corroborar su estructura.

## Qué NO incluye

- No modifica la lógica de negocio ni el core del proyecto.
- No modifica entidades, servicios ni la Web API.
- No modifica el archivo `README.md` directamente (las propuestas de añadir guías de ejecución se mantienen como sugerencias de mejora futura).
