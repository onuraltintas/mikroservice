# Speed Reading single-window preview and heading release

Source commit: `383643f1`. Includes training heading isolation from shared global styles and the custom preview level selector inside the settings dialog. Normal exercise start keeps its separate level selector. Selecting a preview level resets controls to that level's defaults; submission uses the matching exercise ID and temporary settings. Unsupported levels cannot launch; malformed level configurations display an error.

Verification: 167 regression tests passed again before release. Production build and app/spec TypeScript checks passed. Independent review found no blocker. Existing exercise-player CSS budget warning remains. Real authenticated desktop/mobile visual checks are not claimed.

Deployment targets only Speed Reading frontend. Image tag: `custom-preview-levels-20261005-383643f1`. No API restart, database migration or user-data changes. Previous immutable image is recorded by `prepare`; rollback uses the saved override. Rollback triggers: unhealthy container or failed public route/asset smoke checks. No staging environment created.

Release directory: `/var/lib/eduivme/releases/custom-preview-levels-20261005`.
Script: `artifacts/custom-preview-levels-release-20261005.sh` (`build`, `prepare`, `deploy`, `smoke`, `rollback`).
