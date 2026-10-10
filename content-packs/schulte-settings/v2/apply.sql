-- Only the twenty named training exercises. Assessment/session snapshots are untouched.
BEGIN;
SET LOCAL lock_timeout='10s';
SET LOCAL statement_timeout='60s';
SELECT pg_advisory_xact_lock(20261011, 2001);
LOCK TABLE speed_reading.exercises IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE schulte_patch ON COMMIT DROP AS
SELECT (item->>'id')::uuid id,(item->>'ageGroupId')::uuid age_group_id,
 (item->>'level')::int level,(item->>'oldGridSize')::int old_grid,
 (item->>'oldTimeLimit')::int old_time,(item->>'gridSize')::int new_grid,
 (item->>'timeLimit')::int new_time
FROM jsonb_array_elements($settings$[{"age":"Child","id":"add57a25-c884-5851-b27f-8fe0fe413f6f","ageGroupId":"10000000-0000-0000-0000-000000000001","level":1,"oldGridSize":3,"oldTimeLimit":90,"gridSize":3,"timeLimit":45},{"age":"Child","id":"3ea61ae4-3637-56d4-a6a6-a3d8057f406b","ageGroupId":"10000000-0000-0000-0000-000000000001","level":2,"oldGridSize":3,"oldTimeLimit":75,"gridSize":3,"timeLimit":30},{"age":"Child","id":"c1fc2991-2b6e-5ae4-ad6d-aa62e0ef0abd","ageGroupId":"10000000-0000-0000-0000-000000000001","level":3,"oldGridSize":4,"oldTimeLimit":120,"gridSize":4,"timeLimit":60},{"age":"Child","id":"e43c9878-8c70-51c0-b9fd-7526747fa5d9","ageGroupId":"10000000-0000-0000-0000-000000000001","level":4,"oldGridSize":4,"oldTimeLimit":100,"gridSize":4,"timeLimit":45},{"age":"Child","id":"670c82a7-66cc-5f2a-8feb-ae3002f02c2a","ageGroupId":"10000000-0000-0000-0000-000000000001","level":5,"oldGridSize":5,"oldTimeLimit":150,"gridSize":5,"timeLimit":90},{"age":"Teen","id":"aaabc650-2bfa-5c38-a8f8-5c4728aa33ef","ageGroupId":"10000000-0000-0000-0000-000000000002","level":1,"oldGridSize":4,"oldTimeLimit":120,"gridSize":4,"timeLimit":60},{"age":"Teen","id":"1da1b433-db34-5d05-af76-8a7a63a8b4db","ageGroupId":"10000000-0000-0000-0000-000000000002","level":2,"oldGridSize":4,"oldTimeLimit":100,"gridSize":4,"timeLimit":45},{"age":"Teen","id":"84334437-52d8-5352-802d-76045fad0dc5","ageGroupId":"10000000-0000-0000-0000-000000000002","level":3,"oldGridSize":5,"oldTimeLimit":150,"gridSize":5,"timeLimit":90},{"age":"Teen","id":"40feec30-398e-51a9-bd86-68679a80399b","ageGroupId":"10000000-0000-0000-0000-000000000002","level":4,"oldGridSize":5,"oldTimeLimit":120,"gridSize":5,"timeLimit":60},{"age":"Teen","id":"b04edcad-d063-5928-a646-40301db65385","ageGroupId":"10000000-0000-0000-0000-000000000002","level":5,"oldGridSize":5,"oldTimeLimit":100,"gridSize":6,"timeLimit":120},{"age":"YoungAdult","id":"b10650e6-3bd1-5228-86ed-6eeb60de5a97","ageGroupId":"10000000-0000-0000-0000-000000000004","level":1,"oldGridSize":4,"oldTimeLimit":120,"gridSize":4,"timeLimit":60},{"age":"YoungAdult","id":"61248859-b87b-5849-b175-7956c01a9b1a","ageGroupId":"10000000-0000-0000-0000-000000000004","level":2,"oldGridSize":4,"oldTimeLimit":100,"gridSize":5,"timeLimit":90},{"age":"YoungAdult","id":"fc18cf9d-81a7-5645-b533-f5e0ae20d5bc","ageGroupId":"10000000-0000-0000-0000-000000000004","level":3,"oldGridSize":5,"oldTimeLimit":150,"gridSize":5,"timeLimit":60},{"age":"YoungAdult","id":"515cf27a-b306-5a37-86e8-4768512e076f","ageGroupId":"10000000-0000-0000-0000-000000000004","level":4,"oldGridSize":5,"oldTimeLimit":120,"gridSize":6,"timeLimit":120},{"age":"YoungAdult","id":"34d9e9dc-4515-56c2-b348-16c79d7a7445","ageGroupId":"10000000-0000-0000-0000-000000000004","level":5,"oldGridSize":5,"oldTimeLimit":100,"gridSize":7,"timeLimit":150},{"age":"Adult","id":"622ef34e-d579-57dd-aac4-af82e7c192ca","ageGroupId":"10000000-0000-0000-0000-000000000003","level":1,"oldGridSize":4,"oldTimeLimit":120,"gridSize":4,"timeLimit":60},{"age":"Adult","id":"71e3cf3f-5e10-5995-a3e1-363a5a87b899","ageGroupId":"10000000-0000-0000-0000-000000000003","level":2,"oldGridSize":4,"oldTimeLimit":100,"gridSize":5,"timeLimit":90},{"age":"Adult","id":"061da412-99ff-515f-be25-eb5518a0ae0a","ageGroupId":"10000000-0000-0000-0000-000000000003","level":3,"oldGridSize":5,"oldTimeLimit":150,"gridSize":5,"timeLimit":60},{"age":"Adult","id":"9b8454f0-ee0f-51a6-aea4-24a1edef4b82","ageGroupId":"10000000-0000-0000-0000-000000000003","level":4,"oldGridSize":5,"oldTimeLimit":120,"gridSize":6,"timeLimit":120},{"age":"Adult","id":"b0a7e63c-edc5-573e-8416-df18584a9b0d","ageGroupId":"10000000-0000-0000-0000-000000000003","level":5,"oldGridSize":5,"oldTimeLimit":100,"gridSize":7,"timeLimit":150}]$settings$::jsonb) item;
DO $$ BEGIN
 IF (SELECT count(*) FROM schulte_patch)<>20 OR (SELECT count(DISTINCT id) FROM schulte_patch)<>20 THEN
  RAISE EXCEPTION 'Expected twenty distinct Schulte patches';
 END IF;
 IF (SELECT count(*) FROM speed_reading.exercises e JOIN schulte_patch p ON e.id=p.id
  WHERE e.type_code='SchulteTable' AND e.target_age_group_id=p.age_group_id AND e.difficulty_level=p.level
   AND e.is_active AND NOT e.is_deleted
   AND e.configuration_json->>'engineType'='grid_interaction'
   AND e.configuration_json#>>'{engineConfig,sequenceType}'='numeric')<>20 THEN
  RAISE EXCEPTION 'Schulte identity/age/level/engine changed';
 END IF;
 IF (SELECT count(*) FROM speed_reading.exercises
  WHERE type_code='SchulteTable' AND is_active AND NOT is_deleted
   AND target_age_group_id IN(SELECT age_group_id FROM schulte_patch))<>20 THEN
  RAISE EXCEPTION 'Unexpected duplicate or additional Schulte training exercise';
 END IF;
 IF EXISTS(SELECT 1 FROM speed_reading.exercises e JOIN schulte_patch p ON e.id=p.id
  WHERE NOT(
   (e.configuration_json#>'{engineConfig,gridSize}' IS NOT DISTINCT FROM to_jsonb(p.old_grid)
    AND e.configuration_json#>'{engineConfig,rules,timeLimit}' IS NOT DISTINCT FROM to_jsonb(p.old_time))
   OR (e.configuration_json#>'{engineConfig,gridSize}' IS NOT DISTINCT FROM to_jsonb(p.new_grid)
    AND e.configuration_json#>'{engineConfig,rules,timeLimit}' IS NOT DISTINCT FROM to_jsonb(p.new_time)))
   OR jsonb_typeof(e.configuration_json->'engineConfig') IS DISTINCT FROM 'object'
   OR jsonb_typeof(e.configuration_json#>'{engineConfig,rules}') IS DISTINCT FROM 'object'
   OR EXISTS(SELECT 1 FROM jsonb_object_keys(e.configuration_json) k(key)
    WHERE lower(key) IN('gridsize','timelimit','timelimitseconds','timing','rules','grid')
     OR (lower(key) IN('engineconfig','enginetype','difficultylevel') AND key NOT IN('engineConfig','engineType','difficultyLevel')))
   OR EXISTS(SELECT 1 FROM jsonb_object_keys(e.configuration_json->'engineConfig') k(key)
    WHERE lower(key) IN('timelimit','timelimitseconds','timing','grid')
     OR (lower(key) IN('gridsize','rules','sequencetype') AND key NOT IN('gridSize','rules','sequenceType')))
   OR EXISTS(SELECT 1 FROM jsonb_object_keys(e.configuration_json#>'{engineConfig,rules}') k(key)
    WHERE lower(key)='timelimit' AND key<>'timeLimit')) THEN
  RAISE EXCEPTION 'Schulte settings differ from the reviewed source or have conflicting aliases';
 END IF;
END $$;
CREATE TEMP TABLE schulte_before ON COMMIT DROP AS
SELECT e.* FROM speed_reading.exercises e JOIN schulte_patch p ON e.id=p.id;
UPDATE speed_reading.exercises e
SET configuration_json=jsonb_set(jsonb_set(e.configuration_json,
 '{engineConfig,gridSize}',to_jsonb(p.new_grid)), '{engineConfig,rules,timeLimit}',to_jsonb(p.new_time)),
 updated_at=now(),updated_by='system:schulte-settings-v2',version=e.version+1
FROM schulte_patch p WHERE e.id=p.id
 AND (e.configuration_json#>'{engineConfig,gridSize}'<>to_jsonb(p.new_grid)
  OR e.configuration_json#>'{engineConfig,rules,timeLimit}'<>to_jsonb(p.new_time));
DO $$ BEGIN
 IF EXISTS(SELECT 1 FROM speed_reading.exercises e JOIN schulte_patch p ON e.id=p.id
  WHERE e.configuration_json#>'{engineConfig,gridSize}' IS DISTINCT FROM to_jsonb(p.new_grid)
   OR e.configuration_json#>'{engineConfig,rules,timeLimit}' IS DISTINCT FROM to_jsonb(p.new_time)) THEN
  RAISE EXCEPTION 'Schulte settings verification failed';
 END IF;
 IF EXISTS(SELECT 1 FROM speed_reading.exercises e JOIN schulte_before b ON e.id=b.id
  JOIN schulte_patch p ON e.id=p.id
  WHERE e.configuration_json IS DISTINCT FROM jsonb_set(jsonb_set(b.configuration_json,
   '{engineConfig,gridSize}',to_jsonb(p.new_grid)), '{engineConfig,rules,timeLimit}',to_jsonb(p.new_time))
   OR (to_jsonb(e)-ARRAY['configuration_json','updated_at','updated_by','version'])
    IS DISTINCT FROM (to_jsonb(b)-ARRAY['configuration_json','updated_at','updated_by','version'])) THEN
  RAISE EXCEPTION 'Unexpected change outside Schulte dimensions/timing/audit fields';
 END IF;
END $$;
COMMIT;
