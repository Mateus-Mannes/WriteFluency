#!/bin/bash

retry_kubectl() {
  local max_attempts=5
  local attempt=1
  local delay=2

  while true; do
    if kubectl --request-timeout=60s "$@"; then
      return 0
    fi

    if [ "$attempt" -ge "$max_attempts" ]; then
      echo "kubectl command failed after ${max_attempts} attempts: kubectl $*" >&2
      return 1
    fi

    echo "kubectl command failed (attempt ${attempt}/${max_attempts}). Retrying in ${delay}s..." >&2
    sleep "$delay"
    attempt=$((attempt + 1))
    delay=$((delay * 2))
  done
}

apply_stdin_with_retry() {
  local tmp_file
  tmp_file="$(mktemp)"
  cat > "$tmp_file"
  retry_kubectl apply --validate=false -f "$tmp_file"
  rm -f "$tmp_file"
}
