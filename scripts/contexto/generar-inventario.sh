#!/usr/bin/env bash
# Genera el inventario del repositorio para la skill contexto-habittracker.
#
# Uso:  bash scripts/contexto/generar-inventario.sh            -> escribe el archivo
#       bash scripts/contexto/generar-inventario.sh --stdout   -> lo imprime (lo usa verificar-contexto.sh)
#
# Solo mira archivos versionados (git ls-files) y ordena todo, para que la salida sea la misma en Windows y en el CI.
# No incluye fechas ni rutas absolutas: si el código no cambia, el inventario tampoco.
set -euo pipefail
export LC_ALL=C

RAIZ="$(git rev-parse --show-toplevel)"
cd "$RAIZ"
DESTINO=".claude/skills/contexto-habittracker/referencias/inventario.generado.md"

generar() {
  echo "# Inventario generado"
  echo
  echo "> Generado por \`scripts/contexto/generar-inventario.sh\`. No editar a mano: regenerar tras cambiar"
  echo "> controllers, migraciones, pruebas, features o contenido. El CI (\`verificar-contexto.sh\`) falla si quedó viejo."
  echo

  echo "## Endpoints (Host/WebApi/Controllers)"
  echo
  echo "| Controller | Verbo | Ruta | Acceso |"
  echo "|---|---|---|---|"
  for archivo in $(git ls-files 'Host/WebApi/Controllers/*.cs' | sort); do
    awk -v nombre="$(basename "$archivo" .cs)" '
      function limpiar(s) { gsub(/^[ \t]+|[ \t\r]+$/, "", s); return s }
      function acceso_de(linea,   a) {
        if (linea ~ /AllowAnonymous/) return "anónimo"
        if (match(linea, /Roles *= *[^)\]]+/)) {
          a = substr(linea, RSTART, RLENGTH); sub(/Roles *= */, "", a); gsub(/"/, "", a)
          # Constantes de los controllers (RolEstandar, RolAdministrador) -> nombre del rol.
          sub(/^Rol/, "", a)
          return "rol " a
        }
        return "autenticado"
      }
      BEGIN { base = ""; acceso_clase = "—"; dentro = 0; acceso_metodo = ""; n = 0 }
      /^[ \t]*\[Route\(/ && !dentro { r = $0; sub(/.*\[Route\("/, "", r); sub(/"\).*/, "", r); base = r }
      /^[ \t]*\[(Authorize|AllowAnonymous)/ { if (!dentro) acceso_clase = acceso_de($0); else acceso_metodo = acceso_de($0) }
      /class [A-Za-z]+Controller/ { dentro = 1 }
      dentro && /^[ \t]*\[Http(Get|Post|Put|Patch|Delete)/ {
        v = $0; sub(/.*\[Http/, "", v); verbo = toupper(substr(v, 1, match(v, /[\(\]]/) - 1))
        plantilla = ""
        if (v ~ /\("/) { plantilla = v; sub(/^[^"]*"/, "", plantilla); sub(/".*/, "", plantilla) }
        n++; verbos[n] = verbo; plantillas[n] = plantilla
      }
      dentro && /^[ \t]*public .*\(/ && n > 0 {
        acc = (acceso_metodo != "") ? acceso_metodo : acceso_clase
        for (i = 1; i <= n; i++) {
          ruta = "/" base; if (plantillas[i] != "") ruta = ruta "/" plantillas[i]
          printf "| %s | %s | `%s` | %s |\n", nombre, verbos[i], ruta, acc
        }
        n = 0; acceso_metodo = ""
      }
      dentro && /^[ \t]*public .*\(/ { acceso_metodo = "" }
    ' "$archivo"
  done
  echo

  echo "## Migraciones por contexto"
  echo
  for modulo in $(git ls-files 'Modules/*Migrations/*.cs' | grep -v -e Designer -e Snapshot | cut -d/ -f2 | sort -u); do
    echo "- **$modulo:** $(git ls-files "Modules/$modulo/*Migrations/*.cs" | grep -v -e Designer -e Snapshot | xargs -n1 basename | sed 's/^[0-9]*_//; s/\.cs$//' | paste -sd, - | sed 's/,/, /g')"
  done
  echo

  echo "## Proyectos de prueba (backend)"
  echo
  echo "| Proyecto | Archivos | [Fact] | [Theory] |"
  echo "|---|---|---|---|"
  total_f=0; total_t=0
  for proyecto in $(git ls-files 'tests/*.csproj' | xargs -n1 dirname | sort -u); do
    archivos=$(git ls-files "$proyecto/*.cs" | wc -l | tr -d ' ')
    f=$( { git ls-files -z "$proyecto/*.cs" | xargs -0 grep -h '\[Fact' 2>/dev/null || true; } | wc -l | tr -d ' ')
    t=$( { git ls-files -z "$proyecto/*.cs" | xargs -0 grep -h '\[Theory' 2>/dev/null || true; } | wc -l | tr -d ' ')
    total_f=$((total_f + f)); total_t=$((total_t + t))
    echo "| $(basename "$proyecto") | $archivos | $f | $t |"
  done
  echo "| **Total** | | **$total_f** | **$total_t** |"
  echo
  echo "Los \`[Theory]\` cuentan varios casos cada uno: el total real lo da \`dotnet test\`."
  echo

  echo "## Adaptadores del Host (puertos entre módulos)"
  echo
  git ls-files 'Host/WebApi/Adapters/*.cs' | sort | while read -r a; do
    impl=$(grep -h -o 'class [A-Za-z]* *[:(][^{]*' "$a" | head -1 | sed 's/[[:space:]]*$//')
    echo "- \`$(basename "$a")\`: \`$impl\`"
  done
  echo

  echo "## Workers (BackgroundService)"
  echo
  git ls-files 'Modules/*.cs' 'Host/*.cs' | sort | xargs grep -l ': BackgroundService' 2>/dev/null | while read -r w; do echo "- \`$w\`"; done
  echo

  echo "## Frontend: features"
  echo
  for feature in $(git ls-files 'frontend/src/features/*' | cut -d/ -f4 | sort -u); do
    echo "- \`$feature\`: $(git ls-files "frontend/src/features/$feature/*" | grep -c -E '\.(ts|tsx)$') archivos"
  done
  echo

  echo "## Frontend: contratos propuestos (sin backend)"
  echo
  # Cada contrato declara `metodo: 'GET',` y en la línea siguiente `ruta: '/api/...'` (ver propuestas/*.ts).
  local contratos
  contratos=$(git ls-files 'frontend/src/mocks/handlers/propuestas/*.ts' | grep -v -e 'index\.ts$' -e '\.test\.ts$' | sort | while read -r f; do
    awk -v archivo="$(basename "$f")" "
      /metodo: *'[A-Z]+'/ { m = \$0; sub(/.*metodo: *'/, \"\", m); sub(/'.*/, \"\", m) }
      /ruta: *'/ && m != \"\" { r = \$0; sub(/.*ruta: *'/, \"\", r); sub(/'.*/, \"\", r); printf \"- \`%s %s\` (%s)\n\", m, r, archivo; m = \"\" }
    " "$f"
  done)
  if [ -n "$contratos" ]; then echo "$contratos"; else echo "- (ninguno: todo está conectado al backend)"; fi
  echo

  echo "## Contenido del módulo de negocio"
  echo
  local planes mensajes
  planes=$(git ls-files '*catalogo-planes.json' | head -1)
  mensajes=$(git ls-files '*mensajes-motivacion.json' | head -1)
  if [ -n "$planes" ]; then echo "- \`$planes\`: $(grep -c '"slug"' "$planes") planes"; else echo "- \`catalogo-planes.json\`: aún no existe (Fase 3)"; fi
  if [ -n "$mensajes" ]; then echo "- \`$mensajes\`: $(grep -c '"texto"' "$mensajes") mensajes"; else echo "- \`mensajes-motivacion.json\`: aún no existe (Fase 4)"; fi
}

if [ "${1:-}" = "--stdout" ]; then
  generar
else
  generar > "$DESTINO"
  echo "Inventario escrito en $DESTINO"
fi
