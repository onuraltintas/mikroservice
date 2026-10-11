# Final exercise release — 2026-10-12

Scope: Skimming v1, Eye Tracking v1, Exam Simulation v1; current Speed Reading API/frontend.
35 catalog configurations, 36 new texts, 144 questions; no schema migration.
Other services, programs and student history remain unchanged. Superseded untracked program-v1
directories and generated local artifacts are not release source.

Prepare a fresh Release API publish and production Angular build under
`artifacts/final-exercises-20261012/{api,web}`, then run `prepare.mjs <output> --seal`.
Upload the sealed archive into `/var/lib/eduivme/releases/final-exercises-20261012`.
Run `bash deploy.sh validate`, `build`, `rehearse`, then `promote` in that directory.
Rehearsal restores a backup into a uniquely named temporary database and compares protected
row hashes and idempotence. Promotion takes a fresh backup with the API stopped, applies
all SQL in one transaction, and restarts only the Speed Reading API/frontend.
Run `monitor.sh` for 30 samples over 15 minutes and check public TLS routes/assets.

Rollback: stop the new API; inspect logs and `speedreading-before.dump` checksum. Apply
`rollback-all.sql` with `psql -X -v ON_ERROR_STOP=1 -U eduplatform -d speedreading_owned_db`
via postgres container stdin. It restores catalog settings and deactivates new Skimming
texts without deleting student history. Set a scoped compose override for the image IDs
saved in `previous-api-image.txt` and `previous-web-image.txt`; reuse the recorded compose
file chain and `--no-deps --pull never`. Do not restore the whole production database over
new student activity, reset the dirty VPS source checkout, or prune shared volumes.
