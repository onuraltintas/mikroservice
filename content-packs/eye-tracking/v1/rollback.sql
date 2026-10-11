-- Restore only these 20 catalog configurations, never delete sessions/results/programs.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('eye-tracking-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE tracking_settings ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->'previousConfiguration' old_config, item->'configuration' config
FROM jsonb_array_elements($settings$[
  {
    "id": "6d05bc6b-136d-5a7d-98f3-d5d2330ad00c",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "age": "child",
    "level": 1,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "circle"
        },
        "timing": {
          "durationSeconds": 30,
          "speedMs": 3000
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "horizontal"
        },
        "timing": {
          "durationSeconds": 45,
          "speedMs": 4000
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "74f49a2e-2010-5bba-a5be-c939d13b687a",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "age": "child",
    "level": 2,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "horizontal"
        },
        "timing": {
          "durationSeconds": 40,
          "speedMs": 2800
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "circle"
        },
        "timing": {
          "durationSeconds": 60,
          "speedMs": 3800
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "350b5194-aaea-5674-8e7f-be637a8a830e",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "age": "child",
    "level": 3,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "circle"
        },
        "timing": {
          "durationSeconds": 45,
          "speedMs": 2500
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "circle"
        },
        "timing": {
          "durationSeconds": 75,
          "speedMs": 3600
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "555c285c-8c6a-500c-8278-707acdcd6e9a",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "age": "child",
    "level": 4,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "infinity8"
        },
        "timing": {
          "durationSeconds": 45,
          "speedMs": 3000
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "infinity8"
        },
        "timing": {
          "durationSeconds": 90,
          "speedMs": 3400
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "098b2568-ea3f-5257-bda7-b5c263ed423f",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "age": "child",
    "level": 5,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "infinity8"
        },
        "timing": {
          "durationSeconds": 60,
          "speedMs": 2800
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "infinity8"
        },
        "timing": {
          "durationSeconds": 105,
          "speedMs": 3200
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "cc9e4ee9-af4b-5107-b721-6a9eaa685466",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "age": "teen",
    "level": 1,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "horizontal"
        },
        "timing": {
          "durationSeconds": 40,
          "speedMs": 3000
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "horizontal"
        },
        "timing": {
          "durationSeconds": 60,
          "speedMs": 3600
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "7eb93296-212a-5af4-8619-5de147ae5db0",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "age": "teen",
    "level": 2,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "circle"
        },
        "timing": {
          "durationSeconds": 45,
          "speedMs": 2800
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "circle"
        },
        "timing": {
          "durationSeconds": 75,
          "speedMs": 3400
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "0c68a21e-e0c5-5a60-abf4-f95b5012c42a",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "age": "teen",
    "level": 3,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "circle"
        },
        "timing": {
          "durationSeconds": 45,
          "speedMs": 2600
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "circle"
        },
        "timing": {
          "durationSeconds": 90,
          "speedMs": 3200
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "be78d65c-0404-5a02-bed2-6f94631f73fd",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "age": "teen",
    "level": 4,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "infinity8"
        },
        "timing": {
          "durationSeconds": 60,
          "speedMs": 2500
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "infinity8"
        },
        "timing": {
          "durationSeconds": 105,
          "speedMs": 3000
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "b4e42319-939f-5581-be48-1253dfdb85ea",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "age": "teen",
    "level": 5,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "infinity8"
        },
        "timing": {
          "durationSeconds": 60,
          "speedMs": 2300
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "infinity8"
        },
        "timing": {
          "durationSeconds": 120,
          "speedMs": 2800
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "044e0883-df6e-560a-b50a-73406dd3292e",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "age": "young-adult",
    "level": 1,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "horizontal"
        },
        "timing": {
          "durationSeconds": 40,
          "speedMs": 3000
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "horizontal"
        },
        "timing": {
          "durationSeconds": 75,
          "speedMs": 3400
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "15b9f327-47ef-5f93-bc29-de4edff6fb51",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "age": "young-adult",
    "level": 2,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "circle"
        },
        "timing": {
          "durationSeconds": 45,
          "speedMs": 2800
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "circle"
        },
        "timing": {
          "durationSeconds": 90,
          "speedMs": 3200
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "c1292ed5-2e91-54e6-8b08-5e2f788826e2",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "age": "young-adult",
    "level": 3,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "circle"
        },
        "timing": {
          "durationSeconds": 45,
          "speedMs": 2600
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "circle"
        },
        "timing": {
          "durationSeconds": 105,
          "speedMs": 3000
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "bcf0ad9b-9030-5979-b632-1b9e0049f914",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "age": "young-adult",
    "level": 4,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "infinity8"
        },
        "timing": {
          "durationSeconds": 60,
          "speedMs": 2500
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "infinity8"
        },
        "timing": {
          "durationSeconds": 120,
          "speedMs": 2800
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "f7309daa-6a6f-53d6-b5a8-a55ab2699cdc",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "age": "young-adult",
    "level": 5,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "infinity8"
        },
        "timing": {
          "durationSeconds": 60,
          "speedMs": 2300
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "infinity8"
        },
        "timing": {
          "durationSeconds": 135,
          "speedMs": 2600
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "61f6b205-f34a-5b44-a189-ca44a9739570",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "age": "adult",
    "level": 1,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "horizontal"
        },
        "timing": {
          "durationSeconds": 40,
          "speedMs": 3000
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "horizontal"
        },
        "timing": {
          "durationSeconds": 90,
          "speedMs": 3200
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "a8710c96-b35d-5074-b733-47b3688d10a2",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "age": "adult",
    "level": 2,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "circle"
        },
        "timing": {
          "durationSeconds": 45,
          "speedMs": 2800
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "circle"
        },
        "timing": {
          "durationSeconds": 105,
          "speedMs": 3000
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "5eb88e8b-0384-5de6-a795-b4cb0f584b65",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "age": "adult",
    "level": 3,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "circle"
        },
        "timing": {
          "durationSeconds": 45,
          "speedMs": 2600
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "circle"
        },
        "timing": {
          "durationSeconds": 120,
          "speedMs": 2800
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "fe947caa-d394-56a6-8acd-8b717107845f",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "age": "adult",
    "level": 4,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "infinity8"
        },
        "timing": {
          "durationSeconds": 60,
          "speedMs": 2500
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "infinity8"
        },
        "timing": {
          "durationSeconds": 135,
          "speedMs": 2600
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "3ac88567-f6de-545e-adf1-f41acb2a28a9",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "age": "adult",
    "level": 5,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "infinity8"
        },
        "timing": {
          "durationSeconds": 60,
          "speedMs": 2300
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "tracking",
        "path": {
          "type": "infinity8"
        },
        "timing": {
          "durationSeconds": 150,
          "speedMs": 2400
        },
        "content": {
          "type": "dot",
          "pointSize": 36
        }
      },
      "difficultyLevel": 5
    }
  }
]$settings$::jsonb) item;
DO $$ BEGIN
  IF (SELECT count(*) FROM tracking_settings)<>20 OR (SELECT count(DISTINCT id) FROM tracking_settings)<>20
    THEN RAISE EXCEPTION 'EyeTracking rollback payload incomplete'; END IF;
  IF EXISTS(SELECT 1 FROM tracking_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE e.id IS NULL OR t.name IS DISTINCT FROM 'EyeTracking' OR t.engine_type IS DISTINCT FROM 'motion_path'
      OR NOT t.is_active OR t.is_deleted OR NOT e.is_active OR e.is_deleted
      OR e.target_age_group_id IS DISTINCT FROM s.age_id OR e.difficulty_level IS DISTINCT FROM s.level
      OR (e.configuration_json IS DISTINCT FROM s.config AND e.configuration_json IS DISTINCT FROM s.old_config))
    THEN RAISE EXCEPTION 'EyeTracking rollback baseline changed; review before applying'; END IF;
END $$;
UPDATE speed_reading.exercises e SET configuration_json=s.old_config,updated_at=now(),updated_by='eye-tracking-v1-rollback',version=e.version+1
FROM tracking_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.old_config;
COMMIT;
