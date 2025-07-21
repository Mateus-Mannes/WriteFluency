#!/bin/bash

set -e

KUBE_CONTEXT=$1

./generate-k8s-secret.sh

kubectl apply -f https://github.com/cert-manager/cert-manager/releases/latest/download/cert-manager.yaml

aspirate apply -i ./aspirate-overlays --non-interactive --kube-context "$KUBE_CONTEXT"