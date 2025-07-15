#!/bin/bash

set -e

KUBE_CONTEXT=$1

./generate-k8s-secret.sh

aspirate apply -i ./aspirate-overlays --non-interactive --kube-context "$KUBE_CONTEXT"