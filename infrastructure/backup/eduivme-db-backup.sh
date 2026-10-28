#!/usr/bin/env bash
# Roadmap P1: nightly PostgreSQL backup of every Eduİvme database.
#
#  - One custom-format pg_dump per database plus the roles (pg_dumpall --globals-only), each dump read back with
#    pg_restore --list, SHA256SUMS next to them. A night that fails leaves no half backup and writes last-status.
#  - Kept on the server: 7 daily, 4 weekly (Sunday) and 6 monthly (first backup of the month). Weekly and monthly
#    copies are hard links, so they take no extra space while the daily copy still exists.
#  - Off-site (optional): every file is encrypted with age for BACKUP_AGE_RECIPIENT (only the public key is on the
#    server) and copied with rclone to BACKUP_RCLONE_REMOTE/<date>; copies older than BACKUP_REMOTE_KEEP_DAYS go.
#
# Settings (environment, e.g. /etc/eduivme/db-backup.env):
#   BACKUP_DIR (default /var/backups/eduivme-db), POSTGRES_CONTAINER (default postgres),
#   BACKUP_RCLONE_REMOTE, BACKUP_REMOTE_PREFIX (folder per container, set by the systemd unit),
#   BACKUP_AGE_RECIPIENT, BACKUP_REMOTE_KEEP_DAYS (default 190),
#   BACKUP_HEALTHCHECK_URL (optional, pinged on success and with /fail on failure),
#   BACKUP_DATE (testing only; default today in Europe/Istanbul).
set -euo pipefail
umask 077

backup_dir=${BACKUP_DIR:-/var/backups/eduivme-db}
container=${POSTGRES_CONTAINER:-postgres}
date=${BACKUP_DATE:-$(TZ=Europe/Istanbul date +%F)}
remote=${BACKUP_RCLONE_REMOTE:-}
prefix=${BACKUP_REMOTE_PREFIX:-}
[[ -z "$prefix" || "$prefix" =~ ^[A-Za-z0-9_.-]+$ ]] || { echo "BACKUP_REMOTE_PREFIX yalnız harf, rakam, _ . - içerebilir" >&2; exit 2; }
recipient=${BACKUP_AGE_RECIPIENT:-}
remote_keep_days=${BACKUP_REMOTE_KEEP_DAYS:-190}
partial="$backup_dir/daily/.$date.partial"

ping() { [[ -z "${BACKUP_HEALTHCHECK_URL:-}" ]] || curl -fsS -m 10 --retry 3 "${BACKUP_HEALTHCHECK_URL}$1" >/dev/null || true; }
fail() {
  echo "Yedek başarısız: $1" >&2
  rm -rf "$partial" "$backup_dir/offsite/$date"
  mkdir -p "$backup_dir" && printf 'failed %s %s\n' "$date" "$1" > "$backup_dir/last-status"
  ping /fail
  exit 1
}
trap 'fail "satır $LINENO"' ERR

[[ "$date" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}$ ]] || { echo "BACKUP_DATE yyyy-MM-dd olmalı" >&2; exit 2; }
if [[ -n "$remote" && -z "$recipient" ]]; then
  echo "BACKUP_RCLONE_REMOTE için BACKUP_AGE_RECIPIENT (age açık anahtarı) gerekir; şifresiz yedek dışarı gönderilmez." >&2
  exit 2
fi

mkdir -p "$backup_dir/daily" "$backup_dir/weekly" "$backup_dir/monthly"
rm -rf "$partial"; mkdir -p "$partial"

user=$(docker exec "$container" printenv POSTGRES_USER)
mapfile -t databases < <(docker exec "$container" psql -U "$user" -d postgres -Atc \
  "SELECT datname FROM pg_database WHERE NOT datistemplate AND datname <> 'postgres' ORDER BY datname")
(( ${#databases[@]} > 0 )) || fail "veritabanı bulunamadı"

docker exec "$container" pg_dumpall -U "$user" --globals-only > "$partial/globals.sql"
for database in "${databases[@]}"; do
  [[ "$database" =~ ^[A-Za-z0-9_]+$ ]] || fail "beklenmeyen veritabanı adı"
  docker exec "$container" pg_dump -U "$user" -d "$database" -Fc > "$partial/$database.dump" || fail "$database dökümü alınamadı"
  [[ -s "$partial/$database.dump" ]] || fail "$database dökümü boş"
  docker exec -i "$container" pg_restore --list < "$partial/$database.dump" > /dev/null || fail "$database dökümü okunamadı"
done
(cd "$partial" && sha256sum -- *.dump globals.sql > SHA256SUMS)

rm -rf "$backup_dir/daily/$date"
mv "$partial" "$backup_dir/daily/$date"

# Weekly copy on Sunday, monthly copy for the first backup of each month (hard links).
if [[ $(date -u -d "$date" +%u) == 7 ]]; then rm -rf "$backup_dir/weekly/$date"; cp -al "$backup_dir/daily/$date" "$backup_dir/weekly/$date"; fi
month=${date:0:7}
if ! compgen -G "$backup_dir/monthly/$month-*" > /dev/null; then cp -al "$backup_dir/daily/$date" "$backup_dir/monthly/$date"; fi

keep_newest() { # folder count
  local folder=$1 count=$2
  mapfile -t old < <(find "$folder" -mindepth 1 -maxdepth 1 -type d -name '????-??-??' -printf '%f\n' | sort | head -n -"$count")
  for name in "${old[@]}"; do rm -rf -- "${folder:?}/$name"; done
}
keep_newest "$backup_dir/daily" 7
keep_newest "$backup_dir/weekly" 4
keep_newest "$backup_dir/monthly" 6

if [[ -n "$remote" ]]; then
  staged="$backup_dir/offsite/$date"
  rm -rf "$staged"; mkdir -p "$staged"
  for file in "$backup_dir/daily/$date"/*; do
    age -r "$recipient" -o "$staged/$(basename "$file").age" "$file"
  done
  target="$remote${prefix:+/$prefix}"
  rclone copy "$staged" "$target/$date"
  rclone delete --min-age "${remote_keep_days}d" "$target"
  rm -rf "$staged"
fi

printf 'ok %s %s\n' "$date" "${#databases[@]}" > "$backup_dir/last-status"
ping ""
echo "Yedek tamam: $date (${#databases[@]} veritabanı)"
