# Catalog Access Teaser And Full-Catalog Funnel

## Summary

The catalog access teaser lets free users experience one restricted full-catalog exercise before subscribing, while keeping the latest 18 exercises free for everyone.

Current catalog access is based on the global latest 18 exercises. Exercises outside that window require Pro. This flow adds an aggressive funnel for restricted exercises:

- Latest 18 exercises remain free and behave as they do today.
- Pro users keep full catalog access and can still receive audio before clicking Begin.
- Anonymous users can preview and start one restricted full-catalog exercise.
- Anonymous users who already used that sample see a login CTA before accessing more restricted exercises.
- Logged-in free users receive one lifetime restricted full-catalog exercise unlock.
- Logged-in free users after that lifetime unlock see a Pro subscription CTA.
- There is no monthly free catalog reset.

For restricted non-Pro exercises, preview audio may be returned before Begin, but quota is consumed only when the user clicks Begin.

## Goals

- Preserve current Pro behavior: Pro users can load/listen to audio before clicking Begin.
- Preserve current latest-18 behavior: free exercises can keep the existing background audio resolution path.
- Let free users hear and evaluate one restricted full-catalog exercise before committing.
- Consume catalog teaser quota only on intentional Begin, not on page render, hydration, or background status checks.
- Keep CDN-cached page retrieval unblocked by entitlement checks.
- Keep protected data protected: restricted audio and original text must still come only from API decisions.
- Avoid monthly free access for catalog unlocks; this funnel is intentionally more aggressive than Pro review teaser access.

## Non-Goals

- Do not make the whole full catalog temporarily free.
- Do not consume quota during background page hydration.
- Do not return restricted original text to the browser before access is granted.
- Do not depend on localStorage or cookies as the source of truth for access control.
- Do not expose raw IP addresses or raw fingerprints in logs.
- Do not change Pro subscription entitlement rules.

## Terms

- **Free catalog window**: The current global latest 18 exercises by `CreatedAt` and `Id`.
- **Restricted exercise**: An exercise outside the latest 18 free catalog window.
- **Catalog teaser preview**: Non-consuming access to restricted exercise audio before Begin.
- **Preview lease**: Short-lived server-side authorization to preview audio for one restricted exercise.
- **Catalog teaser quota**: Lifetime count of restricted exercises a free user may unlock.
- **Catalog exercise grant**: Server-side record that a subject can access one specific restricted exercise after Begin.
- **Anonymous sample**: One restricted exercise unlock for an anonymous browser/IP fingerprint.
- **Logged-in free intro**: One lifetime restricted exercise unlock for a logged-in non-Pro user.
- **Pro access**: Full catalog access from the users-service `isPro` entitlement.

## Current Access Context

The existing backend already has the right protection boundaries:

1. Catalog listing returns exercises with `requiresPro`.
2. Exercise metadata is safe to return for restricted exercises.
3. `POST /proposition/{id}/begin` protects audio.
4. `POST /text-comparison/compare-texts` protects the server-owned original text.

The new teaser flow should keep those boundaries. The catalog page can show full catalog metadata, but old restricted exercise audio and original text are still gated by API calls.

## High-Level Flow

1. CDN returns the cached page or Angular shell.
2. Angular renders public metadata.
3. After render, Angular resolves access in the background:
   - Pro or latest-18 exercise: use the existing `begin` audio preload path.
   - Restricted non-Pro exercise: use a new non-consuming preview access path.
4. If preview is allowed, backend returns a short-lived audio URL and creates/refreshes a preview lease.
5. User can listen before clicking Begin.
6. User clicks Begin.
7. Backend consumes the correct catalog teaser quota and creates a catalog exercise grant.
8. The exercise starts.
9. On submit, compare endpoint accepts latest-18, Pro, or the catalog exercise grant.

## Eligibility Decisions

Suggested status values:

