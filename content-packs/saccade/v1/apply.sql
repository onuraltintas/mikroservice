-- Only identified Saccade training records; no history, assessment or assignment deletion.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('saccade-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE saccade_settings ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->'previousConfiguration' old_config, item->'configuration' config
FROM jsonb_array_elements($settings$[
  {
    "id": "aeeca01f-1ce7-5689-9b60-fc7e919f4966",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 1,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 900,
          "durationSeconds": 30
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 900,
          "durationSeconds": 40
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "efd3f58d-5798-5baf-87a2-145dc2a6aaed",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 2,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 800,
          "durationSeconds": 35
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 850,
          "durationSeconds": 45
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "6f062c54-97bc-5182-9ae5-be7474822339",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 3,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 750,
          "durationSeconds": 40
        },
        "content": {
          "type": "number",
          "pattern": "vertical",
          "pointSize": 44
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 800,
          "durationSeconds": 50
        },
        "content": {
          "type": "number",
          "pattern": "vertical",
          "pointSize": 44
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "b2cb492e-760a-562c-a47c-898aa420bbab",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 4,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 700,
          "durationSeconds": 45
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 750,
          "durationSeconds": 55
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "67fb334c-878b-5cbb-bd12-bb353daf362e",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 5,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 650,
          "durationSeconds": 45
        },
        "content": {
          "type": "number",
          "pattern": "random",
          "pointSize": 44
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 700,
          "durationSeconds": 60
        },
        "content": {
          "type": "number",
          "pattern": "random",
          "pointSize": 44
        }
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "88c23c60-bb49-5998-9aa7-050432a47089",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 1,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 800,
          "durationSeconds": 35
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 850,
          "durationSeconds": 45
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "7e8c1a46-8816-5166-941d-2d30e1cad08a",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 2,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 750,
          "durationSeconds": 40
        },
        "content": {
          "type": "number",
          "pattern": "vertical",
          "pointSize": 44
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 800,
          "durationSeconds": 50
        },
        "content": {
          "type": "number",
          "pattern": "vertical",
          "pointSize": 44
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "2733b693-b05e-584b-a103-57d2057e46e0",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 3,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 700,
          "durationSeconds": 45
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 750,
          "durationSeconds": 60
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "bc1f3eca-4c63-590d-a199-d15b6bb36ef2",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 4,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 650,
          "durationSeconds": 45
        },
        "content": {
          "type": "number",
          "pattern": "vertical",
          "pointSize": 44
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 700,
          "durationSeconds": 70
        },
        "content": {
          "type": "number",
          "pattern": "vertical",
          "pointSize": 44
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "49f3cffd-39cd-5d49-951b-cbf166fc68f7",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 5,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 600,
          "durationSeconds": 45
        },
        "content": {
          "type": "number",
          "pattern": "random",
          "pointSize": 44
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 650,
          "durationSeconds": 80
        },
        "content": {
          "type": "number",
          "pattern": "random",
          "pointSize": 44
        }
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "d3aabed8-260b-5372-9236-eab48522e9c6",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 1,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 800,
          "durationSeconds": 35
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 800,
          "durationSeconds": 50
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "9fd33b56-157d-5525-9e6f-2bd7c7ab4b51",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 2,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 750,
          "durationSeconds": 40
        },
        "content": {
          "type": "number",
          "pattern": "vertical",
          "pointSize": 44
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 750,
          "durationSeconds": 60
        },
        "content": {
          "type": "number",
          "pattern": "vertical",
          "pointSize": 44
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "1c633a1e-bd58-5f03-869c-779cb9e8decb",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 3,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 700,
          "durationSeconds": 45
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 700,
          "durationSeconds": 70
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "64fe8828-b439-5cff-b58f-0e9cd88198ad",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 4,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 650,
          "durationSeconds": 45
        },
        "content": {
          "type": "number",
          "pattern": "vertical",
          "pointSize": 44
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 650,
          "durationSeconds": 80
        },
        "content": {
          "type": "number",
          "pattern": "vertical",
          "pointSize": 44
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "822040a0-3ad3-59ae-a17c-650280175640",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 5,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 600,
          "durationSeconds": 45
        },
        "content": {
          "type": "number",
          "pattern": "random",
          "pointSize": 44
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 600,
          "durationSeconds": 90
        },
        "content": {
          "type": "number",
          "pattern": "random",
          "pointSize": 44
        }
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "affcd5cd-a45c-5e9e-acbb-739f329d8fb2",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 1,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 800,
          "durationSeconds": 35
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 850,
          "durationSeconds": 50
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "61de30ea-ae36-5cc6-b38b-d0bc0a4f0cb5",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 2,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 750,
          "durationSeconds": 40
        },
        "content": {
          "type": "number",
          "pattern": "vertical",
          "pointSize": 44
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 800,
          "durationSeconds": 60
        },
        "content": {
          "type": "number",
          "pattern": "vertical",
          "pointSize": 44
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "bf6a1707-20f6-50c6-b12d-98d567296d01",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 3,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 700,
          "durationSeconds": 45
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 750,
          "durationSeconds": 70
        },
        "content": {
          "type": "number",
          "pattern": "horizontal",
          "pointSize": 44
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "36fbcbb0-e078-5503-b1b9-ab5dfcef2616",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 4,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 650,
          "durationSeconds": 45
        },
        "content": {
          "type": "number",
          "pattern": "vertical",
          "pointSize": 44
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 700,
          "durationSeconds": 80
        },
        "content": {
          "type": "number",
          "pattern": "vertical",
          "pointSize": 44
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "489942f0-b120-5a2f-8b1c-5049eb23b298",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 5,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 600,
          "durationSeconds": 45
        },
        "content": {
          "type": "number",
          "pattern": "random",
          "pointSize": 44
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "saccade",
        "timing": {
          "holdMs": 650,
          "durationSeconds": 90
        },
        "content": {
          "type": "number",
          "pattern": "random",
          "pointSize": 44
        }
      },
      "difficultyLevel": 5
    }
  }
]$settings$::jsonb) item;
DO $$ BEGIN
  IF (SELECT count(*) FROM saccade_settings)<>20 OR (SELECT count(DISTINCT id) FROM saccade_settings)<>20
    THEN RAISE EXCEPTION 'Saccade payload incomplete'; END IF;
  IF EXISTS(SELECT 1 FROM saccade_settings s
    WHERE s.config->>'engineType' IS DISTINCT FROM 'motion_path'
      OR s.config#>>'{engineConfig,mode}' IS DISTINCT FROM 'saccade'
      OR NOT CASE WHEN jsonb_typeof(s.config#>'{engineConfig,timing,holdMs}')='number'
        AND s.config#>>'{engineConfig,timing,holdMs}' ~ '^[1-9][0-9]{0,4}$'
        THEN (s.config#>>'{engineConfig,timing,holdMs}')::int BETWEEN 50 AND 10000 ELSE false END
      OR NOT CASE WHEN jsonb_typeof(s.config#>'{engineConfig,timing,durationSeconds}')='number'
        AND s.config#>>'{engineConfig,timing,durationSeconds}' ~ '^[1-9][0-9]{0,4}$'
        THEN (s.config#>>'{engineConfig,timing,durationSeconds}')::int BETWEEN 5 AND 3600 ELSE false END
      OR (s.config #- '{engineConfig,timing,holdMs}' #- '{engineConfig,timing,durationSeconds}')
        IS DISTINCT FROM (s.old_config #- '{engineConfig,timing,holdMs}' #- '{engineConfig,timing,durationSeconds}'))
    THEN RAISE EXCEPTION 'Saccade payload changes unapproved fields'; END IF;
  IF EXISTS(SELECT 1 FROM saccade_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE e.id IS NULL OR t.name IS DISTINCT FROM 'Saccade' OR t.engine_type IS DISTINCT FROM 'motion_path'
      OR NOT t.is_active OR t.is_deleted OR NOT e.is_active OR e.is_deleted
      OR e.target_age_group_id IS DISTINCT FROM s.age_id OR e.difficulty_level IS DISTINCT FROM s.level
      OR (e.configuration_json IS DISTINCT FROM s.old_config AND e.configuration_json IS DISTINCT FROM s.config))
    THEN RAISE EXCEPTION 'Saccade baseline changed; review before applying'; END IF;
  IF EXISTS(SELECT 1 FROM saccade_settings s JOIN speed_reading.exercises e
      ON e.target_age_group_id=s.age_id AND e.difficulty_level=s.level AND e.id<>s.id
      JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
      WHERE t.name='Saccade' AND e.is_active AND NOT e.is_deleted)
    THEN RAISE EXCEPTION 'Saccade duplicate training record; review before applying'; END IF;
END $$;
UPDATE speed_reading.exercises e SET configuration_json=s.config,
  updated_at=now(),updated_by='saccade-v1',version=e.version+1
FROM saccade_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.config;
DO $$ BEGIN
  IF (SELECT count(*) FROM saccade_settings s JOIN speed_reading.exercises e ON e.id=s.id
    WHERE e.configuration_json=s.config)<>20 THEN RAISE EXCEPTION 'Saccade verification failed'; END IF;
END $$;
COMMIT;
