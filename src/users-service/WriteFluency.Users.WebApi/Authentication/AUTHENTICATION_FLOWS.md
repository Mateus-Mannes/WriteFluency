# WriteFluency Authentication Flows

This document explains the custom authentication flow implemented by `users-service`
and consumed by the Angular web app. The product intentionally uses one sign-in
surface for account creation and login, so a user can start with any supported
method and later use another method with the same email address.

## Goals

- Users can sign in or create an account with Google, Microsoft, email code, or email/password.
- The same email address represents the same account across all methods.
- A provider-created or OTP-created account can later add password sign-in.
- A password-created account can later use provider or OTP sign-in.
- The UI must not expose separate "create account" and "sign in" routes.
- Confirmation/setup links should be explicit, idempotent where practical, and avoid e-mail resend loops.

## Main Endpoints

| Endpoint | Purpose |
| --- | --- |
| `POST /users/auth/password/continue` | Unified password sign-in/sign-up/setup endpoint. |
| `POST /users/auth/password/setup/confirm` | Confirms a password setup token for an existing account that had no password. |
| `POST /users/auth/passwordless/request` | Sends an OTP sign-in code, creating a passwordless account when needed. |
| `POST /users/auth/passwordless/verify` | Verifies the OTP, confirms the email if needed, and signs the user in. |
| `GET /users/auth/external/{provider}/start` | Starts Google or Microsoft login. |
| `GET /users/auth/external/{provider}/callback` | Completes Google or Microsoft login, links by verified email, and signs the user in. |
| `GET /users/auth/confirmEmail` | Identity confirmation endpoint used by the email confirmation link. |

The Angular app wraps these endpoints in `AuthApiService` and drives the single
login UI from `login.component`.

## Password Continue Contract

`POST /users/auth/password/continue` accepts:

```json
{
  "email": "user@example.com",
  "password": "user typed password",
  "sendEmail": true
}
```

`sendEmail` defaults to `true`.

- `sendEmail: true` means this is the initial user action and the backend may send a confirmation/setup email.
- `sendEmail: false` means the user clicked the UI button after opening an email link. The backend should only check whether the account can now sign in. It must not send another confirmation/setup email.

Response shape:

```json
{
  "status": "signed_in",
  "isNewUser": false
}
```

Known statuses:

| Status | Meaning |
| --- | --- |
| `signed_in` | Password was valid, email/setup is complete, and the auth cookie was issued. |
| `confirmation_required` | The account exists or was created, but email confirmation is still required. |
| `password_setup_required` | The account exists and the email is confirmed, but it has no password yet. |
| `wrong_password` | The account has a password, but the submitted password did not match. |
| `account_locked` | Identity lockout blocked the attempt. |

## Email + Password Flow

### New email

1. User enters email and password in the same login form.
2. Web app calls `/password/continue` with `sendEmail: true`.
3. Backend creates an `ApplicationUser` with `PasswordHash` and `EmailConfirmed = false`.
4. Backend sends the normal confirmation email.
5. Backend returns:

   ```json
   { "status": "confirmation_required", "isNewUser": true }
   ```

6. UI keeps the typed password in the current form and shows `I confirmed, continue`.
7. User opens the confirmation email link.
8. User returns to the original tab and clicks `I confirmed, continue`.
9. Web app calls `/password/continue` with `sendEmail: false`.
10. Backend signs the user in if the email is now confirmed.

If the user clicks `I confirmed, continue` before opening the link, the backend
returns `confirmation_required` without sending another email.

### Existing password account

1. User enters email and password.
2. Backend checks `PasswordHash`.
3. If password is valid and `EmailConfirmed = true`, backend signs the user in.
4. If password is valid but email is not confirmed:
   - `sendEmail: true` sends a confirmation email.
   - `sendEmail: false` only returns `confirmation_required`.
5. If password is invalid, backend returns `wrong_password`.

## Existing Social/OTP Account Adding Password

This is not a reset flow. The user already has an account, but it has no
password because it was created with a social provider or email code.

1. User enters the existing email and a desired password in the same login form.
2. Web app calls `/password/continue` with `sendEmail: true`.
3. Backend finds the user and sees `PasswordHash` is empty.
4. If the email is already confirmed:
   - Backend hashes the submitted password immediately.
   - Backend stores only the password hash and user id in Redis under `auth:password-setup:{token}`.
   - The Redis entry expires after 30 minutes.
   - Backend sends a setup email with a link to `/auth/confirm-email?passwordSetupToken=...`.
   - Backend returns `password_setup_required`.