| Status | Meaning | Audio Returned | Quota Consumed |
| --- | --- | --- | --- |
| `granted_free_window` | Exercise is in the latest 18 free exercises. | Yes through current begin path | No |
| `granted_pro` | User is Pro. | Yes through current begin path | No |
| `preview_available_anonymous_sample` | Anonymous user can preview one restricted exercise. | Yes | No |
| `login_required_to_unlock_exercise` | Anonymous sample is already used; login may unlock another restricted exercise. | No | No |
| `preview_available_free_intro` | Logged-in free user can preview one restricted exercise. | Yes | No |
| `upgrade_required_to_unlock_exercise` | Logged-in free user has already used their lifetime restricted exercise unlock. | No | No |
| `granted_catalog_teaser` | Begin consumed teaser quota or existing grant already covers this exercise. | Yes | Yes if newly claimed |
| `preview_unavailable` | Preview cannot be granted, such as missing fingerprint or store failure. | No | No |

The status contract can be separate from `MistakePatternStatus`; catalog access is a different product surface.

## Anonymous User Behavior

### First Restricted Exercise

If the request is anonymous and the anonymous catalog sample is available:

1. Page loads with public metadata.
2. Frontend calls preview access after render.
3. Backend creates or refreshes a preview lease for that fingerprint and exercise.
4. Backend returns audio URL with status `preview_available_anonymous_sample`.
5. User can listen before Begin.
6. User clicks Begin.
7. Backend verifies the preview lease, consumes anonymous catalog teaser quota, creates a catalog exercise grant, and starts the exercise.

The user can choose any restricted exercise for this sample. The quota is one restricted exercise unlock, not full unlimited catalog access.

### Anonymous User After Sample Is Used

If the anonymous sample quota is already used and the restricted exercise is not already granted:

1. Do not return preview audio.
2. Return status `login_required_to_unlock_exercise`.
3. Frontend shows a login CTA.

Suggested copy:

```text
Log in to unlock one full-catalog exercise.
```

Button:

```text
Log in to unlock
```

Before redirecting to login, frontend may store a short-lived pending catalog access request in `sessionStorage`.

Suggested payload:

```json
{
  "exerciseId": 123,
  "returnUrl": "/english-writing-exercise/123",
  "createdAtUtc": "2026-07-10T12:00:00Z",
  "expiresAtUtc": "2026-07-10T12:15:00Z",
  "source": "catalog_access_login_cta"
}
```

Do not put learner text, original text, or comparison data in the URL.

## Logged-In Free User Behavior

### First Restricted Exercise

If the user is logged in, not Pro, and has not used the logged-in free catalog unlock:

1. Page loads with public metadata.
2. Frontend calls preview access after render.
3. Backend creates or refreshes a preview lease for that user and exercise.
4. Backend returns audio URL with status `preview_available_free_intro`.
5. User can listen before Begin.
6. User clicks Begin.
7. Backend consumes the lifetime logged-in free intro quota, creates a catalog exercise grant, and starts the exercise.

### After Logged-In Free Unlock Is Used

If the logged-in free user has already used the lifetime restricted exercise unlock and the requested restricted exercise is not already granted:

1. Do not return preview audio.
2. Return status `upgrade_required_to_unlock_exercise`.
3. Frontend shows a Pro CTA.

Suggested copy:

```text
Upgrade to Pro to unlock the full exercise catalog.
```

Button:

```text
Upgrade to Pro
```

The CTA should navigate to `/plans` with a source marker and return URL. Do not put protected exercise content in the URL.

## Pro User Behavior

Pro users should keep the current behavior:

1. Page loads.
2. Angular may resolve audio in the background through the current `begin` path.
3. Backend sees `isPro = true`.
4. Backend returns audio URL.
5. No catalog teaser quota or preview lease is involved.

This is the main reason the Angular lifecycle should branch by access type instead of removing background `begin` globally.

## Latest 18 Free Exercise Behavior

Exercises in the latest 18 free catalog window should keep the current behavior:

