#!/bin/bash

set -euo pipefail

TARGET=${1:-}
if [ -z "$TARGET" ]; then
  echo "Usage: ./generate-k8s-secret.sh <infra|users|propositions>"
  exit 1
fi

if [[ "$TARGET" != "infra" && "$TARGET" != "users" && "$TARGET" != "propositions" ]]; then
  echo "Unsupported target '$TARGET'. Allowed values: infra, users, propositions"
  exit 1
fi

kubectl apply -f aspirate-output/namespace.yaml

# CI/environment-driven only. Local user-secrets fallback removed by request.
jwt_key="${Jwt__Key:-}"
google_client_id="${Authentication__Google__ClientId:-}"
google_client_secret="${Authentication__Google__ClientSecret:-}"
microsoft_client_id="${Authentication__Microsoft__ClientId:-}"
microsoft_client_secret="${Authentication__Microsoft__ClientSecret:-}"
apple_client_id="${Authentication__Apple__ClientId:-}"
apple_team_id="${Authentication__Apple__TeamId:-}"
apple_key_id="${Authentication__Apple__KeyId:-}"
apple_private_key="${Authentication__Apple__PrivateKey:-}"
smtp_host="${Smtp__Host:-}"
smtp_port="${Smtp__Port:-}"
smtp_username="${Smtp__Username:-}"
smtp_password="${Smtp__Password:-}"
smtp_from_email="${Smtp__FromEmail:-}"
smtp_from_name="${Smtp__FromName:-}"

openai_key="${ExternalApis__OpenAI__Key:-}"
tts_key="${ExternalApis__TextToSpeech__Key:-}"
news_key="${ExternalApis__News__Key:-}"

app_insights_connection_string="${APPLICATIONINSIGHTS_CONNECTION_STRING:-}"
postgres_password="${POSTGRES_PASSWORD:-}"
minio_password="${MINIO_ROOT_PASSWORD:-}"
cloud_flare_token="${CLOUDFLARE_API_TOKEN:-}"
cloud_flare_cache_token="${CLOUDFLARE_API_TOKEN_CACHE:-${CLOUDFLARE_API_TOKEN:-}}"

propositions_daily_requests_limit="${Propositions__DailyRequestsLimit:-}"
propositions_limit_per_topic="${Propositions__LimitPerTopic:-}"
propositions_news_request_limit="${Propositions__NewsRequestLimit:-}"

ghcr_username="${GHCR_USERNAME:-}"
ghcr_token="${GHCR_TOKEN:-}"

if [ "$TARGET" = "infra" ]; then
  kubectl apply -f - <<EOF2
apiVersion: v1
kind: Secret
metadata:
  name: wf-infra-secrets
  namespace: writefluency
type: Opaque
stringData:
  POSTGRES_PASSWORD: "$postgres_password"
EOF2

  if [ -n "$ghcr_username" ] && [ -n "$ghcr_token" ]; then
    kubectl create secret docker-registry ghcr-secret \
      --docker-server=ghcr.io \
      --docker-username="${ghcr_username}" \
      --docker-password="${ghcr_token}" \
      --namespace=writefluency \
      --dry-run=client -o yaml | kubectl apply -f -
  else
    echo "Skipping ghcr-secret creation because GHCR credentials are not available."
  fi

  if [ -n "$cloud_flare_token" ]; then
    kubectl create namespace cert-manager --dry-run=client -o yaml | kubectl apply -f -
    kubectl apply -f - <<EOF2
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

  exit 0
fi

if [ "$TARGET" = "propositions" ]; then
  kubectl apply -f - <<EOF2
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
  kubectl apply -f - <<EOF2
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
  Authentication__Apple__ClientId: "$apple_client_id"
  Authentication__Apple__TeamId: "$apple_team_id"
  Authentication__Apple__KeyId: "$apple_key_id"
  Authentication__Apple__PrivateKey: "$apple_private_key"
  Smtp__Host: "$smtp_host"
  Smtp__Port: "$smtp_port"
  Smtp__Username: "$smtp_username"
  Smtp__Password: "$smtp_password"
  Smtp__FromEmail: "$smtp_from_email"
  Smtp__FromName: "$smtp_from_name"
  POSTGRES_PASSWORD: "$postgres_password"
  ConnectionStrings__wf-users-postgresdb: Host=wf-infra-postgres;Port=5432;Username=postgres;Password=$postgres_password;Database=wf-users-postgresdb
  APPLICATIONINSIGHTS_CONNECTION_STRING: "$app_insights_connection_string"
EOF2
  exit 0
fi
