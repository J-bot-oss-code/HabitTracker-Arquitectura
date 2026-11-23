#!/usr/bin/env bash
# Restaura un respaldo hecho con respaldar.sh: la base, los documentos y las claves de cifrado.
# Ejecútalo en el servidor, como root o con sudo. PISA los datos actuales: detiene la API mientras restaura.
#
#   scripts/despliegue/restaurar.sh <HabitTracker_...bak> <documentos_...tar.gz> <claves_...tar.gz>
#
# Los archivos cifrados por respaldar.sh (terminan en .age) se descifran con RESPALDO_IDENTIDAD: la ruta del archivo con la
# clave privada de age.
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
temporal="$(mktemp -d)"
trap 'rm -rf "$temporal"' EXIT

# Devuelve una ruta legible: la misma si no está cifrado; si termina en .age, la copia descifrada en la carpeta temporal.
descifrar() {
    case "$1" in
        *.age)
            [ -f "${RESPALDO_IDENTIDAD:-}" ] || { echo "Define RESPALDO_IDENTIDAD (clave privada de age) para $1" >&2; exit 2; }
            local salida
            salida="$temporal/$(basename "${1%.age}")"
            age -d -i "$RESPALDO_IDENTIDAD" -o "$salida" "$1"
            echo "$salida"
            ;;
        *) echo "$1" ;;
    esac
}
bak="$(descifrar "$bak")"
documentos="$(descifrar "$documentos")"
claves="$(descifrar "$claves")"

cd "$(dirname "$0")/../.."
compose=(docker compose -f docker-compose.prod.yml)

echo "Deteniendo la API y el proxy..."
"${compose[@]}" stop web webapi
"${compose[@]}" up -d db

echo "1/3 Base de datos..."
"${compose[@]}" exec -T db bash -c "mkdir -p /var/opt/mssql/backup"
"${compose[@]}" cp "$bak" db:/var/opt/mssql/backup/restaurar.bak
"${compose[@]}" exec -T db bash -c "SQLCMDPASSWORD=\"\$MSSQL_SA_PASSWORD\" /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b -Q \"RESTORE DATABASE [HabitTracker] FROM DISK = N'/var/opt/mssql/backup/restaurar.bak' WITH REPLACE, RECOVERY\""
"${compose[@]}" exec -T db bash -c "rm -f /var/opt/mssql/backup/restaurar.bak"

echo "2/3 Documentos y 3/3 claves de cifrado..."
tar xzf "$documentos" -C "$temporal"
tar xzf "$claves" -C "$temporal"
# "docker cp" sobre un contenedor DETENIDO escribe en su capa y no en el volumen (quedaría tapado al arrancar): se copia con un
# contenedor auxiliar de la misma imagen que monta los volúmenes de la API.
webapi_id="$("${compose[@]}" ps -aq webapi)"
imagen="$(docker inspect --format '{{.Config.Image}}' "$webapi_id")"
origen="$temporal"
if command -v cygpath >/dev/null 2>&1; then origen="$(cygpath -w "$temporal")"; fi  # Git Bash en Windows (pruebas locales)
MSYS_NO_PATHCONV=1 docker run --rm --user root --volumes-from "$webapi_id" -v "$origen:/restaurar:ro" --entrypoint bash "$imagen"     -c "cp -a /restaurar/documentos/. /app/data/documentos/ && cp -a /restaurar/claves/. /app/data/claves/ && chown -R app:app /app/data"

echo "Arrancando todo..."
"${compose[@]}" up -d
echo "Restaurado. Comprueba que puedes iniciar sesión y abrir un documento."
