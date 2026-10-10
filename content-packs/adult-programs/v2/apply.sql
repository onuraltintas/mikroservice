-- psql: supply plan_json from infrastructure/data/adult-program-plan-v2.json.
-- Requires a validated backup. Never deletes account or training history.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
LOCK TABLE speed_reading.program_templates, speed_reading.student_program_progress IN SHARE ROW EXCLUSIVE MODE;
LOCK TABLE speed_reading.review_items IN SHARE ROW EXCLUSIVE MODE;
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types IN SHARE MODE;
CREATE TEMP TABLE adult_plan_v2_input ON COMMIT DROP AS SELECT :'plan_json'::jsonb AS plan;
CREATE TEMP TABLE adult_program_v2_ids(old_id uuid, new_id uuid, level integer) ON COMMIT DROP;
INSERT INTO adult_program_v2_ids VALUES
('c212d2c6-ea6b-5fce-ad7d-111c1ff6b7fe','e2c01003-8d9a-4e6c-a428-000000000001',1),
('6dd7f01d-127e-5340-baa8-a485307f4d15','e2c01003-8d9a-4e6c-a428-000000000002',2),
('161b34c6-29e9-57db-8ad2-d3ded5b88a3c','e2c01003-8d9a-4e6c-a428-000000000003',3),
('dcf57722-3d60-56da-9a89-af8e11fbf4f6','e2c01003-8d9a-4e6c-a428-000000000004',4),
('27a1c700-f15a-5a0b-9ec9-3d1d2444eb35','e2c01003-8d9a-4e6c-a428-000000000005',5);
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='speed_reading'
    AND table_name='daily_exercise_logs' AND column_name='slot_order') THEN
    RAISE EXCEPTION 'Task-slot schema is required';
  END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.student_program_progress p
    JOIN adult_program_v2_ids m ON p."ProgramTemplateId"=m.old_id) THEN
    RAISE EXCEPTION 'Old adult programs have progress; replacement stopped, history preserved';
  END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.review_items r JOIN adult_program_v2_ids m ON r.program_template_id=m.old_id) THEN
    RAISE EXCEPTION 'Old adult programs have review history; replacement stopped, history preserved';
  END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.program_templates p JOIN adult_program_v2_ids m ON p.id=m.old_id
    WHERE p."IsAssessment" OR p."IsDeleted" OR NOT p."IsActive"
      OR p."TargetAgeGroupConfigurationId"<>'10000000-0000-0000-0000-000000000003'
      OR p."InitialDifficultyLevel"<>m.level) THEN
    RAISE EXCEPTION 'Old identifiers do not match the approved active adult training scope';
  END IF;
  IF (SELECT plan->>'ageGroupId' FROM adult_plan_v2_input) IS DISTINCT FROM '10000000-0000-0000-0000-000000000003'
    OR (SELECT plan->>'version' FROM adult_plan_v2_input) IS DISTINCT FROM '2'
    OR (SELECT jsonb_typeof(plan->'programs') FROM adult_plan_v2_input) IS DISTINCT FROM 'array'
    OR (SELECT jsonb_array_length(plan->'programs') FROM adult_plan_v2_input)<>5 THEN
    RAISE EXCEPTION 'Unexpected adult pack or age group';
  END IF;
  IF (SELECT array_agg((p->>'level')::int ORDER BY (p->>'level')::int)
    FROM adult_plan_v2_input i CROSS JOIN LATERAL jsonb_array_elements(i.plan->'programs') p)
    IS DISTINCT FROM ARRAY[1,2,3,4,5] THEN
    RAISE EXCEPTION 'Exactly one program per level is required';
  END IF;
  IF EXISTS (SELECT 1 FROM adult_plan_v2_input i CROSS JOIN LATERAL jsonb_array_elements(i.plan->'programs') p
    WHERE jsonb_typeof(p->'days') IS DISTINCT FROM 'array' OR jsonb_array_length(p->'days')<>28) THEN
    RAISE EXCEPTION 'Each adult program must have 28 days';
  END IF;
  IF EXISTS (SELECT 1 FROM adult_plan_v2_input i
    CROSS JOIN LATERAL jsonb_array_elements(i.plan->'programs') p
    CROSS JOIN LATERAL jsonb_array_elements(p->'days') WITH ORDINALITY d(tasks,position)
    WHERE jsonb_typeof(d.tasks) IS DISTINCT FROM 'array'
      OR jsonb_array_length(d.tasks)<>CASE WHEN d.position<=14 THEN 6 ELSE 7 END
      OR (SELECT count(*) FROM jsonb_array_elements_text(d.tasks) t(name)
        WHERE t.name IN('Focus','SchulteTable','EyeTracking','Saccade','Fixation','Tachistoscope','VisualExpansion'))
        <>CASE WHEN d.position<=14 THEN 4 WHEN d.position<=21 THEN 3 ELSE 2 END) THEN
    RAISE EXCEPTION 'Adult plan must match the approved daily and weekly balance';
  END IF;
END $$;

