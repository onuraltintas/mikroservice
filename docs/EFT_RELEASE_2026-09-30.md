# EFT production release — 2026-09-30

Release: `eft-20260930`.

Updated: Coaching API, Speed Reading API, admin/coaching student frontend,
staff portal, Speed Reading frontend. Gateway reuses its previous image with
only two additional anonymous GET routes for public CAPTCHA configuration.
Identity and Notification were not restarted or changed.

Both products require server-verified reCAPTCHA for EFT submissions. Their
existing CAPTCHA keys, permitted hostnames and score settings are reused.
Disabled CAPTCHA prevents submission. Existing authentication, rate limits and
idempotency remain in place.

Coaching migrations applied:

- `20260930070844_AddCoachingInstitutionPaymentReference`
- `20260930193749_AddCoachingBankTransferRequestReferenceIndex`

Speed Reading required no schema changes.

Database backups: `/var/lib/eduivme/backups/eft-20260930/`.
Image rollback: `bash /var/lib/eduivme/releases/eft-20260930/deploy.sh rollback`.
Rollback retains the additive schema changes; restoring databases is not
automatic and must not discard subsequent user writes.

Verification:

- 63 targeted backend tests passed. An additional PostgreSQL test could not
  initialize because local Docker was unavailable; live migrations succeeded.
- 26 Coaching CAPTCHA frontend tests and 14 Speed Reading frontend tests passed.
- 2 Gateway CAPTCHA routing contract tests passed.
- All three frontend production builds and both API publishes succeeded.
- Public portal pages and both CAPTCHA configuration endpoints returned 200.
- Anonymous student/teacher Coaching EFT and Speed Reading EFT POSTs returned 401.
- Successful authenticated Google CAPTCHA/EFT submission still requires a real
  user browser session; no test payment was created in production.
