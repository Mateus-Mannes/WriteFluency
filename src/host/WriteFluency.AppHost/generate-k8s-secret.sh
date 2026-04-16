#!/bin/bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/kubectl-utils.sh"

TARGET=${1:-}
if [ -z "$TARGET" ]; then
  echo "Usage: ./generate-k8s-secret.sh <infra|users|propositions>"
  exit 1
fi

if [[ "$TARGET" != "infra" && "$TARGET" != "users" && "$TARGET" != "propositions" ]]; then
  echo "Unsupported target '$TARGET'. Allowed values: infra, users, propositions"
  exit 1
fi

retry_kubectl apply --validate=false -f aspirate-output/namespace.yaml

# CI/environment-driven only. Local user-secrets fallback removed by request.
jwt_key="${Jwt__Key:-}"
google_client_id="${Authentication__Google__ClientId:-}"
google_client_secret="${Authentication__Google__ClientSecret:-}"
microsoft_client_id="${Authentication__Microsoft__ClientId:-}"
microsoft_client_secret="${Authentication__Microsoft__ClientSecret:-}"
external_redirect_allowed_return_url_0="${Authentication__ExternalRedirect__AllowedReturnUrls__0:-}"
shared_auth_cookie_scheme="${SharedAuthCookie__Scheme:-Identity.Application}"
shared_auth_cookie_name="${SharedAuthCookie__CookieName:-.AspNetCore.Identity.Application}"
shared_auth_cookie_domain="${SharedAuthCookie__CookieDomain:-.writefluency.com}"
shared_data_protection_application_name="${SharedDataProtection__ApplicationName:-WriteFluency.SharedAuth}"
shared_data_protection_blob_uri="${SharedDataProtection__BlobUri:-}"
shared_data_protection_key_identifier="${SharedDataProtection__KeyIdentifier:-}"

azure_client_id="${AZURE_CLIENT_ID:-}"
azure_tenant_id="${AZURE_TENANT_ID:-}"
azure_client_secret="${AZURE_CLIENT_SECRET:-}"

openai_key="${ExternalApis__OpenAI__Key:-}"
tts_key="${ExternalApis__TextToSpeech__Key:-}"
news_key="${ExternalApis__News__Key:-}"

app_insights_connection_string="${APPLICATIONINSIGHTS_CONNECTION_STRING:-}"
postgres_password="${POSTGRES_PASSWORD:-}"
minio_password="${MINIO_ROOT_PASSWORD:-}"
redis_password="${REDIS_PASSWORD:-}"
cloud_flare_token="${CLOUDFLARE_API_TOKEN:-}"
cloud_flare_cache_token="${CLOUDFLARE_API_TOKEN_CACHE:-${CLOUDFLARE_API_TOKEN:-}}"
dkim_private_key_pem="${DKIM_PRIVATE_KEY_PEM:-}"
dkim_domain="${DKIM_DOMAIN:-writefluency.com}"

propositions_daily_requests_limit="${Propositions__DailyRequestsLimit:-}"
propositions_limit_per_topic="${Propositions__LimitPerTopic:-}"
propositions_news_request_limit="${Propositions__NewsRequestLimit:-}"

ghcr_username="${GHCR_USERNAME:-}"
ghcr_token="${GHCR_TOKEN:-}"

if [ "$TARGET" = "infra" ]; then
  apply_stdin_with_retry <<EOF2
apiVersion: v1
kind: Secret
metadata:
  name: wf-infra-secrets
  namespace: writefluency
type: Opaque
stringData:
  POSTGRES_PASSWORD: "$postgres_password"
  REDIS_PASSWORD: "$redis_password"
EOF2

  if [ -n "$ghcr_username" ] && [ -n "$ghcr_token" ]; then
    kubectl create secret docker-registry ghcr-secret \
      --docker-server=ghcr.io \
      --docker-username="${ghcr_username}" \
      --docker-password="${ghcr_token}" \
      --namespace=writefluency \
      --dry-run=client -o yaml | apply_stdin_with_retry
  else
    echo "Skipping ghcr-secret creation because GHCR credentials are not available."
  fi

  if [ -n "$cloud_flare_token" ]; then
    kubectl create namespace cert-manager --dry-run=client -o yaml | apply_stdin_with_retry
    apply_stdin_with_retry <<EOF2
apiVersion: v1
kind: Secret
metadata:
  name: cloudflare-api-token-secret
  namespace: cert-manager
type: Opaque
stringData:
  api-token: "$cloud_flare_token"
