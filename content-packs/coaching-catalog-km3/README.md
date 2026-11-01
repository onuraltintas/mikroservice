# Coaching catalog KM3 — AGS, ÖABT, YDS and YÖKDİL (English) drafts (01.11.2026)

Package KM3 of the coaching curriculum plan. Lessons → units → topics → outcomes with `EstimatedMinutes` and `YieldWeight`
for:

- **AGS** (exam `AGS`): the six sections of the 2026 ÖSYM distribution as six lessons.
- **ÖABT** (exam `OABT`): all 17 fields of the 2026 MEB-AGS guide, one lesson per field.
- **YDS** (exam `YDS`) and **YÖKDİL** Fen / Sağlık / Sosyal (exam `YOKDIL`), English only: a question-type and grammar-area
  tree, because ÖSYM publishes no topic list for these exams.

**Everything is passive and pending approval.** Lessons, units and topics are `IsActive = false`, every topic–outcome link is
`IsApproved = false`, and all lessons sit in a new, **unpublished** curriculum version `coaching-exam-prep-km3-2026`
(2026–2027). Students see nothing until the team approves it. Nothing has been applied to production.

## Files

| File | What it is |
| --- | --- |
| `catalog.json` | The authored content: every topic row, its official weight or question count, its source document and the duration inputs. **Edit this, then run `node build.mjs`.** |
| `build.mjs` | Builds the files below from `catalog.json` (`--check`: fails when they are stale; `--json`: the computed model). |
| `apply.sql` | Guarded, idempotent load (Coaching database, one transaction). Generated. |
| `verify.sql` | Read-only check with per-exam counts, active/approved counts and planned hours. Generated. |
| `catalog-review.csv` | One row per topic (weight, questions, YieldWeight, minutes, source URL) for the team's review. Generated. |
| `yks-equivalents.csv` | Reference only (not loaded): existing approved-catalog TYT/AYT/YDT topics with the same or overlapping scope, so existing study material can be reused. |

## Why a content pack and not the admin catalog import

The admin flow (`POST api/coaching-admin/catalog-imports/preview → approve → publish`, `CoachingCatalogImporter`) needs exactly
six legacy JSON files (lessons, units-derived, upper-subjects, subjects, lgs-programs, …) and maps a lesson's grade label
only to `LGS`, `TYT`, `AYT`, `YDT` or `TDP`. It cannot express the exams `AGS`, `OABT_*`, `YDS` or `YOKDIL`, topic
minutes, `YieldWeight`, outcomes, sources or a curriculum version. So this is a guarded pack, and the review and approval
happen in the admin panel afterwards (see below).

## Codes (aligned with KM1, PR #98, `feat/km1-exam-types`)

- Source of every row: `coaching-catalog-km3`. Row keys (`SourceId`): lesson `<CODE>`, unit `<CODE>.<n>`, topic and outcome
  `<CODE>.<n>.<nn>`. Ids are fixed (md5 of the key), so reruns hit the same rows.
- AGS lessons (`ExamCode` `AGS`): `AGS_SOZ`, `AGS_SAY`, `AGS_TAR`, `AGS_COG`, `AGS_EGT`, `AGS_MEV` (KM1 section codes).
- ÖABT lessons: `OABT_TUR`, `OABT_IMAT`, `OABT_FEN`, `OABT_SOS`, `OABT_TDE`, `OABT_TAR`, `OABT_COG`, `OABT_MAT`, `OABT_FIZ`,
  `OABT_KIM`, `OABT_BIO`, `OABT_DIN`, `OABT_REH`, `OABT_SINIF`, `OABT_OKUL`, `OABT_BED`, `OABT_OZEL`. **Their `ExamCode` is the
  field code** (KM1 decision, 01.11.2026: the field marker the catalog lacked). `study_catalog_lessons.ExamCode` is a foreign
  key to `study_catalog_exams`, so the pack adds these 17 codes as exams named `ÖABT · <alan>`, **passive**, and only when the
  code does not exist yet. KM1's setup service still lists lessons of `AGS` and `OABT`; the follow-up that filters ÖABT lessons
  by the student's field (`PrimaryScoreType`) must read these codes.
- YDS: `YDS_EN` "YDS İngilizce" (`YDS`). YÖKDİL: `YOKDIL_FEN` "YÖKDİL Fen Bilimleri İngilizce", `YOKDIL_SAG` "YÖKDİL Sağlık
  Bilimleri İngilizce", `YOKDIL_SOS` "YÖKDİL Sosyal Bilimler İngilizce" (all `YOKDIL`: the 10-character code cannot carry the
  field, so each field is its own lesson).
