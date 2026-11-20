#!/usr/bin/env bash
# Restaura un respaldo hecho con respaldar.sh: la base, los documentos y las claves de cifrado.
# Ejecútalo en el servidor, como root o con sudo. PISA los datos actuales: detiene la API mientras restaura.
#
#   scripts/despliegue/restaurar.sh <HabitTracker_...bak> <documentos_...tar.gz> <claves_...tar.gz>
set -euo pipefail

if [ "$#" -ne 3 ]; then
    echo "Uso: $0 <archivo.bak> <documentos.tar.gz> <claves.tar.gz>" >&2
    exit 2
fi
bak="$(realpath "$1")"
documentos="$(realpath "$2")"
claves="$(realpath "$3")"
for archivo in "$bak" "$documentos" "$claves"; do
    [ -f "$archivo" ] || { echo "No existe: $archivo" >&2; exit 2; }
done

cd "$(dirname "$0")/../.."
compose=(docker compose -f docker-compose.prod.yml)
temporal="$(mktemp -d)"
trap 'rm -rf "$temporal"' EXIT

echo "Deteniendo la API y el proxy..."
"${compose[@]}" stop web webapi
"${compose[@]}" up -d db

echo "1/3 Base de datos..."
"${compose[@]}" exec -T db mkdir -p /var/opt/mssql/backup
"${compose[@]}" cp "$bak" db:/var/opt/mssql/backup/restaurar.bak
"${compose[@]}" exec -T db bash -c "/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P \"\$MSSQL_SA_PASSWORD\" -b -Q \"RESTORE DATABASE [HabitTracker] FROM DISK = N'/var/opt/mssql/backup/restaurar.bak' WITH REPLACE, RECOVERY\""
"${compose[@]}" exec -T db rm -f /var/opt/mssql/backup/restaurar.bak

echo "2/3 Documentos y 3/3 claves de cifrado..."
tar xzf "$documentos" -C "$temporal"
tar xzf "$claves" -C "$temporal"
"${compose[@]}" cp "$temporal/documentos/." webapi:/app/data/documentos
"${compose[@]}" cp "$temporal/claves/." webapi:/app/data/claves

echo "Arrancando todo..."
"${compose[@]}" up -d
echo "Restaurado. Comprueba que puedes iniciar sesión y abrir un documento."
