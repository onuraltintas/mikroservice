#!/usr/bin/env node
// KM3 (01.11.2026): builds the AGS, ÖABT, YDS and YÖKDİL catalog pack from catalog.json.
//   node build.mjs           writes apply.sql, verify.sql, catalog-review.csv and the README totals
//   node build.mjs --check   exits 1 when one of them is out of date
//   node build.mjs --json    prints the computed model (used by the contract test)
// Durations and weights are "koçluk tahmini" (coaching estimates); the method is in README.md and in each outcome note.
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const dir = fileURLToPath(new URL('.', import.meta.url));
const raw = readFileSync(dir + 'catalog.json', 'utf8');
const data = JSON.parse(raw);
const SOURCE = data.source;
const PACKAGE_SHA = createHash('sha256').update(raw).digest('hex');
const PENDING_PDF_SHA = '0'.repeat(64); // placeholder for a document without a recorded hash (README)
// The release that production runs (README "Production state"): its package hash and the names it wrote. The guard treats
// exactly these stored values as equal to this pack's, so a rerun neither stops on them nor changes them.
const PREVIOUS = data.previousRelease;
const uuid = key => { const h = createHash('md5').update(`${SOURCE}:${key}`).digest('hex');
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`; };
const examLabel = { AGS: 'AGS', OABT: 'ÖABT', YDS: 'YDS', YOKDIL: 'YÖKDİL' };
const round5 = x => Math.round(x / data.method.roundTo) * data.method.roundTo;
const fmtPct = x => (Math.round(x * 100) / 100).toString().replace('.', ',');
const band = pct => data.method.yieldBands.find(([min]) => pct >= min - 1e-9)[1];

// ---- model -------------------------------------------------------------------------------------------------------
function expandUnits(lesson) {
  const units = lesson.template ? data.templates[lesson.template] : lesson.units;
  return units.map(unit => {
    const topics = (unit.topics ?? []).map(t => ({ ...t }));
    if (unit.grammar) {
      const total = data.grammarAreas.reduce((s, g) => s + g.size, 0);
      for (const area of data.grammarAreas) topics.push({ name: area.name, q: unit.grammar / data.grammarAreas.length, size: area.size,
        grammar: true, qNote: `dil bilgisi sorularının (${unit.range}) on iki alana eşit bölünmüş payı; alan ayrımı koçluk analizi` });
      void total;
    }
    return { ...unit, topics };
  });
}

const lessons = data.lessons.map(lesson => {
  const doc = data.documents[lesson.doc];
  const units = expandUnits(lesson).map((unit, ui) => {
    const topics = unit.topics.map((topic, ti) => {
      let pct, pctNote;
      if (topic.pct !== undefined) { pct = topic.pct; pctNote = topic.pctNote; }
      else if (topic.q !== undefined) pct = topic.q * 100 / lesson.questions;
      else { pct = unit.sectionPct / unit.topics.length;
        pctNote = `bölüm ağırlığı %${fmtPct(unit.sectionPct)} konulara eşit bölündü (ÖSYM konu başına ağırlık vermiyor)`; }
      return { ...topic, pct, pctNote, size: topic.size ?? 1, code: `${lesson.code}.${ui + 1}.${String(ti + 1).padStart(2, '0')}`, order: ti + 1 };
    });
    return { ...unit, code: `${lesson.code}.${ui + 1}`, order: ui + 1, topics };
  });
  return { ...lesson, doc, units, pool: lesson.budget === 'AGS' ? 'AGS' : lesson.code };
});

// Minutes: each test (pool) gets a budget; a topic's share is proportional to question weight × topic size.
const pools = {};
for (const lesson of lessons) for (const unit of lesson.units) for (const topic of unit.topics)
  (pools[lesson.pool] ??= { budget: data.method.budgetHours[lesson.budget], weight: 0 }).weight += topic.pct * topic.size;
for (const lesson of lessons) for (const unit of lesson.units) for (const topic of unit.topics) {
  const pool = pools[lesson.pool];
  topic.rawMinutes = Math.max(data.method.minMinutes, round5(pool.budget * 60 * topic.pct * topic.size / pool.weight));
  topic.minutes = Math.min(topic.rawMinutes, data.method.maxMinutes);
  topic.yieldWeight = Math.min(5, band(topic.pct) + (topic.grammar ? data.method.grammarYieldBonus : 0));
  const testQuestions = lesson.exam === 'OABT' ? 50 : 80;
  topic.questions = topic.q ?? topic.pct * testQuestions / 100;
  topic.minutesNote = `Süre koçluk tahmini: ${examLabel[lesson.exam]} hazırlık bütçesi ${pool.budget} saat; konu payı soru ağırlığı`
    + ` (yaklaşık %${fmtPct(topic.pct)}, ~${fmtPct(topic.questions)} soru) × konu büyüklüğü (${String(topic.size).replace('.', ',')}`
    + `${topic.sizeNote ? ': ' + topic.sizeNote : ''}) ile orantılı; ${topic.rawMinutes} dk`
    + (topic.rawMinutes > topic.minutes ? `, planlayıcı sınırı nedeniyle ${topic.minutes} dk kaydedildi; konu birden çok turda planlanmalı.` : '.');
  topic.sourceUrl = lesson.doc.url;
}

// "17–26 (iki paragraf × 5)" → "…, 17–26. sorular (iki paragraf × 5)" (review O3; production was renamed the same way).
const outcomeName = (lesson, unit, topic, name = topic.name) => {
  if (topic.scope) return `${name} (${topic.scope})`;
  if (unit.range) {
    const [, numbers, detail] = unit.range.match(/^(\S+)(?: \((.+)\))?$/);
    return `${name} — ${lesson.doc.title.split(' temel soru')[0]}, ${numbers}. sorular${detail ? ` (${detail})` : ''}`;
  }
  return name;
};
// The outcome name the previous release wrote (old template "<range>. sorular", previous topic names).
const previousOutcomeName = (lesson, unit, topic) => {
  const name = PREVIOUS.topicNames[topic.code] ?? topic.name;
  if (!topic.scope && unit.range) return `${name} — ${lesson.doc.title.split(' temel soru')[0]}, ${unit.range}. sorular`;
  return outcomeName(lesson, unit, topic, name);
};
const previousName = (current, previous) => previous === undefined || previous === current ? null : previous;
const outcomeNotes = (lesson, unit, topic) => [
  `Kaynak: ${lesson.doc.title}.`,
  lesson.exam === 'OABT' && lesson.doc === data.documents.AGS2025
    ? '2026 belgesi yalnız AGS ile yeni Özel Eğitim alanını yayımladı; bu alanın en güncel resmî konu dağılımı 2025 belgesidir.' : null,
  unit.sectionPct && !topic.pct ? `ÖSYM bölüm ağırlığı: %${fmtPct(unit.sectionPct)}.` : null,
  topic.pctNote ? `Ağırlık: ${topic.pctNote}.` : null,
  topic.qNote ? `Soru sayısı: ${topic.qNote}.` : null,
  unit.split ? `Not: ${unit.split}.` : null,
  unit.openList ? 'Belgedeki liste "vb." ile bitiyor; konu listesi kapalı değildir.' : null,
  lesson.note ? `Not: ${lesson.note}` : null,
  lesson.domain ? `Okuma metinleri ${lesson.domain} alanındandır.` : null,
  lesson.exam === 'YDS' || lesson.exam === 'YOKDIL'
    ? 'ÖSYM bu sınav için konu listesi yayımlamıyor; ağaç soru tipi ve dil bilgisi alanlarına göre kurulmuş koçluk analizidir.' : null,
  'Resmî kazanım kodu değildir; sınav hazırlık kapsamıdır.',
  topic.minutesNote,
  `YieldWeight ${topic.yieldWeight}: testteki yaklaşık paya göre bant (≥8→5, ≥6→4, ≥4→3, ≥2→2, diğer→1)`
    + (topic.grammar ? ' + 1 (dil bilgisi cloze ve cümle tamamlamada da kullanılır; koçluk tahmini).' : '.'),
].filter(Boolean).join(' ');

const model = {
  lessons: lessons.map(l => ({
    code: l.code, exam: l.exam, name: l.name, complete: true,
    units: l.units.map(u => u.name),
    unitQuestions: l.units.map(u => Math.round(u.topics.reduce((s, t) => s + t.questions, 0) * 100) / 100),
    topics: l.units.flatMap(u => u.topics.map(t => ({ code: t.code, unit: u.name, name: t.name, pct: Math.round(t.pct * 10000) / 10000,
      questions: Math.round(t.questions * 100) / 100, size: t.size, yieldWeight: t.yieldWeight, minutes: t.minutes, rawMinutes: t.rawMinutes,
      minutesNote: t.minutesNote, sourceUrl: t.sourceUrl }))),
  })),
};
const totals = {};
for (const l of model.lessons) {
  const exam = examLabel[l.exam];
  const row = totals[exam] ??= { lessons: 0, units: 0, topics: 0, minutes: 0, rawMinutes: 0, budget: 0, capped: 0 };
  row.lessons++; row.units += l.units.length; row.topics += l.topics.length;
  for (const t of l.topics) { row.minutes += t.minutes; row.rawMinutes += t.rawMinutes; if (t.rawMinutes > t.minutes) row.capped++; }
}
model.totals = totals;

// ---- SQL ---------------------------------------------------------------------------------------------------------
const q = v => v === null || v === undefined ? 'NULL' : typeof v === 'number' ? String(v) : `'${String(v).replace(/'/g, "''")}'`;
const values = rows => rows.map(r => `    (${r.map(q).join(', ')})`).join(',\n');
const cv = data.curriculum, cvId = uuid(`curriculum:${cv.code}`);
const rows = { exams: [], lessons: [], units: [], topics: [], editions: [], official: [], outcomes: [], sources: [], maps: [], bindings: [] };
for (const l of lessons) {
  const lessonId = uuid(`lesson:${l.code}`), editionId = uuid(`edition:${l.code}`);
  const editionCode = `km3-${l.code.toLowerCase().replace(/_/g, '-')}-2026`;
  const scopeUrl = (l.evidenceDoc ? data.documents[l.evidenceDoc] : l.exam === 'OABT' ? data.documents.GUIDE2026 : l.doc).url;
  const examCode = l.exam === 'OABT' ? l.code : l.exam; // KM1: ÖABT lessons carry their field code (OABT_IMAT, …)
  if (l.exam === 'OABT') rows.exams.push([uuid(`exam:${l.code}`), l.code, l.examName ?? `ÖABT · ${l.field}`, 20 + rows.exams.length]);
  rows.lessons.push([lessonId, l.code, l.name, examCode]);
  rows.editions.push([editionId, editionCode, examCode, `${l.name} — sınav hazırlık kapsamı`, l.doc.url, scopeUrl, l.doc.sha256 ?? PENDING_PDF_SHA]);
  rows.bindings.push([editionId, lessonId]);
  for (const u of l.units) {
    const unitId = uuid(`unit:${u.code}`);
    rows.units.push([unitId, u.code, lessonId, u.name, u.order]);
    for (const t of u.topics) {
      const topicId = uuid(`topic:${t.code}`), officialId = uuid(`official:${t.code}`), outcomeId = uuid(`outcome:${t.code}`);
      const previousTopicName = previousName(t.name, PREVIOUS.topicNames[t.code]), current = outcomeName(l, u, t);
      rows.topics.push([topicId, t.code, lessonId, unitId, t.name, t.order, t.minutes, t.yieldWeight, previousTopicName]);
      rows.official.push([officialId, editionId, t.code, t.name, previousTopicName]);
      rows.outcomes.push([outcomeId, editionId, officialId, t.code, current, outcomeNotes(l, u, t), t.order, t.sourceUrl,
        previousName(current, previousOutcomeName(l, u, t))]);
      rows.sources.push([uuid(`outcome-source:${t.code}`), outcomeId, t.code]);
      rows.maps.push([topicId, outcomeId]);
    }
  }
}

