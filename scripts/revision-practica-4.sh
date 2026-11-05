#!/usr/bin/env bash
# Revisión extremo a extremo de la Práctica 4 contra http://localhost:8080.
# Requisitos: el proyecto levantado con "docker compose up --build" y este script ejecutado DESDE la carpeta
# del repositorio (usa ./.env y "docker compose logs / exec"). Necesita bash y curl (Git Bash en Windows).
# Uso: bash scripts/revision-practica-4.sh [fase]   fase = completa (por defecto) | persistencia
BASE="http://localhost:8080"
TMP="${TMPDIR:-/tmp}/p4-revision"; mkdir -p "$TMP"
# curl nativo de Windows no entiende rutas /tmp dentro de -F archivo=@ruta: se usa la ruta con formato C:/...
TMP="$(cd "$TMP" && { pwd -W 2>/dev/null || pwd; })"
META="5f1c0a00-0000-4000-8000-000000000002"
envval() { grep -E "^$1=" .env 2>/dev/null | head -1 | cut -d= -f2- | tr -d '[:cntrl:]'; }
DB_PASSWORD="$(envval DB_PASSWORD)"
sql() { MSYS_NO_PATHCONV=1 docker compose exec -T db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$DB_PASSWORD" -d HabitTracker -h -1 -W -Q "SET NOCOUNT ON; $1" 2>&1 | tr -d "[:space:]"; }
ADMIN_C="$(envval ADMIN_CORREO)"; ADMIN_P="$(envval ADMIN_CLAVE)"
[ -z "$ADMIN_C" ] && ADMIN_C="admin@ejemplo.com"; [ -z "$ADMIN_P" ] && ADMIN_P="Ejemplo_Admin_2026!"
SUF="${E2E_SUFIJO:-$RANDOM}"
ANA="ana$SUF@example.com"; BETO="beto$SUF@example.com"
PASS_N=0; FAIL_N=0
STATUS=""; BODY=""

ok()   { PASS_N=$((PASS_N+1)); echo "  PASA   $1"; }
bad()  { FAIL_N=$((FAIL_N+1)); echo "  FALLA  $1   (obtenido: $2)"; }
check(){ if [ "$3" = "$2" ]; then ok "$1 [$3]"; else bad "$1 esperado=$2" "$3 | ${BODY:0:160}"; fi; }
has()  { if echo "$BODY" | grep -q -- "$2"; then ok "$1"; else bad "$1 (no contiene '$2')" "${BODY:0:160}"; fi; }
hasnt(){ if echo "$BODY" | grep -q -- "$2"; then bad "$1 (contiene '$2')" "${BODY:0:160}"; else ok "$1"; fi; }

# req METODO RUTA [JSON] [TOKEN]
req() {
  local m="$1" p="$2" d="$3" t="$4"
  local args=(-s -o "$TMP/body" -w "%{http_code}" -X "$m" "$BASE$p")
  [ -n "$t" ] && args+=(-H "Authorization: Bearer $t")
  [ -n "$d" ] && args+=(-H "Content-Type: application/json" -d "$d")
  STATUS=$(curl "${args[@]}"); BODY=$(cat "$TMP/body")
}
campo() { echo "$BODY" | sed -n "s/.*\"$1\":\"\([^\"]*\)\".*/\1/p" | head -1; }

# espera una línea de correo en el log para un destinatario con el patrón dado y devuelve el código
leer_correo() {
  local destino="$1" asunto="$2" i linea
  for i in $(seq 1 30); do
    linea=$(docker compose logs webapi 2>&1 | grep "CORREO EN CONSOLA" | grep "$destino" | grep "$asunto" | tail -1)
    if [ -n "$linea" ]; then echo "$linea" | sed -n 's/.*Código: \([^ |]*\).*/\1/p'; return; fi
    sleep 3
  done
}
login() { req POST /api/auth/login "{\"email\":\"$1\",\"password\":\"$2\"}"; TOKEN=$(campo token); [ -z "$TOKEN" ] && TOKEN=$(campo accessToken); }
registrar_y_activar() {
  req POST /api/auth/register "{\"nombre\":\"Prueba $1\",\"correo\":\"$1\",\"password\":\"ClaveSegura-Nexus-2026\"}"
  check "registro $1" 201 "$STATUS"
  local tok; tok=$(leer_correo "$1" "Activa")
  [ -z "$tok" ] && { bad "el log trae el correo de activación de $1" "sin línea [CORREO EN CONSOLA]"; return; }
  if echo "$tok" | grep -Eq '^[0-9]{6}$'; then ok "el correo de $1 trae un código de 6 dígitos ($tok)"; else bad "código de 6 dígitos" "$tok"; fi
  req POST /api/auth/activate "{\"correo\":\"$1\",\"codigo\":\"$tok\"}"
  check "activación $1 con correo + código" 200 "$STATUS"
  req POST /api/auth/activate "{\"correo\":\"$1\",\"codigo\":\"$tok\"}"
  check "reutilizar el código se rechaza" 400 "$STATUS"
}

if [ "$1" = "persistencia" ]; then
  echo "=== Persistencia tras docker compose down / up ==="
  ANA=$(cat "$TMP/ana-correo.txt"); login "$ANA" "ClaveNueva-Nexus-2026"; check "el usuario inicia sesión después de down/up" 200 "$STATUS"
  TA="$TOKEN"
  req GET /api/documents "" "$TA"; check "listado tras reiniciar" 200 "$STATUS"; has "el documento sigue en el listado" "informe.pdf"
  DOC=$(echo "$BODY" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p' | head -1)
  curl -s -o "$TMP/bajado2.pdf" -H "Authorization: Bearer $TA" "$BASE/api/documents/$DOC/download"
  if cmp -s "$TMP/informe.pdf" "$TMP/bajado2.pdf"; then ok "el documento se descarga con los mismos bytes"; else bad "descarga idéntica tras reiniciar" "bytes distintos"; fi
  echo "=== RESUMEN: $PASS_N pasan, $FAIL_N fallan ==="; exit $FAIL_N
fi

echo "=== 0. La aplicación responde ==="
curl -s -o /dev/null -w "%{http_code}" "$BASE/swagger/index.html" | { read c; [ "$c" = 200 ] && echo "  PASA   Swagger responde [200]" || echo "  FALLA  Swagger [$c]"; }
req GET /api/documents; check "sin sesión, listar documentos" 401 "$STATUS"

echo "=== 1. Registro con activación por correo ==="
registrar_y_activar "$ANA"
registrar_y_activar "$BETO"
req POST /api/auth/register "{\"nombre\":\"x\",\"correo\":\"debil$SUF@example.com\",\"password\":\"123\"}"; check "registro con clave débil se rechaza" 400 "$STATUS"
echo "  -- código de activación: fuerza bruta y reenvío (Carla) --"
CARLA="carla$SUF@example.com"
req POST /api/auth/register "{\"nombre\":\"Carla\",\"correo\":\"$CARLA\",\"password\":\"ClaveSegura-Nexus-2026\"}"; check "registro de Carla" 201 "$STATUS"
CC=$(leer_correo "$CARLA" "Activa")
MALO="000000"; [ "$CC" = "000000" ] && MALO="000001"
req POST /api/auth/activate "{\"correo\":\"$CARLA\",\"codigo\":\"$MALO\"}"; check "código incorrecto" 400 "$STATUS"
req POST /api/auth/activate "{\"correo\":\"otro$SUF@example.com\",\"codigo\":\"$CC\"}"; check "el código con el correo de otra persona" 400 "$STATUS"
for i in 1 2 3 4; do req POST /api/auth/activate "{\"correo\":\"$CARLA\",\"codigo\":\"$MALO\"}"; done
req POST /api/auth/activate "{\"correo\":\"$CARLA\",\"codigo\":\"$CC\"}"; check "tras 5 fallos, el código correcto ya no sirve" 400 "$STATUS"
ANTES=$(docker compose logs webapi 2>&1 | grep "CORREO EN CONSOLA" | grep -c "$CARLA")
req POST /api/auth/resend-activation "{\"correo\":\"$CARLA\"}"; check "reenviar de inmediato responde igual (no revela nada)" 200 "$STATUS"
sleep 18
DESPUES=$(docker compose logs webapi 2>&1 | grep "CORREO EN CONSOLA" | grep -c "$CARLA")
[ "$ANTES" = "$DESPUES" ] && ok "el enfriamiento de 60 s evita un segundo correo y un contador reiniciado" || bad "enfriamiento del reenvío" "correos $ANTES -> $DESPUES"
req POST /api/auth/activate "{\"correo\":\"$CARLA\",\"codigo\":\"abc\"}"; check "código con formato raro se rechaza sin romper nada" 400 "$STATUS"
req POST /api/auth/login "{\"email\":\"$CARLA\",\"password\":\"ClaveSegura-Nexus-2026\"}"; check "Carla sigue sin poder entrar (cuenta inactiva, rechazo controlado)" 400 "$STATUS"

echo "  -- ráfaga de 12 intentos EN PARALELO contra un código (Dora): la reserva debe ser atómica --"
DORA="dora$SUF@example.com"
req POST /api/auth/register "{\"nombre\":\"Dora\",\"correo\":\"$DORA\",\"password\":\"ClaveSegura-Nexus-2026\"}"; check "registro de Dora" 201 "$STATUS"
DC=$(leer_correo "$DORA" "Activa")
MALO2="000000"; [ "$DC" = "000000" ] && MALO2="000001"
rm -f "$TMP"/rafaga.*.txt
# Un archivo por petición: en Windows varios procesos que hacen >> al mismo archivo a la vez se pisan.
for i in $(seq 1 12); do
  curl -s -o /dev/null -w "%{http_code}
" -X POST "$BASE/api/auth/activate" -H "Content-Type: application/json" -d "{\"correo\":\"$DORA\",\"codigo\":\"$MALO2\"}" > "$TMP/rafaga.$i.txt" &
done
wait
LLEGARON=$(cat "$TMP"/rafaga.*.txt | grep -c '^400$'); LIMITADAS=$(cat "$TMP"/rafaga.*.txt | grep -c '^429$')
CONTADOR=$(sql "SELECT IntentosActivacion FROM usuarios WHERE Correo = '$DORA'")
echo "  (de 12 peticiones: $LLEGARON llegaron al servicio, $LIMITADAS fueron limitadas por IP; contador en la base: $CONTADOR)"
[ "$CONTADOR" = "$LLEGARON" ] && ok "el contador de la base cuenta TODOS los intentos en paralelo (sin pérdidas)" || bad "contador atómico" "contador=$CONTADOR, llegaron=$LLEGARON"
[ "$LLEGARON" -gt 5 ] && ok "se superó el tope de 5 con peticiones simultáneas y ninguna extra comparó el código" || bad "ráfaga suficiente" "$LLEGARON"
req POST /api/auth/activate "{\"correo\":\"$DORA\",\"codigo\":\"$DC\"}"; check "tras la ráfaga, el código correcto ya no sirve" 400 "$STATUS"
[ "$(sql "SELECT CAST(Activo AS int) FROM usuarios WHERE Correo = '$DORA'")" = "0" ] && ok "la cuenta de Dora sigue inactiva" || bad "la cuenta de Dora sigue inactiva" "activa"
echo "=== 2. Inicio y cierre de sesión ==="
login "$ANA" "ClaveSegura-Nexus-2026"; check "login" 200 "$STATUS"; TA="$TOKEN"
[ -n "$TA" ] && ok "se recibe un token" || bad "se recibe un token" "vacío"
req GET /api/auth/me "" "$TA"; check "me con sesión" 200 "$STATUS"
req POST /api/auth/logout "" "$TA"; echo "  (logout -> $STATUS)"
req GET /api/auth/me "" "$TA"; check "me tras logout" 401 "$STATUS"
login "$ANA" "ClaveIncorrecta1"; check "login con clave incorrecta" 401 "$STATUS"

echo "  -- límite de peticiones por IP en la activación --"
RL=$(for i in $(seq 1 40); do curl -s -o /dev/null -w "%{http_code}
" -X POST "$BASE/api/auth/activate" -H "Content-Type: application/json" -d "{\"correo\":\"nadie$SUF@example.com\",\"codigo\":\"123456\"}"; done | sort | uniq -c | tr '
' ' ')
echo "  (40 peticiones seguidas -> $RL)"
echo "$RL" | grep -q "429" && ok "pasado el límite por minuto la API responde 429" || bad "límite por IP" "$RL"
echo "=== 3. Estándar contra una operación de Administrador ==="
login "$ANA" "ClaveSegura-Nexus-2026"; TA="$TOKEN"
req GET /api/admin/usuarios "" "$TA"; check "Estándar en /api/admin/usuarios" 403 "$STATUS"
login "$ADMIN_C" "$ADMIN_P"; check "login del Administrador inicial" 200 "$STATUS"; TADMIN="$TOKEN"
req GET /api/admin/usuarios "" "$TADMIN"; check "Administrador en /api/admin/usuarios" 200 "$STATUS"

echo "=== 4. Recuperación de contraseña ==="
req POST /api/auth/forgot-password "{\"correo\":\"$ANA\"}"; check "forgot-password" 200 "$STATUS"
COD=$(leer_correo "$ANA" "Recuperaci")
[ -n "$COD" ] && ok "el log trae el código de recuperación ($COD)" || bad "el log trae el código" "vacío"
req POST /api/auth/reset-password "{\"correo\":\"$ANA\",\"codigo\":\"$COD\",\"nuevaPassword\":\"ClaveNueva-Nexus-2026\"}"; check "reset-password" 200 "$STATUS"
req POST /api/auth/reset-password "{\"correo\":\"$ANA\",\"codigo\":\"$COD\",\"nuevaPassword\":\"ClaveOtra-Nexus-2026\"}"; check "reutilizar el código se rechaza" 400 "$STATUS"
login "$ANA" "ClaveNueva-Nexus-2026"; check "login con la clave nueva" 200 "$STATUS"; TA="$TOKEN"
login "$ANA" "ClaveSegura-Nexus-2026"; check "la clave vieja ya no sirve" 401 "$STATUS"
login "$BETO" "ClaveSegura-Nexus-2026"; TB="$TOKEN"

echo "=== 5. Gestión de permisos ==="
req POST /api/permissions '{"permisoSolicitado":"Habits.AdvancedReconfig"}' "$TA"; check "crear solicitud" 201 "$STATUS"
P1=$(echo "$BODY" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')
req POST "/api/permissions/$P1/approve" "" "$TA"; check "un Estándar no puede aprobar" 403 "$STATUS"
req POST "/api/permissions/$P1/approve" "" "$TADMIN"; echo "  (aprobar -> $STATUS)"; [ "$STATUS" = 200 -o "$STATUS" = 204 ] && ok "el Administrador aprueba" || bad "el Administrador aprueba" "$STATUS"
req POST "/api/permissions/$P1/approve" "" "$TADMIN"; check "aprobar otra vez (transición fuera de la tabla)" 400 "$STATUS"
req POST "/api/permissions/$P1/reject" '{"motivoRechazo":"ya aprobada"}' "$TADMIN"; check "rechazar una ya aprobada" 400 "$STATUS"
req POST /api/permissions '{"permisoSolicitado":"Reportes.Ver"}' "$TA"; P2=$(echo "$BODY" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')
req POST "/api/permissions/$P2/reject" '{"motivoRechazo":"no procede"}' "$TADMIN"; [ "$STATUS" = 200 -o "$STATUS" = 204 ] && ok "el Administrador rechaza [$STATUS]" || bad "el Administrador rechaza" "$STATUS"
req POST "/api/permissions/$P2/approve" "" "$TADMIN"; check "aprobar una ya rechazada" 400 "$STATUS"
req POST /api/permissions '{"permisoSolicitado":"Propio.Admin"}' "$TADMIN"; P3=$(echo "$BODY" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')
req POST "/api/permissions/$P3/approve" "" "$TADMIN"; echo "  (autoaprobación -> $STATUS)"; [ "$STATUS" = 400 -o "$STATUS" = 403 ] && ok "autoaprobación prohibida [$STATUS]" || bad "autoaprobación prohibida" "$STATUS"

echo "=== 6. Módulo de negocio (hábitos) ==="
req POST /api/habits "{\"nombre\":\"Leer 20 paginas\",\"periodicidad\":\"Diaria\",\"cantidadObjetivo\":20,\"unidad\":\"paginas\",\"metaId\":\"$META\"}" "$TA"; check "crear hábito con la Meta de demostración" 201 "$STATUS"
H1=$(echo "$BODY" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')
req POST "/api/habits/$H1/transition" '{"nuevoEstado":"Activo"}' "$TA"; [ "$STATUS" = 200 -o "$STATUS" = 204 ] && ok "Planificado -> Activo [$STATUS]" || bad "Planificado -> Activo" "$STATUS"
req POST "/api/habits/$H1/transition" '{"nuevoEstado":"Completado"}' "$TA"; check "Activo -> Completado sin cumplir la meta (prohibida)" 400 "$STATUS"
req POST /api/habits "{\"nombre\":\"Meditar\",\"periodicidad\":\"Diaria\",\"cantidadObjetivo\":10,\"unidad\":\"min\",\"metaId\":\"$META\"}" "$TA"; H2=$(echo "$BODY" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')
req POST "/api/habits/$H2/transition" '{"nuevoEstado":"Completado"}' "$TA"; check "Planificado -> Completado (prohibida)" 400 "$STATUS"
req GET /api/habits "" "$TA"; check "listar hábitos" 200 "$STATUS"

echo "=== 7. Manejador de documentos ==="
printf '%%PDF-1.4 contenido de prueba P4 %s' "$SUF" > "$TMP/informe.pdf"
: > "$TMP/vacio.pdf"
printf 'MZ binario' > "$TMP/virus.exe"
printf '<script>alert(1)</script>' > "$TMP/nota.txt"
subir() { STATUS=$(curl -s -o "$TMP/body" -w "%{http_code}" -X POST "$BASE/api/documents" -H "Authorization: Bearer $1" -F "archivo=@$2;type=${3:-application/pdf}"); BODY=$(cat "$TMP/body"); }
STATUS=$(curl -s -o "$TMP/body" -w "%{http_code}" -X POST "$BASE/api/documents" -H "Authorization: Bearer $TA"); BODY=$(cat "$TMP/body"); check "subir sin archivo -> rechazo controlado" 400 "$STATUS"; hasnt "sin trazas de pila" "   at "
subir "$TA" "$TMP/vacio.pdf"; check "subir archivo vacío -> rechazo controlado" 400 "$STATUS"
subir "$TA" "$TMP/virus.exe" "application/octet-stream"; check "subir .exe -> rechazo" 400 "$STATUS"
STATUS=$(curl -s -o "$TMP/body" -w "%{http_code}" -X POST "$BASE/api/documents" -F "archivo=@$TMP/informe.pdf"); check "subir sin sesión" 401 "$STATUS"
subir "$TA" "$TMP/informe.pdf"; check "subir documento (dueño)" 201 "$STATUS"
has "registra el nombre" '"nombre":"informe.pdf"'; has "registra el tipo" '"tipo":"application/pdf"'; has "registra el tamaño" '"tamanoBytes":'; has "registra la fecha" '"fechaSubidaUtc":"'; has "registra quién lo subió" "$ANA"
DOC=$(echo "$BODY" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')
req GET "/api/documents/$DOC" "" "$TA"; check "consultar el registro (dueño)" 200 "$STATUS"
req GET /api/documents "" "$TA"; check "listar (dueño)" 200 "$STATUS"; has "el listado trae el documento" "$DOC"
curl -s -D "$TMP/cab" -o "$TMP/bajado.pdf" -H "Authorization: Bearer $TA" "$BASE/api/documents/$DOC/download"
if cmp -s "$TMP/informe.pdf" "$TMP/bajado.pdf"; then ok "descarga idéntica byte a byte"; else bad "descarga idéntica" "distinta"; fi
grep -qi "content-disposition: attachment" "$TMP/cab" && ok "Content-Disposition: attachment" || bad "Content-Disposition" "ausente"
grep -qi "x-content-type-options: nosniff" "$TMP/cab" && ok "X-Content-Type-Options: nosniff" || bad "nosniff" "ausente"
echo "  -- otro usuario Estándar (Beto) --"
req GET /api/documents "" "$TB"; check "Beto lista" 200 "$STATUS"; hasnt "Beto NO ve el documento de Ana" "$DOC"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" -H "Authorization: Bearer $TB" "$BASE/api/documents/$DOC/download"); check "Beto descarga el documento de Ana" 403 "$STATUS"
req DELETE "/api/documents/$DOC" "" "$TB"; check "Beto elimina el documento de Ana" 403 "$STATUS"
req GET "/api/documents/$DOC" "" "$TB"; check "Beto consulta el registro de Ana" 403 "$STATUS"
echo "  -- Administrador --"
req GET /api/documents "" "$TADMIN"; has "el Administrador ve todos" "$DOC"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" -H "Authorization: Bearer $TADMIN" "$BASE/api/documents/$DOC/download"); check "el Administrador descarga" 200 "$STATUS"
echo "  -- tipo MIME no controlado por el cliente --"
subir "$TA" "$TMP/nota.txt" "text/html"; check "subir .txt declarado como text/html" 201 "$STATUS"; has "se guarda como text/plain" '"tipo":"text/plain"'
DOCTXT=$(echo "$BODY" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')
echo "  -- eliminación lógica --"
req DELETE "/api/documents/$DOCTXT" "" "$TA"; check "el dueño elimina" 204 "$STATUS"
req GET /api/documents "" "$TA"; hasnt "el eliminado deja de listarse" "$DOCTXT"
req GET "/api/documents/$DOCTXT" "" "$TA"; check "el eliminado responde 404" 404 "$STATUS"
req GET /api/documents "" "$TADMIN"; hasnt "tampoco el Administrador lo lista" "$DOCTXT"
FILAS=$(MSYS_NO_PATHCONV=1 docker compose exec -T db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$DB_PASSWORD" -d HabitTracker -h -1 -W -Q "SET NOCOUNT ON; SELECT CAST(Eliminado AS int) FROM Documentos WHERE Id = '$DOCTXT'" 2>&1 | tr -d '[:space:]')
[ "$FILAS" = "1" ] && ok "el registro sigue en la base con Eliminado=1" || bad "el registro sigue en la base" "'$FILAS'"
N=$(MSYS_NO_PATHCONV=1 docker compose exec -T webapi sh -c 'ls /app/data/documentos | wc -l' | tr -d '[:space:]')
[ "$N" -ge 2 ] 2>/dev/null && ok "los archivos siguen en el volumen ($N)" || bad "los archivos siguen en el volumen" "$N"

CLAVE_CLIMA="$(envval OPENWEATHER_API_KEY)"
if [ -z "$CLAVE_CLIMA" ] || [ "$CLAVE_CLIMA" = "pon_aqui_tu_clave_de_openweathermap" ]; then
  echo "=== 8. API externa (clave de ejemplo -> rechazo controlado) ==="
  req GET "/api/habits/plan-del-dia?ciudad=Santo%20Domingo" "" "$TA"; check "plan del día con clave de ejemplo" 503 "$STATUS"; has "mensaje comprensible" "clima"; hasnt "sin la clave" "pon_aqui"; hasnt "sin trazas" "Exception"
else
  echo "=== 8. API externa (clave REAL -> datos del proveedor) ==="
  req GET "/api/habits/plan-del-dia?ciudad=Santo%20Domingo" "" "$TA"; check "plan del día con la clave real" 200 "$STATUS"
  has "trae el clima" '"clima":{"temperaturaC":'; has "trae el agua adicional" '"aguaAdicionalMl":'; has "recomienda para el hábito activo" '"lugarSugerido":"'
  hasnt "la respuesta no contiene la clave" "$CLAVE_CLIMA"
  if docker compose logs webapi 2>&1 | grep -q -F -- "$CLAVE_CLIMA"; then bad "la clave no aparece en los logs" "APARECE"; else ok "la clave no aparece en los logs"; fi
  req GET "/api/habits/plan-del-dia?ciudad=CiudadQueNoExiste12345" "" "$TA"; echo "  (ciudad inexistente -> $STATUS)"; [ "$STATUS" = 404 -o "$STATUS" = 400 ] && ok "ciudad inexistente se rechaza [$STATUS]" || bad "ciudad inexistente" "$STATUS"
fi
req GET /api/habits "" "$TA"; check "el resto del proyecto sigue funcionando" 200 "$STATUS"
req GET "/api/habits/plan-del-dia" "" "$TA"; check "sin ciudad" 400 "$STATUS"
req GET "/api/habits/plan-del-dia?ciudad=%3Cscript%3E" "" "$TA"; check "ciudad con caracteres no válidos" 400 "$STATUS"
req GET "/api/habits/plan-del-dia?ciudad=Santo%20Domingo" "" ""; check "sin sesión" 401 "$STATUS"
req GET "/api/habits/plan-del-dia?ciudad=Santo%20Domingo" "" "$TADMIN"; check "el Administrador (no Estándar)" 403 "$STATUS"

echo "=== RESUMEN: $PASS_N pasan, $FAIL_N fallan ==="
echo "$ANA" > "$TMP/ana-correo.txt"
exit $FAIL_N
