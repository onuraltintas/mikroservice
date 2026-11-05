# KM3 topic minutes v1 (01.11.2026)

User decision (01.11.2026, "sınırı yükselt"): a topic may need up to **4320 minutes** (72 hours) instead of 1440; topics are
**not** split into sub-topics. The coaching service accepts up to 4320 minutes per topic and per outcome duration version
(migration `20261111090000_TopicMinutesCap4320`) and schedules a long topic as several tasks on several days, each within one
day (1440).

Releases c1ce79c and 5056431e of `content-packs/coaching-catalog-km3` derived each topic's minutes from the official ÖSYM
weights but stored at most 1440, so **39 topics** (35 ÖABT, 1 YDS, 3 YÖKDİL) were capped: ÖABT İlköğretim Matematik
"Analiz" (%36) got the same 1440 minutes as "Uygulamalı Matematik" (%16). This pack writes their uncapped estimates
(`catalog-review.csv` → `raw_minutes`, the same `build.mjs` method with the 4320 limit; the largest is 3960). The 44 KM3
topics stored at 1440 include **5 whose estimate is exactly 1440** (`OABT_SOS.1.03`, `OABT_SOS.1.04`, `OABT_TAR.1.07`,
`OABT_REH.1.01`, `OABT_BED.1.01`); they were never capped and do not change. Nothing else changes.

| File | What it does | Writes |
| --- | --- | --- |
| `topics.csv` | The 39 topics: topic and outcome id, code, exam, old and new minutes, current name. | – |
| `check.sql` | Per topic: `code|name|old → new|state` (to change, done, DRIFT: why); totals. | no (`BEGIN READ ONLY`) |
| `apply.sql` | The change below in one guarded transaction. | yes |
| `verify.sql` | Checks the result after `apply.sql`; prints `km3 minutes verify: ok`. | no (`BEGIN READ ONLY`) |
| `build.mjs` | Generates the files above from the KM3 model (`node build.mjs`, `--check` in the contract test). | – |

## What `apply.sql` changes

Records are matched **by id** (topic `topic:<code>`, outcome `outcome:<code>` of the KM3 pack), never by name: the panel
renamed 4 Okul Öncesi topics and 8 YDS/YÖKDİL outcomes.