- Editions (`official_curriculum_editions`): `km3-<code>-2026`, one per lesson, 2026–2027.

## Sources (every outcome carries its source URL)

| Content | Official document |
| --- | --- |
| AGS sections, weights and topic rows; the four laws under Mevzuat; ÖABT Özel Eğitim | ÖSYM, AGS ve ÖABT Özel Eğitim konu dağılımı, 08.01.2026 — https://dokuman.osym.gov.tr/pdfdokuman/2026/MEB-AGS/konudagilimi08012026.pdf |
| The other 16 ÖABT fields (topic rows and weights) | ÖSYM, MEB-AGS açıklama ve konu dağılımları, 16.01.2025 — https://dokuman.osym.gov.tr/pdfdokuman/2025/MEB-AGS/konudagilimi16012025.pdf. The 2026 document publishes only AGS and the new Özel Eğitim field, so the 2025 document is the latest official distribution for these 16 fields. |
| The 17 fields, test durations (90 / 70 min) | 2026 MEB-AGS Kılavuzu — https://dokuman.osym.gov.tr/web/2026/7/basvuru-kilavuzu-p9djhk-30105733.pdf |
| YDS question types and counts | 2026-YDS/1 İngilizce temel soru kitapçığı (05.04.2026) — https://dokuman.osym.gov.tr/pdfdokuman/2026/YDS-1/TSK/ing_yd1d05042026.pdf; same distribution in 2025-YDS/2 (16.11.2025) — https://dokuman.osym.gov.tr/pdfdokuman/2025/YDS-2/TSK/ingilizce_yd2d16112025.pdf |
| YÖKDİL question types and counts | 2026-YÖKDİL/2 (09.08.2026) İngilizce Fen — https://dokuman.osym.gov.tr/web/2026/8/ngilizce-fen-bilimleri-testi-ve-cevap-anahtari-10-4ehn8t-09134606.pdf, Sağlık — https://dokuman.osym.gov.tr/web/2026/8/ngilizce-saglik-bilimleri-testi-ve-cevap-anahtari-10-66hydo-09134606.pdf, Sosyal — https://dokuman.osym.gov.tr/web/2026/8/ngilizce-sosyal-bilimler-testi-ve-cevap-anahtari-10-8eh7a1-09134606.pdf |

Question counts per type were counted from the question-number ranges of each directive in these booklets:

| Type | YDS (2026-YDS/1) | YÖKDİL Fen / Sağlık / Sosyal (2026-YÖKDİL/2) |
| --- | --- | --- |
| Sözcük / ifade (sentence blank) | 1–6: 6 | 1–20: 20 (one directive for words and grammar) |
| Dil bilgisi | 7–16: 10 | inside 1–20 |
| Cloze test | 17–21, 22–26: 10 | 21–25, 26–30: 10 |
| Cümle tamamlama | 27–36: 10 | 31–41: 11 |
| Çeviri | 37–42: 6 (both directions, one directive) | 42–47 EN→TR: 6, 48–53 TR→EN: 6 |
| Okuduğunu anlama | 43–62: 20 (5 passages × 4) | 66–80: 15 (5 passages × 3) |
| Diyalog tamamlama | 63–67: 5 | — |
| Anlamca en yakın cümle | 68–71: 4 | — |
| Paragraf tamamlama | 72–75: 4 | 54–59: 6 |
| Anlam bütünlüğünü bozan cümle | 76–80: 5 | 60–65: 6 |
| Total | 80 | 80 (identical in all three fields) |

YÖKDİL has no dialogue and no restatement questions, twice as many translation items and shorter reading sets.

## Method (koçluk tahmini)

**YieldWeight (1–5)** comes from the topic's approximate share of its own test: ≥8% → 5, ≥6% → 4, ≥4% → 3, ≥2% → 2, else 1.
- ÖABT: the official per-topic weight. Where a test part is a list without weights (Sınıf Öğretmenliği Alan Eğitimi %66,
  Okul Öncesi Alan Eğitimi %40) the part's weight is split equally over the listed rows.
- AGS: ÖSYM weights sections only, so the section weight is split equally over its topics (approximate, labelled).
- YDS / YÖKDİL: question count ÷ 80. The 12 grammar areas share the grammar questions equally and get +1 because grammar
  also carries cloze and sentence-completion items.