5. User opens the setup link.
6. Angular `confirm-email.component` detects `passwordSetupToken` and calls `/password/setup/confirm`.
7. Backend consumes the Redis entry, sets `PasswordHash`, sets `EmailConfirmed = true`, updates the security stamp, and deletes the setup token.
8. User returns to the original login tab and clicks `I confirmed, continue`.
9. Web app calls `/password/continue` with `sendEmail: false`.
10. Backend signs the user in with the password that was typed in the original tab.

If the user clicks `I confirmed, continue` before opening the setup link, the
backend returns `password_setup_required` without sending another setup email.

Security notes:

- The plaintext password is never stored in Redis.
- The browser only keeps the typed password in the active form control; it is not written to local storage/session storage.
- Setup tokens are one-time-use because confirmation deletes the Redis key.
- If the account gets a password before a setup token is consumed, confirmation is treated as successful and does not overwrite the existing password.

## Email Code Flow

1. User enters an email in the email code form.
2. Web app calls `/passwordless/request`.
3. Backend rate-limits by normalized email and IP through `PasswordlessOtpStore`.
4. If the account does not exist, backend creates an `ApplicationUser` with no password and `EmailConfirmed = false`.
5. Backend sends a 6-digit OTP email.
6. User enters the OTP.
7. Web app calls `/passwordless/verify`.
8. Backend validates the code, confirms the email if needed, and signs the user in using authentication method `passwordless_email_otp`.

The request endpoint intentionally returns a generic success message so callers
cannot use it to enumerate valid accounts.

## Social Provider Flow

Supported providers are Google and Microsoft.

1. Web app starts login through `/external/{provider}/start`.
2. Backend validates provider availability and `returnUrl`.
3. Provider redirects back to `/external/{provider}/callback`.
4. Backend requires a verified provider email.
5. Backend looks up an existing user by email.
6. If no user exists:
   - Creates a new user with `EmailConfirmed = true`.
   - Adds the external login.
   - Signs the user in.
7. If a user exists but `EmailConfirmed = false`:
   - Confirms the user email through Identity.
   - Adds the external login if it is not already linked.
   - Signs the user in.
8. If the external login is already linked to another user, backend rejects with `external_login_conflict`.

This makes social login interoperable with accounts originally created by
password or OTP, provided the provider email is verified.

## Frontend State Model

The login UI has two modes:

- `Email + Password`
- `Email Sign-In` using OTP

There is no separate sign-up screen. Copy should describe account creation as
automatic and should not tell users to use a different email when the email
already belongs to an account.

For password flows, `login.component` tracks `awaitingEmailConfirmation`.
When that value matches the current email field, the submit button becomes
`I confirmed, continue`. That button must call `/password/continue` with
`sendEmail: false`.

## Email Templates

Relevant templates live in `EmailTemplateBuilder`.

| Template | Used for |
| --- | --- |
| `BuildConfirmationEmail` | New password account or unconfirmed existing password account. |
| `BuildPasswordSetupLinkEmail` | Existing social/OTP account adding password sign-in. |
| `BuildPasswordlessOtpEmail` | Email code sign-in/sign-up. |
| `BuildPasswordResetLinkEmail` / `BuildPasswordResetCodeEmail` | Actual reset flows only. Do not use these for first password setup. |

Password setup email language must not say "reset password". It should explain
that the account already exists and the user is confirming the email address to
add password sign-in.

## Configuration Notes

`Authentication:ConfirmationRedirectUrl` must point to the web app origin used
in the environment, for example:

- Local: `http://localhost:4200`
- Production: `https://writefluency.com`

The password setup link is built as:

```text
{Authentication:ConfirmationRedirectUrl}/auth/confirm-email?passwordSetupToken=...
```

The normal email confirmation link is generated by the Identity email sender and
also redirects users through the web app confirmation page.

## Test Coverage

Keep these scenarios covered when changing auth:

- New email + password creates account and sends one confirmation email.
- Clicking `I confirmed, continue` before confirming does not send another email.
- Passwordless/social account with no password sends a setup email, not a reset email.
- Setup email includes both a button and a raw link in the HTML body.
- Clicking `I confirmed, continue` before password setup confirmation does not send another setup email.
- Password setup confirmation commits the pending password hash and allows password login.
- OTP verification confirms the email and signs in.
- Social login links by verified email and handles conflicts.
