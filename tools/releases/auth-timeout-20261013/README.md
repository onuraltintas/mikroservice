# Speed Reading auth request timeout — 2026-10-13

This narrowly-scoped release is built from production frontend revision
`319b250c104539dc55a136077f2e5bf99c6c99e4`, plus only the login/registration
timeout, friendly timeout feedback, and loading-state cleanup. The existing
registration legal-consent flow is preserved. Proxy/Caddy configuration is
not changed in this release.

No API, Identity, Coaching, database, or other product service is changed.
GitHub is not updated by this release.

The package is prepared with `node prepare.mjs <isolated-build-root>`. On the
VPS, run `bash deploy.sh validate`, then `bash deploy.sh build`, then
`bash deploy.sh promote`. Promotion recreates and smoke-tests only the Speed
Reading frontend. `bash deploy.sh rollback` restores the previous frontend
image if rollback is required.
