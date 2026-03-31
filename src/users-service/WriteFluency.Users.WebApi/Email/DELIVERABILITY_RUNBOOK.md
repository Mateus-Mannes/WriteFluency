# VPS SMTP Deliverability Runbook (Transactional Auth Email)

This runbook covers WriteFluency account confirmation and OTP emails sent from a self-hosted SMTP relay.

## 1. Sender Identity Checklist

Use one public mail identity:

- SMTP hostname (HELO/EHLO): `mail.writefluency.com`
- Header `From`: `noreply@writefluency.com`
- Envelope sender (`MAIL FROM`): `noreply@writefluency.com`
- Message-ID domain: `writefluency.com`

DNS and network:

- `A` record for `mail.writefluency.com` to VPS public IP
- `PTR` (reverse DNS) for VPS IP back to `mail.writefluency.com`
- Forward/reverse match must stay consistent

## 2. Authentication Records

SPF example (`writefluency.com`):

```txt
v=spf1 ip4:<YOUR_VPS_IP> -all
```

DKIM:

- Generate 2048-bit key (selector `wf1`)
- Publish TXT record for `wf1._domainkey.writefluency.com`
- Ensure outgoing emails are DKIM-signed

DKIM setup model used by this repository:

- DNS is configured manually in Cloudflare.
- Private key is stored manually in GitHub secret `DKIM_PRIVATE_KEY_PEM`.
- Infra pipeline syncs that secret into Kubernetes secret `wf-infra-smtp-dkim`.
- SMTP pod mounts key files at `/etc/opendkim/keys` and uses:
  - `DKIM_DOMAIN=writefluency.com`
  - `DKIM_SELECTOR=wf1`

Manual one-time setup:

1. Generate DKIM key pair (on a secure machine/VPS):

```bash
mkdir -p ~/dkim && cd ~/dkim
openssl genrsa -out writefluency.com.private 2048
openssl rsa -in writefluency.com.private -pubout -out writefluency.com.public.pem
```

2. Build DNS TXT content from public key:

```bash
PUB=$(awk 'BEGIN{capture=0} /BEGIN PUBLIC KEY/{capture=1;next} /END PUBLIC KEY/{capture=0} capture {gsub(/\r/, ""); printf "%s", $0}' writefluency.com.public.pem)
echo "v=DKIM1; k=rsa; p=${PUB}"
```

3. Create Cloudflare DKIM TXT:
- Type: `TXT`
- Name: `wf1._domainkey`
- Content: output from step 2
- TTL: `Auto` (or `3600`)
- Proxy: `DNS only`

4. Save private key into GitHub secret `DKIM_PRIVATE_KEY_PEM`:
- Include full PEM text:
  - `-----BEGIN PRIVATE KEY-----` or `-----BEGIN RSA PRIVATE KEY-----`
  - key body
  - `-----END ...-----`

5. Run `Deploy Infra` workflow:
- Pipeline reads `DKIM_PRIVATE_KEY_PEM`.
- Pipeline creates/updates k8s secret `wf-infra-smtp-dkim`.
- SMTP deployment mounts key at `/etc/opendkim/keys/writefluency.com.private`.

6. Verify:
- `dig TXT wf1._domainkey.writefluency.com`
- Send a test email to Gmail and verify `DKIM=PASS` in message headers.

DMARC rollout (`_dmarc.writefluency.com`):

Phase 1 (monitoring):

```txt
v=DMARC1; p=none; adkim=s; aspf=s; rua=mailto:dmarc@writefluency.com; fo=1
```

Phase 2 (partial enforcement):

```txt
v=DMARC1; p=quarantine; pct=50; adkim=s; aspf=s; rua=mailto:dmarc@writefluency.com; fo=1
```

Phase 3 (full enforcement):

```txt
v=DMARC1; p=reject; adkim=s; aspf=s; rua=mailto:dmarc@writefluency.com; fo=1
```

## 3. Monitoring Metrics (Alerts Only)

These are health indicators, not hard blocks.

- Complaint rate = `spam_complaints / delivered_emails * 100`
- Hard-bounce rate = `permanent_bounces / sent_emails * 100`

Alert thresholds:

- Complaint rate `> 0.3%` => inbox reputation risk
- Hard-bounce rate `> 2.0%` => recipient quality/configuration risk

## 4. Weekly Review Playbook

1. Validate SPF, DKIM, DMARC alignment for recent samples.
2. Check complaint and bounce trends in mailbox-provider dashboards.
3. Review SMTP failures by category (`temporary`, `permanent`, `unknown`) from application logs/metrics.
4. If thresholds are exceeded:
   - Pause traffic spikes.
   - Inspect recent recipient cohorts and failures.
   - Re-check DNS auth and PTR.
   - Remove invalid recipients from retry flows.

## 5. Scope Guardrails

- These emails are transactional (OTP and account confirmation).
- Do not add `List-Unsubscribe` headers to OTP/confirmation emails.
- If marketing emails are introduced later, split streams and implement one-click unsubscribe for that stream only.