1. Page loads.
2. Angular may resolve audio in the background through the current `begin` path.
3. Backend sees `requiresPro = false`.
4. Backend returns audio URL.
5. No catalog teaser quota or preview lease is involved.

## Audio Preview And Begin Separation

Restricted non-Pro exercises need a two-step path:

### Preview Access

Preview access is non-consuming.

Suggested endpoint:

```text
POST /proposition/{id}/preview-access
```

Response shape:

```json
{
  "accessStatus": "preview_available_anonymous_sample",
  "audioUrl": "https://...",
  "audioExpiresAtUtc": "2026-07-10T20:00:00Z",
  "metadata": {
    "id": 123,
    "requiresPro": true
  }
}
```

If access is not available:

```json
{
  "accessStatus": "login_required_to_unlock_exercise",
  "audioUrl": null,
  "audioExpiresAtUtc": null,
  "metadata": {
    "id": 123,
    "requiresPro": true
  }
}
```

### Begin Access

Begin access is consuming for restricted non-Pro teaser access.

Existing endpoint:

```text
POST /proposition/{id}/begin
```

For Pro/latest-18:

- Keep current behavior.
- Return `access = "granted"` and audio URL.
- Do not touch teaser counters.

For restricted non-Pro:

- If an exercise grant already exists for this exercise, return `access = "granted"` and audio URL.
- Else if a valid preview lease exists and quota is available, consume quota, create grant, return `access = "granted"` and audio URL.
- Else return a locked status that maps to login or upgrade UI.

## Catalog Exercise Grants

The grant is the durable access decision for one restricted exercise.

Recommended semantics:

- A grant applies to exactly one exercise.
- A grant is scoped to either a logged-in user or anonymous fingerprint.
- A grant is created when Begin succeeds for restricted teaser access.
- A grant allows future preview/begin/compare access for that same exercise.

This avoids a broken experience after refresh. If a user spends the teaser quota on exercise 123, they should be able to reload exercise 123 and continue. They should not be able to access other restricted exercises unless they log in, use another available quota, or upgrade to Pro.

Grant lifetime options:

| Scope | Suggested Lifetime | Notes |
| --- | --- | --- |
| Anonymous grant | 7-30 days | Enough to survive refresh or same-session retry, but not permanent account-like access. |
| Logged-in free grant | Permanent | Stable user account access to the one unlocked restricted exercise. |
| Pro access | Subscription period | Already governed by users-service entitlement. |

## Preview Leases

Preview leases allow listening before Begin without consuming quota.

Recommended semantics:

- Short lifetime, such as 15-60 minutes.
- Scope to subject and exercise.
- Require server-side fingerprint for anonymous users.
- Optionally allow only one active restricted preview lease at a time per subject.
- Do not count as consumed quota.
- Do not allow compare access by itself.

The preview lease protects against unlimited old-catalog listening. If the user has already used quota and has no grant for that exercise, preview access should not return audio.

## Quota Storage

Recommended counters:

| Scope | Key | Purpose |
| --- | --- | --- |
| Anonymous restricted sample | `anonymousFingerprintHash + catalog_access_anonymous_sample` | One restricted exercise unlock for anonymous users. |
| Logged-in restricted intro | `userId + catalog_access_free_intro` | One lifetime restricted exercise unlock for logged-in free users. |
| Catalog exercise grant | `subjectId + exerciseId` | Durable permission for one restricted exercise. |
| Preview lease | `subjectId + exerciseId + expiresAtUtc` | Non-consuming temporary audio preview authorization. |

Feature names:

```text
catalog_access_anonymous_sample
catalog_access_free_intro
```

The existing AI usage limiter should not be reused blindly if its naming and cost fields make the code misleading. A shared quota counter abstraction is fine, but catalog access is not AI usage.

## Backend Responsibilities

### Catalog Listing

The catalog listing can stay mostly as it is:

1. Return full filtered/paged catalog.
2. Mark each item with `requiresPro`.
3. Do not personalize every card from the CDN path.

