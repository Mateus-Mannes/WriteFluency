# Pro Review Teaser And Free Credit Flow

## Summary

The Pro review feature gives learner-friendly mistake tags and short explanations after the normal listen-and-write correction flow. Pro users receive the real AI-generated review when usage limits allow it. Free users should see a limited teaser path:

- First anonymous eligible attempt can receive one real Pro review.
- Anonymous users who already used the sample see a blurred mock review with a login CTA.
- Logged-in free users receive one real free Pro review, then one real free Pro review per calendar month.
- Logged-in free users outside that quota see a blurred mock review with a Pro subscription CTA.
- Pro users continue through the paid Pro review path, protected by the AI usage limiter.

The blurred review must be a mock/static preview. It must not call the AI classifier.

## Goals

- Let free users experience the value of Pro review before subscribing.
- Avoid AI cost for locked or blurred states.
- Make login conversion useful by returning users to the same result and unlocking the review when they still have a free credit.
- Keep deterministic corrections available for every user.
- Log enough structured data to understand conversion, quota usage, denial reasons, and AI cost without storing raw learner text in telemetry.

## Non-Goals

- Do not expose exact remaining quota in the UI for v1.
- Do not show raw IP addresses in logs.
- Do not put learner text, correction snippets, or full comparison data in URLs.
- Do not call AI just to blur or hide the result.

## Terms

- **Deterministic corrections**: The normal correction result after static comparison and deterministic refinement.
- **Pro review**: Mistake-pattern annotations attached to final comparisons, including tags and `mistakePatternPhrase`.
- **Real review**: AI classifier was called and returned annotations.
- **Mock review**: A static or locally generated blurred preview shown as a teaser. No AI call is made.
- **Anonymous sample credit**: The first anonymous eligible real review for a browser/IP fingerprint.
- **Free monthly credit**: One real review per calendar month for a logged-in non-Pro user.
- **Paid Pro quota**: The existing Pro AI usage limiter, such as daily/monthly/cost caps.

## High-Level Flow

1. User submits an exercise.
2. Backend runs deterministic correction as usual.
3. Backend validates whether final comparisons are eligible for review.
4. Backend checks Pro review eligibility before calling the classifier.
5. If eligible, backend reserves the correct quota and calls the AI classifier.
6. If not eligible, backend returns deterministic corrections plus a locked review status.
7. Frontend renders:
   - real Pro review when generated,
   - login CTA with blurred mock review when login can unlock a credit,
   - Pro CTA with blurred mock review when a subscription is needed,
   - usage-limit message for Pro users when paid quota is reached.

## Eligibility Decisions

The backend should return a result-level status and a short reason code. Suggested statuses:

| Status | Meaning | AI Called |
| --- | --- | --- |
| `generated` | Real review generated successfully. | Yes |
| `login_required_to_unlock_review` | Anonymous quota is used, but a logged-in free credit may be available. | No |
| `upgrade_required_to_unlock_review` | Free user has no available review credit. | No |
| `skipped_anonymous_quota_used` | Anonymous sample was already used and no login unlock is being offered by this response. | No |
| `skipped_free_monthly_quota_used` | Logged-in free monthly credit already used. | No |
| `skipped_usage_limit` | Pro user reached paid AI usage limits. | No |
| `classifier_failed` | Classifier failed after eligibility/reservation. | Attempted |
| `not_applicable` | No review should be shown, such as no comparisons. | No |

The current `MistakePatternStatus` can be extended with these values, or a separate `ProReviewStatus` can be introduced if the naming becomes too broad.

## Anonymous User Behavior

### First Eligible Anonymous Attempt

If the request is anonymous and the anonymous sample quota is available:

1. Reserve anonymous sample credit.
2. Call the AI classifier.
3. Return deterministic corrections plus real Pro review annotations.
4. Set status `generated`.

Anonymous quota should use a defensive server-side fingerprint:

- Hash of IP address with a server-side secret salt.
- Optional user-agent bucket for additional abuse resistance.
- Optional client hint cookie/localStorage value to avoid repeated checks from the same browser.

Do not log raw IP addresses.

### Anonymous Attempt After Sample Used

If the request is anonymous and the anonymous sample was already used:

1. Do not call the classifier.
2. Return deterministic corrections.
3. Return status `login_required_to_unlock_review`.
4. Frontend shows a blurred mock review and a CTA:
   - Primary copy: `Log in to unlock your free Pro review.`
   - Button: `Log in to unlock`