**EstimatedMinutes**: each test gets a preparation budget (one full pass from scratch to exam level): AGS 150 h; ÖABT 180 h for
the 90-minute tests (İlköğretim Matematik, Fen Bilimleri, Matematik, Fizik, Kimya) and 150 h for the 70-minute tests; YDS
160 h; each YÖKDİL field 140 h. A topic gets `budget × (share × size) / Σ(share × size)` of its test, rounded to 5 minutes,
at least 30. `size` is 1 unless a reason is recorded (e.g. Paragrafta Anlam 1,5, Sınıf Yönetimi 0,75); ÖABT topics use 1
because ÖSYM's own weights already reflect their size. The planner refuses a topic over 1440 minutes ("Bir konu için
toplam süre 1440 dakikayı geçemez"), so larger estimates are stored as 1440 and the outcome note says the topic must be
planned in several rounds. Each outcome note spells out its own calculation.

<!-- totals:start -->
| Sınav | Ders | Ünite | Konu (= kazanım) | Planlanan süre (konu sınırı 1440 dk) | Tahmin (sınırsız) | 1440 dk ile sınırlanan konu |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| AGS | 6 | 6 | 29 | 149,7 saat | 149,7 saat | 0 |
| ÖABT | 17 | 20 | 146 | 2.056,1 saat | 2.699,9 saat | 35 |
| YDS | 1 | 10 | 22 | 137,8 saat | 159,8 saat | 1 |
| YÖKDİL | 3 | 21 | 60 | 402,5 saat | 420,3 saat | 3 |

| Ders | Konu | Planlanan | Tahmin |
| --- | ---: | ---: | ---: |
| AGS_SOZ — Sözel Yetenek (AGS) | 5 | 28,1 saat | 28,1 saat |
| AGS_SAY — Sayısal Yetenek (AGS) | 3 | 29,1 saat | 29,1 saat |
| AGS_TAR — Tarih (AGS) | 4 | 11,3 saat | 11,3 saat |
| AGS_COG — Türkiye Coğrafyası (AGS) | 2 | 10,7 saat | 10,7 saat |
| AGS_EGT — Eğitim Bilimleri ve Türk Millî Eğitim Sistemi (AGS) | 11 | 56,3 saat | 56,3 saat |
| AGS_MEV — Mevzuat (AGS) | 4 | 14,3 saat | 14,3 saat |
| OABT_TUR — Türkçe (ÖABT) | 7 | 114 saat | 150 saat |
| OABT_IMAT — İlköğretim Matematik (ÖABT) | 4 | 96 saat | 180 saat |
| OABT_FEN — Fen Bilimleri (ÖABT) | 6 | 93,5 saat | 179,9 saat |
| OABT_SOS — Sosyal Bilgiler (ÖABT) | 4 | 96 saat | 150 saat |
| OABT_TDE — Türk Dili ve Edebiyatı (ÖABT) | 4 | 96 saat | 150 saat |
| OABT_TAR — Tarih (ÖABT) | 9 | 147 saat | 150 saat |
| OABT_COG — Coğrafya (ÖABT) | 3 | 72 saat | 150 saat |
| OABT_MAT — Matematik (ÖABT) | 4 | 96 saat | 180 saat |
| OABT_FIZ — Fizik (ÖABT) | 5 | 114 saat | 180 saat |
| OABT_KIM — Kimya / Kimya Teknolojisi (ÖABT) | 5 | 117,6 saat | 180 saat |
| OABT_BIO — Biyoloji (ÖABT) | 6 | 120 saat | 150 saat |
| OABT_DIN — Din Kültürü ve Ahlak Bilgisi / İmam-Hatip Lisesi Meslek Dersleri (ÖABT) | 15 | 150 saat | 150 saat |
| OABT_REH — Rehberlik (ÖABT) | 8 | 144 saat | 150 saat |
| OABT_SINIF — Sınıf Öğretmenliği (ÖABT) | 15 | 150 saat | 150 saat |
| OABT_OKUL — Okul Öncesi (ÖABT) | 16 | 150 saat | 150 saat |
| OABT_BED — Beden Eğitimi (ÖABT) | 16 | 150 saat | 150 saat |
| OABT_OZEL — Özel Eğitim (ÖABT) | 19 | 150 saat | 150 saat |
| YDS_EN — YDS İngilizce | 22 | 137,8 saat | 159,8 saat |
| YOKDIL_FEN — YÖKDİL Fen Bilimleri İngilizce | 20 | 134,2 saat | 140,1 saat |
| YOKDIL_SAG — YÖKDİL Sağlık Bilimleri İngilizce | 20 | 134,2 saat | 140,1 saat |
| YOKDIL_SOS — YÖKDİL Sosyal Bilimler İngilizce | 20 | 134,2 saat | 140,1 saat |
<!-- totals:end -->

