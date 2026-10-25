# Security hardening: deployment checklist

This branch is **not production-ready**. Do not deploy or merge until the requirements below are satisfied and tests pass.

## Registration endpoint migration

The former `PUT /_synapse/admin/v2/users/{user_id}` is unsuitable for public invite registration: it can update **existing** Matrix users, including their password.

The new `SynapseAdminService` uses Synapse's create-only shared-secret registration endpoint (`GET/POST /_synapse/admin/v1/register`). It does not require the admin access token for this operation.

1. Set a **new, random, long** `registration_shared_secret` (or `registration_shared_secret_path`) in Synapse's private homeserver configuration. Keep `enable_registration: false` for public signup. Do not expose the secret in git, public URLs, frontend bundles or logs.
2. Restart Synapse using its supported deployment procedure and check its logs for successful startup.
3. Configure the backend's secret privately as `Synapse__RegistrationSharedSecret` and set `Synapse__AdminBaseUrl` to an internal endpoint (e.g. `http://synapse:8008` inside the same Compose network). For Windows development, use a secure SSH port forwarding to `http://127.0.0.1:18008` and configure that address as `Synapse__AdminBaseUrl`. Public domains are deliberately rejected by the implementation. Never add secrets to `appsettings.json` or the Git repository.
4. **Block public access** to `/_synapse/admin/` on the Cloudflare-published Matrix endpoint using a routing/proxy rule, and allow only the private network path for the API. The new `AdminBaseUrl` restriction only prevents accidental public routing from this application; it does not by itself prevent outsiders from reaching a public Synapse endpoint. Confirm the public route no longer exposes administrative APIs before deployment.
5. Test with a fresh disposable username. Expect **one** new Matrix user and one local DB user.
6. Test with an existing Matrix-only account; expect HTTP 409 and **no password change**.
7. Test an invalid nonce, expired nonce, unavailable Synapse, and unsuccessful SQL transaction. The latter can still leave an orphaned Matrix account and needs a reconciliation workflow.

**Do not enable this until you've confirmed your Synapse version supports the shared-secret registration API and its exact response format.** This is a breaking deployment configuration change.

## Additional hardening in this branch

- JWT validation now queries SQL Server for current user status and role. Old admin tokens will be rejected after demotion; suspended or removed accounts cannot use their old HolyChat API tokens. This does **not** revoke a user's Matrix sessions, which must be handled separately.
- Android's release manifest now requests Internet permission.
- The stale generated Flutter widget test was replaced.
- Text is preserved in the composer when send fails.

## Still required before production

- Suspend/deactivate users in Synapse, revoke their Matrix devices and decide whether to reject future Matrix access.
- Apply MFA to Matrix itself (not only to HolyChat API JWT issuance); document the unified account/session trust model.
- Add rate limiting to login/TOTP and enforce invite usage atomically (including concurrent requests).
- Prevent the Matrix account/SQL Server split-brain problem during failed registrations.
- Avoid discarded Matrix login tokens in `MatrixAuthService`; review logout and login session lifecycles.
- Shorten JWT expiry and implement controlled token renewal/revocation.
- Encrypt TOTP secrets at rest and plan key rotation.
- Add .NET and Flutter CI/test suites and verify E2EE, backup/restore and server configuration.
- Verify the private compose, federation setting, public registration, firewall/proxy, backups and leaked secret rotation.

## Validation not yet performed

Neither `dotnet` nor `flutter` is installed in the assistant's execution environment. No claim of a successful build or integration test is made. The branch should stay unmerged until validated in a local dev environment.

Recommended local commands:

```powershell
cd C:\xampp\htdocs\HolyChat\backend\HolyChat.Api
dotnet restore
dotnet build

cd C:\xampp\htdocs\HolyChat\frontend\holychat_app
flutter pub get
flutter analyze
flutter test
flutter build apk --debug
```