EOF2
  else
    echo "Skipping cloudflare-api-token-secret because CLOUDFLARE_API_TOKEN is not available."
  fi

  if [ -z "${dkim_private_key_pem//[[:space:]]/}" ]; then
    echo "Missing DKIM_PRIVATE_KEY_PEM. Set it in CI secrets before infra deploy." >&2
    exit 1
  fi

  dkim_tmp_dir="$(mktemp -d)"
  dkim_private_key_path="${dkim_tmp_dir}/${dkim_domain}.private"
  printf '%s\n' "$dkim_private_key_pem" > "$dkim_private_key_path"
  chmod 600 "$dkim_private_key_path"

  kubectl create secret generic wf-infra-smtp-dkim \
    --namespace=writefluency \
    --from-file="${dkim_domain}.private=${dkim_private_key_path}" \
    --dry-run=client -o yaml | apply_stdin_with_retry

  rm -rf "$dkim_tmp_dir"

  exit 0
fi

if [ "$TARGET" = "propositions" ]; then
  apply_stdin_with_retry <<EOF2
apiVersion: v1
kind: Secret
metadata:
  name: wf-propositions-secrets
  namespace: writefluency
type: Opaque
stringData:
  Jwt__Key: "$jwt_key"
  Authentication__Google__ClientId: "$google_client_id"
  Authentication__Google__ClientSecret: "$google_client_secret"
  ExternalApis__OpenAI__Key: "$openai_key"
  ExternalApis__TextToSpeech__Key: "$tts_key"
  ExternalApis__News__Key: "$news_key"
  ExternalApis__Cloudflare__ApiToken: "$cloud_flare_cache_token"
  MINIO_ROOT_USER: minioadmin
  MINIO_ROOT_PASSWORD: "$minio_password"
  POSTGRES_PASSWORD: "$postgres_password"
  NODE_ENV: production
  ConnectionStrings__wf-propositions-postgresdb: Host=wf-infra-postgres;Port=5432;Username=postgres;Password=$postgres_password;Database=wf-propositions-postgresdb
  ConnectionStrings__wf-infra-minio: Endpoint=http://wf-infra-minio:9000;AccessKey=minioadmin;SecretKey=$minio_password
  APPLICATIONINSIGHTS_CONNECTION_STRING: "$app_insights_connection_string"
  Propositions__DailyRequestsLimit: "$propositions_daily_requests_limit"
  Propositions__LimitPerTopic: "$propositions_limit_per_topic"
  Propositions__NewsRequestLimit: "$propositions_news_request_limit"
EOF2
  exit 0
fi

if [ "$TARGET" = "users" ]; then
  apply_stdin_with_retry <<EOF2
apiVersion: v1
kind: Secret
metadata:
  name: wf-users-secrets
  namespace: writefluency
type: Opaque
stringData:
  Jwt__Key: "$jwt_key"
  Authentication__Google__ClientId: "$google_client_id"
  Authentication__Google__ClientSecret: "$google_client_secret"
  Authentication__Microsoft__ClientId: "$microsoft_client_id"
  Authentication__Microsoft__ClientSecret: "$microsoft_client_secret"
  Authentication__ExternalRedirect__AllowedReturnUrls__0: "$external_redirect_allowed_return_url_0"
  Authentication__ExternalRedirect__AllowedReturnUrls__1: "$external_redirect_allowed_return_url_0"
  Authentication__ExternalRedirect__AllowedReturnUrls__2: "$external_redirect_allowed_return_url_0"
  Authentication__ExternalRedirect__AllowedReturnUrls__3: "$external_redirect_allowed_return_url_0"
  Cors__AllowedOrigins__0: "https://writefluency.com"
  Cors__AllowedOrigins__1: "https://writefluency.com"
  Cors__AllowedOrigins__2: "https://writefluency.com"
  AZURE_CLIENT_ID: "$azure_client_id"
  AZURE_TENANT_ID: "$azure_tenant_id"
  AZURE_CLIENT_SECRET: "$azure_client_secret"
  SharedAuthCookie__Scheme: "$shared_auth_cookie_scheme"
  SharedAuthCookie__CookieName: "$shared_auth_cookie_name"
  SharedAuthCookie__CookieDomain: "$shared_auth_cookie_domain"
  SharedDataProtection__ApplicationName: "$shared_data_protection_application_name"
  SharedDataProtection__BlobUri: "$shared_data_protection_blob_uri"
  SharedDataProtection__KeyIdentifier: "$shared_data_protection_key_identifier"
  Smtp__Host: "wf-infra-smtp"
  Smtp__Port: "2525"
  Smtp__FromEmail: "noreply@writefluency.com"
  Smtp__FromName: "WriteFluency"
  Smtp__ReplyToEmail: "support@writefluency.com"
  Smtp__EnvelopeFrom: "noreply@writefluency.com"
  Smtp__MessageIdDomain: "writefluency.com"
  POSTGRES_PASSWORD: "$postgres_password"
  ConnectionStrings__wf-infra-redis: "wf-infra-redis:6379,password=$redis_password"
  ConnectionStrings__wf-users-postgresdb: Host=wf-infra-postgres;Port=5432;Username=postgres;Password=$postgres_password;Database=wf-users-postgresdb
  APPLICATIONINSIGHTS_CONNECTION_STRING: "$app_insights_connection_string"
EOF2
  exit 0
fi
