#!/usr/bin/env bash
# Restaura um backup em um banco NOVO (nunca sobrescreve o banco existente).
# Uso: scripts/db-restore.sh <arquivo.dump> <banco_destino> [container]
set -euo pipefail
FILE="${1:?informe o arquivo .dump}"
TARGET="${2:?informe o nome do banco de destino (será criado)}"
CONTAINER="${3:-notee-db}"
PGPORT="${PGPORT:-5432}"

if docker exec "$CONTAINER" psql -U postgres -p "$PGPORT" -tAc "SELECT 1 FROM pg_database WHERE datname='$TARGET'" | grep -q 1; then
  echo "O banco '$TARGET' já existe. Escolha outro nome (restore não sobrescreve)." >&2
  exit 1
fi

docker exec "$CONTAINER" createdb -U postgres -p "$PGPORT" "$TARGET"
docker exec -i "$CONTAINER" pg_restore -U postgres -p "$PGPORT" -d "$TARGET" --no-owner --exit-on-error < "$FILE"
echo "Restaurado em '$TARGET'. Verifique com: scripts/db-verify.sh <banco_origem> $TARGET"
