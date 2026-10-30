-- H19 (audit 30.10.2026): read-only list of active badges that students cannot earn, or that only some age groups can.
-- Needs PostgreSQL 16+ (pg_input_is_valid). Run on the Speed Reading database:  psql -X -qAt -v ON_ERROR_STOP=1 -f audit.sql
BEGIN READ ONLY;
WITH badges AS (
  SELECT a.id, a."Name" name, lower(btrim(a."CriteriaType")) criteria, a."CriteriaValue" raw, a."IsRepeatable" is_repeatable,
    CASE WHEN pg_input_is_valid(a."CriteriaValue", 'jsonb') AND jsonb_typeof(a."CriteriaValue"::jsonb)='object'
      THEN a."CriteriaValue"::jsonb END criteria_json
  FROM speed_reading.achievements a WHERE a."IsActive" AND NOT a."IsDeleted"
), available AS (
  SELECT DISTINCT lower(t.name) type_name, e.target_age_group_id age_group_id
  FROM speed_reading.exercises e JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
  WHERE e.is_active AND NOT e.is_deleted AND NOT e.is_assessment_only AND t.is_active AND NOT t.is_deleted
), age_groups AS (
  SELECT DISTINCT "TargetAgeGroupConfigurationId" age_group_id FROM speed_reading.program_templates
  WHERE "IsActive" AND NOT "IsDeleted" AND NOT "IsAssessment"
), needed AS (
  SELECT b.*, CASE
    WHEN b.criteria LIKE 'vocabulary\_%' THEN 'vocabulary'
    WHEN b.criteria LIKE 'rsvp\_%' THEN 'rsvp'
    WHEN b.criteria='exercise_type_first' THEN lower(btrim(b.criteria_json->>'type')) END type_name
  FROM badges b
), problems AS (
  SELECT id,name,criteria,raw,'Kriter JSON nesnesi değil' problem FROM needed WHERE criteria_json IS NULL
  UNION ALL SELECT id,name,criteria,raw,'Tekrarlanabilir rozet desteklenmiyor' FROM needed WHERE is_repeatable
  UNION ALL SELECT id,name,criteria,raw,'Bilinmeyen kriter türü' FROM needed WHERE criteria NOT IN
    ('streak','level_reached','activity_count','total_xp','reading_minutes','wpm_reached','comprehension_score','reading_count',
     'rsvp_count','exercise_count','vocabulary_learned','vocabulary_box','vocabulary_streak','vocabulary_categories','rsvp_wpm',
     'rsvp_comprehension','exercise_type_first','exercise_variety')
  UNION ALL SELECT id,name,criteria,raw,'Kriterde gerekli alan eksik' FROM needed WHERE criteria_json IS NOT NULL AND NOT (criteria_json ?
    CASE criteria WHEN 'streak' THEN 'days' WHEN 'level_reached' THEN 'level' WHEN 'total_xp' THEN 'xp' WHEN 'reading_minutes' THEN 'minutes'
      WHEN 'wpm_reached' THEN 'wpm' WHEN 'rsvp_wpm' THEN 'wpm' WHEN 'comprehension_score' THEN 'score' WHEN 'rsvp_comprehension' THEN 'score'
      WHEN 'vocabulary_box' THEN 'box' WHEN 'exercise_type_first' THEN 'type' WHEN 'exercise_variety' THEN 'types' ELSE 'count' END)
  UNION ALL SELECT id,name,criteria,raw,'Egzersiz türü doğrulanamıyor (göz ısınması, gruplama, tempolu okuma veya N-back); hiç kazanılamaz'
    FROM needed WHERE criteria='exercise_type_first' AND type_name IN ('chunking','eyetracking','saccade','speedreading','focus')
  UNION ALL SELECT n.id,n.name,n.criteria,n.raw,'Hiçbir yaş grubunda etkin egzersizi yok; hiç kazanılamaz'
    FROM needed n WHERE n.type_name IS NOT NULL AND NOT EXISTS(SELECT 1 FROM available a WHERE a.type_name=n.type_name)
  UNION ALL SELECT n.id,n.name,n.criteria,n.raw,'Şu yaş grubunda kazanılamaz (öğrenciye gösterilmez): '||g.age_group_id
    FROM needed n CROSS JOIN age_groups g WHERE n.type_name IS NOT NULL
      AND EXISTS(SELECT 1 FROM available a WHERE a.type_name=n.type_name)
      AND NOT EXISTS(SELECT 1 FROM available a WHERE a.type_name=n.type_name AND (a.age_group_id IS NULL OR a.age_group_id=g.age_group_id))
  UNION ALL SELECT id,name,criteria,raw,'Anlama puanı 100 üzerinde; hiç kazanılamaz' FROM needed
    WHERE criteria IN ('comprehension_score','rsvp_comprehension') AND (criteria_json->>'score') ~ '^[0-9.]+$' AND (criteria_json->>'score')::numeric>100
)
SELECT id||'|'||name||'|'||criteria||'|'||raw||'|'||problem FROM problems ORDER BY name, problem;
SELECT 'Etkin rozet: '||count(*) FROM speed_reading.achievements WHERE "IsActive" AND NOT "IsDeleted";
COMMIT;