If the catalog grid needs personalized lock state, fetch it after render with a non-consuming access status call.

### Metadata Endpoint

The metadata endpoint should stay safe:

1. Return public exercise metadata.
2. Include `requiresPro`.
3. Do not return audio URL.
4. Do not return original text.

### Preview Access Endpoint

The preview endpoint should:

1. Resolve session and anonymous fingerprint.
2. Load exercise metadata.
3. If latest-18 or Pro, it may either return a status telling the frontend to use normal begin, or return audio using the same internal audio URL helper.
4. If restricted non-Pro and eligible, create/refresh preview lease and return audio.
5. If not eligible, return login/upgrade status without audio.
6. Never consume quota.

### Begin Endpoint

The begin endpoint should:

1. Preserve current Pro/latest-18 behavior.
2. For restricted non-Pro, check existing grant first.
3. If no grant, require a valid preview lease before consuming teaser quota.
4. Consume quota in a transaction.
5. Create grant.
6. Return audio URL.
7. Return login/upgrade status if denied.

### Compare Endpoint

The compare endpoint should:

1. Preserve current Pro/latest-18 behavior.
2. For restricted non-Pro, require a catalog exercise grant.
3. Return Pro-required/locked result if no grant exists.
4. Never accept client-provided original text for restricted access.

## Frontend Responsibilities

### Exercise Page Lifecycle

Use the existing metadata and auth state to choose the background call:

```ts
if (authState.isPro || proposition.requiresPro !== true) {
  resolveExerciseAudioAccess(); // existing begin path
} else {
  resolveRestrictedPreviewAccess(); // new non-consuming preview path
}
```

The Begin button should still call the begin flow for every exercise:

```ts
beginExercise() {
  callBegin();
}
```

For restricted non-Pro exercises, this is where quota is consumed and a grant is created.

### Catalog Grid

The catalog grid can keep rendering based on `requiresPro`, but should use authenticated state carefully:

- Pro users see restricted cards as normal links.
- Non-Pro users can click restricted cards and navigate to the exercise page if preview may be available.
- If the product wants to block from the grid, the modal should use access status fetched after render, not cached page data.

For this funnel, navigating to the restricted exercise page can be useful because the preview audio and CTA are exercise-specific.

### UI States

Suggested UI mapping:

| Status | UI |
| --- | --- |
| `preview_available_anonymous_sample` | Audio available; Begin button says `Start this full-catalog exercise free`. |
| `login_required_to_unlock_exercise` | No audio; login CTA. |
| `preview_available_free_intro` | Audio available; Begin button says `Start your free full-catalog exercise`. |
| `upgrade_required_to_unlock_exercise` | No audio; Pro CTA. |
| `granted_catalog_teaser` | Normal exercise UI. |
| `granted_pro` | Normal exercise UI. |
| `granted_free_window` | Normal exercise UI. |

## Login Return Flow

When anonymous user clicks the login CTA:

1. Store pending catalog access request in `sessionStorage`.
2. Navigate to login with:

```text
returnUrl=/english-writing-exercise/{id}
source=catalog_access_login_cta
```

3. After login, return to the same exercise page.
4. Frontend calls preview access as a logged-in free user.
5. If eligible, audio is returned without consuming quota.
6. Begin consumes the logged-in free intro quota.

## Logging And Telemetry

Do not log raw learner text, raw original text, raw IP, or raw fingerprint.

### Backend Events

#### `catalog_access_preview_checked`

Fields:

- `exerciseId`
- `isAuthenticated`
- `isPro`
- `requiresPro`
- `accessStatus`
- `anonymousFingerprintPresent`
- `userIdHash`
- `previewLeaseCreated`
- `grantAlreadyExists`
- `durationMs`

#### `catalog_access_begin_claimed`

Fields:

