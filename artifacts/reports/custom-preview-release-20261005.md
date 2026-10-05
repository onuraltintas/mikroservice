# Speed Reading custom preview release

Source: `e98f4c8d`, branch `codex/platform-hardening`.

Admin/SystemAdmin/Teacher-only catalogue preview controls cover 17 motors, with controls conditional on the motor mode and available content. Student, assignment, assessment, learning-path and persistent program sessions are unchanged. Configuration is cloned; content/questions/IDs are preserved. Preview does not save training results or vocabulary progress.

Verification: 159 ChromeHeadless regression tests passed. Application and spec TypeScript checks passed. Production build passed with the existing player CSS budget warning (128.18 kB against the 120 kB warning threshold). Independent review found no blocker after the guided countdown/progress fix. Settings helper coverage measured before the last presentation fix: 100% lines, 87.35% branches; this is not whole-application coverage.

Deploy target: only `eduivme-production-speed-reading-frontend-1`, immutable image `eduivme/speed-reading-frontend:custom-preview-20261005-e98f4c8d`. No migration, API restart or user-data change. Script records the previous immutable image in a rollback override before deployment. Roll back if the container becomes unhealthy or public SPA/assets fail checks. Real authenticated desktop/mobile feature verification remains separate from public smoke checks.

Deployment script: `artifacts/custom-preview-release-20261005.sh`; commands `build`, `prepare`, `deploy`, `smoke`, `rollback`. Production release directory: `/var/lib/eduivme/releases/custom-preview-20261005`.

## Published

Published on 2026-10-05. Image ID: `sha256:af1151718b81796eaabe95aa13d8299136c7556094509f517678c7d1c4d5da8c`. Container healthy; public home, exercise SPA and release worker checks passed. Previous image retained for rollback. Authenticated admin/teacher interaction and mobile visual checks remain unverified; public smoke checks are not a substitute for those checks.