- **Topic** `EstimatedMinutes` 1440 → the uncapped estimate.
- **Outcome note**: its minute sentence ("…; 3890 dk, planlayıcı sınırı nedeniyle 1440 dk kaydedildi; konu birden çok
  turda planlanmalı.") becomes "…; 3890 dk." — exactly what a fresh KM3 install writes.
- **Duration versions**: KM3 creates none (the planner uses the topic minutes). If the team published a version for one of
  these outcomes and its total is 1440, the outcome gets a **new published version** (number = highest + 1, `ReviewedStudy`,
  `ReviewedBy` = `-v reviewer_id`, the previous confidence) whose learning/practice/check minutes and topic shares are scaled
  to the new total (largest remainder, as the admin panel does when it rebases shares). **No version is withdrawn**, as in
  the admin publication flow: plans keep the version they were made with, the planner offers the newest published one.
- **Audit records** (`coaching."AdminAuditRecords"`, actor = reviewer, user agent `content-packs/coaching-catalog-km3-minutes/v1`):
  `CurriculumInlineEdit`/`topics` per topic (before/after minutes, like the panel's inline edit), and `OutcomeEstimate` +
  `OutcomePublish`/`StudyOutcome` per new version.

Guards: one transaction, `lock_timeout` 5 s, the KM3 importer lock (`hashtext('coaching-catalog-km3')`) and the admin catalog
lock (`4188, 100`). It **stops with "Nothing changed"** without a valid `-v reviewer_id`, before the 4320 migration is
deployed, and when a listed record drifted: topic missing or not KM3, minutes other than 1440 / the new value, an edited
outcome note, a missing or unapproved topic–outcome link, or a published version whose total is neither 1440 nor the new
value. Idempotent: a rerun changes nothing and writes no audit record. The KM3 pack keeps rerunning cleanly before and
after this pack (its guard counts the 1440 values as equal).

## Running it (production)

1. Deploy the coaching service with migration `20261111090000_TopicMinutesCap4320` (the pack refuses before it).
2. Take the usual database snapshot (docs/DB_BACKUP_RUNBOOK.md).
3. `docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U eduplatform -d "$POSTGRES_DB_COACHING" < check.sql`
   — expected: 39 lines `… to change`, then `km3 minutes check: 39 to change, 0 done, 0 drifted`.
4. `docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -v reviewer_id=<admin user id> -U eduplatform -d "$POSTGRES_DB_COACHING" < apply.sql`
   — expected NOTICEs: `before: km3 topics at 1440 44, to change 39, done 0, duration versions to add 0`,
   `changed: topics 39, outcome notes 39, duration versions 0, topic shares 0, audit records 39`,
   `after: km3 topics at 1440 5, over 1440 39, max 3960` (versions/audits are higher if the team timed some of these outcomes).
5. `… < verify.sql` — `km3 minutes verify: ok`.

Rollback: the migration's Down refuses while a topic needs more than 1440 minutes; to go back, set the 39 topics to 1440
in the admin panel (or restore the snapshot) first.

## Topics

<!-- topics:start -->
| Konu | Sınav | Ad | Önce | Sonra |
| --- | --- | --- | ---: | ---: |
| `OABT_TUR.1.01` | ÖABT | Anlama ve Anlatma Teknikleri | 1440 | 2880 |
| `OABT_TUR.1.02` | ÖABT | Dil Bilgisi ve Dil Bilimi | 1440 | 2160 |
| `OABT_IMAT.1.01` | ÖABT | Analiz | 1440 | 3890 |
| `OABT_IMAT.1.02` | ÖABT | Cebir | 1440 | 2590 |
| `OABT_IMAT.1.03` | ÖABT | Geometri | 1440 | 2590 |
| `OABT_IMAT.1.04` | ÖABT | Uygulamalı Matematik | 1440 | 1730 |
| `OABT_FEN.1.01` | ÖABT | Fizik | 1440 | 3455 |
| `OABT_FEN.1.02` | ÖABT | Kimya | 1440 | 3240 |
| `OABT_FEN.1.03` | ÖABT | Biyoloji | 1440 | 2810 |
| `OABT_SOS.1.01` | ÖABT | Tarih | 1440 | 3960 |
| `OABT_SOS.1.02` | ÖABT | Coğrafya | 1440 | 2160 |
| `OABT_TDE.1.01` | ÖABT | Eski Türk Dili ve Yeni Türk Dili | 1440 | 2700 |
| `OABT_TDE.1.02` | ÖABT | Türk Halk Edebiyatı | 1440 | 1800 |
| `OABT_TDE.1.03` | ÖABT | Eski Türk Edebiyatı | 1440 | 1800 |
| `OABT_TDE.1.04` | ÖABT | Yeni Türk Edebiyatı | 1440 | 2700 |
| `OABT_TAR.1.06` | ÖABT | Osmanlı Tarihi | 1440 | 1620 |
| `OABT_COG.1.01` | ÖABT | Fiziki Coğrafya | 1440 | 3600 |
| `OABT_COG.1.02` | ÖABT | Beşerî ve Ekonomik Coğrafya | 1440 | 3600 |
| `OABT_COG.1.03` | ÖABT | Kıtalar ve Ülkeler Coğrafyası | 1440 | 1800 |
| `OABT_MAT.1.01` | ÖABT | Analiz | 1440 | 3890 |
| `OABT_MAT.1.02` | ÖABT | Cebir | 1440 | 2590 |
| `OABT_MAT.1.03` | ÖABT | Geometri | 1440 | 2160 |
| `OABT_MAT.1.04` | ÖABT | Uygulamalı Matematik | 1440 | 2160 |
| `OABT_FIZ.1.01` | ÖABT | Mekanik | 1440 | 3240 |
| `OABT_FIZ.1.02` | ÖABT | Elektrik ve Manyetizma | 1440 | 2590 |
| `OABT_FIZ.1.04` | ÖABT | Dalgalar ve Optik | 1440 | 1730 |
| `OABT_FIZ.1.05` | ÖABT | Modern Fizik | 1440 | 2160 |
| `OABT_KIM.1.01` | ÖABT | Temel Kimya | 1440 | 3455 |
| `OABT_KIM.1.02` | ÖABT | Analitik Kimya | 1440 | 2160 |
| `OABT_KIM.1.03` | ÖABT | Anorganik Kimya | 1440 | 1730 |
| `OABT_KIM.1.04` | ÖABT | Organik Kimya | 1440 | 2160 |
| `OABT_BIO.1.01` | ÖABT | Hücre ve Metabolizma | 1440 | 1800 |
| `OABT_BIO.1.03` | ÖABT | İnsan ve Hayvan Biyolojisi | 1440 | 2520 |
| `OABT_BIO.1.06` | ÖABT | Genetik | 1440 | 1800 |
| `OABT_REH.1.02` | ÖABT | Psikolojik Danışma Kuram İlke ve Teknikleri | 1440 | 1800 |
| `YDS_EN.6.01` | YDS | Okuduğunu anlama (paragraf soruları) | 1440 | 2760 |
| `YOKDIL_FEN.7.01` | YÖKDİL | Okuduğunu anlama (paragraf soruları) | 1440 | 1795 |
| `YOKDIL_SAG.7.01` | YÖKDİL | Okuduğunu anlama (paragraf soruları) | 1440 | 1795 |
| `YOKDIL_SOS.7.01` | YÖKDİL | Okuduğunu anlama (paragraf soruları) | 1440 | 1795 |
<!-- topics:end -->

## Tests

- `node --test tests/E2E/coaching-catalog-km3-minutes.contract.test.mjs` — files current, the 39 rows against the KM3 model,
  guards present.
- `COACHING_CATALOG_KM3_TEST_DATABASE_URL=postgres://…/postgres COACHING_CATALOG_SNAPSHOT_DIR=<catalog export>
  node --test tests/E2E/coaching-catalog-km3-minutes.postgres.test.mjs` — production export + KM3 release 5056431e,
  activated and approved like production; check, refusals, apply, rerun, verify and the KM3 rerun.
