# Program ordering v4

Based on v3: 20 normal programs, 28 days each, 6 tasks on days 1–14 and
7 tasks on days 15–28. Assessment programs and the exercise catalogue are unchanged.

Vocabulary remains only in Child and Teen programs. YoungAdult and Adult vocabulary
slots become existing reading practices at the same age and difficulty. Daily tasks
are stably ordered: eye/visual warmups, reading/vocabulary, then attention/N-back.

Run `node generate.mjs`, then `node --test pack.test.mjs` from this directory.
`snapshot.sql` exports a read-only live snapshot without user identifiers.
`prepare-release.mjs <snapshot.json> <private-directory>` creates guarded apply,
rollback and inventory scripts. Private snapshots must not be committed.

Existing completed/started days are preserved. Only unstarted days of active
assignments are revised; task counts and all results remain unchanged. Source or
activity drift causes the transition to stop. Before live use, restore a database
backup into a temporary database, verify protected data, schedule resolution,
idempotence and exact rollback. Apply with only the Speed Reading API paused.

## Applied — 2026-10-13

Live: 20 templates and unstarted days in 3 active schedules updated. Two already
started days retained their original order. 11 pack/content tests passed; actual
PostgreSQL rehearsal verified protected-table fingerprints, idempotence, exact
rollback, and runtime resolution of 20 programs / 560 days / 3,640 tasks. Live
schedule verification matched the expected revision. API live/ready and the
public homepage returned HTTP 200 after restart. No application images changed;
no Coaching/Identity/Notification data changed. The disposable rehearsal database
was removed; private backups remain in the VPS release directory.
