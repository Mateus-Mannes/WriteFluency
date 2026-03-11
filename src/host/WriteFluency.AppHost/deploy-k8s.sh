#!/bin/bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/kubectl-utils.sh"

TARGET=${1:-}
KUBE_CONTEXT=${2:-}
CERT_MANAGER_VERSION="v1.19.4"

if [ -z "$TARGET" ] || [ -z "$KUBE_CONTEXT" ]; then
  echo "Usage: ./deploy-k8s.sh <infra|users|propositions|webapp> <kube-context>"
  exit 1
fi

if [[ "$TARGET" != "infra" && "$TARGET" != "users" && "$TARGET" != "propositions" && "$TARGET" != "webapp" ]]; then
  echo "Unsupported target '$TARGET'. Allowed values: infra, users, propositions, webapp"
  exit 1
fi

case "$TARGET" in
  infra)
    OVERLAY_PATH="./aspirate-overlays-infra"
    ;;
  users)
    OVERLAY_PATH="./aspirate-overlays-users"
    ;;
  propositions)
    OVERLAY_PATH="./aspirate-overlays-propositions"
    ;;
  webapp)
    OVERLAY_PATH="./aspirate-overlays-webapp"
    ;;
esac
if [ ! -d "$OVERLAY_PATH" ]; then
  echo "Overlay path not found: $OVERLAY_PATH"
  exit 1
fi

if [[ "$TARGET" == "infra" ]]; then
  kubectl create namespace cert-manager --dry-run=client -o yaml | apply_stdin_with_retry
  ./generate-k8s-secret.sh infra

  retry_kubectl apply --validate=false -f "https://github.com/cert-manager/cert-manager/releases/download/${CERT_MANAGER_VERSION}/cert-manager.yaml"

  echo "Waiting for cert-manager deployments"
  kubectl rollout status deployment cert-manager -n cert-manager --timeout=180s
  kubectl rollout status deployment cert-manager-webhook -n cert-manager --timeout=180s
  kubectl rollout status deployment cert-manager-cainjector -n cert-manager --timeout=180s
else
  if [[ "$TARGET" == "users" || "$TARGET" == "propositions" ]]; then
    ./generate-k8s-secret.sh "$TARGET"
  fi
fi

aspirate apply -i "$OVERLAY_PATH" --non-interactive --kube-context "$KUBE_CONTEXT"

if [[ "$TARGET" == "users" ]]; then
  kubectl rollout status deployment wf-users-api -n writefluency --timeout=180s
fi

if [[ "$TARGET" == "propositions" ]]; then
  kubectl rollout status deployment wf-propositions-api -n writefluency --timeout=180s
  kubectl rollout status deployment wf-propositions-news-worker -n writefluency --timeout=180s
fi

if [[ "$TARGET" == "webapp" ]]; then
  kubectl rollout status deployment wf-webapp -n writefluency --timeout=180s
fi

echo "Deployment completed for target '$TARGET'"
