# Coaching catalog KM2 v1 — YKS/LGS gaps (01.11.2026)

Package KM2 of the coaching curriculum plan. Everything here was checked against the read-only production catalog
snapshot of 01.11.2026 (`coaching-catalog-20261101`, 15 CSVs + `schema.sql`; not committed). Nothing has been applied
to production. New content is **passive** until the coaching team approves it in the admin panel.

| # | Gap in the snapshot | Verified | What this pack does |
| --- | --- | --- | --- |
| 1 | AYT Tarih: 73 leaf topics, only 26 with outcome links and YKS scopes | yes (73 / 26 / 26) | 206 pending links for the other 47 leaves; durations + scopes only after review |
| 2 | TDP "Matematik (Destek)": 54 topics, no `EstimatedMinutes` on the 28 leaves | yes (54 / 28 empty; `YieldWeight` is the default 3 everywhere) | 28 "koçluk tahmini" minute proposals, opt-in step |
| 3 | LGS Matematik: 19 topics vs 30–46 in the other LGS lessons | yes (19 topics, 13 leaves) | nothing to add — the 8th-grade program is complete (see below) |
| 4 | LGS schools (`target_catalog`, `CatalogType=School`, 3,150 rows): no year | yes (0 / 0) | no year invented; read-only check query + note |

## Why a guarded SQL pack and not the admin catalog import

`POST api/coaching-admin/catalog-imports/{preview,approve,publish}` (`CoachingCatalogImporter`) only **adds** lessons,
units, topics and LGS schools; it cannot write topic–outcome links, YKS scopes, outcome durations or a topic's
`EstimatedMinutes`, and it treats any change to an existing row as a conflict. The outcome import
(`CoachingOutcomeCatalogService.ImportAsync`) only creates a *new* edition — an existing edition such as
`yks-preparation-2026-tar` refuses any package whose hash differs from the original. So none of items 1–2 can be expressed
there. (Separately: the catalog-import controller requires exactly 6 files while `CatalogPreflight` and the admin page
accept exactly 5, so that endpoint currently rejects every request — reported to the team, not changed here.)

The pack therefore writes the same rows the admin services would write, in the same shapes and states:
links are created with `IsApproved=false` and no reviewer exactly like `StudyCatalogTopicOutcome.Create`; the team then
approves or rejects each link with the existing admin review (`ReviewMappingAsync`).

## Files

| File | Writes | When |
| --- | --- | --- |
| `verify.sql` | no (`BEGIN READ ONLY … ROLLBACK`) | before and after every step; fails on a broken invariant |
| `01-stage-ayt-tarih-links.sql` | 206 pending links (`study_catalog_topic_outcomes`) | any time; repeatable |
| `02-activate-ayt-tarih.sql` | duration versions, shares, YKS scopes for **reviewed** topics only | after the team review; repeatable |
| `03-apply-tdp-estimates.sql` | `EstimatedMinutes` of the 28 TDP leaves (only where empty) | after the team approves the CSV; repeatable |
| `lgs-school-year-check.sql` | no | for item 4 |
| `data/ayt-tarih-links.csv`, `data/tdp-matematik-estimates.csv` | — | the reviewed proposals, human-readable |
| `build.py`, `templates/` | — | regenerate the SQL + CSV from a snapshot |

Every write step runs in one transaction with `lock_timeout`, an advisory lock, and guards that refuse (and change
nothing) when the catalog no longer matches the reviewed snapshot. Each prints a before/after line.

## Runbook

1. Take the usual database snapshot (`docs/DB_BACKUP_RUNBOOK.md`).
2. `docker exec -i postgres psql -X -qAt -v ON_ERROR_STOP=1 -U eduplatform -d "$POSTGRES_DB_COACHING" < verify.sql`
   Expected today: `ayt tarih: leaves 73 | linked 26 | km2 pending 0 | km2 approved 0 | with yks scope 26`.
3. `… < 01-stage-ayt-tarih-links.sql` → `km2 stage after: inserted 206 | pending 206 | AYT Tarih leaves without links 0`.
   Students see no change: an unapproved link is never read by plans or topic pages, and no YKS scope is added.
