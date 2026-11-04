#!/usr/bin/env bash
# Segunda red además de NuGetAudit: lista los paquetes NuGet vulnerables (directos y transitivos),
# deja la tabla en el resumen del job y falla si alguno tiene severidad Alta o Crítica.
# Requiere: dotnet y jq (jq viene instalado en ubuntu-24.04).
set -euo pipefail

salida="${1:-paquetes-vulnerables.json}"

dotnet list HabitTrackerSystem.slnx package --vulnerable --include-transitive --format json > "$salida"

filas=$(jq -r '
  (.projects // [])[] | .path as $p | (.frameworks // [])[]
  | ((.topLevelPackages // []) + (.transitivePackages // []))[]
  | . as $pkg | (.vulnerabilities // [])[]
  | [($p | split("/") | last), $pkg.id, $pkg.resolvedVersion, .severity, .advisoryurl] | @tsv' "$salida")

{
  echo "### Paquetes NuGet vulnerables"
  if [ -z "$filas" ]; then
    echo "Ninguno."
  else
    echo "| Proyecto | Paquete | Versión | Severidad | Aviso |"
    echo "|---|---|---|---|---|"
    echo "$filas" | awk -F'\t' '{printf "| %s | %s | %s | %s | %s |\n", $1, $2, $3, $4, $5}'
  fi
} >> "${GITHUB_STEP_SUMMARY:-/dev/stdout}"

graves=$(echo "$filas" | awk -F'\t' '$4 == "High" || $4 == "Critical"' | grep -c . || true)
if [ "$graves" -gt 0 ]; then
  echo "::error title=NuGet::$graves paquete(s) con vulnerabilidad alta o crítica"
  exit 1
fi
echo "NuGet: sin vulnerabilidades altas ni críticas."
