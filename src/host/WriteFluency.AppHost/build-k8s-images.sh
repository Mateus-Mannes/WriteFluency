#!/bin/bash

set -euo pipefail

TARGET=${1:-}
TAG=${2:-}
MANIFEST_FILE="./manifest.json"

usage() {
  echo "Usage: ./build-k8s-images.sh <users|propositions> <tag>"
}

require_command() {
  local cmd="$1"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "'$cmd' is required but not installed."
    exit 1
  fi
}

update_overlay_tag() {
  local file="$1"
  local tag="$2"

  echo "Setting image tags to '${tag}' in ${file}"
  if [[ "$OSTYPE" == "darwin"* ]]; then
    sed -E -i '' "s/(newTag:[[:space:]]*).*/\\1${tag}/g" "$file"
  else
    sed -E -i "s/(newTag:[[:space:]]*).*/\\1${tag}/g" "$file"
  fi
}

build_filtered_manifest() {
  local prefix="$1"
  local output_file="$2"

  local jq_filter
  jq_filter=$(cat <<'JQ'
    .resources as $all
    | .resources = (
        $all
        | to_entries
        | map(
            select(
              (.key | startswith($prefix))
              or (.key | startswith("wf-infra-"))
              or (.value.type | startswith("parameter."))
              or (
                (.value.type | startswith("value."))
                and (.key | startswith($prefix))
              )
            )
          )
        | from_entries
      )
JQ
  )

  jq --arg prefix "$prefix" "$jq_filter" "$MANIFEST_FILE" > "$output_file"
}

if [[ -z "$TARGET" || -z "$TAG" ]]; then
  usage
  exit 1
fi

case "$TARGET" in
  users)
    KUSTOMIZATION_FILE="./aspirate-overlays-users/kustomization.yaml"
    MANIFEST_PREFIX="wf-users-"
    ;;
  propositions)
    KUSTOMIZATION_FILE="./aspirate-overlays-propositions/kustomization.yaml"
    MANIFEST_PREFIX="wf-propositions-"
    ;;
  *)
    echo "Unsupported target '$TARGET'. Allowed values: users, propositions"
    exit 1
    ;;
esac

if [[ ! -f "$KUSTOMIZATION_FILE" ]]; then
  echo "Kustomization file not found: $KUSTOMIZATION_FILE"
  exit 1
fi

require_command jq
update_overlay_tag "$KUSTOMIZATION_FILE" "$TAG"

FILTERED_MANIFEST=$(mktemp "${TMPDIR:-/tmp}/aspirate-manifest-${TARGET}.XXXXXX.json")
trap 'rm -f "$FILTERED_MANIFEST"' EXIT

echo "Creating filtered manifest for target '${TARGET}' with prefix '${MANIFEST_PREFIX}'"
build_filtered_manifest "$MANIFEST_PREFIX" "$FILTERED_MANIFEST"

echo "Building and pushing images via Aspirate using filtered manifest: $FILTERED_MANIFEST"
aspirate build -ct "$TAG" --non-interactive -m "$FILTERED_MANIFEST"

echo "Build completed for target '${TARGET}' with tag '${TAG}'"
