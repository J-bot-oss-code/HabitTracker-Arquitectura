#!/usr/bin/env bash
# Valida que los mensajes de commit sigan la regla del AGENTS.md:
#   tipo: descripción en español [RF-XX-NN, RD-NN]
# Uso: verificar-commits.sh <base_sha> <head_sha>   valida los commits nuevos de una PR (sin merges)
#      verificar-commits.sh --mensaje "<asunto>"    valida un asunto suelto (lo usan las pruebas)
set -euo pipefail
export LC_ALL=C.UTF-8

ID='(RF-[A-Z]+-[0-9]{2}|RD-[0-9]{2})'
PATRON="^(feat|fix|docs|test|refactor|perf|style|build|ci|chore|revert)(\([a-z0-9._/-]+\))?!?: [^ ].+ \[${ID}(, ${ID})*\]$"
# Primera palabra típica de un mensaje en inglés (el AGENTS.md exige español).
INGLES='^(add|adds|added|update|updates|updated|fix|fixes|fixed|remove|removes|removed|implement|implements|implemented|create|creates|created|refactor|bump|bumps|change|changes|changed|improve|improves|delete|deletes|rename|move|merge|initial|use|set|make|allow)$'

# validar <asunto>: imprime el motivo y devuelve 1 si no cumple.
validar() {
  local asunto="$1" desc primera
  if [[ ! "$asunto" =~ $PATRON ]]; then
    echo "formato inválido (esperado: 'tipo: descripción en español [RF-XX-NN, RD-NN]')"
    return 1
  fi
  desc="${asunto#*: }"
  primera="${desc%% *}"
  primera="${primera,,}"
  if [[ "$primera" =~ $INGLES ]]; then
    echo "la descripción parece estar en inglés ('$primera')"
    return 1
  fi
  return 0
}

if [ "${1:-}" = "--mensaje" ]; then
  validar "${2:-}"
  exit $?
fi

if [ "$#" -ne 2 ]; then
  echo "Uso: $0 <base_sha> <head_sha> | --mensaje \"<asunto>\"" >&2
  exit 2
fi

base="$1"
head="$2"
fallos=0

while IFS=$'\t' read -r sha correo asunto; do
  if [[ "$correo" == *"dependabot[bot]"* ]]; then
    echo "omitido (Dependabot): ${sha:0:7}"
    continue
  fi
  if motivo=$(validar "$asunto"); then
    echo "ok  ${sha:0:7} $asunto"
  else
    echo "::error title=Commit ${sha:0:7}::$motivo — '$asunto'"
    fallos=$((fallos + 1))
  fi
done < <(git log --no-merges --format='%H%x09%ae%x09%s' "$base..$head")

if [ "$fallos" -ne 0 ]; then
  echo "$fallos commit(s) no cumplen el formato de AGENTS.md"
  exit 1
fi
echo "Todos los commits cumplen el formato."
