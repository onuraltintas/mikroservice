# Speed Reading incomplete-session cleanup — 2026-10-13

Scope: the authenticated Speed Reading API/frontend changes in commit
`319b250c104539dc55a136077f2e5bf99c6c99e4`. Course Management and all other
services are excluded. The release changes no database schema and applies no SQL.
It deletes only the current user's incomplete exercise sessions on explicit exit
or logout; completed results and assessment definitions are preserved.

Locally, run the Speed Reading application unit tests, the production Angular
build, and the three focused Angular specs where ChromeHeadless is available.
Prepare the payload with `node tools/releases/speed-session-cleanup-20261013/prepare.mjs`.
On the VPS, run `bash deploy.sh validate`, `build`, then `promote`. Promotion
recreates only the Speed Reading API and frontend, validates readiness and the
public homepage, and automatically restores the prior images if a health check
fails. `bash monitor.sh` checks health for 15 minutes.

There is no data rollback because no database or content package is applied.
The prior API/frontend image tags and browser assets are retained in this
release directory for application rollback. Do not deploy the unrelated local
`content-packs/*-programs/v1` files with this release.
