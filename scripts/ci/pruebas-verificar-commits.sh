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

echo "$superadas/$total pruebas superadas"
[ "$superadas" -eq "$total" ]
