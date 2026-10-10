-- psql: supply plan_json from infrastructure/data/child-program-plan-v2.json.
-- Requires the new task-slot service and migration; run only after a DB backup.
BEGIN;
LOCK TABLE speed_reading.program_templates IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE child_plan_v2_input ON COMMIT DROP AS SELECT :'plan_json'::jsonb AS plan;
CREATE TEMP TABLE child_program_v2_ids(old_id uuid, new_id uuid, level integer) ON COMMIT DROP;
INSERT INTO child_program_v2_ids VALUES
('8e6b102d-5c6d-5247-916d-d67ab3f7719e','e2c01001-8d9a-4e6c-a428-000000000001',1),
('aac00b94-c237-56d0-89fb-30548e953ae1','e2c01001-8d9a-4e6c-a428-000000000002',2),
('bef6047b-2ac5-5162-8967-8aaed9c49893','e2c01001-8d9a-4e6c-a428-000000000003',3),
('b89ab8e0-fa03-5ab0-ba8a-7085aec613df','e2c01001-8d9a-4e6c-a428-000000000004',4),
('5619aefe-ac50-5afc-9f55-a422899c6a80','e2c01001-8d9a-4e6c-a428-000000000005',5);
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='speed_reading'
    AND table_name='daily_exercise_logs' AND column_name='slot_order') THEN
    RAISE EXCEPTION 'Task-slot migration must be applied before child program replacement';
  END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.student_program_progress p
    JOIN child_program_v2_ids m ON p."ProgramTemplateId"=m.old_id) THEN
    RAISE EXCEPTION 'Old child programs have progress; deletion is blocked to preserve history';
  END IF;
  IF EXISTS (SELECT 1 FROM speed_reading.program_templates p JOIN child_program_v2_ids m ON p.id=m.old_id
    WHERE p."IsAssessment" OR p."TargetAgeGroupConfigurationId"<>'10000000-0000-0000-0000-000000000001') THEN
    RAISE EXCEPTION 'Old program identifiers do not match the approved child training scope';
  END IF;
  IF (SELECT plan->>'ageGroupId' FROM child_plan_v2_input) IS DISTINCT FROM '10000000-0000-0000-0000-000000000001'
    OR (SELECT plan->>'version' FROM child_plan_v2_input) IS DISTINCT FROM '2'
    OR (SELECT jsonb_typeof(plan->'days') FROM child_plan_v2_input) IS DISTINCT FROM 'array'
    OR (SELECT jsonb_array_length(plan->'days') FROM child_plan_v2_input) <> 28 THEN
    RAISE EXCEPTION 'Unexpected child plan or age group';
  END IF;
  IF EXISTS (SELECT 1 FROM child_plan_v2_input i
    CROSS JOIN LATERAL jsonb_array_elements(i.plan->'days') WITH ORDINALITY day(tasks,position)
    WHERE jsonb_array_length(day.tasks) <> CASE WHEN position<=14 THEN 6 ELSE 7 END
      OR (SELECT count(*) FROM jsonb_array_elements_text(day.tasks) task(name)
        WHERE task.name IN('Focus','SchulteTable','EyeTracking','Saccade','Fixation','Tachistoscope','VisualExpansion'))
        <> CASE WHEN position<=14 THEN 4 WHEN position<=21 THEN 3 ELSE 2 END) THEN
    RAISE EXCEPTION 'Child plan must match the approved 182-task weekly distribution';
  END IF;
END $$;

CREATE TEMP TABLE desired_child_programs_v2 ON COMMIT DROP AS
WITH days AS (
  SELECT m.level, ((d.position-1)/7+1)::int AS week, ((d.position-1)%7+1)::int AS day,
    jsonb_agg(jsonb_build_object('type',task.name,'count',1,'difficulty',
      CASE WHEN task.name='Chunking' THEN least(m.level,4)
           WHEN task.name='FreeReading' AND m.level=4 THEN 3 ELSE m.level END)
      ORDER BY task.position) AS tasks
  FROM child_program_v2_ids m CROSS JOIN child_plan_v2_input i
  CROSS JOIN LATERAL jsonb_array_elements(i.plan->'days') WITH ORDINALITY d(tasks,position)
  CROSS JOIN LATERAL jsonb_array_elements_text(d.tasks) WITH ORDINALITY task(name,position)
  GROUP BY m.level,d.position
), weeks AS (
  SELECT level,week,jsonb_object_agg('day'||day,tasks ORDER BY day) AS days
  FROM days GROUP BY level,week
), plans AS (
  SELECT level,jsonb_object_agg('week'||week,days ORDER BY week)
    || jsonb_build_object('placementLevel',level) AS pattern FROM weeks GROUP BY level
)
SELECT m.new_id,m.level,plans.pattern,p.*
FROM child_program_v2_ids m JOIN plans USING(level)
CROSS JOIN LATERAL (
  SELECT source.* FROM speed_reading.program_templates source
  WHERE source.id IN(m.old_id,m.new_id)
    AND source."TargetAgeGroupConfigurationId"='10000000-0000-0000-0000-000000000001'
    AND NOT source."IsAssessment" AND NOT source."IsDeleted"
  ORDER BY (source.id=m.old_id) DESC LIMIT 1
) p;

