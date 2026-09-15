# Reading question repair v1

The JSON files in this directory are manually reviewed question packs. `catalog-length-balanced.jsonl` is the deterministic overlay for the remaining active questions whose correct option was uniquely the longest. It changes one distractor at a time, keeps the answer key unchanged, and records the original options and source lengths for rollback and editorial review.

Validation must pass before applying a release:

```text
python tools/reading-content-repair/validate_catalog_overlay.py .tmp-current-reading-catalog.jsonl content-packs/reading-question-repair/v1 content-packs/reading-question-repair/v1/catalog-length-balanced.jsonl --report artifacts/content-audits/reading-catalog-overlay-20260915.json
```

Apply the generated SQL only after taking a database snapshot. The statement is idempotent and updates active matching question ids in either supported reading-question table.