const evidence = 'KM3 taslak paketi (01.11.2026): ÖSYM kaynak satırı veya soru tipi; ekip onayı bekliyor.';
const apply = `-- KM3 coaching catalog pack (01.11.2026): AGS, ÖABT (17 fields), YDS and YÖKDİL (English) drafts.
-- GENERATED by content-packs/coaching-catalog-km3/build.mjs from catalog.json. Do not edit by hand.
--
-- Everything is loaded PASSIVE: lessons, units and topics IsActive = false, topic-outcome links IsApproved = false and
-- the curriculum version "${cv.code}" unpublished. The team reviews, activates and approves them in the admin panel
-- ("Ders ve Müfredat Yönetimi"), then publishes the version. Nothing here is visible to students before that.
--
-- Needs the KM1 exams AGS, OABT, YDS and YOKDIL (content-packs/exam-calendar/2027) and stops otherwise.
-- Idempotent: rows are keyed by fixed ids; a rerun inserts only what is missing, keeps the team's activation and approval
-- decisions and stops (changing nothing) when a stored row's content differs from this pack. Stored values that are
-- exactly what release ${PREVIOUS.commit} wrote (its package hash, the names in "previous_name") count as equal and are kept.
--   psql -X -qAt -v ON_ERROR_STOP=1 -f apply.sql      (Coaching database; prints before/inserted/after NOTICEs)
BEGIN;
SET LOCAL lock_timeout = '5s';

DO $$
DECLARE missing text;
BEGIN
    SELECT string_agg(code, ', ' ORDER BY code) INTO missing FROM unnest(ARRAY['AGS','OABT','YDS','YOKDIL']) code
    WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_exams e WHERE e."Code" = code);
    IF missing IS NOT NULL THEN
        RAISE EXCEPTION 'KM1 exams missing: % — apply content-packs/exam-calendar/2027 (KM1) first. Nothing changed.', missing;
    END IF;
END $$;

SELECT pg_advisory_xact_lock(hashtext('${SOURCE}'));
LOCK TABLE coaching.study_catalog_lessons, coaching.study_catalog_units, coaching.study_catalog_topics IN SHARE ROW EXCLUSIVE MODE;

DO $$
DECLARE deleted integer;
BEGIN
    IF to_regclass('coaching."AdminAuditRecords"') IS NOT NULL THEN
        EXECUTE $q$SELECT count(*) FROM coaching."AdminAuditRecords" WHERE "ServiceName" = 'Coaching'
            AND "Action" = 'CatalogPermanentDelete' AND ("ChangedFieldsJson")::jsonb ->> 'source' = '${SOURCE}'$q$ INTO deleted;
        IF deleted > 0 THEN
            RAISE EXCEPTION 'The team permanently deleted % record(s) of source ${SOURCE}; this pack would bring them back. Nothing changed.', deleted;
        END IF;
    END IF;
END $$;

-- KM1 decision (01.11.2026): an ÖABT lesson's ExamCode is its field code, so the catalog knows the field. ExamCode is a
-- foreign key to study_catalog_exams, so the 17 field codes are added as exams too: PASSIVE, and only when missing (an
-- existing exam with the same code keeps its name and status).
CREATE TEMP TABLE km3_exams (id uuid PRIMARY KEY, code text, name text, display_order int) ON COMMIT DROP;
INSERT INTO km3_exams VALUES
${values(rows.exams)};
CREATE TEMP TABLE km3_lessons (id uuid PRIMARY KEY, source_id text, name text, exam text) ON COMMIT DROP;
INSERT INTO km3_lessons VALUES
${values(rows.lessons)};
CREATE TEMP TABLE km3_units (id uuid PRIMARY KEY, source_id text, lesson_id uuid, name text, display_order int) ON COMMIT DROP;
INSERT INTO km3_units VALUES
${values(rows.units)};
CREATE TEMP TABLE km3_topics (id uuid PRIMARY KEY, source_id text, lesson_id uuid, unit_id uuid, name text, display_order int,
    minutes int, yield_weight int, previous_name text) ON COMMIT DROP;
INSERT INTO km3_topics VALUES
${values(rows.topics)};
CREATE TEMP TABLE km3_editions (id uuid PRIMARY KEY, code text, exam text, subject text, source_url text, scope_url text,
    document_sha text) ON COMMIT DROP;
INSERT INTO km3_editions VALUES
${values(rows.editions)};
CREATE TEMP TABLE km3_official (id uuid PRIMARY KEY, edition_id uuid, code text, name text, previous_name text) ON COMMIT DROP;
INSERT INTO km3_official VALUES
${values(rows.official)};
CREATE TEMP TABLE km3_outcomes (id uuid PRIMARY KEY, edition_id uuid, official_id uuid, code text, name text, notes text,
    display_order int, source_url text, previous_name text) ON COMMIT DROP;
INSERT INTO km3_outcomes VALUES
${values(rows.outcomes)};
CREATE TEMP TABLE km3_outcome_sources (id uuid PRIMARY KEY, outcome_id uuid, source_id text) ON COMMIT DROP;
INSERT INTO km3_outcome_sources VALUES
${values(rows.sources)};
CREATE TEMP TABLE km3_maps (topic_id uuid, outcome_id uuid, PRIMARY KEY (topic_id, outcome_id)) ON COMMIT DROP;
INSERT INTO km3_maps VALUES
${values(rows.maps)};
CREATE TEMP TABLE km3_bindings (edition_id uuid, lesson_id uuid, PRIMARY KEY (edition_id, lesson_id)) ON COMMIT DROP;
INSERT INTO km3_bindings VALUES
${values(rows.bindings)};

DO $$
DECLARE conflict text;
BEGIN
    RAISE NOTICE 'before: lessons %, units %, topics %, outcomes %, editions %, curriculum versions %, exams %',
        (SELECT count(*) FROM coaching.study_catalog_lessons), (SELECT count(*) FROM coaching.study_catalog_units),
        (SELECT count(*) FROM coaching.study_catalog_topics), (SELECT count(*) FROM coaching.study_catalog_outcomes),
        (SELECT count(*) FROM coaching.official_curriculum_editions), (SELECT count(*) FROM coaching.curriculum_versions),
        (SELECT count(*) FROM coaching.study_catalog_exams);

    -- Stored rows that share an id or key with this pack must hold the same content (activation/approval excluded).
    SELECT string_agg(x, ', ') INTO conflict FROM (
        SELECT 'curriculum version ' || v."Code" x FROM coaching.curriculum_versions v
        WHERE (v."Id" = ${q(cvId)} OR v."Code" = ${q(cv.code)})
          AND (v."Id", v."Code", v."Name", v."VersionLabel", v."ValidFromYear", v."ValidToYear", v."Source", v."SourceUrl")
              IS DISTINCT FROM (${q(cvId)}::uuid, ${q(cv.code)}::varchar, ${q(cv.name)}::varchar, ${q(cv.versionLabel)}::varchar, ${cv.validFrom}, ${cv.validTo}, ${q(cv.source)}::varchar, ${q(cv.sourceUrl)}::varchar)
          -- the exact name, label and source release ${PREVIOUS.commit} wrote (before the server renamed the version) count as equal
          AND (v."Id", v."Code", v."Name", v."VersionLabel", v."ValidFromYear", v."ValidToYear", v."Source", v."SourceUrl")
              IS DISTINCT FROM (${q(cvId)}::uuid, ${q(cv.code)}::varchar, ${q(PREVIOUS.curriculum.name)}::varchar, ${q(PREVIOUS.curriculum.versionLabel)}::varchar, ${cv.validFrom}, ${cv.validTo}, ${q(PREVIOUS.curriculum.source)}::varchar, ${q(cv.sourceUrl)}::varchar)
        UNION ALL SELECT 'lesson ' || l."SourceId" FROM coaching.study_catalog_lessons l
        LEFT JOIN km3_lessons k ON k.id = l."Id"
        WHERE (l."Source" = '${SOURCE}' OR k.id IS NOT NULL)
          AND (k.id IS NULL OR (l."Source", l."SourceId", l."Name", l."GradeNumber", l."ExamCode", l."CurriculumVersionId")
              IS DISTINCT FROM ('${SOURCE}'::varchar, k.source_id::varchar, k.name::varchar, NULL::int, k.exam::varchar, ${q(cvId)}::uuid))
        UNION ALL SELECT 'unit ' || u."SourceId" FROM coaching.study_catalog_units u
        LEFT JOIN km3_units k ON k.id = u."Id"
        WHERE (u."Source" = '${SOURCE}' OR k.id IS NOT NULL)
          AND (k.id IS NULL OR (u."Source", u."SourceId", u."LessonId", u."Name", u."DisplayOrder")
              IS DISTINCT FROM ('${SOURCE}'::varchar, k.source_id::varchar, k.lesson_id, k.name::varchar, k.display_order))
        UNION ALL SELECT 'topic ' || t."SourceId" FROM coaching.study_catalog_topics t
        LEFT JOIN km3_topics k ON k.id = t."Id"
        WHERE (t."Source" = '${SOURCE}' OR k.id IS NOT NULL)
          AND (k.id IS NULL OR (t."Source", t."SourceId", t."LessonId", t."UnitId", t."ParentId",
                  CASE WHEN t."Name" = k.previous_name THEN k.name ELSE t."Name" END, t."DisplayOrder", t."EstimatedMinutes", t."YieldWeight")
              IS DISTINCT FROM ('${SOURCE}'::varchar, k.source_id::varchar, k.lesson_id, k.unit_id, NULL::uuid, k.name::varchar, k.display_order, k.minutes, k.yield_weight))
        UNION ALL SELECT 'edition ' || e."Code" FROM coaching.official_curriculum_editions e
        LEFT JOIN km3_editions k ON k.id = e."Id"
        WHERE (e."Code" IN (SELECT code FROM km3_editions) OR k.id IS NOT NULL)
          AND (k.id IS NULL OR (e."Code", e."ExamCode", e."GradeNumber", e."SubjectName", e."AcademicYearStart", e."AcademicYearEnd", e."SourceUrl",
                  e."ScopeEvidenceUrl", CASE WHEN e."PackageSha256" = '${PREVIOUS.packageSha256}' THEN '${PACKAGE_SHA}' ELSE e."PackageSha256" END)
              IS DISTINCT FROM (k.code::varchar, k.exam::varchar, NULL::int, k.subject::varchar, 2026, 2027, k.source_url::varchar, k.scope_url::varchar, '${PACKAGE_SHA}'::varchar))
        UNION ALL SELECT 'official topic ' || o."Code" FROM coaching.official_curriculum_topics o
        JOIN km3_official k ON k.id = o."Id"
        WHERE (o."EditionId", o."Code", CASE WHEN o."Name" = k.previous_name THEN k.name ELSE o."Name" END, o."OutcomeCount", o."LessonPeriods")
              IS DISTINCT FROM (k.edition_id, k.code::varchar, k.name::varchar, 1, NULL::int)
        UNION ALL SELECT 'outcome ' || o."Code" FROM coaching.study_catalog_outcomes o
        JOIN km3_outcomes k ON k.id = o."Id"
        WHERE (o."EditionId", o."OfficialTopicId", o."Code", CASE WHEN o."Name" = k.previous_name THEN k.name ELSE o."Name" END, o."Notes",
                  o."SourcePage", o."DisplayOrder", o."SourceKind", o."SourceUrl")
              IS DISTINCT FROM (k.edition_id, k.official_id, k.code::varchar, k.name::varchar, k.notes::varchar, NULL::int, k.display_order, 'ExamSkill'::varchar, k.source_url::varchar)
        UNION ALL SELECT 'outcome source ' || s."SourceId" FROM coaching.study_catalog_outcome_sources s
        LEFT JOIN km3_outcome_sources k ON k.id = s."Id"
        WHERE (s."Source" = '${SOURCE}' OR k.id IS NOT NULL)
          AND (k.id IS NULL OR (s."OutcomeId", s."Source", s."SourceId") IS DISTINCT FROM (k.outcome_id, '${SOURCE}'::varchar, k.source_id::varchar))
    ) conflicts;
    IF conflict IS NOT NULL THEN
        RAISE EXCEPTION 'Stored catalog differs from this pack (%). Review the changes in the admin panel; nothing changed.', left(conflict, 1500);
    END IF;
END $$;

INSERT INTO coaching.curriculum_versions ("Id", "Code", "Name", "VersionLabel", "ValidFromYear", "ValidToYear", "Source", "SourceUrl",
    "IsPublished", "CreatedAt", "CreatedBy", "Version")
SELECT ${q(cvId)}, ${q(cv.code)}, ${q(cv.name)}, ${q(cv.versionLabel)}, ${cv.validFrom}, ${cv.validTo}, ${q(cv.source)}, ${q(cv.sourceUrl)},
    false, now(), 'content-pack:${SOURCE}', 1
WHERE NOT EXISTS (SELECT 1 FROM coaching.curriculum_versions WHERE "Id" = ${q(cvId)});

CREATE TEMP TABLE km3_inserted (kind text, n int) ON COMMIT DROP;
WITH x AS (INSERT INTO coaching.study_catalog_exams ("Id", "Code", "Name", "DisplayOrder", "IsActive")
    SELECT k.id, k.code, k.name, k.display_order, false FROM km3_exams k
    WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_exams e WHERE e."Code" = k.code OR e."Id" = k.id) RETURNING 1)
INSERT INTO km3_inserted SELECT 'exams', count(*) FROM x;
WITH x AS (INSERT INTO coaching.study_catalog_lessons ("Id", "Source", "SourceId", "Name", "GradeNumber", "ExamCode", "IsActive", "CurriculumVersionId")
    SELECT k.id, '${SOURCE}', k.source_id, k.name, NULL, k.exam, false, ${q(cvId)} FROM km3_lessons k
    WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_lessons l WHERE l."Id" = k.id) RETURNING 1)
INSERT INTO km3_inserted SELECT 'lessons', count(*) FROM x;
WITH x AS (INSERT INTO coaching.study_catalog_units ("Id", "Source", "SourceId", "LessonId", "Name", "DisplayOrder", "IsActive")
    SELECT k.id, '${SOURCE}', k.source_id, k.lesson_id, k.name, k.display_order, false FROM km3_units k
    WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_units u WHERE u."Id" = k.id) RETURNING 1)
INSERT INTO km3_inserted SELECT 'units', count(*) FROM x;
WITH x AS (INSERT INTO coaching.study_catalog_topics ("Id", "Source", "SourceId", "LessonId", "UnitId", "ParentId", "Name", "DisplayOrder",
        "EstimatedMinutes", "IsActive", "YieldWeight")
    SELECT k.id, '${SOURCE}', k.source_id, k.lesson_id, k.unit_id, NULL, k.name, k.display_order, k.minutes, false, k.yield_weight FROM km3_topics k
    WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_topics t WHERE t."Id" = k.id) RETURNING 1)
INSERT INTO km3_inserted SELECT 'topics', count(*) FROM x;
WITH x AS (INSERT INTO coaching.official_curriculum_editions ("Id", "Code", "ExamCode", "GradeNumber", "SubjectName", "AcademicYearStart",
        "AcademicYearEnd", "SourceUrl", "DocumentSha256", "ScopeEvidenceUrl", "PackageSha256")
    SELECT k.id, k.code, k.exam, NULL, k.subject, 2026, 2027, k.source_url, k.document_sha, k.scope_url, '${PACKAGE_SHA}' FROM km3_editions k
    WHERE NOT EXISTS (SELECT 1 FROM coaching.official_curriculum_editions e WHERE e."Id" = k.id) RETURNING 1)
INSERT INTO km3_inserted SELECT 'editions', count(*) FROM x;
INSERT INTO coaching.official_curriculum_lesson_bindings ("EditionId", "LessonId")
SELECT k.edition_id, k.lesson_id FROM km3_bindings k
WHERE NOT EXISTS (SELECT 1 FROM coaching.official_curriculum_lesson_bindings b WHERE b."EditionId" = k.edition_id AND b."LessonId" = k.lesson_id);
INSERT INTO coaching.official_curriculum_topics ("Id", "EditionId", "Code", "Name", "OutcomeCount", "LessonPeriods")
SELECT k.id, k.edition_id, k.code, k.name, 1, NULL FROM km3_official k
WHERE NOT EXISTS (SELECT 1 FROM coaching.official_curriculum_topics o WHERE o."Id" = k.id);
WITH x AS (INSERT INTO coaching.study_catalog_outcomes ("Id", "EditionId", "OfficialTopicId", "Code", "Name", "Notes", "SourcePage",
        "DisplayOrder", "IsArchived", "SourceKind", "SourceUrl")
    SELECT k.id, k.edition_id, k.official_id, k.code, k.name, k.notes, NULL, k.display_order, false, 'ExamSkill', k.source_url FROM km3_outcomes k
    WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_outcomes o WHERE o."Id" = k.id) RETURNING 1)
INSERT INTO km3_inserted SELECT 'outcomes', count(*) FROM x;
INSERT INTO coaching.study_catalog_outcome_sources ("Id", "OutcomeId", "Source", "SourceId")
SELECT k.id, k.outcome_id, '${SOURCE}', k.source_id FROM km3_outcome_sources k
WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_outcome_sources s WHERE s."Id" = k.id);
WITH x AS (INSERT INTO coaching.study_catalog_topic_outcomes ("TopicId", "OutcomeId", "IsApproved", "Evidence")
    SELECT k.topic_id, k.outcome_id, false, ${q(evidence)} FROM km3_maps k
    WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_topic_outcomes m WHERE m."TopicId" = k.topic_id AND m."OutcomeId" = k.outcome_id) RETURNING 1)
INSERT INTO km3_inserted SELECT 'mappings', count(*) FROM x;

DO $$
DECLARE missing integer;
BEGIN
    RAISE NOTICE 'inserted: lessons %, units %, topics %, outcomes %, editions %, mappings %, ÖABT field exams %',
        (SELECT n FROM km3_inserted WHERE kind = 'lessons'), (SELECT n FROM km3_inserted WHERE kind = 'units'),
        (SELECT n FROM km3_inserted WHERE kind = 'topics'), (SELECT n FROM km3_inserted WHERE kind = 'outcomes'),
        (SELECT n FROM km3_inserted WHERE kind = 'editions'), (SELECT n FROM km3_inserted WHERE kind = 'mappings'),
        (SELECT n FROM km3_inserted WHERE kind = 'exams');
    SELECT (SELECT count(*) FROM km3_lessons k WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_lessons x WHERE x."Id" = k.id))
         + (SELECT count(*) FROM km3_units k WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_units x WHERE x."Id" = k.id))
         + (SELECT count(*) FROM km3_topics k WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_topics x WHERE x."Id" = k.id))
         + (SELECT count(*) FROM km3_outcomes k WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_outcomes x WHERE x."Id" = k.id))
         + (SELECT count(*) FROM km3_maps k WHERE NOT EXISTS (SELECT 1 FROM coaching.study_catalog_topic_outcomes x WHERE x."TopicId" = k.topic_id AND x."OutcomeId" = k.outcome_id))
      INTO missing;
    IF missing <> 0 THEN RAISE EXCEPTION 'after-check failed: % pack rows missing. Nothing changed.', missing; END IF;
    RAISE NOTICE 'after: lessons %, units %, topics %, outcomes %, editions %, curriculum versions %, exams %',
        (SELECT count(*) FROM coaching.study_catalog_lessons), (SELECT count(*) FROM coaching.study_catalog_units),
        (SELECT count(*) FROM coaching.study_catalog_topics), (SELECT count(*) FROM coaching.study_catalog_outcomes),
        (SELECT count(*) FROM coaching.official_curriculum_editions), (SELECT count(*) FROM coaching.curriculum_versions),
        (SELECT count(*) FROM coaching.study_catalog_exams);
END $$;
COMMIT;
`;

