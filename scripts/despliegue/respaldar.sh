#!/usr/bin/env bash
# Respaldo de producción: la base de datos, los documentos y las claves de cifrado de los datos de salud.
# Ejecútalo en el servidor, desde cualquier carpeta, como root o con sudo (los archivos del contenedor tienen otro dueño).
#
# Las claves de cifrado van a una carpeta DISTINTA de la base: un respaldo con las dos juntas anula el cifrado, y sin las claves
# los cuestionarios de salud no se pueden leer. Copia las dos carpetas FUERA del servidor, a sitios distintos (ver docs/produccion.md).
#
# Variables opcionales: DESTINO_RESPALDOS (/var/respaldos/habittracker) y DIAS_A_CONSERVAR (14).
set -euo pipefail

cd "$(dirname "$0")/../.."
compose=(docker compose -f docker-compose.prod.yml)
destino="${DESTINO_RESPALDOS:-/var/respaldos/habittracker}"
dias="${DIAS_A_CONSERVAR:-14}"
sello="$(date -u +%Y%m%dT%H%M%SZ)"

mkdir -p "$destino/datos" "$destino/claves"
chmod 700 "$destino" "$destino/datos" "$destino/claves"
temporal="$(mktemp -d)"
trap 'rm -rf "$temporal"' EXIT

echo "1/3 Base de datos..."
# La contraseña la lee el propio contenedor (no pasa por la línea de comandos del servidor).
"${compose[@]}" exec -T db bash -c "mkdir -p /var/opt/mssql/backup && /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P \"\$MSSQL_SA_PASSWORD\" -b -Q \"BACKUP DATABASE [HabitTracker] TO DISK = N'/var/opt/mssql/backup/HabitTracker_${sello}.bak' WITH INIT, COMPRESSION, CHECKSUM\""
"${compose[@]}" cp "db:/var/opt/mssql/backup/HabitTracker_${sello}.bak" "$destino/datos/HabitTracker_${sello}.bak"
"${compose[@]}" exec -T db rm -f "/var/opt/mssql/backup/HabitTracker_${sello}.bak"

echo "2/3 Documentos..."
"${compose[@]}" cp webapi:/app/data/documentos "$temporal/documentos"
tar czf "$destino/datos/documentos_${sello}.tar.gz" -C "$temporal" documentos

echo "3/3 Claves de cifrado (carpeta aparte)..."
"${compose[@]}" cp webapi:/app/data/claves "$temporal/claves"
tar czf "$destino/claves/claves_${sello}.tar.gz" -C "$temporal" claves
chmod 600 "$destino"/datos/*_"${sello}"* "$destino"/claves/*_"${sello}"*

# Rotación: se borran los respaldos de más de N días.
find "$destino" -type f -mtime "+${dias}" -delete

echo "Listo (${sello}):"
echo "  base y documentos: $destino/datos"
echo "  claves de cifrado: $destino/claves   <- cópialas a un sitio distinto del de la base"
