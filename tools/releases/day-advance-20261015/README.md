# Day advance release — 2026-10-15

Application source revision: `7c40fc20694de76e4c9233f42008e4df6d7d9243`.

The package was built from the exact deployed API/student/admin sources with only this feature overlaid. No unrelated working-tree changes were included. The API overlay contains the four SpeedReading assemblies and their PDBs. Existing hashed frontend files remain available in the base images.

From `/var/lib/eduivme/releases/day-advance-20261015`, run each phase separately and verify its exit status:

```sh
bash deploy.sh prepare
bash deploy.sh build
bash deploy.sh drill
bash deploy.sh promote
bash deploy.sh validate
```

`payload.sha256` must use LF line endings. `prepare` requires the three exact prior image IDs. The compose configuration freezes existing image IDs and environment values. `build` verifies candidate assembly hashes against the verifier. `drill` restores a fresh production backup into the guarded `day_advance_drill_20261015` database and removes only that named clone afterwards.

The verifier requires exactly `20261015202217_AddProgramDayAdvance` to be pending, uses production's `speed_reading.__ef_migrations_history`, and compares all existing non-history table fingerprints before/after migration. The new program offset is omitted from its original-row comparison and is separately required to be zero. Both clone and production runs preserved all 85 existing tables.

Promotion installs the rollback trap before stopping the sole SpeedReading-owned database writer. It then creates a fresh production backup, applies the additive migration, and starts API → student frontend → admin frontend. Failure restores all three previous exact application images and checks readiness. The additive schema remains; do not run `Down` against live progress.

Published at `2026-10-15T20:43:46Z`. Images:

- API: `sha256:c40844b87666367df3c4810a5601b709dfe98752e7c3b14741151c2a22e6b8b9`
- Student: `sha256:4d05f2e3483046ee8ab4081a73d400487ef740d25be1023f8632dc1e5f304c23`
- Admin: `sha256:5519d7d3c8071a9577d6156db2532546e17b4c264aa1cb4762eb6bc62cd5a708`

Post-promotion checks matched all 8 API, 141 student, and 274 admin files to the manifest. At `20:59:28Z`, all three containers were running with zero restarts and zero new error/fatal/unhandled-exception log entries. Other production containers retained their IDs. Production backup and private compose/env files remain only in the protected server release directory.

The final check at `21:19:29Z` remained clean after the live manual day-2 → day-3 transition. Exact before/after fingerprints preserved all 12 daily logs, 20 sessions, and 20 results; only the earned-day offset changed from zero to one. The test student's preference was restored to `inherit`, the default remained off, and the already earned day stayed available. The LF-normalized archive is also retained as `day-advance-20261015-lf.tar.gz` on the release server.
