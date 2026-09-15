# Low-density reading question supplement v1

This pack adds five questions to each of the 16 active reading texts that had
only three questions. It preserves the existing questions and uses order
indexes 4–8, so every target text reaches eight questions.

The generated question ids are deterministic. Re-running
`tools/reading-content-repair/generate_low_density_supplement.py` produces the
same ids and the SQL skips rows that already exist in either supported store:

- `public."ReadingQuestions"` (legacy store)
- `speed_reading.reading_questions` (owned store)

The SQL is intentionally a content migration artifact. It should be reviewed
and applied through the normal database release process; it does not run during
application startup. The rollback script soft-deletes only these 80 ids so
foreign-key references remain safe.

The generator verifies:

- 16 texts and 80 new questions;
- five new rows per text, with order indexes 4–8;
- non-empty explanations and four distinct options;
- no correct answer is uniquely the longest or shortest option;
- the final eight-question set has exactly two A, B, C and D answers per text.

Run locally with:

```powershell
python tools/reading-content-repair/generate_low_density_supplement.py
python -m unittest tools/reading-content-repair/test_generate_low_density_supplement.py -v
```

The generated audit report is `low-density-supplement.audit.json` and the
database artifacts are `low-density-supplement.sql` and
`low-density-supplement.rollback.sql`.
