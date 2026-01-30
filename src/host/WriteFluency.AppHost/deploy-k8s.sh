#!/bin/bash

set -e

KUBE_CONTEXT=$1

# Create cert-manager namespace first (required before applying cert-manager resources)
kubectl create namespace cert-manager --dry-run=client -o yaml | kubectl apply -f -

./generate-k8s-secret.sh

kubectl apply -f https://github.com/cert-manager/cert-manager/releases/latest/download/cert-manager.yaml

echo "⏳ Waiting for cert-manager to be ready..."
kubectl rollout status deployment cert-manager -n cert-manager --timeout=120s
kubectl rollout status deployment cert-manager-webhook -n cert-manager --timeout=120s
kubectl rollout status deployment cert-manager-cainjector -n cert-manager --timeout=120s


aspirate apply -i ./aspirate-overlays --non-interactive --kube-context "$KUBE_CONTEXT"