4. The team reviews the 206 links in the admin panel (Kazanımlar → `yks-preparation-2026-tar` → konu eşleştirmeleri;
   every KM2 link's evidence starts with "KM2 önerisi"). `data/ayt-tarih-links.csv` lists them by topic.
5. `… -v reviewer_id=<reviewing admin's user id> -v reason='<gerekçe>' < 02-activate-ayt-tarih.sql`
   For each AYT Tarih topic whose KM2 links are **all** reviewed and at least one approved (and which has no scope yet):
   * every approved outcome gets a new published duration version (`ReviewedStudy`, the given reviewer). Its topic
     shares are the previous published shares, unchanged (TYT students keep their minutes), plus one share for the AYT
     topic equal to the TYT twin's share (koçluk tahmini; the 26 already linked AYT topics also carry their own share).
     This is what `EstimateAsync` + `PublishAsync` would produce and it satisfies `OutcomeScopeBudget.Validate`.
   * the topic gets the three YKS scopes the 26 linked AYT Tarih topics already have: EA `AYT.EDESOS1`, SÖZ `AYT.EDESOS1`
     (Tarih-1) and SÖZ `AYT.SOS2` (Tarih-2). Scopes come last because a scope alone makes the topic plannable for YKS students.
   A topic with a pending link is skipped untouched; run the step again after the remaining reviews.
   A link approved later on an already activated topic needs a duration version from the admin panel.
6. After the team approves `data/tdp-matematik-estimates.csv`:
   `… -v approval_reason='<gerekçe>' < 03-apply-tdp-estimates.sql` → `km2 tdp: … updated 28 | empty after 0`.
   (Alternatively enter the values by hand in Koçluk müfredatı → konu düzenleme.)
7. `verify.sql` again; keep the outputs with the release notes.

Rollback: restore the step-1 snapshot, or delete exactly the rows this pack wrote — links whose `Evidence` starts with
`KM2 önerisi`, estimates whose `Evidence` starts with `KM2 koçluk tahmini` (and their shares), scopes whose `Evidence`
starts with `KM2:` — and set the 28 TDP leaves back to `NULL`. Once plans have used a KM2 duration version
(`study_plan_task_outcomes` references it), withdraw it in the admin panel instead of deleting it.

## Item 1 — AYT Tarih

* Each of the 47 unlinked AYT Tarih leaves has exactly one TYT Tarih leaf with the same MEBİ title (punctuation-insensitive
  match). The TYT twin's 206 outcomes (all `ExamSkill`, edition `yks-preparation-2026-tar`, already bound to the
  "Tarih (AYT)" lesson) are the suitable existing outcomes, so no new outcome was needed.
* Test structure (ÖSYM, 2026 AYT question booklet, verified):
  "Bu testte sırasıyla Türk Dili ve Edebiyatı (1-24), Tarih-1 (25-34), Coğrafya-1 (35-40) alanlarına ait toplam 40 soru
  vardır." and "Bu testte sırasıyla Tarih-2 (1-11), Coğrafya-2 (12-22), Felsefe Grubu (23-34), Din Kültürü ve Ahlak
  Bilgisi (35-40) …". In this booklet Tarih-1 runs from early Turkish states to the Republic and its reforms, and Tarih-2
  from the science of history to the Second World War (only Tarih-2 has a WWII question). Both tests cover a wide range of
  periods and ÖSYM does not publish a split of the Tarih program between them, which is why every AYT Tarih topic gets both
  tests (as the 26 have). This is one booklet, not an official scope rule.
  Source: https://dokuman.osym.gov.tr/pdfdokuman/2026/YKS/TSK/yks_ayt_2026_kitapcik_kt12.pdf
* Like the existing YKS scopes, this is the 2026 exam structure, not a published 2027 scope.

## Item 2 — TDP Matematik (Destek) estimates ("koçluk tahmini")

Method (reproducible with `build.py`): for each TDP leaf, the **lower median of the `EstimatedMinutes` of the comparable
TYT Matematik leaves** listed in `data/tdp-matematik-estimates.csv` (our own content mapping, same mathematics at TYT
level); leaves with no comparable TYT leaf (Uzunluk ve Zaman Ölçme, Silindir, Koni, Çember ve Daire, Sıvı Ölçme,
Dönüşüm Geometrisi) use the lower median of all 128 TYT Matematik leaves (90). Result: 28 × 90 min.

