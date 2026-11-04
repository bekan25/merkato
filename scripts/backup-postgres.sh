#!/usr/bin/env sh
set -eu

timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup_dir="${BACKUP_DIR:-./backups}"
mkdir -p "$backup_dir"

docker compose exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB"' \
  | gzip > "$backup_dir/merkato-$timestamp.sql.gz"

echo "Backup written to $backup_dir/merkato-$timestamp.sql.gz"
echo "Copy this file to encrypted off-host storage and verify restore regularly."