const expected = Object.entries({ AGS: 'AGS', OABT: 'OABT', YDS: 'YDS', YOKDIL: 'YOKDIL' }).map(([exam]) => {
  const ls = lessons.filter(l => l.exam === exam);
  const units = ls.reduce((s, l) => s + l.units.length, 0), topics = ls.reduce((s, l) => s + l.units.reduce((a, u) => a + u.topics.length, 0), 0);
  return [exam, ls.length, units, topics];
});
const verify = `-- KM3 coaching catalog pack — read-only check (GENERATED by build.mjs). Prints one line per exam:
--   exam|lessons|units|topics|outcomes linked|N active|N approved|planned hours
-- then "pending PDF hashes", and "km3 verify: ok". Fails when a pack row is missing or a rule is broken.
--   psql -X -qAt -v ON_ERROR_STOP=1 -f verify.sql
BEGIN READ ONLY;
WITH t AS (
    SELECT CASE WHEN l."ExamCode" LIKE 'OABT\\_%' THEN 'OABT' ELSE l."ExamCode" END exam, l."Id" lesson_id, u."Id" unit_id, t."Id" topic_id,
        t."IsActive" active, t."EstimatedMinutes" minutes,
        (SELECT count(*) FROM coaching.study_catalog_topic_outcomes m WHERE m."TopicId" = t."Id") outcomes,
        (SELECT count(*) FROM coaching.study_catalog_topic_outcomes m WHERE m."TopicId" = t."Id" AND m."IsApproved") approved
    FROM coaching.study_catalog_lessons l
    JOIN coaching.study_catalog_units u ON u."LessonId" = l."Id" AND u."Source" = '${SOURCE}'
    JOIN coaching.study_catalog_topics t ON t."UnitId" = u."Id" AND t."Source" = '${SOURCE}'
    WHERE l."Source" = '${SOURCE}')
SELECT exam || '|' || count(DISTINCT lesson_id) || '|' || count(DISTINCT unit_id) || '|' || count(*) || '|' || sum(outcomes) || '|'
    || count(*) FILTER (WHERE active) || ' active|' || sum(approved) || ' approved|' || round(sum(minutes) / 60.0, 1) || ' h'
FROM t GROUP BY exam ORDER BY exam;
SELECT 'pending PDF hashes: ' || count(*) || ' editions' FROM coaching.official_curriculum_editions
WHERE "Code" LIKE 'km3-%' AND "DocumentSha256" = '${PENDING_PDF_SHA}';
DO $$
DECLARE problems text;
BEGIN
    SELECT string_agg(p, '; ') INTO problems FROM (
        SELECT 'exam ' || e.exam || ': expected ' || e.lessons || '/' || e.units || '/' || e.topics || ', found '
            || coalesce(f.lessons, 0) || '/' || coalesce(f.units, 0) || '/' || coalesce(f.topics, 0) p
        FROM (VALUES ${expected.map(([e, a, b, c]) => `('${e}', ${a}, ${b}, ${c})`).join(', ')}) e(exam, lessons, units, topics)
        LEFT JOIN (SELECT CASE WHEN l."ExamCode" LIKE 'OABT\\_%' THEN 'OABT' ELSE l."ExamCode" END exam, count(DISTINCT l."Id") lessons, count(DISTINCT u."Id") units, count(DISTINCT t."Id") topics
                   FROM coaching.study_catalog_lessons l
                   LEFT JOIN coaching.study_catalog_units u ON u."LessonId" = l."Id" AND u."Source" = '${SOURCE}'
                   LEFT JOIN coaching.study_catalog_topics t ON t."UnitId" = u."Id" AND t."Source" = '${SOURCE}'
                   WHERE l."Source" = '${SOURCE}' GROUP BY 1) f ON f.exam = e.exam
        WHERE (e.lessons, e.units, e.topics) IS DISTINCT FROM (f.lessons, f.units, f.topics)
        UNION ALL SELECT 'topic without outcome ' || t."SourceId" FROM coaching.study_catalog_topics t
        WHERE t."Source" = '${SOURCE}' AND NOT EXISTS (SELECT 1 FROM coaching.study_catalog_topic_outcomes m WHERE m."TopicId" = t."Id")
        UNION ALL SELECT 'topic minutes/weight ' || t."SourceId" FROM coaching.study_catalog_topics t
        WHERE t."Source" = '${SOURCE}' AND ("EstimatedMinutes" IS NULL OR "EstimatedMinutes" NOT BETWEEN 1 AND 1440 OR "YieldWeight" NOT BETWEEN 1 AND 5)
        UNION ALL SELECT 'outcome source ' || o."Code" FROM coaching.study_catalog_outcomes o
        JOIN coaching.official_curriculum_editions e ON e."Id" = o."EditionId"
        WHERE e."Code" LIKE 'km3-%' AND (o."SourceKind" <> 'ExamSkill' OR o."SourceUrl" IS NULL)
        UNION ALL SELECT 'lesson outside curriculum version ' || l."SourceId" FROM coaching.study_catalog_lessons l
        WHERE l."Source" = '${SOURCE}' AND l."CurriculumVersionId" IS DISTINCT FROM ${q(cvId)}::uuid
    ) problems;
    IF problems IS NOT NULL THEN RAISE EXCEPTION 'km3 verify failed: %', left(problems, 1500); END IF;
END $$;
SELECT 'km3 verify: ok';
COMMIT;
`;