* **Yüzdeler: 90, review decision** (content review 01.11.2026, B1; `build.py` → `TDP_OVERRIDES`, CSV `method`
  `review-decision`, `method_note` "tutarlılık: karşılaştırma kümesinde problem yaprağı olan diğer konular gibi 90"). Its
  two comparable TYT leaves are both "problem" leaves (115), so the median gave 115; Oran-Orantı and Kesirler also have a
  problem leaf in their comparison set and got 90, and a 7th-grade support topic is not harder than those. Production
  already holds 90: step 3 wrote 115 on 01.11.2026 and the team set 90 in the admin panel. Step 3 only fills empty
  values, so a rerun keeps 90.
* Watch list (B2): the broad leaves Kareköklü İfadeler, Doğrusal Denklemler, Özel Dörtgenler, Kümeler and Kesirler get
  90 as a starting value; revisit them with the first 4–6 weeks of real study times.

* MEB ders saati were considered and not used: they are class periods for a whole unit, not individual study minutes
  (the outcome import says the same), and the 5th–7th grade periods could not be fetched from mufredat.meb.gov.tr here.
  For the 10 leaves that match an 8th-grade unit, `lgs_reference_minutes` shows the LGS Matematik minutes for comparison.
* `YieldWeight` stays 3 (the default every TYT/AYT/LGS/TDP topic has today): TDP is a support package with no exam, so
  there is no exam frequency to encode.

## Item 3 — LGS Matematik

The 2027 LGS (2026–2027 school year) uses the current 8th-grade program: MEB's 2026–2027 Türkiye Yüzyılı Maarif Modeli
plans apply to grades 1, 2, 3 and 5, 6, 7 only ("2026-2027 eğitim öğretim yılında 1, 2, 3 ve 5, 6, 7. sınıflarda"),
https://tymm.meb.gov.tr/taslak-cerceve-planlari/temel-egitim — grade 8 moves in 2027–2028.

The edition `meb-2018-lgs-matematik-2026-pilot-v1` already holds the whole 8th-grade part of that program: 12 alt öğrenme
alanı (M.8.1.1 … M.8.5.1), 52 outcomes (source pages 73–78), 180 ders saati (= 5 h × 36 weeks), and all 52 outcomes are
approved on the 13 LGS Matematik leaves. The lower topic count is granularity (one leaf per MEB unit), not missing
content, so nothing is added. `verify.sql` prints this check. Open: MEB has not published a 2027 LGS kapsam announcement
that we could find; re-run the check when it does.

## Item 4 — LGS school year

Not established; nothing is written. The rows come from the MEBİ "Hedef belirle" export
(`data/target-catalog/2026/raw/mebi-lgs-full-20261020-export.json`), which has `"sourceYear": null` and
`"placementStage": null`; the import tool deliberately refuses to infer them. Comparing with year-labelled official MEB
rows already in the repo gives mixed results: the İzmir 2026 first-placement list (konakram.meb.k12.tr) matches some
schools to the cent (e.g. Çiğli Mehpare Yağcı AİHL 149.7576 → 149.76) but 52 of 135 compared programs differ, and the
2026 Menderes figures (415.685 / 311.926) differ from MEBİ (414.93 / 310.81). The MEBİ values are therefore not a single
known year. `lgs-school-year-check.sql` shows the state and these spot checks; set a year only from an official,
year- and stage-labelled export (see `data/target-catalog/2026/lgs-source-status.json`).

## Test

```
KM2_TEST_DATABASE_URL=postgres://user:pass@127.0.0.1:5432/postgres \
KM2_SNAPSHOT_DIR=/path/to/coaching-catalog-20261101 node --test tests/E2E/coaching-catalog-km2.postgres.test.mjs
```

It seeds a disposable database from the snapshot's `schema.sql` and CSVs (the export has no `outcome_duration_estimates`,
so the test rebuilds one published estimate per outcome from its shares), then checks counts, guards, idempotency, that
everything staged is pending/passive, and that `build.py` reproduces the committed files byte for byte.