CREATE TEMP TABLE desired_adult_programs_v2 ON COMMIT DROP AS
WITH days AS (
  SELECT m.level, ((d.position-1)/7+1)::int AS week, ((d.position-1)%7+1)::int AS day,
    jsonb_agg(jsonb_build_object('type',t.name,'count',1,'difficulty',
      CASE WHEN t.name='Chunking' THEN least(m.level,4) ELSE m.level END) ORDER BY t.position) AS tasks
  FROM adult_program_v2_ids m CROSS JOIN adult_plan_v2_input i
  CROSS JOIN LATERAL jsonb_array_elements(i.plan->'programs') p
  CROSS JOIN LATERAL jsonb_array_elements(p->'days') WITH ORDINALITY d(tasks,position)
  CROSS JOIN LATERAL jsonb_array_elements_text(d.tasks) WITH ORDINALITY t(name,position)
  WHERE (p->>'level')::int=m.level GROUP BY m.level,d.position
), weeks AS (
  SELECT level,week,jsonb_object_agg('day'||day,tasks ORDER BY day) AS days FROM days GROUP BY level,week
), plans AS (
  SELECT level,jsonb_object_agg('week'||week,days ORDER BY week)
    ||jsonb_build_object('placementLevel',level) AS pattern FROM weeks GROUP BY level
)
SELECT m.new_id,m.level,plans.pattern,p.*
FROM adult_program_v2_ids m JOIN plans USING(level)
CROSS JOIN LATERAL (
  SELECT source.* FROM speed_reading.program_templates source
  WHERE source.id IN(m.old_id,m.new_id)
    AND source."TargetAgeGroupConfigurationId"='10000000-0000-0000-0000-000000000003'
    AND NOT source."IsAssessment" AND NOT source."IsDeleted"
  ORDER BY (source.id=m.old_id) DESC LIMIT 1
) p;

DO $$ BEGIN
  IF (SELECT count(*) FROM desired_adult_programs_v2)<>5 THEN
    RAISE EXCEPTION 'Exactly five known adult program sources are required';
  END IF;
  IF EXISTS (SELECT 1 FROM desired_adult_programs_v2 p
    CROSS JOIN LATERAL jsonb_each(p.pattern) w
    CROSS JOIN LATERAL jsonb_each(CASE WHEN jsonb_typeof(w.value)='object' THEN w.value ELSE '{}'::jsonb END) d
    CROSS JOIN LATERAL jsonb_array_elements(d.value) t
    WHERE NOT EXISTS (SELECT 1 FROM speed_reading.exercises e JOIN speed_reading.exercise_types et ON et.id=e.exercise_type_id
      WHERE e.target_age_group_id=p."TargetAgeGroupConfigurationId" AND et.name=t->>'type'
        AND e.difficulty_level=(t->>'difficulty')::int AND e.is_active AND NOT e.is_deleted
        AND et.is_active AND NOT et.is_deleted)) THEN
    RAISE EXCEPTION 'Missing active adult exercise at the configured level';
  END IF;
END $$;

INSERT INTO speed_reading.program_templates(id,"Name","Description","TargetAgeGroupConfigurationId",
  "MinAssessmentScore","MaxAssessmentScore","WeeklyPatternJson","InitialDifficultyLevel",
  "WeeksPerDifficultyIncrease","MaxDifficultyLevel","TotalWeeks","TotalDays","IsActive","DisplayOrder",
  "ProgramType","ExamType","IsAssessment",created_at,created_by,version,"IsDeleted")
SELECT new_id,'Yetişkin — Seviye '||level||' — Dengeli 4 Haftalık Program',
  '22 yaş ve üzeri. 28 gün, 182 görev. İlk 14 gün 6, son 14 gün 7 görev. Başlangıçta dikkat ve görsel algı ağırlığı; sonraki haftalarda artan okuma ve anlama çalışmaları.',
  "TargetAgeGroupConfigurationId","MinAssessmentScore","MaxAssessmentScore",pattern,level,0,
  "MaxDifficultyLevel",4,28,true,level,"ProgramType","ExamType",false,now(),'system:adult-programs-v2',1,false
FROM desired_adult_programs_v2 ON CONFLICT(id) DO NOTHING;

DO $$ BEGIN
  IF (SELECT count(*) FROM speed_reading.program_templates p JOIN desired_adult_programs_v2 d ON p.id=d.new_id
    WHERE p."WeeklyPatternJson"=d.pattern AND p."IsActive" AND NOT p."IsDeleted"
      AND p."TotalDays"=28 AND p."TotalWeeks"=4 AND p."WeeksPerDifficultyIncrease"=0
      AND NOT p."IsAssessment" AND p."TargetAgeGroupConfigurationId"=d."TargetAgeGroupConfigurationId"
      AND p."InitialDifficultyLevel"=d.level AND p."DisplayOrder"=d.level AND p."MaxDifficultyLevel"=d."MaxDifficultyLevel"
      AND p."MinAssessmentScore"=d."MinAssessmentScore" AND p."MaxAssessmentScore"=d."MaxAssessmentScore"
      AND p."ProgramType"=d."ProgramType" AND p."ExamType" IS NOT DISTINCT FROM d."ExamType"
      AND p."Name"='Yetişkin — Seviye '||d.level||' — Dengeli 4 Haftalık Program'
      AND p."Description"='22 yaş ve üzeri. 28 gün, 182 görev. İlk 14 gün 6, son 14 gün 7 görev. Başlangıçta dikkat ve görsel algı ağırlığı; sonraki haftalarda artan okuma ve anlama çalışmaları.'
      AND p.created_by='system:adult-programs-v2')<>5 THEN
    RAISE EXCEPTION 'Adult replacement verification failed; transaction rolled back';
  END IF;
END $$;
DELETE FROM speed_reading.program_templates WHERE id IN(SELECT old_id FROM adult_program_v2_ids);
COMMIT;
