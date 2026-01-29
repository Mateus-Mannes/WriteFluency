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