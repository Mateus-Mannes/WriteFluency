#!/bin/bash

# Script for applying the Kubernetes cluster secrets based on .NET user secrets
# Used for testing the deployment on the local cluster only

kubectl apply -f aspirate-output/namespace.yaml

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
  Propositions__DailyRequestsLimit: "5"
  Propositions__LimitPerTopic: "3000"
  MINIO_ROOT_PASSWORD: "$minio_password"
  POSTGRES_PASSWORD: "$postgres_password"
  NODE_ENV: production
  ConnectionStrings__wf-postgresdb: Host=wf-postgres;Port=5432;Username=postgres;Password=$postgres_password;Database=wf-postgresdb
  ConnectionStrings__wf-minio: Endpoint=http://wf-minio:9000;AccessKey=minioadmin;SecretKey=$minio_password
  APPLICATIONINSIGHTS_CONNECTION_STRING: "$app_insights_connection_string"
EOF