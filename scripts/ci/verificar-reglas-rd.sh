#!/usr/bin/env bash
# Comprobaciones estáticas de los requisitos de diseño (RD) que se pueden verificar leyendo el código:
# RD-03 (dependencias entre capas y módulos), RD-05 (hash débil), RD-08 (trazas al cliente),
# RD-10 (secretos versionados) y RD-11 (fechas en UTC). El resto de RD las cubre la revisión humana
# del agente core-transversal-reviewer y las pruebas.
# Cada fallo imprime una anotación ::error de GitHub y el script termina con código 1.
# Solo mira archivos versionados (git grep). El directorio tests/ queda fuera a propósito: algunas
# pruebas usan DateTime.Now para comprobar que el dominio rechaza fechas locales.
set -uo pipefail
export LC_ALL=C.UTF-8

cd "$(git rev-parse --show-toplevel)" || exit 2

REGLAS=("RD-03" "RD-05" "RD-08" "RD-10" "RD-11")
DESCRIPCIONES=(
  "Dependencias entre capas y módulos"
  "Sin hash débil (MD5 / SHA1)"
  "Sin trazas de pila hacia el cliente"
  "Sin secretos versionados"
  "Fechas en UTC (sin DateTime.Now)"
)
FALLIDAS=" "

# comprobar <regla> <motivo> <salida de git grep o ls-files>: si hay salida, cada línea es un fallo.
comprobar() {
  local regla="$1" motivo="$2" salida="$3" linea ubicacion
  [ -z "$salida" ] && return 0
  while IFS= read -r linea; do
    ubicacion=$(echo "$linea" | cut -d: -f1,2)
    echo "::error title=${regla}::${ubicacion}: ${motivo}"
  done <<< "$salida"
  case "$FALLIDAS" in *" $regla "*) ;; *) FALLIDAS="${FALLIDAS}${regla} " ;; esac
}

# ---- RD-03: el Core no depende del negocio y las capas solo apuntan hacia dentro ----
comprobar "RD-03" "un proyecto Domain no puede tener ProjectReference" \
  "$(git grep -nE '<ProjectReference' -- 'Modules/*.Domain.csproj')"
comprobar "RD-03" "Domain y Application no pueden depender de Entity Framework" \
  "$(git grep -nE 'Microsoft\.EntityFrameworkCore' -- 'Modules/*.Domain.csproj' 'Modules/*.Application.csproj')"
comprobar "RD-03" "Application no puede referenciar Infrastructure" \
  "$(git grep -nE 'Infrastructure' -- 'Modules/*.Application.csproj')"
for modulo in AccessControl Permissions DocumentManager Auditing Notifications Reports; do
  comprobar "RD-03" "una pieza del Core (${modulo}) no puede depender del módulo de negocio HabitTracker" \
    "$(git grep -nE 'HabitTracker\.(Domain|Application|Infrastructure)|^[[:space:]]*using[[:space:]]+HabitTracker' -- "Modules/${modulo}/*")"
done

# ---- RD-05: nada de hash débil ----
comprobar "RD-05" "algoritmo de hash débil (MD5/SHA1); usar PBKDF2/bcrypt/argon2 para contraseñas y SHA-256 o superior" \
  "$(git grep -nE '\b(MD5|SHA1)\.(Create|HashData)\b|new[[:space:]]+(MD5|SHA1)(CryptoServiceProvider|Managed)?\(' -- 'Host/*.cs' 'Modules/*.cs')"

# ---- RD-08: sin trazas de pila ni ex.ToString() en el código de producción ----
comprobar "RD-08" "no se debe exponer StackTrace ni ex.ToString(); el cliente recibe mensajes genéricos" \
  "$(git grep -nE '\.StackTrace\b|\b(ex|exception|e)\.ToString\(\)' -- 'Host/*.cs' 'Modules/*.cs')"

# ---- RD-10: sin secretos en el repositorio ----
comprobar "RD-10" "archivo de secretos o claves versionado; debe ir en variables de entorno o User Secrets" \
  "$(git ls-files | grep -E '(^|/)\.env$|\.env\.local$|(^|/)secrets\.json$|appsettings\..*\.local\.json$|\.(pfx|key|p12)$' || true)"
comprobar "RD-10" "JwtSettings:Clave con valor en appsettings; debe quedar vacía y venir del entorno" \
  "$(git grep -nE '"Clave"[[:space:]]*:[[:space:]]*"[^"]+"' -- 'Host/WebApi/appsettings*.json')"
comprobar "RD-10" "contraseña dentro de una cadena de conexión versionada" \
  "$(git grep -niE '(Password|Pwd)[[:space:]]*=' -- 'Host/WebApi/appsettings*.json')"

# ---- RD-11: fechas siempre en UTC ----
comprobar "RD-11" "usar DateTime.UtcNow en lugar de DateTime.Now / DateTime.Today" \
  "$(git grep -nE '\bDateTime(Offset)?\.(Now|Today)\b' -- 'Host/*.cs' 'Modules/*.cs')"

# ---- Resumen ----
{
  echo "### Reglas del repositorio (RD verificables)"
  echo "| Regla | Qué comprueba | Resultado |"
  echo "|---|---|---|"
  for i in "${!REGLAS[@]}"; do
    marca="✅"
    case "$FALLIDAS" in *" ${REGLAS[$i]} "*) marca="❌" ;; esac
    echo "| ${REGLAS[$i]} | ${DESCRIPCIONES[$i]} | ${marca} |"
  done
} >> "${GITHUB_STEP_SUMMARY:-/dev/stdout}"

if [ "$FALLIDAS" != " " ]; then
  echo "Reglas incumplidas:${FALLIDAS}"
  exit 1
fi
echo "Todas las reglas RD verificables se cumplen."
