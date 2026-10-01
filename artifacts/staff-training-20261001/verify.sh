#!/usr/bin/env bash
set -euo pipefail
ip=$(docker inspect eduivme-production-speed-reading-service-1 --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
curl -fsS "http://$ip:8080/health/ready"
echo
docker exec postgres sh -c 'psql -U "$POSTGRES_USER" -d speedreading_owned_db -Atc "$1"' sh "SELECT column_name FROM information_schema.columns WHERE table_schema = 'speed_reading' AND column_name IN ('is_staff_training','program_progress_id');"
docker exec -i postgres pg_restore -l > /dev/null < /var/lib/eduivme/backups/staff-training-20261001/speedreading_owned_db.dump
