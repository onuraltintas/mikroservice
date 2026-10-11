-- Only the twenty identified normal Focus records; assessments and history stay unchanged.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('focus-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE focus_settings ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->'previousConfiguration' old_config, item->'configuration' config
FROM jsonb_array_elements($settings$[
  {
    "id": "1f9bcc44-6bf6-588e-a6c9-87fbcc57f5e6",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 1,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 12,
        "speedMs": 2500,
        "gridSize": 3
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 32,
        "speedMs": 3000,
        "gridSize": 3
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "eb968e3b-1d20-551c-bf84-0ce50acd7510",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 2,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 16,
        "speedMs": 2300,
        "gridSize": 3
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 40,
        "speedMs": 2800,
        "gridSize": 3
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "cb0b533d-84bd-5d78-a06c-199b968f0bd8",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 3,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 20,
        "speedMs": 2100,
        "gridSize": 3
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 48,
        "speedMs": 2600,
        "gridSize": 3
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "f05d5094-094d-5739-8a06-5d58cf39bb53",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 4,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 24,
        "speedMs": 2000,
        "gridSize": 3
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 2,
        "totalSteps": 48,
        "speedMs": 3000,
        "gridSize": 3
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "730afda5-23eb-5323-b237-77b527c8a27c",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 5,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 24,
        "speedMs": 1800,
        "gridSize": 3
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 2,
        "totalSteps": 56,
        "speedMs": 2800,
        "gridSize": 3
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "4cd282b6-9a2b-5c52-bd1a-2213f08a98d2",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 1,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 16,
        "speedMs": 2300,
        "gridSize": 3
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 40,
        "speedMs": 2800,
        "gridSize": 3
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "ffe98e72-6131-5a84-b5fb-d723fc319637",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 2,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 20,
        "speedMs": 2100,
        "gridSize": 3
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 48,
        "speedMs": 2500,
        "gridSize": 3
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "0a7f05d8-f836-5a29-8d55-927150eedad8",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 3,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 24,
        "speedMs": 2000,
        "gridSize": 3
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 2,
        "totalSteps": 48,
        "speedMs": 2800,
        "gridSize": 3
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "f63b5845-7541-5828-9973-8610e198110f",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 4,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 28,
        "speedMs": 1800,
        "gridSize": 3
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 2,
        "totalSteps": 56,
        "speedMs": 2500,
        "gridSize": 3
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "0f487932-7f27-513a-a0e5-0bf9404b8c8b",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 5,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 30,
        "speedMs": 1600,
        "gridSize": 3
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 3,
        "totalSteps": 56,
        "speedMs": 3000,
        "gridSize": 3
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "fa53b143-e64f-5bcf-8986-ea5394047453",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 1,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 16,
        "speedMs": 2300,
        "gridSize": 3
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 48,
        "speedMs": 2600,
        "gridSize": 3
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "64a9789a-f515-58b9-b606-59f9a3ca68b3",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 2,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 20,
        "speedMs": 2100,
        "gridSize": 3
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 2,
        "totalSteps": 48,
        "speedMs": 2900,
        "gridSize": 3
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "69261140-377b-55d4-aba0-6e108980fd00",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 3,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 24,
        "speedMs": 2000,
        "gridSize": 3
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 2,
        "totalSteps": 56,
        "speedMs": 2600,
        "gridSize": 3
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "5f1dc195-0be9-5b0e-b3e1-8ca60a1bdfc6",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 4,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 28,
        "speedMs": 1800,
        "gridSize": 3
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 3,
        "totalSteps": 56,
        "speedMs": 3000,
        "gridSize": 3
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "be744984-094c-5d13-ace5-c6f9810bf66e",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 5,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 30,
        "speedMs": 1600,
        "gridSize": 3
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 3,
        "totalSteps": 64,
        "speedMs": 2700,
        "gridSize": 3
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "71abefe8-6122-55b9-8ebe-ae4d8d27123d",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 1,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 16,
        "speedMs": 2300,
        "gridSize": 3
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 48,
        "speedMs": 2700,
        "gridSize": 3
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "ba1bcf8c-6884-532a-a59f-1a7cfc74cd9c",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 2,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 20,
        "speedMs": 2100,
        "gridSize": 3
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 2,
        "totalSteps": 48,
        "speedMs": 3000,
        "gridSize": 3
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "4f987dc5-213e-5b89-a933-52b3aa552433",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 3,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 24,
        "speedMs": 2000,
        "gridSize": 3
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 2,
        "totalSteps": 56,
        "speedMs": 2700,
        "gridSize": 3
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "1c66c565-e9ed-5ed0-8eb4-94dd40fcb64b",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 4,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 28,
        "speedMs": 1800,
        "gridSize": 3
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 3,
        "totalSteps": 56,
        "speedMs": 3100,
        "gridSize": 3
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "f37af5c9-d3c5-58bc-9a2f-8123961bb77f",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 5,
    "previousConfiguration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 1,
        "totalSteps": 30,
        "speedMs": 1600,
        "gridSize": 3
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "focus",
      "engineConfig": {
        "mode": "position",
        "nLevel": 3,
        "totalSteps": 64,
        "speedMs": 2800,
        "gridSize": 3
      },
      "difficultyLevel": 5
    }
  }
]$settings$::jsonb) item;
DO $$ BEGIN
  IF (SELECT count(*) FROM focus_settings)<>20 OR (SELECT count(DISTINCT id) FROM focus_settings)<>20
    OR (SELECT count(DISTINCT (age_id,level)) FROM focus_settings)<>20
    THEN RAISE EXCEPTION 'Focus payload incomplete'; END IF;
  IF EXISTS(SELECT 1 FROM focus_settings s
    WHERE s.config->>'engineType' IS DISTINCT FROM 'focus'
      OR s.config#>>'{engineConfig,mode}' IS DISTINCT FROM 'position'
      OR s.config#>'{engineConfig,gridSize}' IS DISTINCT FROM '3'::jsonb
      OR NOT CASE WHEN jsonb_typeof(s.config#>'{engineConfig,nLevel}')='number'
        THEN s.config#>>'{engineConfig,nLevel}' IN ('1','2','3') ELSE false END
      OR NOT CASE WHEN jsonb_typeof(s.config#>'{engineConfig,speedMs}')='number'
        AND s.config#>>'{engineConfig,speedMs}' ~ '^[1-9][0-9]{0,4}$'
        THEN (s.config#>>'{engineConfig,speedMs}')::int BETWEEN 2500 AND 3100 ELSE false END
      OR NOT CASE WHEN jsonb_typeof(s.config#>'{engineConfig,totalSteps}')='number'
        THEN s.config#>>'{engineConfig,totalSteps}' IN ('32','40','48','56','64') ELSE false END
      OR (s.config #- '{engineConfig,nLevel}' #- '{engineConfig,speedMs}' #- '{engineConfig,totalSteps}')
        IS DISTINCT FROM (s.old_config #- '{engineConfig,nLevel}' #- '{engineConfig,speedMs}' #- '{engineConfig,totalSteps}'))
    THEN RAISE EXCEPTION 'Focus payload changes unapproved fields'; END IF;
  IF EXISTS(SELECT 1 FROM focus_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE e.id IS NULL OR t.name IS DISTINCT FROM 'Focus' OR t.engine_type IS DISTINCT FROM 'focus'
      OR NOT t.is_active OR t.is_deleted OR NOT e.is_active OR e.is_deleted
      OR e.target_age_group_id IS DISTINCT FROM s.age_id OR e.difficulty_level IS DISTINCT FROM s.level
      OR (e.configuration_json IS DISTINCT FROM s.old_config AND e.configuration_json IS DISTINCT FROM s.config))
    THEN RAISE EXCEPTION 'Focus baseline changed; review before applying'; END IF;
  IF EXISTS(SELECT 1 FROM focus_settings s JOIN speed_reading.exercises e
      ON e.target_age_group_id=s.age_id AND e.difficulty_level=s.level AND e.id<>s.id
      JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
      WHERE t.name='Focus' AND e.is_active AND NOT e.is_deleted)
    THEN RAISE EXCEPTION 'Focus duplicate training record; review before applying'; END IF;
END $$;
UPDATE speed_reading.exercises e SET configuration_json=s.config,
  updated_at=now(),updated_by='focus-v1',version=e.version+1
FROM focus_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.config;
DO $$ BEGIN
  IF (SELECT count(*) FROM focus_settings s JOIN speed_reading.exercises e ON e.id=s.id
    WHERE e.configuration_json=s.config)<>20 THEN RAISE EXCEPTION 'Focus verification failed'; END IF;
END $$;
COMMIT;