## Gaps and things to check before approval

- **Document hashes.** The PDFs could not be downloaded from the authoring environment (ÖSYM is reachable only through a
  text reader), so `official_curriculum_editions."DocumentSha256"` holds the placeholder `000…0` (64 zeros). `verify.sql`
  prints `pending PDF hashes: N editions`. Download each PDF, `sha256sum` it and set the hash in the admin panel
  (edition edit) before publishing.
- **ÖABT topics are the official areas, not finer units.** ÖSYM publishes 3–19 rows per field (e.g. İlköğretim Matematik:
  Analiz, Cebir, Geometri, Uygulamalı Matematik). No finer official list exists, so none was invented; 35 broad ÖABT topics
  hit the 1440-minute planner limit.
- **Open lists.** Sınıf Öğretmenliği and Okul Öncesi "Alan Eğitimi" lists end with "vb." in the official document.
- **Din Kültürü.** The table has three column groups (Ortak Alan Bilgisi; Temel İslam Bilimleri; İslam Tarihi, Felsefe ve
  Din Bilimleri) with totals %20 / %80; the column of each value could not be read reliably, so the 15 rows are one unit and
  Fıkıh carries both of its values (%4 + %10 = %14).
- **Coaching estimates (labelled in every note):** the 3 + 3 split of YDS translation items (the directive covers both
  directions; item 37 is EN→TR and item 42 TR→EN in 2026-YDS/1), the 10 + 10 word/grammar split of YÖKDİL items 1–20, the
  12 grammar areas and their sizes, the budgets and size factors.
- **AGS Mevzuat** has four rows; "İnsan Hakları Hukuku" is part of the Anayasa row in the document and stays there.
- No outcome duration estimates (`outcome_duration_estimates`) are created: the planner uses the topic minutes. The team can
  add estimates per outcome later in the admin panel.

## Running it

Prerequisite: KM1's exams `AGS`, `OABT`, `YDS`, `YOKDIL` (`content-packs/exam-calendar/2027/apply.sql`). Otherwise the pack
stops with `KM1 exams missing` and changes nothing.

1. Take the usual database snapshot (docs/DB_BACKUP_RUNBOOK.md).
2. `docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U eduplatform -d "$POSTGRES_DB_COACHING" < apply.sql`
   — prints `before: …`, `inserted: …` and `after: …`.
3. `docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U eduplatform -d "$POSTGRES_DB_COACHING" < verify.sql`
   — one line per exam (`exam|lessons|units|topics|outcomes|N active|N approved|planned hours`), then `km3 verify: ok`.

Guards: one transaction with a 5 s lock timeout and the same advisory lock as the catalog importer; stops if the team
permanently deleted a record of this source; stops (changing nothing) if a stored row with one of the pack's ids or keys has
different content. A rerun inserts only what is missing and keeps every activation and approval the team made.

## Approval in the admin panel

"Ders ve Müfredat Yönetimi": review the lessons, units and topics (names, minutes, weights) and activate them; approve the
topic–outcome links; activate the exams (`AGS`, `OABT`, `YDS`, `YOKDIL` from KM1 and the `OABT_*` field exams); set the
document hashes; then publish the curriculum version `coaching-exam-prep-km3-2026`. Before approval, records can simply be
left passive or permanently deleted in the panel; the pack will not bring deleted records back.

## Tests

- `node --test tests/E2E/coaching-catalog-km3.contract.test.mjs` — the data against the official numbers, KM1 codes, sources,
  limits and that the generated files are current (with `COACHING_CATALOG_SNAPSHOT_DIR` also the YKS-equivalent ids).
- `COACHING_CATALOG_KM3_TEST_DATABASE_URL=postgres://…/postgres COACHING_CATALOG_SNAPSHOT_DIR=<catalog export>
  node --test tests/E2E/coaching-catalog-km3.postgres.test.mjs` — applies the pack on a database seeded from the read-only
  production catalog export (`schema.sql` + CSVs; never committed).
