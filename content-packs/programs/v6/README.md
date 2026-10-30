# Program revision v6 — daily blocks and weekly weighting

Decisions 30.10.2026 (research summary in the project note `claude/speed-reading-program-review.md`):

- **Same blocks every day:** warm-up (one eye or attention exercise) → perception and chunking → reading → comprehension
  → vocabulary. The order is enforced by `blockOf` and `verify.sql`.
- **Weekly weighting follows the decided sequence:**
  1. Week 1: eye warm-ups, then perception speed with tachistoscope and peripheral letters every day.
  2. Week 2: chunking every day; tachistoscope and peripheral letters alternate.
  3. Week 3: chunked fast reading. Guided reading plus one speed tool for the level: regression reduction, text fading,
     RSVP, subvocalization or scanning.
  4. Week 4: real reading (free reading, skimming, scanning) plus strategies (visualization, error analysis at levels
     4–5, regression).
- **Warm-ups:** eye warm-ups take 4 of 7 days in weeks 1–2 and 2 of 7 in weeks 3–4. Attention (Hedef Avı, Schulte)
  takes the other days.
- **Comprehension:** one text with questions every day. Day 7 of each week is the measurement (`evaluation`).
- **Vocabulary:** every day, Child and Teen only. YoungAdult and Adult read a free text instead.
- **Daily load:** Child 6 tasks every day (168 tasks). Teen, YoungAdult and Adult 6 tasks on days 1–14 and 7 on days 15–28
  (182 tasks). Totals: 20 programs / 560 days / 3,570 tasks.
- **Levels:** slots stay at the program level, as before. A type is used only from the first level the live catalogue has
  for that age (`levelRange`). The ≥80% comprehension gate for moving up a level is a code change (package H15), not part
  of this pack.

The program descriptions are rewritten (`describe`), and the previous texts are kept for rollback.

**Active assignments** (decision 30.10.2026): days that are finished, already started or before the current day keep their
exercises. Every other day is rebuilt from the v6 day at the level that day already had, so the H7 adaptive offset is
kept. Child days 15–28 therefore go from 7 to 6 tasks. Logs, results, current day and counters are unchanged.

## Files

- `rules.mjs`: `buildPattern`, `describe`, `reviseAssignedSchedule`.
- `generate.mjs`: writes `programs.json` from v5.
- `snapshot.sql` and `prepare-release.mjs <snapshot.json> <private-dir>`: guarded apply, rollback and inventory scripts.
  They also patch `Description`. Private snapshots must not be committed.
- `verify.sql`, `verify-release.mjs <before.json> <after.json>`, `inventory.sql`.
- `rehearse.sh` (needs `RELEASE_DIR` and `CHECK_DATABASE`) and `deploy.sh` (needs `RELEASE_DIR`): same procedure as v5,
  with the runtime audit expecting 20/560/3570.

## Checks

```sh
node content-packs/programs/v6/generate.mjs
node --test content-packs/programs/v6/pack.test.mjs
PROGRAMS_V6_TEST_DATABASE_URL=postgres://… node --test tests/E2E/program-blocks-v6.postgres.test.mjs
```

Not applied to production. Live use needs separate release approval, a restored-backup rehearsal, and only the Speed
Reading API paused. It should go live together with or after H15, so the gate and the new days start together.