Before redirecting to login, frontend stores a short-lived pending review request in `sessionStorage`.

Suggested `sessionStorage` payload:

```json
{
  "exerciseId": 123,
  "draftUserText": "...",
  "returnUrl": "/english-writing-exercise/123",
  "createdAtUtc": "2026-07-08T12:00:00Z",
  "expiresAtUtc": "2026-07-08T12:15:00Z",
  "source": "pro_review_login_cta"
}
```

Do not put `draftUserText` in the URL.

After login, the app returns to the exercise result context and requests review again as a logged-in user.

## Logged-In Free User Behavior

### First Logged-In Free Review

If the user is logged in, not Pro, and has never used a logged-in free review:

1. Reserve free review credit.
2. Call the AI classifier.
3. Return real Pro review annotations.
4. Set status `generated`.

### Monthly Free Review

After the first logged-in credit, the user receives one free real Pro review per calendar month.

The quota key should be:

```text
userId + feature + periodKey
```

Where:

```text
feature = mistake_pattern_classification_free
periodKey = yyyy-MM
```

### Free User After Credits Used

If a logged-in free user has no credit available:

1. Do not call the classifier.
2. Return deterministic corrections.
3. Return status `upgrade_required_to_unlock_review` or `skipped_free_monthly_quota_used`.
4. Frontend shows a blurred mock review and Pro CTA.

Suggested copy:

```text
Unlock Pro review for every attempt.
See your mistakes grouped by pattern, with tags, explanations, and linked highlights.
```

Button:

```text
Upgrade to Pro
```

The CTA should navigate to `/plans` with a source/return marker, without learner text in the URL.

## Pro User Behavior

Pro users use the existing paid AI usage limiter:

1. Check Pro paid usage limits before classifier call.
2. If allowed, reserve usage.
3. Call classifier.
4. Record actual token usage and estimated cost.
5. Return `generated`.

If Pro quota is reached:

1. Do not call the classifier.
2. Return deterministic corrections.
3. Return status `skipped_usage_limit`.
4. Frontend shows the Pro limit message, not a subscription CTA.

Suggested copy:

```text
Your Pro AI review limit was reached for now. Your correction highlights are still available.
If this keeps happening, contact support.
```

## Mock Review UI

The mock review should look close to the real Pro review, but it must be visibly locked.

Allowed mock sources:

- Static examples from curated correction fixtures.
- Deterministic comparison snippets already returned by the correction result.
- Generic sample rows such as:
  - `Everyday -> Every day`, tag `word boundary`
  - `forwar -> forward`, tag `spelling`
  - `ways -> new ways`, tag `missing or extra word`

The mock review should:

- Use blur or overlay treatment.
- Include one clear CTA.
- Avoid implying AI already reviewed the user's text.
- Avoid showing fake personalized analysis as if it were real.

## Backend Responsibilities

### Compare Endpoint

The compare endpoint should keep this order:

1. Validate request size and empty text.
2. Run deterministic correction.
3. If no final comparisons, return `not_applicable`.
4. Check review eligibility.
5. Only call classifier for eligible real-review decisions.
6. Return deterministic corrections plus review status and optional annotations.

### Quota Storage

Recommended quota counters:

| Scope | Key | Purpose |
| --- | --- | --- |
| Anonymous sample | `anonymousFingerprintHash + feature` | One anonymous real review sample. |
| Logged-in first free credit | `userId + feature + first_credit` | One logged-in free sample. |
| Logged-in monthly free credit | `userId + feature + yyyy-MM` | One free review per month. |
| Pro paid usage | `userId + feature + yyyy-MM` and day key | Paid usage limiter and cost reporting. |

Anonymous and free quotas can share the same usage limiter abstraction if the feature names are distinct.

Suggested feature names:

```text
mistake_pattern_classification_anonymous_sample
mistake_pattern_classification_free_monthly
mistake_pattern_classification_pro
```

### Reservation Semantics

Use reservation before classifier call:

1. Check quota in a transaction or distributed lock.
2. Increment reserved request count.
3. Call classifier.
4. On success, record completed count and token/cost diagnostics.
5. On failure, record failed count.

This prevents parallel submissions from bypassing request limits.

## Frontend Responsibilities

### Result Rendering

Frontend chooses review UI from status:

