---
name: writefluency-deploy
description: WriteFluency production deployment workflow for committing staged changes, pushing master, creating service version tags, and pushing those tags to trigger GitHub Actions deploys. Use when the user asks to deploy, push to production/prd, trigger production deploys, release staged changes, or create/push deploy tags for WriteFluency services.
---

# WriteFluency Deploy

## Workflow

Use this workflow for production deploys from `/Users/mateusmedeiros/Desktop/repos/WriteFluency`.

1. Inspect the staged set and branch:

```bash
git status --short --branch
git diff --cached --stat
git diff --cached --name-only
```

Commit only staged changes unless the user explicitly asks to stage more. If there are no staged changes, stop and tell the user.

2. Identify deploy targets from staged paths:

- `src/propositions-service/**` or `tests/propositions-service/**` -> `propositions-v*`
- `src/webapp/**` -> `webapp-v*`
- `src/users-service/**` or `tests/users-service/**` -> `users-v*`
- `src/users-progress-service/**`, `tests/users-progress-service/**`, or `infra/users-progress/**` -> check workflows before tagging; current workflows use `users-v*` for `deploy-users-progress.yml`, despite historical `users-progress-v*` tags.
- `.github/workflows/deploy-*.yml`, `src/host/WriteFluency.AppHost/deploy-k8s.sh`, or `src/host/WriteFluency.AppHost/aspirate-overlays-*` -> inspect workflows and ask if service tags are not obvious.

3. Verify tag triggers before creating tags:

```bash
rg -n "tags:|propositions-v|webapp-v|users-v|users-progress-v" .github/workflows
git tag --sort=-creatordate | head -n 30
```

Choose the next patch version for each affected service. Examples:

- latest `propositions-v1.0.8` -> next `propositions-v1.0.9`
- latest `webapp-v1.0.9` -> next `webapp-v1.0.10`
- latest `users-v1.1.16` -> next `users-v1.1.17`

4. Commit staged changes with a concise message:

```bash
git commit -m "<message>"
```

5. Push `master` before tags:

```bash
git push origin master
```

6. Create tags on the pushed commit and push them:

```bash
git tag <service-vX.Y.Z> HEAD
git push origin <service-vX.Y.Z> [<another-service-vX.Y.Z>...]
```

7. Confirm final state:

```bash
git status --short --branch
git log --oneline -n 3 --decorate
git ls-remote --tags origin <tag-1> <tag-2>
```

## Guardrails

- Do not rewrite, delete, or move existing tags.
- Do not run `git reset`, `git checkout --`, or otherwise discard work.
- Do not commit unstaged changes unless the user explicitly asks.
- If staged changes span multiple services, create one tag per affected service after pushing the commit.
- If the branch is not `master`, ask before proceeding unless the user explicitly requested the current branch.
- If `origin/master` has new commits, pull/rebase only after telling the user what will happen.
- If tests were not already run in the session, run focused tests when the staged changes include code, unless the user asked for a fast deploy.

## Known Tags

As of the current repo workflow, production deploys are triggered by:

- `.github/workflows/deploy-propositions.yml`: `propositions-v*`
- `.github/workflows/deploy-webapp.yml`: `webapp-v*`
- `.github/workflows/deploy-users.yml`: `users-v*`
- `.github/workflows/deploy-users-progress.yml`: `users-v*`
