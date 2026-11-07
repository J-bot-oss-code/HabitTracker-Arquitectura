#!/usr/bin/env bash
# Comprueba que la skill .claude/skills/contexto-habittracker sigue siendo cierta:
#   1. Toda ruta del repositorio citada entre comillas invertidas existe.
#   2. referencias/inventario.generado.md coincide con lo que genera scripts/contexto/generar-inventario.sh.
#   3. Los documentos de entrada no crecen sin control (SKILL.md, producto.md y el ESTADO).
#
# Uso: bash scripts/ci/verificar-contexto.sh
set -euo pipefail
export LC_ALL=C

RAIZ="$(git rev-parse --show-toplevel)"
cd "$RAIZ"

SKILL=".claude/skills/contexto-habittracker"
INVENTARIO="$SKILL/referencias/inventario.generado.md"
fallos=0

fallar() {
  echo "FALLA: $*"
  fallos=$((fallos + 1))
}

# --- 1. Rutas citadas -----------------------------------------------------------------------------------------------
# Solo rutas que empiezan por una carpeta del repo. Se ignoran las que llevan comodines o marcadores (<M>, *, {a,b}).
while IFS= read -r ruta; do
  [ -e "$ruta" ] || fallar "la skill cita '$ruta', que no existe"
done < <(
  # shellcheck disable=SC2016 # la comilla invertida es literal: busca `ruta` en el Markdown, no es una sustitución.
  grep -h -o '`[^` ]*`' "$SKILL"/SKILL.md "$SKILL"/referencias/*.md \
    | tr -d '`' \
    | grep -E '^(Host|Modules|tests|frontend|docs|scripts|\.github|\.claude)/' \
    | grep -v -E '[<>*{}]' \
    | sed -E 's/[.,;:)]+$//' \
    | sort -u
)

# --- 2. Inventario al día -------------------------------------------------------------------------------------------
if [ ! -f "$INVENTARIO" ]; then
  fallar "falta $INVENTARIO (ejecuta: bash scripts/contexto/generar-inventario.sh)"
else
  esperado="$(mktemp)"
  trap 'rm -f "$esperado"' EXIT
  bash scripts/contexto/generar-inventario.sh --stdout > "$esperado"
  if ! diff --strip-trailing-cr -u "$INVENTARIO" "$esperado" > /dev/null; then
    diff --strip-trailing-cr -u "$INVENTARIO" "$esperado" | head -40 || true
    fallar "el inventario está viejo (ejecuta: bash scripts/contexto/generar-inventario.sh y versiona el resultado)"
  fi
fi

# --- 3. Topes de tamaño ---------------------------------------------------------------------------------------------
comprobar_tope() {
  local archivo="$1" tope="$2" lineas
  [ -f "$archivo" ] || { fallar "falta $archivo"; return; }
  lineas=$(wc -l < "$archivo" | tr -d ' ')
  [ "$lineas" -le "$tope" ] || fallar "$archivo tiene $lineas líneas (tope $tope): mueve la historia a docs/historial/"
}
comprobar_tope "$SKILL/SKILL.md" 150
comprobar_tope "$SKILL/referencias/producto.md" 80
comprobar_tope "docs/ESTADO-DEL-PROYECTO.md" 120

if [ "$fallos" -gt 0 ]; then
  echo "verificar-contexto: $fallos problema(s). La skill de contexto debe describir el repositorio tal como es."
  exit 1
fi
echo "verificar-contexto: la skill, el inventario y el ESTADO están al día."