DO $$ BEGIN
  IF (SELECT count(*) FROM desired_child_programs_v2)<>5 THEN
    RAISE EXCEPTION 'Exactly five known child program sources are required';
  END IF;
  IF EXISTS (SELECT 1 FROM desired_child_programs_v2 p
    CROSS JOIN LATERAL jsonb_each(p.pattern) week
    CROSS JOIN LATERAL jsonb_each(CASE WHEN jsonb_typeof(week.value)='object' THEN week.value ELSE '{}'::jsonb END) day
    CROSS JOIN LATERAL jsonb_array_elements(day.value) task
    WHERE NOT EXISTS (SELECT 1 FROM speed_reading.exercises e
      JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
      WHERE e.target_age_group_id=p."TargetAgeGroupConfigurationId" AND t.name=task->>'type'
        AND e.difficulty_level=(task->>'difficulty')::int AND e.is_active AND NOT e.is_deleted
        AND t.is_active AND NOT t.is_deleted)) THEN
    RAISE EXCEPTION 'Missing active child exercise at the configured level';
  END IF;
END $$;

INSERT INTO speed_reading.program_templates(id,"Name","Description","TargetAgeGroupConfigurationId",
  "MinAssessmentScore","MaxAssessmentScore","WeeklyPatternJson","InitialDifficultyLevel",
  "WeeksPerDifficultyIncrease","MaxDifficultyLevel","TotalWeeks","TotalDays","IsActive","DisplayOrder",
  "ProgramType","ExamType","IsAssessment",created_at,created_by,version,"IsDeleted")
SELECT new_id,'Çocuk — Seviye '||level||' — Dengeli 4 Haftalık Program',
  '9–12 yaş. 28 gün, 182 görev. İlk 14 gün 6, son 14 gün 7 görev. Başlangıçta dikkat ve görsel algı ağırlığı; sonraki haftalarda artan okuma ve anlama çalışmaları.',
  "TargetAgeGroupConfigurationId","MinAssessmentScore","MaxAssessmentScore",pattern,level,0,
  "MaxDifficultyLevel",4,28,true,level,"ProgramType","ExamType",false,now(),'system:child-programs-v2',1,false
FROM desired_child_programs_v2 ON CONFLICT(id) DO NOTHING;

DO $$ BEGIN
  IF (SELECT count(*) FROM speed_reading.program_templates p JOIN desired_child_programs_v2 d ON p.id=d.new_id
    WHERE p."WeeklyPatternJson"=d.pattern AND p."IsActive" AND NOT p."IsDeleted"
      AND p."TotalDays"=28 AND p."TotalWeeks"=4 AND p."WeeksPerDifficultyIncrease"=0
      AND NOT p."IsAssessment" AND p."TargetAgeGroupConfigurationId"=d."TargetAgeGroupConfigurationId"
      AND p."InitialDifficultyLevel"=d.level AND p."DisplayOrder"=d.level
      AND p."MaxDifficultyLevel"=d."MaxDifficultyLevel"
      AND p."MinAssessmentScore"=d."MinAssessmentScore" AND p."MaxAssessmentScore"=d."MaxAssessmentScore"
      AND p."ProgramType"=d."ProgramType" AND p."ExamType" IS NOT DISTINCT FROM d."ExamType"
      AND p."Name"='Çocuk — Seviye '||d.level||' — Dengeli 4 Haftalık Program'
      AND p."Description"='9–12 yaş. 28 gün, 182 görev. İlk 14 gün 6, son 14 gün 7 görev. Başlangıçta dikkat ve görsel algı ağırlığı; sonraki haftalarda artan okuma ve anlama çalışmaları.'
      AND p.created_by='system:child-programs-v2')<>5 THEN
    RAISE EXCEPTION 'Child program verification failed; transaction rolled back';
  END IF;
END $$;
DELETE FROM speed_reading.program_templates WHERE id IN(SELECT old_id FROM child_program_v2_ids);
COMMIT;
