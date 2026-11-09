#!/usr/bin/env bash
# Compara a contagem de linhas das tabelas principais entre dois bancos (origem x restaurado).
# Uso: scripts/db-verify.sh <banco_origem> <banco_restaurado> [container]
set -euo pipefail
SRC="${1:?}"; DST="${2:?}"; CONTAINER="${3:-notee-db}"; PGPORT="${PGPORT:-5432}"
TABLES='"AspNetUsers" "Notes" "NoteRevisions" "Doubts" "ResearchJobs" "Boards" "BoardEdges" "Publications" "__EFMigrationsHistory"'
status=0
for t in $TABLES; do
  a=$(docker exec "$CONTAINER" psql -U postgres -p "$PGPORT" -d "$SRC" -tAc "SELECT count(*) FROM $t")
  b=$(docker exec "$CONTAINER" psql -U postgres -p "$PGPORT" -d "$DST" -tAc "SELECT count(*) FROM $t")
  flag=OK; [ "$a" = "$b" ] || { flag=DIFERENTE; status=1; }
  printf '%-24s %8s %8s  %s\n' "$t" "$a" "$b" "$flag"
done
exit $status
