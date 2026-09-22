#!/bin/sh
set -eu

until mc alias set local "$MINIO_SERVER_URL" "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD"; do
  sleep 2
done

if [ "$ATTACHMENT_MINIO_BUCKET" != "eduplatform-attachments" ]; then
  echo "ATTACHMENT_MINIO_BUCKET must be eduplatform-attachments" >&2
  exit 1
fi

mc mb --ignore-existing "local/$ATTACHMENT_MINIO_BUCKET"
mc admin policy create local coaching-attachments /bootstrap/coaching-attachments-policy.json
mc admin user add local "$ATTACHMENT_MINIO_ACCESS_KEY" "$ATTACHMENT_MINIO_SECRET_KEY"
mc admin policy attach local coaching-attachments --user="$ATTACHMENT_MINIO_ACCESS_KEY"
