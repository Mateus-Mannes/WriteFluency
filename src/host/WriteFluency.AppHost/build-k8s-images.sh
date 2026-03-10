#!/bin/bash

set -euo pipefail

TARGET=${1:-}
TAG=${2:-}

if [ -z "$TARGET" ] || [ -z "$TAG" ]; then
  echo "Usage: ./build-k8s-images.sh <users|propositions> <tag>"
  exit 1
fi

if [[ "$TARGET" != "users" && "$TARGET" != "propositions" ]]; then
  echo "Unsupported target '$TARGET'. Allowed values: users, propositions"
  exit 1
fi

case "$TARGET" in
  users)
    KUSTOMIZATION_FILE="./aspirate-overlays-users/kustomization.yaml"
    ;;
  propositions)
    KUSTOMIZATION_FILE="./aspirate-overlays-propositions/kustomization.yaml"
    ;;
esac
if [ ! -f "$KUSTOMIZATION_FILE" ]; then
  echo "Kustomization file not found: $KUSTOMIZATION_FILE"
  exit 1
fi

echo "Setting image tags for target '${TARGET}' to '${TAG}' in ${KUSTOMIZATION_FILE}"
if [[ "$OSTYPE" == "darwin"* ]]; then
  sed -E -i '' "s/(newTag:[[:space:]]*).*/\\1${TAG}/g" "$KUSTOMIZATION_FILE"
else
  sed -E -i "s/(newTag:[[:space:]]*).*/\\1${TAG}/g" "$KUSTOMIZATION_FILE"
fi

echo "Building and pushing images via Aspirate using manifest.json"
aspirate build -ct "$TAG" --non-interactive -m ./manifest.json

echo "Build completed for target '${TARGET}' with tag '${TAG}'"
