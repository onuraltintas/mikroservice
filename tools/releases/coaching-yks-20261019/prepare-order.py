import json
from pathlib import Path

root = Path(__file__).resolve().parents[3]
research = root / 'artifacts/mebi-yks-research-20261019'
catalog = json.loads((research / 'mebi-catalog.json').read_text(encoding='utf-8'))
ours = json.loads((research / 'ours-live.json').read_text(encoding='utf-8'))
lessons = {lesson['source_id']: lesson['id'] for lesson in ours['lessons']}
topics = {(topic['lesson_id'], topic['source_id']): topic for topic in ours['topics']}
rows = []
for exam in catalog:
    for lesson in exam['lessons']:
        if exam['name'] == 'YDT' and 'İngilizce' not in lesson['name']:
            continue
        lesson_id = lessons[lesson['id']]
        for upper_order, upper in enumerate(lesson['uppers'], 1):
            entries = [('upper:' + upper['id'], upper_order)]
            entries += [('topic:' + subject['id'], order) for order, subject in enumerate(upper['subjects'], 1)]
            for source_id, order in entries:
                topic = topics[(lesson_id, source_id)]
                rows.append(dict(id=topic['id'], lessonId=lesson_id, sourceId=source_id, order=order))
assert len(rows) == 1454 and len({row['id'] for row in rows}) == 1454
document = dict(sourceUrl='https://ogmmateryal.eba.gov.tr/mebi', capturedDate='2026-10-19', topics=rows)
(root / 'data/coaching-outcomes/yks-topic-order-20261019.json').write_text(json.dumps(document, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
values = ',\n'.join("('%s','%s','%s',%d)" % (r['id'], r['lessonId'], r['sourceId'], r['order']) for r in rows)
sql = '''BEGIN;
CREATE TEMP TABLE expected_order(id uuid PRIMARY KEY, lesson_id uuid, source_id text, display_order int);
INSERT INTO expected_order VALUES
''' + values + ''';
DO $$ BEGIN
 IF (SELECT count(*) FROM expected_order e JOIN coaching.study_catalog_topics t ON t."Id"=e.id AND t."LessonId"=e.lesson_id AND t."SourceId"=e.source_id JOIN coaching.study_catalog_lessons l ON l."Id"=t."LessonId" WHERE l."ExamCode" IN ('TYT','AYT','YDT') AND l."Name" NOT ILIKE '%Arap%')<>1454 THEN RAISE EXCEPTION 'Catalog ownership mismatch'; END IF;
END $$;
CREATE TEMP TABLE task_fingerprints AS SELECT 'task' kind,"Id"::text id,md5(to_jsonb(t)::text) hash FROM coaching.study_plan_tasks t UNION ALL SELECT 'link',"TaskId"::text||"OutcomeId"::text,md5(to_jsonb(t)::text) FROM coaching.study_plan_task_outcomes t;
UPDATE coaching.study_catalog_topics t SET "DisplayOrder"=e.display_order FROM expected_order e WHERE t."Id"=e.id AND t."DisplayOrder" IS DISTINCT FROM e.display_order;
UPDATE coaching.study_catalog_units u SET "DisplayOrder"=e.display_order FROM expected_order e JOIN coaching.study_catalog_topics t ON t."Id"=e.id WHERE t."ParentId" IS NULL AND u."LessonId"=e.lesson_id AND u."SourceId"=substring(e.source_id from 7) AND u."DisplayOrder" IS DISTINCT FROM e.display_order;
DO $$ BEGIN
 IF EXISTS(SELECT 1 FROM task_fingerprints f LEFT JOIN (SELECT 'task' kind,"Id"::text id,md5(to_jsonb(t)::text) hash FROM coaching.study_plan_tasks t UNION ALL SELECT 'link',"TaskId"::text||"OutcomeId"::text,md5(to_jsonb(t)::text) FROM coaching.study_plan_task_outcomes t)n USING(kind,id,hash) WHERE n.id IS NULL) THEN RAISE EXCEPTION 'Study record changed'; END IF;
 IF EXISTS(SELECT 1 FROM expected_order e JOIN coaching.study_catalog_topics t ON t."Id"=e.id WHERE t."DisplayOrder"<>e.display_order) THEN RAISE EXCEPTION 'Ordering incomplete'; END IF;
END $$;
COMMIT;
SELECT 'MEBI topic sequence verified; existing tasks and outcome links unchanged.';
'''
(Path(__file__).parent / 'order-apply.sql').write_text(sql, encoding='utf-8')
print('1454 verified TYT/AYT/English catalog sequence entries prepared.')
