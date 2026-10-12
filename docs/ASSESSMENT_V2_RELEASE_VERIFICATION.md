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
Malformed metadata and blank answer choices are excluded from new forms.
Legacy six-round forms remain valid.

The Angular build retains a pre-existing stylesheet-size warning (128.99 kB
versus a 120 kB warning threshold). It is not a build failure and this release
does not expand or refactor that stylesheet.

Production deployment results will be recorded after the backup rehearsal and
health checks. Passing functional tests does not establish clinical validity
or pedagogical efficacy; these are pilot product parameters.
