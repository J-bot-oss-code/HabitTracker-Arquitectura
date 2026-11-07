#!/usr/bin/env bash
# Pruebas del validador de commits (RD-12): corren sin red ni repositorio, solo con bash.
# Los casos válidos son asuntos reales del historial; los inválidos cubren cada forma de incumplir la regla.
set -uo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
validador="$dir/verificar-commits.sh"
total=0
superadas=0

# esperar <ok|falla> <asunto>
esperar() {
  local esperado="$1" asunto="$2" resultado
  total=$((total + 1))
  if bash "$validador" --mensaje "$asunto" > /dev/null 2>&1; then resultado="ok"; else resultado="falla"; fi
  if [ "$resultado" = "$esperado" ]; then
    superadas=$((superadas + 1))
  else
    echo "FALLA: se esperaba '$esperado' y fue '$resultado' para: $asunto"
  fi
}

# --- Deben ser válidos ---
esperar ok "feat: el correo de activación trae un código de 6 dígitos con límite de intentos [RF-CA-01, RF-CA-15, RF-CA-17, RD-06, RD-07, RD-08, RD-12]"
esperar ok "docs: registra la etiqueta practica-4 en el estado del proyecto [RD-10]"
esperar ok "fix: fija System.Security.Cryptography.Pkcs 9.0.21 en DocumentManager.Infrastructure [RF-DOC-01, RD-10]"
esperar ok "ci: flujo de integración continua para backend y frontend [RD-12]"
esperar ok "test: script de revisión extremo a extremo de la práctica 4 [RF-DOC-01, RF-DOC-02, RF-DOC-03, RD-06, RD-09]"
esperar ok "feat(frontend): landing pública con catálogo de metas [RF-NEG-02, RD-01]"
esperar ok "refactor: separa la validación de transiciones en un único punto [RF-GP-06, RD-04]"
esperar ok "feat: panel de administración con métricas anónimas y auditoría [RF-AUD-01, RF-AUD-06, RF-AUD-07, RF-NEG-08]"
esperar ok "build: auditoría de NuGet y reglas de seguridad del compilador como error [RD-05, RD-07, RD-10]"

# --- Deben ser inválidos ---
esperar falla "feat: implementa el login sin referencia a requisitos"
esperar falla "feat: implementa el login [RF-CA-1]"
esperar falla "feat: implementa el login [rd-10]"
esperar falla "feature: implementa el login [RF-CA-01]"
esperar falla "feat: add login [RF-CA-01]"
esperar falla "feat:implementa el login [RF-CA-01]"
esperar falla "feat: implementa el login [RF-CA-01 RD-10]"
esperar falla "feat: implementa el login []"
esperar falla "implementa el login [RF-CA-01]"
esperar falla "Update README.md"
esperar falla "fix: corrige el cálculo [RF-CA-01] y algo más"

# --- Rango de commits: los ya publicados en main se omiten, los nuevos se validan ---
# Repositorio temporal: main tiene un commit antiguo fuera de formato; develop lo trae con un merge (sincronización).
esperar_rango() {
  local esperado="$1" descripcion="$2" base="$3" cabeza="$4" resultado
  total=$((total + 1))
  if (cd "$repo" && VERIFICAR_COMMITS_RAMA_PUBLICADA=refs/heads/main bash "$validador" "$base" "$cabeza") > /dev/null 2>&1; then
    resultado="ok"
  else
    resultado="falla"
  fi
  if [ "$resultado" = "$esperado" ]; then
    superadas=$((superadas + 1))
  else
    echo "FALLA: se esperaba '$esperado' y fue '$resultado' para el rango: $descripcion"
  fi
}

repo="$(mktemp -d)"
trap 'rm -rf "$repo"' EXIT
(
  cd "$repo" || exit 1
  git init -q -b main
  git config user.email "prueba@ejemplo.com"
  git config user.name "Prueba"
  git commit -q --allow-empty -m "docs: base del repositorio [RD-12]"
  git branch develop
  git commit -q --allow-empty -m "eliminar archivos redundantes"   # antiguo, fuera de formato, ya publicado en main
  git switch -q -c sincronizar develop
  git merge -q --no-ff main -m "Merge main en develop"
  git switch -q -c nueva-mal sincronizar
  git commit -q --allow-empty -m "add login"                        # nuevo y fuera de formato
)

esperar_rango ok    "sincronizar main en develop (solo trae commits ya publicados)" develop sincronizar
esperar_rango falla "un commit nuevo fuera de formato sigue fallando"               develop nueva-mal
total=$((total + 1))
if (cd "$repo" && VERIFICAR_COMMITS_RAMA_PUBLICADA=refs/heads/no-existe bash "$validador" develop sincronizar) > /dev/null 2>&1; then
  echo "FALLA: sin rama publicada, el commit antiguo fuera de formato debe fallar"
else
  superadas=$((superadas + 1))
fi

echo "$superadas/$total pruebas superadas"
[ "$superadas" -eq "$total" ]
