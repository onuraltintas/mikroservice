# Assessment Takistoskop initialization fix — 2026-10-12

## Cause and scope

The player required a pinned reading passage for every `text_stream` assessment.
Takistoskop instead receives server-owned number stimuli through validation calls;
its server session correctly contains no reading passage. The player now exempts
only the `text_stream` + `Tachistoscope` combination. RSVP and the other reading
engines retain the fail-closed passage requirement.

This is a frontend-only release. No API, schema, catalog, user progress or
assessment settings change is required. Unrelated course-management working-tree
changes are excluded.

## Verification

- RED: 9 of 20 new component tests reproduced the incorrect passage gate.
- GREEN: all 20 component tests passed across Child, Teen, YoungAdult and Adult;
  fresh/resumed sessions, absent reading passages, and reading-engine regression
  checks are included.
- Full Angular suite: 969 passed.
- Existing backend Tachistoscope / AssessmentThreeTask tests: 41 passed.
- Production Angular build passed. The existing stylesheet budget warning remains
  (128.99 kB against a 120 kB warning threshold); this patch changes no styles.
- Browser matrix: four catalog age fixtures × fresh/resumed session × desktop/mobile.
  Local browser tests: 16/16 passed. Published-frontend browser tests: 16/16 passed.
  The actual Angular engine runs all remaining rounds and displays saved results.
  API responses are controlled fixtures, so these tests do not claim a new live
  Identity login or a real user's persisted assessment result.

## Release procedure

The scoped deployment script builds on the exact running frontend image, retaining
old hashed assets for open tabs. It records the previous image digest, validates
nginx, verifies the new frontend health and public index bytes, and checks that all
other container IDs/images remain unchanged. Failed promotion rolls back only the
frontend and checks recovery health. Release artifacts retain the rollback target.

The browser configuration can also test the published frontend with
`ASSESSMENT_UI_BASE_URL=https://masterhizliokuma.com`. All API calls are intercepted
and service workers are blocked for this deterministic UI verification; no real
program enrollment or user result is created.

## Published result

- GitHub branch: `codex/platform-hardening`.
- Production image: `eduivme/speed-reading-frontend:assessment-tachistoscope-0b1e4156`.
- Production source revision: `0b1e41566ee05907cc1b590a82e8822e95846497`.
- Published at: `2026-10-12T17:51:02Z` (20:51 Turkey time).
- Frontend health and unchanged backend readiness: healthy.
- Public index SHA256 equals the packaged index:
  `5547579b08b471e49e11e9b53cc2e8b8be371cf5f5f0b808ec67e951a2450c4c`.
- All non-frontend container IDs/images remained unchanged, including PostgreSQL,
  Identity, Coaching, Notification and the Hızlı Okuma API.
- BuildKit initially rejected a bare local image ID during preparation, before
  any live change. The script now tags that exact local image for the build;
  the subsequent build/nginx checks and controlled promotion passed.
