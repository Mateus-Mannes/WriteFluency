#!/bin/bash

# Script for applying the Kubernetes cluster secrets based on .NET user secrets
# or GitHub Actions secrets (with real variable names like Authentication__Google__ClientId)

kubectl apply -f aspirate-output/namespace.yaml

if [ "$GITHUB_ACTIONS" = "true" ]; then
  echo "🔐 Running in GitHub Actions — using environment variables from secrets"

  jwt_key="${Jwt__Key}"
  google_client_id="${Authentication__Google__ClientId}"
  google_client_secret="${Authentication__Google__ClientSecret}"
  openai_key="${ExternalApis__OpenAI__Key}"
  tts_key="${ExternalApis__TextToSpeech__Key}"
  news_key="${ExternalApis__News__Key}"
  app_insights_connection_string="${APPLICATIONINSIGHTS_CONNECTION_STRING}"
  postgres_password="${POSTGRES_PASSWORD}"
  minio_password="${MINIO_ROOT_PASSWORD}"
  cloud_flare_token="${CLOUDFLARE_API_TOKEN}"
  propositions_daily_requests_limit="${Propositions__DailyRequestsLimit}"
  propositions_limit_per_topic="${Propositions__LimitPerTopic}"
  propositions_news_request_limit="${Propositions__NewsRequestLimit}"

  # Create image pull secret for GHCR
  echo "🔑 Creating GHCR image pull secret..."
  kubectl create secret docker-registry ghcr-secret \
    --docker-server=ghcr.io \
    --docker-username="${GHCR_USERNAME}" \
    --docker-password="${GHCR_TOKEN}" \
    --namespace=writefluency \
    --dry-run=client -o yaml | kubectl apply -f -

else
  echo "🔐 Running locally — using .NET user secrets"

  USER_SECRETS=~/.microsoft/usersecrets/58584eae-ca83-4318-9cb9-76ac1239e00a/secrets.json

  jwt_key=$(jq -r '.["Jwt:Key"]' "$USER_SECRETS")
  google_client_id=$(jq -r '.["Authentication:Google:ClientId"]' "$USER_SECRETS")
  google_client_secret=$(jq -r '.["Authentication:Google:ClientSecret"]' "$USER_SECRETS")
  openai_key=$(jq -r '.["ExternalApis:OpenAI:Key"]' "$USER_SECRETS")
  tts_key=$(jq -r '.["ExternalApis:TextToSpeech:Key"]' "$USER_SECRETS")
  news_key=$(jq -r '.["ExternalApis:News:Key"]' "$USER_SECRETS")
  app_insights_connection_string=$(jq -r '.["APPLICATIONINSIGHTS_CONNECTION_STRING"]' "$USER_SECRETS")
  postgres_password="postgres"
  minio_password="admin123"
  propositions_daily_requests_limit=$(jq -r '.["Propositions:DailyRequestsLimit"]' "$USER_SECRETS")
  propositions_limit_per_topic=$(jq -r '.["Propositions:LimitPerTopic"]' "$USER_SECRETS")
  propositions_news_request_limit=$(jq -r '.["Propositions:NewsRequestLimit"]' "$USER_SECRETS")
fi

kubectl apply -f - <<EOF
apiVersion: v1
kind: Secret
metadata:
  name: wf-app-secrets
  namespace: writefluency
type: Opaque
stringData:
  Jwt__Key: "$jwt_key"
  Authentication__Google__ClientId: "$google_client_id"
  Authentication__Google__ClientSecret: "$google_client_secret"
  ExternalApis__OpenAI__Key: "$openai_key"
  ExternalApis__TextToSpeech__Key: "$tts_key"
  ExternalApis__News__Key: "$news_key"
  MINIO_ROOT_USER: minioadmin
  MINIO_ROOT_PASSWORD: "$minio_password"
  POSTGRES_PASSWORD: "$postgres_password"
  NODE_ENV: production
  ConnectionStrings__wf-postgresdb: Host=wf-postgres;Port=5432;Username=postgres;Password=$postgres_password;Database=wf-postgresdb
  ConnectionStrings__wf-minio: Endpoint=http://wf-minio:9000;AccessKey=minioadmin;SecretKey=$minio_password
  APPLICATIONINSIGHTS_CONNECTION_STRING: "$app_insights_connection_string"
  Propositions__DailyRequestsLimit: "$propositions_daily_requests_limit"
  Propositions__LimitPerTopic: "$propositions_limit_per_topic"
  Propositions__NewsRequestLimit: "$propositions_news_request_limit"
EOF

kubectl apply -f - <<EOF
apiVersion: v1
kind: Secret
metadata:
  name: cloudflare-api-token-secret
  namespace: cert-manager
type: Opaque
stringData:
  api-token: "$cloud_flare_token"
EOF

