# Assessment v2 verification — 2026-10-12

Scope: the four-age assessment Tachistoscope, Visual Expansion and
Comprehension protocols. No normal-training or other-product release.

## Local checks

| Check | Result |
|---|---|
| Speed Reading application unit suite | 1,278 passed |
| Browser unit safety/protocol checks | 56 passed |
| Real PostgreSQL protocol and catalog checks | 9 passed |
| Desktop/mobile legacy and v2 Tachistoscope start/resume/completion | 32 passed |
| Desktop/mobile four-age comprehension review/no-immediate-feedback | 8 passed |
| API Release publish and Angular production build | Passed |

The PostgreSQL checks include fixed block values, three unscored practice
trials, unique numbers, four-age scored counts, pause/resume, sanitized answers,
valid age-specific questions, first-reading WPM excluding question time,
idempotent/atomic catalog updates, rollback and unchanged immutable snapshots.
Malformed metadata, blank answer choices and blank text bodies/titles are
excluded from new forms. The real PostgreSQL tests also reject a catalog
transition when only blank-content candidates remain.
Legacy six-round forms remain valid.

The Angular build retains a pre-existing stylesheet-size warning (128.99 kB
versus a 120 kB warning threshold). It is not a build failure and this release
does not expand or refactor that stylesheet.

## Production release

- Application revision: `67eb8d6abba651d6099702fbea0627ffe6dcbdb0`, pushed to
  `origin/codex/platform-hardening` (implementation commits `1a3b4588`,
  `dbffae20`, `67eb8d6a`).
- Promoted at `2026-10-12T19:14:14Z` on the existing production VPS.
- Release directory: `/var/lib/eduivme/releases/assessment-v2-20261012-67eb8d6a`.
- Backup clone rehearsal: apply changed 12 assessment settings; a second apply
  changed zero; verification and catalog rollback passed. The temporary clone
  was dropped after the rehearsal.
- Fresh production Speed Reading database backup retained in the release
  directory. No Identity or Coaching database changes were made.
- Production apply updated all 12 expected assessment-only settings.
- Protected table and non-target exercise fingerprints matched immediately
  before/after the transition. All other running container identities/images
  matched; only the Speed Reading API and frontend were recreated.
- API readiness returned `Healthy`; frontend Docker health passed. Public
  homepage, `/student/assessment` and `/ngsw.json` returned HTTP 200; the public
  page served the new `main-KDA5QGBP.js` bundle.
- Authenticated browser check: the new adult catalog showed 3 practice plus
  30 measurement trials. Resuming the already-pinned legacy attempt still
  used six trials and its original 900 ms setting; it was left paused. No
  answers were submitted. New four-age complete flows were tested locally
  with isolated browser API fixtures and real PostgreSQL server tests, not
  by replacing the user's live attempt or creating new production assessments.
- API/frontend images: `eduivme/speed-reading-service:assessment-v2-20261012-67eb8d6a`
  and `eduivme/speed-reading-frontend:assessment-v2-20261012-67eb8d6a`.

All 1,383 targeted test cases passed. Passing functional tests does not
establish clinical validity or pedagogical efficacy; these are pilot product
parameters.