| Status | UI |
| --- | --- |
| `generated` | Real Pro review list. |
| `login_required_to_unlock_review` | Blurred mock review with login CTA. |
| `upgrade_required_to_unlock_review` | Blurred mock review with Pro CTA. |
| `skipped_free_monthly_quota_used` | Blurred mock review with Pro CTA. |
| `skipped_usage_limit` | Pro usage-limit alert. |
| `classifier_failed` | Hide review or show non-blocking unavailable message. |
| `not_applicable` | Hide review. |

### Login Return Flow

When anonymous user clicks login CTA:

1. Store pending review request in `sessionStorage`.
2. Navigate to login with:

```text
returnUrl=/english-writing-exercise/{id}
source=pro_review_login_cta
```

3. After login, restore pending request.
4. Resubmit/re-request review.
5. Clear pending request after success, failure, or expiry.

The stored request should expire quickly, such as after 15 minutes.

## Logging And Telemetry

Logs should be structured and safe. Never include raw `originalText`, `userText`, snippets, or raw IP.

### Backend Events

#### `pro_review_eligibility_checked`

Fields:

- `exerciseId`
- `isAuthenticated`
- `isPro`
- `decision`
- `reasonCode`
- `anonymousFingerprintPresent`
- `userIdHash`
- `periodKey`
- `comparisonCount`
- `accuracyPercentage`
- `durationMs`

#### `pro_review_quota_reserved`

Fields:

- `quotaType`
- `feature`
- `periodKey`
- `decision`
- `reservedRequestCount`
- `remainingMonthlyCredits` if available

#### `pro_review_quota_denied`

Fields:

- `quotaType`
- `feature`
- `periodKey`
- `reasonCode`
- `reservedRequestCount`
- `completedRequestCount`

#### `pro_review_classifier_completed`

Fields:

- `feature`
- `requestCount`
- `inputTokens`
- `outputTokens`
- `totalTokens`
- `estimatedCostUsd`
- `durationMs`
- `annotationCount`

#### `pro_review_classifier_failed`

Fields:

- `feature`
- `reasonCode`
- `durationMs`
- `exceptionType`

### Frontend Events

#### `pro_review_cta_shown`

Fields:

- `ctaType`: `login_to_unlock`, `upgrade_to_pro`, `usage_limit`
- `exerciseId`
- `reviewStatus`
- `comparisonCount`

#### `pro_review_cta_clicked`

Fields:

- `ctaType`
- `exerciseId`
- `returnUrlPresent`
- `reviewStatus`

#### `pro_review_pending_restore_attempted`

Fields:

- `exerciseId`
- `source`
- `isExpired`
- `hasDraftUserText`

## Privacy And Abuse Controls

- Hash IP addresses with a server-side secret salt before storing or logging.
- Consider rotating the salt on a long interval if old anonymous quota data can expire.
- Store anonymous quota records with TTL or cleanup policy.
- Use request-size limits before correction and review eligibility.
- Do not use only localStorage/cookies for quota enforcement; they are hints, not authority.
- Do not expose quota internals or fingerprint values to the client.

## Failure Behavior

| Failure | Behavior |
| --- | --- |
| Quota store unavailable | Skip real review and return deterministic corrections with a safe locked/unavailable status. |
| Classifier unavailable | Return deterministic corrections and `classifier_failed`. |
| Login return payload expired | Show normal exercise page; do not auto-submit. |
| Pending request missing user text | Show normal result flow; do not call classifier. |
| Anonymous fingerprint unavailable | Treat as no anonymous real-review eligibility, show login CTA. |

## Test Plan

### Backend

- Anonymous first eligible request calls classifier and records anonymous sample usage.
- Anonymous second request skips classifier and returns login CTA status.
- Logged-in free first request calls classifier and records free credit usage.
- Logged-in free monthly credit is limited to one per period.
- Logged-in free over quota skips classifier and returns Pro CTA status.
- Pro user under paid limit calls classifier.
- Pro user over paid limit skips classifier and returns usage-limit status.
- Classifier failure records failed usage and still returns deterministic corrections.
- Raw learner text and raw IP are not logged.

### Frontend

- Generated status renders real review.
- Login-required status renders blurred mock review and login CTA.
- Login CTA stores pending request and navigates with return URL.
- Pending request restores after login and clears after use.
- Upgrade-required status renders blurred mock review and Pro CTA.
- Usage-limit status renders limit alert.
- Mock review does not require real annotations.

### Telemetry

- Eligibility check event is emitted for generated and skipped decisions.
- CTA shown/clicked events include review status and CTA type.
- Classifier diagnostics include token and cost fields when generated.

