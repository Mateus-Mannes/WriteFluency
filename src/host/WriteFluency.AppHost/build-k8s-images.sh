#!/bin/bash

set -e

TAG=$1
KUSTOMIZATION_FILE="./aspirate-overlays/kustomization.yaml"

if [ -z "$TAG" ]; then
  echo "❌ Please provide the new image tag. Example: ./build-k8s-images.sh 0.0.4"
  exit 1
fi

echo "🔧 Replacing 'replace_with_tag' with '$TAG' in $KUSTOMIZATION_FILE"

# Choose sed syntax based on OS
if [[ "$OSTYPE" == "darwin"* ]]; then
  # macOS
  sed -i '' "s/replace_with_tag/$TAG/g" "$KUSTOMIZATION_FILE"
else
  # Linux
  sed -i "s/replace_with_tag/$TAG/g" "$KUSTOMIZATION_FILE"
fi

echo "🏗️  Running aspirate build with tag: $TAG"
aspirate build -ct "$TAG" --non-interactive -m ./manifest.json

echo "✅ Build completed successfully with tag: $TAG"
