FROM alpine:3.20

RUN apk add --no-cache ca-certificates curl \
  && curl -sSL https://dl.min.io/client/mc/release/linux-amd64/mc -o /usr/local/bin/mc \
  && chmod +x /usr/local/bin/mc

COPY init-buckets.sh /init/init-buckets.sh

ENTRYPOINT ["sh", "/init/init-buckets.sh"]