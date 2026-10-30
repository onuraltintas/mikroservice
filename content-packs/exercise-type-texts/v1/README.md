# Exercise type texts v1

Decision 30.10.2026: exercise descriptions should say what each exercise practises, in accurate and meaningful wording.
They should not make unproven promises such as "strengthens the eye muscles", "widens the visual field" or "increases
the brain's perception capacity". The research summary is in the project note `claude/speed-reading-program-review.md`.

- `texts.json`: new `description` for the 20 exercise types the programs use. Display names are unchanged.
- `snapshot.sql`: read-only export of the active exercise types.
- `prepare-release.mjs <snapshot.json> <private-dir>` writes:
  - `apply.sql`: guarded and idempotent. It stops if a description changed after the snapshot.
  - `rollback.sql`: restores the exact previous description, version and updated fields.
- Exercise types outside the list (N-back/Focus, exam simulation) are not touched.

The student app texts with the same kind of claims (settings, assessment intro, reading samples, the player title) are
changed in code. `tests/E2E/support/speed-reading-claims.contract.test.mjs` keeps them out.

## Checks

```sh
node --test content-packs/exercise-type-texts/v1/pack.test.mjs
TYPE_TEXTS_TEST_DATABASE_URL=postgres://… node --test tests/E2E/exercise-type-texts.postgres.test.mjs
```

Not applied to production. Live use: snapshot → prepare → apply (no API stop is needed; one-table text update) → check the
catalogue in the admin panel. Requires release approval.