const csvCell = v => { const s = v === null || v === undefined ? '' : String(v); return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s; };
const review = ['exam,lesson_code,lesson,unit,topic_code,topic,approx_pct,approx_questions,yield_weight,minutes,raw_minutes,source_url']
  .concat(lessons.flatMap(l => l.units.flatMap(u => u.topics.map(t => [examLabel[l.exam], l.code, l.name, u.name, t.code, t.name,
    Math.round(t.pct * 100) / 100, Math.round(t.questions * 100) / 100, t.yieldWeight, t.minutes, t.rawMinutes, t.sourceUrl].map(csvCell).join(',')))))
  .join('\n') + '\n';

const h = m => (Math.round(m / 6) / 10).toLocaleString('tr-TR');
const totalsTable = ['| Sınav | Ders | Ünite | Konu (= kazanım) | Planlanan süre (konu sınırı 1440 dk) | Tahmin (sınırsız) | 1440 dk ile sınırlanan konu |',
  '| --- | ---: | ---: | ---: | ---: | ---: | ---: |']
  .concat(Object.entries(totals).map(([exam, r]) => `| ${exam} | ${r.lessons} | ${r.units} | ${r.topics} | ${h(r.minutes)} saat | ${h(r.rawMinutes)} saat | ${r.capped} |`))
  .concat(['', '| Ders | Konu | Planlanan | Tahmin |', '| --- | ---: | ---: | ---: |'])
  .concat(model.lessons.map(l => `| ${l.code} — ${l.name} | ${l.topics.length} | ${h(l.topics.reduce((s, t) => s + t.minutes, 0))} saat | ${h(l.topics.reduce((s, t) => s + t.rawMinutes, 0))} saat |`))
  .join('\n');

const readmePath = dir + 'README.md';
const readme = readFileSync(readmePath, 'utf8');
const nextReadme = readme.replace(/<!-- totals:start -->[\s\S]*<!-- totals:end -->/, `<!-- totals:start -->\n${totalsTable}\n<!-- totals:end -->`);
const outputs = { 'apply.sql': apply, 'verify.sql': verify, 'catalog-review.csv': review, 'README.md': nextReadme };

const mode = process.argv[2];
if (mode === '--json') { process.stdout.write(JSON.stringify(model)); }
else if (mode === '--check') {
  const stale = Object.entries(outputs).filter(([name, text]) => { try { return readFileSync(dir + name, 'utf8') !== text; } catch { return true; } }).map(([n]) => n);
  if (stale.length) { console.error(`out of date: ${stale.join(', ')} — run node content-packs/coaching-catalog-km3/build.mjs`); process.exit(1); }
} else {
  for (const [name, text] of Object.entries(outputs)) writeFileSync(dir + name, text);
  for (const [exam, r] of Object.entries(totals)) console.log(`${exam}: ${r.lessons} lessons, ${r.units} units, ${r.topics} topics, ${h(r.minutes)} h planned (${h(r.rawMinutes)} h estimated, ${r.capped} capped)`);
}
