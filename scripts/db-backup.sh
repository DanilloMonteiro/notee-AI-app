#!/usr/bin/env bash
# Backup lógico do PostgreSQL do Notee (formato custom do pg_dump).
# Uso: scripts/db-backup.sh [container] [banco] [arquivo]
#   padrão: container notee-db, banco notelab_db, arquivo backups/notee-<data>.dump
set -euo pipefail
CONTAINER="${1:-notee-db}"
DB="${2:-notelab_db}"
OUT="${3:-backups/notee-$(date +%Y%m%d-%H%M%S).dump}"
PGPORT="${PGPORT:-5432}"   # o serviço "db" do compose escuta em 5434: exporte PGPORT=5434

mkdir -p "$(dirname "$OUT")"
docker exec "$CONTAINER" pg_dump -U postgres -p "$PGPORT" -d "$DB" -Fc --no-owner > "$OUT"
echo "Backup gravado em $OUT ($(wc -c < "$OUT") bytes)"
