-- Hedef Avı v1: adds the TargetHunt exercise type and twenty normal training exercises (4 age groups x 5 levels).
-- Nothing existing is changed or deleted. Programs switch to these exercises separately (programs/v5).
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('target-hunt-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE target_hunt_settings ON COMMIT DROP AS
SELECT $settings${{SETTINGS}}$settings$::jsonb AS payload;
CREATE TEMP TABLE target_hunt_rows ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->>'title' title, item->>'description' description, item->'configuration' config
FROM target_hunt_settings, jsonb_array_elements(payload->'exercises') item;
DO $$
DECLARE type_row jsonb := (SELECT payload->'type' FROM target_hunt_settings);
BEGIN
  IF (SELECT count(*) FROM target_hunt_rows)<>20 OR (SELECT count(DISTINCT id) FROM target_hunt_rows)<>20
    OR (SELECT count(DISTINCT (age_id,level)) FROM target_hunt_rows)<>20
    THEN RAISE EXCEPTION 'TargetHunt payload incomplete'; END IF;
  IF EXISTS(SELECT 1 FROM target_hunt_rows WHERE config->>'engineType' IS DISTINCT FROM 'target_hunt'
      OR (config->>'difficultyLevel')::int IS DISTINCT FROM level OR level NOT BETWEEN 1 AND 5)
    THEN RAISE EXCEPTION 'TargetHunt payload has an unexpected engine or level'; END IF;
  IF NOT EXISTS(SELECT 1 FROM speed_reading.exercise_type_categories
      WHERE name=type_row->>'category' AND NOT is_deleted)
    THEN RAISE EXCEPTION 'Category attention-and-eye-movement is missing; run the taxonomy bootstrap first'; END IF;
  IF EXISTS(SELECT 1 FROM speed_reading.exercise_types
      WHERE NOT is_deleted AND (name=type_row->>'name' OR id=(type_row->>'id')::uuid)
        AND (id<>(type_row->>'id')::uuid OR name<>type_row->>'name' OR engine_type<>type_row->>'engineType'))
    THEN RAISE EXCEPTION 'An existing TargetHunt exercise type conflicts with this pack; review before applying'; END IF;
  IF EXISTS(SELECT 1 FROM target_hunt_rows r JOIN speed_reading.exercises e ON e.id=r.id
      WHERE e.exercise_type_id<>(type_row->>'id')::uuid OR e.configuration_json IS DISTINCT FROM r.config
        OR e.target_age_group_id IS DISTINCT FROM r.age_id OR e.difficulty_level<>r.level)
    THEN RAISE EXCEPTION 'TargetHunt exercises changed since this pack was generated; review before applying'; END IF;
END $$;
INSERT INTO speed_reading.exercise_types
  (id,name,display_name,description,icon_name,color_code,sort_order,is_active,engine_type,category_id,
   created_at,created_by,version,is_deleted)
SELECT (t->>'id')::uuid, t->>'name', t->>'displayName', t->>'description', t->>'iconName', t->>'colorCode', 0, true,
  t->>'engineType', (SELECT c.id FROM speed_reading.exercise_type_categories c WHERE c.name=t->>'category' AND NOT c.is_deleted),
  now(), 'target-hunt-v1', 1, false
FROM (SELECT payload->'type' t FROM target_hunt_settings) source
ON CONFLICT (id) DO NOTHING;
INSERT INTO speed_reading.exercises
  (id,exercise_type_id,title,description,type_code,difficulty_level,configuration_json,target_age_group_id,
   creator_id,is_active,is_assessment_only,created_at,created_by,version,is_deleted)
SELECT r.id, (SELECT (payload->'type'->>'id')::uuid FROM target_hunt_settings), r.title, r.description, 'TargetHunt',
  r.level, r.config, r.age_id, 'b993e2f8-3948-49bd-9cbd-463e8771c20d'::uuid, true, false, now(), 'target-hunt-v1', 1, false
FROM target_hunt_rows r
ON CONFLICT (id) DO NOTHING;
DO $$ BEGIN
  IF (SELECT count(*) FROM target_hunt_rows r JOIN speed_reading.exercises e ON e.id=r.id
      WHERE e.configuration_json=r.config AND e.is_active AND NOT e.is_deleted)<>20
    THEN RAISE EXCEPTION 'TargetHunt verification failed'; END IF;
END $$;
COMMIT;
