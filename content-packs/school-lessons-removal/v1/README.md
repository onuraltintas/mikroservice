# School lessons removal v1 (KM8, 01.11.2026)

User decision (01.11.2026): the coaching catalog keeps **only exam lessons**. The lessons with exam code TYT, AYT, YDT, LGS
and TDP (28 in production on 01.11.2026) and the KM3 lessons (AGS, OABT_*, YDS, YOKDIL) stay. The **141 lessons without an
exam code** go: the grade 1–12 school lessons and the exam-less duplicates Mantık, Psikoloji, Sosyoloji, YDT İngilizce and
YDT Arapça. In the 01.11.2026 export they have 618 units and 3,519 topics and no outcome links, official program bindings
or YKS scopes.

The same release removes the School ("Okul derslerimi desteklemek"), General ("Genel çalışma") and Undecided ("Henüz karar
vermedim") study purposes from new saves; students who still have one are asked to choose their exam (no data change).

| File | What it does | Writes |
| --- | --- | --- |
| `lessons.csv` | The 141 lessons (id, name) from the 01.11.2026 export. | – |
| `check.sql` | Per lesson: units, topics, every student reference, and whether it would be deleted or deactivated; totals; list drift. | no (`BEGIN READ ONLY`) |
| `apply.sql` | Backs up, deletes the unused rows, deactivates the used ones, writes audit records. | yes, one guarded transaction |
| `verify.sql` | Checks the result after `apply.sql`. | no (`BEGIN READ ONLY`) |
| `restore.sql` | Puts every deleted row back from the backup and reactivates what `apply.sql` deactivated. | yes, one guarded transaction |
| `build.mjs` | Copies `lessons.csv` into the four SQL files (`node build.mjs`, `--check` in the contract test). | – |

## Rules

- **Deleted** (permanently): a lesson, unit or topic that nothing uses.
- **Deactivated** (`IsActive = false`, never deleted): a row that student data points at, and the parent topics, unit and
  lesson above it. Student data that can point at the catalog (Coaching model, every reference to
  `study_catalog_lessons/units/topics`):

  | Student data | Column | Points at |
  | --- | --- | --- |
  | Course preferences | `student_course_preferences."LessonId"` (FK) | lesson |
  | Declared topic states | `student_topic_states."TopicId"` (FK) | topic |
  | Study plan tasks | `study_plan_tasks."TopicId"` (FK) | topic |
  | Weekly plan tasks | `study_plan_tasks."WeeklyLessonId"` (no FK) | lesson |
  | Weekly drafts | `study_weekly_drafts."SlotsJson"[*].LessonId` (jsonb) | lesson |
  | Exam lesson answers | `exam_results.lesson_answers[*].LessonId / TopicId` (jsonb, guarded by the exam-history trigger) | lesson, topic |
  | Practice exam topic analyses | `exam_results.topic_analysis.Topics[*].TopicId` (jsonb) | topic |

  Assignments (`Subject` is free text), goals (exam code, target school / program), plan task names
  (`LessonNameSnapshot`) and the study plan outcome links (outcomes, not topics) hold no catalog ids and stay as they are.
  Catalog-internal links (topic–outcome links and shares, YKS scopes, official program bindings) must not exist on these
  lessons: `apply.sql` stops if they do.
- Every deleted or deactivated row is first copied, **with all its columns**, into
  `coaching.removed_school_catalog_20261101_lessons`, `…_units` and `…_topics` (plus `RemovalAction`, `RemovedAt`).
- Every deleted or deactivated row gets a record in `coaching."AdminAuditRecords"`, as the admin catalog writes do:
  `CatalogPermanentDelete` (method DELETE) or `CatalogStatus` (PUT, `isActive: false`), resource type `Lessons` / `Units` /
  `Topics`, payload `{reason, name, source, sourceId, pack}`. The `CatalogPermanentDelete` records are also the tombstones
  the admin catalog import checks: re-importing the old approved-catalog files that contain these lessons is refused.

## Operator steps (production: only with the user's go-ahead)

1. Take the usual database snapshot (docs/DB_BACKUP_RUNBOOK.md).
2. Run the read-only check and **report its output to the user before going further**:

   ```sh
   docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U eduplatform -d "$POSTGRES_DB_COACHING" < check.sql
   ```

   - `list|ok|…` — the list matches the catalog. `list|drift|…` — something changed (a listed lesson renamed, given an exam
     code or missing; an exam-less lesson that is not on the list; catalog links): stop and ask; `apply.sql` refuses.
   - one `lesson|…` line per lesson: id, grade, name, units, topics, then the references (course preferences, topic states,
     plan task topics, plan task lessons, weekly draft slots, exam lesson answers, practice topic analyses), the action
     (`delete` / `deactivate`) and the units/topics deleted and deactivated under it.
   - `total|lessons delete … deactivate …|units …|topics …|student references …` — the numbers to report.

   On the 01.11.2026 export (no student data in it) it reports
   `total|lessons delete 141 deactivate 0|units delete 618 deactivate 0|topics delete 3519 deactivate 0|student references 0`.
   Production has student data, so expect some lessons to be deactivated instead.
3. After the user approves those numbers, apply (optionally recording who ran it):

   ```sh
   docker exec -i -e PGOPTIONS='-c km8.actor=<admin user id>' postgres \
     psql -X -qAt -v ON_ERROR_STOP=1 -U eduplatform -d "$POSTGRES_DB_COACHING" < apply.sql
   ```

   It prints `before:`, `deleted:`, `deactivated:`, `after:` and `audit:` NOTICEs; `deleted` + `deactivated` must equal the
   check's totals. It takes the admin catalog lock (`pg_advisory_xact_lock(4188, 100)`), waits at most 5 s for table locks
   and rolls back with "Nothing changed" on drift or when an after-check fails (an exam lesson changed; an exam-less row
   still active). Running it again changes nothing.
4. Verify: `… < verify.sql` prints `school-lessons-removal verify: ok | …`.

## Rollback

```sh
docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U eduplatform -d "$POSTGRES_DB_COACHING" < restore.sql
```

It inserts every deleted lesson, unit and topic back exactly as it was (parents first), sets the deactivated rows back to
their backed-up `IsActive`, writes `CatalogRestore` / `CatalogStatus` audit records and marks the backup rows `restored`.
Running it again changes nothing; `apply.sql` can run again afterwards. The backup tables are kept; drop them only after
the user confirms the removal is final.

## Tests

- `node --test tests/E2E/school-lessons-removal.contract.test.mjs` — list, generated blocks, guards.
- `SCHOOL_LESSONS_REMOVAL_TEST_DATABASE_URL=postgres://… COACHING_CATALOG_SNAPSHOT_DIR=/path/to/coaching-catalog-20261101 node --test tests/E2E/school-lessons-removal.postgres.test.mjs`
  — the export (never committed) plus synthetic student references to seven school lessons: check counts, apply deletes /
  deactivates, KEEP lessons untouched, idempotency, restore round trip, refusal on drift.
- `SchoolLessonsRemovalPostgresTests` (Coaching.Curriculum) — the same files on the fully migrated Coaching schema.
