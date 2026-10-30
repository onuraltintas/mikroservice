#!/bin/sh
# Pulls the production images that GitHub Actions made for one commit on main (decision 30.10.2026: the server no
# longer compiles images). Usage: infrastructure/scripts/pull-release-images.sh <7-character commit id>
# Needs a one-time `docker login ghcr.io` with a read:packages token and PRODUCTION_IMAGE_REPOSITORY in .env pointing
# to ghcr.io/<owner>/eduivme. Prints each image id and the commit it was made from; changes no running container.
set -eu
tag=${1:?Usage: pull-release-images.sh <7-character commit id>}
case "$tag" in *[!0-9a-f]*) echo "Tag must be a lowercase hexadecimal commit id" >&2; exit 2 ;; esac
[ "${#tag}" -eq 7 ] || { echo "Tag must be the 7-character commit id" >&2; exit 2; }
cd /opt/eduivme
repo=$(sed -n 's/^PRODUCTION_IMAGE_REPOSITORY=//p' .env | tr -d '"' | tail -n 1)
case "$repo" in ghcr.io/*) ;; *) echo "PRODUCTION_IMAGE_REPOSITORY in .env must point to ghcr.io" >&2; exit 3 ;; esac
services="identity-service coaching-service speed-reading-service notification-service api-gateway speed-reading-frontend admin-panel staff-portal"
# shellcheck disable=SC2086
RELEASE_TAG="$tag" docker compose --env-file .env -p eduivme-production -f docker-compose.yml -f docker-compose.production.yml -f docker-compose.production.litespeed.yml pull --quiet $services
status=0
for name in $services; do
  image="$repo/$name:$tag"
  if ! revision=$(docker image inspect "$image" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' 2>/dev/null); then
    echo "MISSING $image" >&2; status=1; continue
  fi
  case "$revision" in "$tag"*) ;; *) echo "WRONG COMMIT $image ($revision)" >&2; status=1; continue ;; esac
  echo "OK $image $(docker image inspect "$image" --format '{{.Id}}' | cut -c8-19) $revision"
done
[ "$status" -eq 0 ] && echo "PASS: all 8 images for $tag are present" || echo "FAIL: do not switch containers" >&2
exit "$status"
