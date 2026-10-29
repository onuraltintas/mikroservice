# Program revision v5 — N-back → Hedef Avı

Decision 29.10.2026: every N-back (`Focus`) slot in the 20 normal programs becomes a Hedef Avı (`TargetHunt`) slot at the
same position, count and difficulty. Based on v4; nothing else changes (20 programs, 560 days, 3,640 tasks; 320 of them
were N-back). Assessment programs and the exercise catalogue are unchanged; N-back exercises stay in the catalogue.

Requires the Hedef Avı engine (API + student app) and the `target-hunt/v1` catalog pack to be live first:
`prepare-release.mjs` refuses a snapshot without a TargetHunt exercise for every age and level a program uses.

Active assignments: days that are finished, already started or before the current day keep their exercises. On the
other days only N-back tasks change, to the TargetHunt exercise of the same age group and level. Task counts, order,
logs and results are unchanged.

## Files

- `rules.mjs` — the revision (`reviseDay`, `reviseAssignedSchedule`); `generate.mjs` writes `programs.json` from v4.
- `snapshot.sql`, `prepare-release.mjs <snapshot.json> <private-dir>` — guarded apply/rollback/inventory scripts from a
  read-only live snapshot. Private snapshots must not be committed.
- `verify.sql` (no Focus slot may remain), `verify-release.mjs <before.json> <after.json>`, `inventory.sql`.
- `rehearse.sh` (needs `RELEASE_DIR` and `CHECK_DATABASE`) and `deploy.sh` (needs `RELEASE_DIR`) — same procedure as v4.

## Checks

```sh
node content-packs/programs/v5/generate.mjs
node --test content-packs/programs/v5/pack.test.mjs
PROGRAMS_V5_TEST_DATABASE_URL=postgres://… node --test tests/E2E/program-target-hunt-v5.postgres.test.mjs
```

The PostgreSQL test rehearses snapshot → prepare → apply → verify → idempotent re-apply → exact rollback on a disposable
database, and checks that apply stops when training activity changed after the snapshot.

Not applied to production. Live use needs separate release approval, a restored-backup rehearsal and only the Speed
Reading API paused, as with v4.