- `exerciseId`
- `quotaType`: `anonymous_sample`, `free_intro`, `existing_grant`, `free_window`, `pro`
- `accessStatus`
- `grantCreated`
- `previewLeasePresent`
- `durationMs`

#### `catalog_access_begin_denied`

Fields:

- `exerciseId`
- `reasonCode`
- `isAuthenticated`
- `isPro`
- `anonymousFingerprintPresent`
- `grantAlreadyExists`

### Frontend Events

#### `catalog_access_cta_shown`

Fields:

- `ctaType`: `login_to_unlock`, `upgrade_to_pro`, `start_free_catalog_exercise`
- `exerciseId`
- `accessStatus`

#### `catalog_access_cta_clicked`

Fields:

- `ctaType`
- `exerciseId`
- `returnUrlPresent`
- `accessStatus`

#### `catalog_access_preview_audio_loaded`

Fields:

- `exerciseId`
- `accessStatus`
- `requiresPro`

## Privacy And Abuse Controls

- Hash IP addresses with a server-side secret salt before storing or logging.
- Include a coarse user-agent bucket for anonymous abuse resistance if useful.
- Do not rely on localStorage/cookies as the enforcement authority.
- Do not return audio preview if fingerprinting is unavailable for anonymous restricted access.
- Consider one active restricted preview lease at a time per anonymous fingerprint.
- Use short preview lease expiry.
- Keep grants scoped to one exercise.

## Failure Behavior

| Failure | Behavior |
| --- | --- |
| Preview store unavailable | Do not return restricted preview audio; show safe locked state. |
| Quota store unavailable | Do not consume quota; show safe locked/unavailable state. |
| Audio URL generation fails | Show normal audio unavailable error. |
| Anonymous fingerprint unavailable | Do not return restricted preview audio; show login CTA. |
| Login return payload expired | Show normal exercise page and re-check preview status. |
| Begin called without preview lease | Return login/upgrade/locked status unless grant already exists. |
| Compare called without grant | Return Pro-required/locked response; do not return original text. |

## Test Plan

### Backend

- Latest-18 exercise keeps current begin behavior for free users.
- Pro restricted exercise keeps current background begin/audio behavior.
- Anonymous restricted preview returns audio and does not consume quota.
- Anonymous restricted Begin consumes quota and creates grant.
- Anonymous after quota used cannot preview another restricted exercise.
- Anonymous existing grant can preview/begin the same restricted exercise again.
- Anonymous after quota used gets login-required for a different restricted exercise.
- Logged-in free restricted preview returns audio and does not consume quota.
- Logged-in free restricted Begin consumes lifetime intro quota and creates grant.
- Logged-in free after lifetime quota used cannot preview another restricted exercise.
- Logged-in free existing grant can preview/begin the same restricted exercise again.
- Compare allows restricted exercise only when latest-18, Pro, or grant exists.
- Compare denies restricted exercise without grant and does not return original text.
- Raw IP/fingerprint/original text are not logged.

### Frontend

- Pro restricted exercise still resolves audio before Begin.
- Latest-18 exercise still resolves audio before Begin.
- Restricted non-Pro exercise uses preview access instead of automatic begin.
- Preview-available status renders audio and free-start Begin copy.
- Login-required status renders login CTA and no audio.
- Upgrade-required status renders Pro CTA and no audio.
- Begin on preview-available restricted exercise consumes/start path.
- Existing grant reload keeps audio available for that same exercise.
- Login CTA stores pending request without learner text.

### Telemetry

- Preview checked event distinguishes preview available, login required, upgrade required, Pro, and free window.
- Begin claimed event records quota type and whether grant was newly created.
- Denied events include reason code without raw protected data.

## Implementation Notes

- Keep `begin` as the single canonical path for starting an exercise.
- Add a separate preview path for restricted non-Pro audio preview.
- Extract shared audio URL creation so preview and begin do not duplicate storage code.
- Avoid treating catalog access as AI usage in naming, even if the quota counter implementation is shared.
- Keep all protected API responses `no-store`.
