#!/usr/bin/env bash
# Descarga un .tar.gz oficial, verifica su SHA-256 fijado y deja el binario en $RUNNER_TEMP/herramientas
# (que se añade al PATH de los pasos siguientes). Si el hash no coincide, el paso falla y no se ejecuta nada.
# Uso: instalar-herramienta.sh <url> <sha256> <nombre-binario>
set -euo pipefail

if [ "$#" -ne 3 ]; then
  echo "Uso: $0 <url> <sha256> <nombre-binario>" >&2
  exit 2
fi

url="$1"
sha="$2"
binario="$3"
destino="${RUNNER_TEMP:-/tmp}/herramientas"

mkdir -p "$destino"
curl -sSfL --retry 3 -o "$destino/paquete.tar.gz" "$url"
echo "$sha  $destino/paquete.tar.gz" | sha256sum -c -
tar -xzf "$destino/paquete.tar.gz" -C "$destino" "$binario"
rm "$destino/paquete.tar.gz"
chmod +x "$destino/$binario"

if [ -n "${GITHUB_PATH:-}" ]; then
  echo "$destino" >> "$GITHUB_PATH"
fi
echo "Instalado: $destino/$binario"
