-- Hedef Avı v1: adds the TargetHunt exercise type and twenty normal training exercises (4 age groups x 5 levels).
-- Nothing existing is changed or deleted. Programs switch to these exercises separately (programs/v5).
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('target-hunt-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE target_hunt_settings ON COMMIT DROP AS
SELECT $settings${
  "type": {
    "id": "f0ad8054-6c26-564e-9cc1-be8cfe21b4bc",
    "name": "TargetHunt",
    "displayName": "Hedef Avı",
    "description": "Satırlardaki hedef harf veya kelimeyi süre bitmeden bulma; benzer görünenlere dokunmama.",
    "engineType": "target_hunt",
    "iconName": "ads_click",
    "colorCode": "#f57c00",
    "category": "attention-and-eye-movement"
  },
  "exercises": [
    {
      "id": "199b2439-5bc3-51eb-ae80-232d97f7207d",
      "ageGroupId": "10000000-0000-0000-0000-000000000001",
      "level": 1,
      "title": "Hedef Avı: \"b\" harfi (Seviye 1)",
      "description": "Satırlardaki her \"b\" harfine dokun; \"a\", \"e\" ve \"o\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "b",
          "distractors": [
            "a",
            "e",
            "o"
          ],
          "rows": 5,
          "columns": 8,
          "targetCount": 10,
          "timeLimit": 60
        },
        "difficultyLevel": 1
      }
    },
    {
      "id": "514ab17c-8834-5d41-91c2-e494f6d821f7",
      "ageGroupId": "10000000-0000-0000-0000-000000000001",
      "level": 2,
      "title": "Hedef Avı: \"b\" harfi (Seviye 2)",
      "description": "Satırlardaki her \"b\" harfine dokun; \"d\", \"o\" ve \"e\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "b",
          "distractors": [
            "d",
            "o",
            "e"
          ],
          "rows": 6,
          "columns": 8,
          "targetCount": 12,
          "timeLimit": 70
        },
        "difficultyLevel": 2
      }
    },
    {
      "id": "d4c3ed74-6f48-502f-bc96-602ae894cfde",
      "ageGroupId": "10000000-0000-0000-0000-000000000001",
      "level": 3,
      "title": "Hedef Avı: \"b\" harfi (Seviye 3)",
      "description": "Satırlardaki her \"b\" harfine dokun; \"d\" ve \"p\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "b",
          "distractors": [
            "d",
            "p"
          ],
          "rows": 6,
          "columns": 10,
          "targetCount": 15,
          "timeLimit": 80
        },
        "difficultyLevel": 3
      }
    },
    {
      "id": "8ba82358-9213-50a2-ab99-231db308f8c2",
      "ageGroupId": "10000000-0000-0000-0000-000000000001",
      "level": 4,
      "title": "Hedef Avı: \"b\" harfi (Seviye 4)",
      "description": "Satırlardaki her \"b\" harfine dokun; \"d\", \"p\" ve \"q\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "b",
          "distractors": [
            "d",
            "p",
            "q"
          ],
          "rows": 7,
          "columns": 10,
          "targetCount": 17,
          "timeLimit": 90
        },
        "difficultyLevel": 4
      }
    },
    {
      "id": "a33d0202-f99c-5b6a-a4fb-050c16f52057",
      "ageGroupId": "10000000-0000-0000-0000-000000000001",
      "level": 5,
      "title": "Hedef Avı: \"ve\" kelimesi (Seviye 5)",
      "description": "Satırlardaki her \"ve\" kelimesine dokun; \"ev\", \"ne\" ve \"ye\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "ve",
          "distractors": [
            "ev",
            "ne",
            "ye"
          ],
          "rows": 7,
          "columns": 10,
          "targetCount": 17,
          "timeLimit": 90
        },
        "difficultyLevel": 5
      }
    },
    {
      "id": "35fe899b-a57a-582f-ba38-a8733675caff",
      "ageGroupId": "10000000-0000-0000-0000-000000000002",
      "level": 1,
      "title": "Hedef Avı: \"b\" harfi (Seviye 1)",
      "description": "Satırlardaki her \"b\" harfine dokun; \"d\", \"o\" ve \"e\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "b",
          "distractors": [
            "d",
            "o",
            "e"
          ],
          "rows": 6,
          "columns": 10,
          "targetCount": 15,
          "timeLimit": 75
        },
        "difficultyLevel": 1
      }
    },
    {
      "id": "d2e33f09-017f-5964-9753-01ba1ccac7af",
      "ageGroupId": "10000000-0000-0000-0000-000000000002",
      "level": 2,
      "title": "Hedef Avı: \"b\" harfi (Seviye 2)",
      "description": "Satırlardaki her \"b\" harfine dokun; \"d\" ve \"p\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "b",
          "distractors": [
            "d",
            "p"
          ],
          "rows": 7,
          "columns": 10,
          "targetCount": 17,
          "timeLimit": 85
        },
        "difficultyLevel": 2
      }
    },
    {
      "id": "9b950d74-d0ed-5f0e-b5c2-43060bfc2334",
      "ageGroupId": "10000000-0000-0000-0000-000000000002",
      "level": 3,
      "title": "Hedef Avı: \"b\" harfi (Seviye 3)",
      "description": "Satırlardaki her \"b\" harfine dokun; \"d\", \"p\" ve \"q\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "b",
          "distractors": [
            "d",
            "p",
            "q"
          ],
          "rows": 8,
          "columns": 10,
          "targetCount": 20,
          "timeLimit": 95
        },
        "difficultyLevel": 3
      }
    },
    {
      "id": "aa28b99c-c4d8-546d-9e78-361fa1f1523b",
      "ageGroupId": "10000000-0000-0000-0000-000000000002",
      "level": 4,
      "title": "Hedef Avı: \"ve\" kelimesi (Seviye 4)",
      "description": "Satırlardaki her \"ve\" kelimesine dokun; \"ev\", \"ne\" ve \"ye\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "ve",
          "distractors": [
            "ev",
            "ne",
            "ye"
          ],
          "rows": 8,
          "columns": 10,
          "targetCount": 20,
          "timeLimit": 90
        },
        "difficultyLevel": 4
      }
    },
    {
      "id": "60bfbda8-e03f-57ca-b0ae-e9921924bf86",
      "ageGroupId": "10000000-0000-0000-0000-000000000002",
      "level": 5,
      "title": "Hedef Avı: \"kar\" kelimesi (Seviye 5)",
      "description": "Satırlardaki her \"kar\" kelimesine dokun; \"kra\", \"rak\" ve \"kır\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "kar",
          "distractors": [
            "kra",
            "rak",
            "kır"
          ],
          "rows": 8,
          "columns": 10,
          "targetCount": 20,
          "timeLimit": 90
        },
        "difficultyLevel": 5
      }
    },
    {
      "id": "592a43a7-94db-589b-835f-ee8cecc84bcd",
      "ageGroupId": "10000000-0000-0000-0000-000000000004",
      "level": 1,
      "title": "Hedef Avı: \"b\" harfi (Seviye 1)",
      "description": "Satırlardaki her \"b\" harfine dokun; \"d\" ve \"p\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "b",
          "distractors": [
            "d",
            "p"
          ],
          "rows": 7,
          "columns": 10,
          "targetCount": 17,
          "timeLimit": 75
        },
        "difficultyLevel": 1
      }
    },
    {
      "id": "a7410db4-b9f6-53d8-abb2-c08276a75210",
      "ageGroupId": "10000000-0000-0000-0000-000000000004",
      "level": 2,
      "title": "Hedef Avı: \"b\" harfi (Seviye 2)",
      "description": "Satırlardaki her \"b\" harfine dokun; \"d\", \"p\" ve \"q\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "b",
          "distractors": [
            "d",
            "p",
            "q"
          ],
          "rows": 8,
          "columns": 10,
          "targetCount": 20,
          "timeLimit": 85
        },
        "difficultyLevel": 2
      }
    },
    {
      "id": "4c769f69-bf50-5bbd-8800-ea993e1b4b9a",
      "ageGroupId": "10000000-0000-0000-0000-000000000004",
      "level": 3,
      "title": "Hedef Avı: \"ve\" kelimesi (Seviye 3)",
      "description": "Satırlardaki her \"ve\" kelimesine dokun; \"ev\", \"ne\" ve \"ye\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "ve",
          "distractors": [
            "ev",
            "ne",
            "ye"
          ],
          "rows": 8,
          "columns": 10,
          "targetCount": 20,
          "timeLimit": 80
        },
        "difficultyLevel": 3
      }
    },
    {
      "id": "4c99f8ff-f2d9-5a48-9541-9e022cf31acb",
      "ageGroupId": "10000000-0000-0000-0000-000000000004",
      "level": 4,
      "title": "Hedef Avı: \"kar\" kelimesi (Seviye 4)",
      "description": "Satırlardaki her \"kar\" kelimesine dokun; \"kra\", \"rak\" ve \"kır\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "kar",
          "distractors": [
            "kra",
            "rak",
            "kır"
          ],
          "rows": 9,
          "columns": 10,
          "targetCount": 22,
          "timeLimit": 90
        },
        "difficultyLevel": 4
      }
    },
    {
      "id": "136d3767-3c97-5b8f-aa28-b8efbad430a5",
      "ageGroupId": "10000000-0000-0000-0000-000000000004",
      "level": 5,
      "title": "Hedef Avı: \"bir\" kelimesi (Seviye 5)",
      "description": "Satırlardaki her \"bir\" kelimesine dokun; \"dir\", \"bil\" ve \"rib\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "bir",
          "distractors": [
            "dir",
            "bil",
            "rib"
          ],
          "rows": 10,
          "columns": 10,
          "targetCount": 25,
          "timeLimit": 95
        },
        "difficultyLevel": 5
      }
    },
    {
      "id": "e8713e4f-7b8a-5b8a-9bb4-e20e8c1e1f50",
      "ageGroupId": "10000000-0000-0000-0000-000000000003",
      "level": 1,
      "title": "Hedef Avı: \"b\" harfi (Seviye 1)",
      "description": "Satırlardaki her \"b\" harfine dokun; \"d\" ve \"p\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "b",
          "distractors": [
            "d",
            "p"
          ],
          "rows": 7,
          "columns": 10,
          "targetCount": 17,
          "timeLimit": 75
        },
        "difficultyLevel": 1
      }
    },
    {
      "id": "b9158650-9e39-5ecf-8e9a-7d6d87697500",
      "ageGroupId": "10000000-0000-0000-0000-000000000003",
      "level": 2,
      "title": "Hedef Avı: \"b\" harfi (Seviye 2)",
      "description": "Satırlardaki her \"b\" harfine dokun; \"d\", \"p\" ve \"q\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "b",
          "distractors": [
            "d",
            "p",
            "q"
          ],
          "rows": 8,
          "columns": 10,
          "targetCount": 20,
          "timeLimit": 85
        },
        "difficultyLevel": 2
      }
    },
    {
      "id": "c8eb6ddb-d9d3-5ab8-9e87-206568f28d44",
      "ageGroupId": "10000000-0000-0000-0000-000000000003",
      "level": 3,
      "title": "Hedef Avı: \"ve\" kelimesi (Seviye 3)",
      "description": "Satırlardaki her \"ve\" kelimesine dokun; \"ev\", \"ne\" ve \"ye\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "ve",
          "distractors": [
            "ev",
            "ne",
            "ye"
          ],
          "rows": 8,
          "columns": 10,
          "targetCount": 20,
          "timeLimit": 80
        },
        "difficultyLevel": 3
      }
    },
    {
      "id": "2e4470cb-a711-536c-9cb8-a338fb79f34f",
      "ageGroupId": "10000000-0000-0000-0000-000000000003",
      "level": 4,
      "title": "Hedef Avı: \"kar\" kelimesi (Seviye 4)",
      "description": "Satırlardaki her \"kar\" kelimesine dokun; \"kra\", \"rak\" ve \"kır\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "kar",
          "distractors": [
            "kra",
            "rak",
            "kır"
          ],
          "rows": 9,
          "columns": 10,
          "targetCount": 22,
          "timeLimit": 90
        },
        "difficultyLevel": 4
      }
    },
    {
      "id": "61bf8b02-215b-5cdf-b6b2-4f16a9b5d94b",
      "ageGroupId": "10000000-0000-0000-0000-000000000003",
      "level": 5,
      "title": "Hedef Avı: \"bir\" kelimesi (Seviye 5)",
      "description": "Satırlardaki her \"bir\" kelimesine dokun; \"dir\", \"bil\" ve \"rib\" gibi benzerlerine dokunma.",
      "configuration": {
        "engineType": "target_hunt",
        "engineConfig": {
          "target": "bir",
          "distractors": [
            "dir",
            "bil",
            "rib"
          ],
          "rows": 10,
          "columns": 10,
          "targetCount": 25,
          "timeLimit": 95
        },
        "difficultyLevel": 5
      }
    }
  ]
}$settings$::jsonb AS payload;
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
