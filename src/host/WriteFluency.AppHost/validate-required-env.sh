#!/bin/bash

set -euo pipefail

if [ "$#" -eq 0 ]; then
  echo "Usage: ./validate-required-env.sh VAR_NAME [VAR_NAME ...]"
  exit 1
fi

missing=()

for var_name in "$@"; do
  value="${!var_name-}"
  trimmed="${value//[[:space:]]/}"

  if [ -z "$trimmed" ]; then
    missing+=("$var_name")
  fi
done

if [ "${#missing[@]}" -gt 0 ]; then
  echo "Missing required environment variables:"
  for var_name in "${missing[@]}"; do
    echo "  - $var_name"
  done
  exit 1
fi

echo "Required environment variables are set."
