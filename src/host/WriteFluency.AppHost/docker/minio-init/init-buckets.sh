#!/usr/bin/env sh
set -e

echo "Waiting for MinIO..."
until mc alias set local http://wf-minio:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD"; do
  sleep 1
done

echo "Ensuring bucket exists..."
mc mb -p local/images || true
mc mb -p local/propositions || true

echo "Setting anonymous download policy..."
mc anonymous set download local/images
mc anonymous set download local/propositions

echo "Done. Current anonymous policies:"
mc anonymous list local/images
mc anonymous list local/propositions