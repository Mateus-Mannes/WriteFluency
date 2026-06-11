# Exercise Transfer

Copies a complete production exercise into the local Aspire environment:

- Proposition and owned news metadata from PostgreSQL
- Audio from the `propositions` MinIO bucket
- Image from the `images` MinIO bucket, when present

The production source is read-only. The tool reads connection details from the
`wf-propositions-secrets` Kubernetes secret and creates temporary port-forwards.

## Prerequisites

1. Start the local Aspire AppHost.
2. Install `kubectl`.
3. Configure a Kubernetes context with permission to:
   - Read `wf-propositions-secrets` in the `writefluency` namespace
   - Port-forward `wf-infra-postgres` and `wf-infra-minio`

List available contexts:

```bash
kubectl config get-contexts
```

## Usage

```bash
dotnet run \
  --project src/tools/WriteFluency.ExerciseTransfer \
  -- \
  --id 2708 \
  --context <production-context>
```

The production ID is preserved so the same exercise URL works locally:

```text
http://localhost:4200/english-writing-exercise/2708
```

If that exercise already exists locally:

```bash
dotnet run \
  --project src/tools/WriteFluency.ExerciseTransfer \
  -- \
  --id 2708 \
  --context <production-context> \
  --replace
```

Run with `--help` to see configuration overrides. Local defaults match the
AppHost PostgreSQL and MinIO ports and credentials.

The tool refuses non-local destinations unless
`--allow-non-local-destination` is explicitly provided.
