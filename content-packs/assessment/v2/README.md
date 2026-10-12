# Assessment v2: three-task pilot

Only the twelve assessment-only catalog entries change. Normal training,
programs, historical answers, existing sessions and pinned assessment forms are
not rewritten. A previously opened assessment continues using its original
configuration. This protocol is a product pilot, not a validated clinical test
or an age-normed reading assessment.

| Age group | Tachistoscope scored trials | Fixed block exposures | Visual scored trials | Relative visual distances | Comprehension questions |
|---|---:|---|---:|---|---:|
| Child | 18 (3 × 6) | 1000 / 750 / 500 ms | 18 | 8 / 12 / 16 | 5 |
| Teen | 24 (3 × 8) | 800 / 600 / 400 ms | 24 | 8 / 13 / 18 | 6 |
| Young adult | 30 (3 × 10) | 750 / 500 / 300 ms | 30 | 8 / 13 / 18 | 8 |
| Adult | 30 (3 × 10) | 750 / 500 / 300 ms | 30 | 8 / 13 / 18 | 8 |

Tachistoscope and Visual Expansion start with three **unscored** practice
trials. Tachistoscope uses unique three-digit numbers. Visual Expansion shows
two horizontal letters for 200 ms; distances are relative screen positions,
not calibrated degrees of visual field. Neither exercise adapts difficulty to
answers. Final reports give per-block recognition accuracy, excluding practice.

Comprehension uses one active, age-specific database text at the existing
assessment difficulty. It requires the configured number of valid multiple
choice questions with visible A–D options. Least-used suitable content is
preferred; text, question IDs and choices are pinned for reproducibility.
Reading is self-paced. Questions are untimed and allow reopening the text,
without answer keys or immediate correctness. First-reading WPM excludes
question/review time. Text-available comprehension and first-reading speed are
reported separately; they must not be interpreted as comprehension at that
speed or combined into a weighted WPM.

## Controlled release

`apply.sql` checks the exact legacy/new baseline and required content before an
atomic update. It is idempotent. `rollback.sql` restores prior catalog settings
and descriptions without modifying pinned forms. Catalog row versions remain
monotonic on rollback.

`inventory.sql` fingerprints every other Speed Reading table and every
non-target exercise. `verify.sql` checks all twelve expected settings.
`infrastructure/scripts/deploy-speed-reading-assessment-v2.sh` builds only the
Speed Reading API/web images, preserves old hashed web assets, rehearses apply,
idempotency and rollback against an isolated backup clone, then stops only the
API for a fresh database backup and atomic promotion. Health failures trigger
catalog/image rollback; all other running container identities must remain
unchanged. No Identity or Coaching data is migrated.

## Checks

- Unit: `AssessmentBlockProtocolTests`, legacy/new `AssessmentThreeTaskTests`.
- PostgreSQL: `AssessmentBlockProtocolPostgresTests`, `AssessmentV2CatalogPostgresTests`.
- Browser unit: `assessment-blocks.spec.ts`, Tachistoscope/visual safety regressions.
- Desktop/mobile: `speed-reading-assessment-tachistoscope-ui.spec.mjs`, legacy/new
  four-age start/resume/completion and age-specific comprehension text-review checks.

Browser fixtures isolate Identity and API transport; PostgreSQL checks verify
real server state, scoring and immutable snapshots independently. These checks
are not evidence of pedagogical or clinical efficacy.
