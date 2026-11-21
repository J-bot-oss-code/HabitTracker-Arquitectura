#!/usr/bin/env bash
# Actualiza producción: respalda, trae el código nuevo, reconstruye y comprueba que la API esté sana.
# Ejecútalo en el servidor, como root o con sudo. Las migraciones se aplican solas al arrancar la API (una sola réplica).
set -euo pipefail

cd "$(dirname "$0")/../.."
compose=(docker compose -f docker-compose.prod.yml)
anterior="$(git rev-parse --short HEAD)"

echo "1/4 Respaldo previo..."
scripts/despliegue/respaldar.sh

echo "2/4 Código nuevo..."
git pull --ff-only

echo "3/4 Reconstruyendo y arrancando..."
"${compose[@]}" up -d --build

echo "4/4 Esperando a la API..."
for _ in $(seq 1 40); do
    contenedor="$("${compose[@]}" ps -q webapi)"
    estado="$(docker inspect --format '{{.State.Health.Status}}' "$contenedor" 2>/dev/null || echo "sin-contenedor")"
    if [ "$estado" = "healthy" ]; then
        echo "La API está sana: $(git rev-parse --short HEAD)."
        exit 0
    fi
    sleep 5
done

echo "La API no quedó sana. Para volver a la versión anterior:" >&2
echo "  git checkout ${anterior} && docker compose -f docker-compose.prod.yml up -d --build" >&2
echo "Si la migración nueva ya cambió la base, restaura el respaldo previo con scripts/despliegue/restaurar.sh." >&2
exit 1
