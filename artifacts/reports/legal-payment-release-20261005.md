# Legal/newsletter and adult-payer release — 2026-10-05

## Publication

- Application source: 54739402 (includes feature commits through d9d704cc).
- Release procedure: ac9c56a3.
- Published images: `eduivme/{coaching-service,speed-reading-service,admin-panel,staff-portal,speed-reading-frontend}:legal-payment-20261005`.
- Identity and Notification were not restarted.
- GitHub branch: `codex/platform-hardening`.

## Verification

- 92 targeted backend/frontend tests passed before release.
- Both APIs returned Healthy from `/health/ready`.
- All five target containers were running with zero restarts; frontend health checks passed.
- EduIvme, staff portal, Coaching and Speed Reading public addresses returned HTTP 200.
- Both central newsletter consent endpoints returned HTTP 200.
- Anonymous subscription/payment-request access returned HTTP 401.
- Coaching migration `20261005090416_RecordAdultPayerDeclaration` and Speed Reading migration `20261005091502_RecordAdultPayerDeclaration` were confirmed in production.
- Speed Reading browser reload showed the short newsletter consent link and document versions 1/1, replacing the old cached long text.
- No actual payment request, CAPTCHA acceptance or legally binding consent was submitted by the agent in production.

## Backup and cleanup

- Protected fresh pre-migration backups remain under `/var/lib/eduivme/releases/legal-payment-20261005`.
- Restore/migration rehearsal passed for both product databases. Speed Reading rehearsal preserved original schema ownership; production permissions were not changed.
- Removed only the two release-owned temporary rehearsal databases. Production data and other existing databases were preserved.
- Rollback image references and procedure remain in the release directory.

## Limits

- Adult payer checkbox is a self-declaration, not verified age or verified guardian identity.
- Existing payment records were not assigned fabricated declarations.
- Legal documents remain operator-provided test-period drafts; this release does not certify readiness for unrestricted commercial sales.
- Browser verification did not cover every authenticated role or a real financial transaction.
