# WriteFluency
WriteFluency is an interactive web application designed to help users practice and improve their English writing skills.

# About the code
This repo includes:
- WriteFluencyApp: a simple angular app with the user interface
- WriteFluencyApi: a simple .net9 api

# Features
## Listen And Write

Listen and write is a route of the site where the user can generate an audio, based on a complexity level and subject he chose, and then verify it. the generation of the audio runs at the api, it does a request to chat gpt api to generate a text based on the user input, then an audio is generated due a request to speech-to-text google cloud api. The verification of the text is also made on the api, using an algorithm that combine Needleman Wunsch Alignment with Levenshtein Distance to compare the user text with the original text. 

# API/SERVER

## Command to create migrations (relative path to WriteFluencyApi directory):

```bash
dotnet ef migrations add add_app_settings -p ./src/propositions-service/WriteFluency.Infrastructure/WriteFluency.Infrastructure.csproj -s ./src/propositions-service/WriteFluency.WebApi/WriteFluency.WebApi.csproj 

docker-compose -f docker-compose.services.yml up
```

run db migrator to update the database


Create github secret:

```bash

first ensure kubernetes.io/service-account.name: github is created with admin role

cat <<'YAML' | kubectl apply -f -
apiVersion: v1
kind: Secret
metadata:
  name: github-token
  namespace: default
  annotations:
    kubernetes.io/service-account.name: github
type: kubernetes.io/service-account-token
YAML


kubectl -n default get secret github-token -o jsonpath='{.data.token}' | base64 -d
echo


kubectl -n default get secret github-token -o jsonpath='{.data.ca\.crt}' | base64 -w 0
echo

export KUBE_CA_BASE64="COLE_AQUI_O_KUBE_CA"
export KUBE_TOKEN="COLE_AQUI_O_KUBE_TOKEN"

echo "$KUBE_CA_BASE64" | base64 -d > /tmp/ca.crt


curl --cacert /tmp/ca.crt \
  -H "Authorization: Bearer $KUBE_TOKEN" \
  https://168.231.100.69:6443/api | head


```

Redirect to ports:

``` bash

# HTTP 80 -> 32379
sudo iptables -t nat -A PREROUTING -p tcp --dport 80  -j REDIRECT --to-ports 32379

# HTTPS 443 -> 30524
sudo iptables -t nat -A PREROUTING -p tcp --dport 443 -j REDIRECT --to-ports 30524


```