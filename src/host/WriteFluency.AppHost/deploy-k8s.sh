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

# Apply CoreDNS rewrite configuration to resolve external API URL to internal service
# This prevents duplicate HTTP requests in Angular SSR by ensuring both SSR and browser use the same URL
echo "🔧 Applying CoreDNS rewrite configuration..."
kubectl apply -f ./aspirate-overlays/coredns-rewrite.yaml

echo "🔄 Restarting CoreDNS to apply configuration..."
kubectl -n kube-system rollout restart deployment coredns
kubectl -n kube-system rollout status deployment coredns --timeout=60s

echo "✅ CoreDNS configuration applied successfully"

aspirate apply -i ./aspirate-overlays --non-interactive --kube-context "$KUBE_CONTEXT"