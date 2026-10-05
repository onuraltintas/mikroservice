#!/usr/bin/env bash
set -euo pipefail
docker exec postgres sh -c 'psql -U "$POSTGRES_USER" -d identity_db -c "\d identity.\"PlatformLegalPages\"" -c "\d identity.\"PlatformLegalPageRevisions\"" -c "\d identity.users"'
