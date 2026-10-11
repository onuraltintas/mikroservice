-- Only the 20 explicitly identified training exercises; assessments/history stay intact.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('visual-expansion-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE expansion_settings ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->'previousConfiguration' old_config, item->'configuration' config
FROM jsonb_array_elements($settings$[
  {
    "id": "2b8df54c-330b-53c2-9dc5-198f33bf3ec7",
    "age": "adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 1,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 12,
        "displayDurationMs": 1100,
        "startDegrees": 8,
        "targetDegrees": 18,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 45,
        "displayDurationMs": 1100,
        "startDegrees": 8,
        "targetDegrees": 18,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "9e9b945a-a399-594c-9973-25cb52dc33d6",
    "age": "adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 2,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 14,
        "displayDurationMs": 1000,
        "startDegrees": 8,
        "targetDegrees": 20,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 50,
        "displayDurationMs": 1000,
        "startDegrees": 8,
        "targetDegrees": 20,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "3207f6d0-a430-5c9d-8031-2e373ac0d20c",
    "age": "adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 3,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 16,
        "displayDurationMs": 950,
        "startDegrees": 8,
        "targetDegrees": 22,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 55,
        "displayDurationMs": 950,
        "startDegrees": 8,
        "targetDegrees": 22,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "a597f22e-4136-5032-b3b4-98c9360ab0f1",
    "age": "adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 4,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 18,
        "displayDurationMs": 900,
        "startDegrees": 8,
        "targetDegrees": 24,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 60,
        "displayDurationMs": 900,
        "startDegrees": 8,
        "targetDegrees": 24,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "1b693157-ad14-53cf-abdb-bf9eb0cb9456",
    "age": "adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 5,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 20,
        "displayDurationMs": 850,
        "startDegrees": 8,
        "targetDegrees": 26,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 65,
        "displayDurationMs": 850,
        "startDegrees": 8,
        "targetDegrees": 26,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "c159bff8-b3ee-556c-9fcc-6eab1cb62f0f",
    "age": "child",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 1,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 10,
        "displayDurationMs": 1200,
        "startDegrees": 8,
        "targetDegrees": 16,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 30,
        "displayDurationMs": 1200,
        "startDegrees": 8,
        "targetDegrees": 16,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "e9283521-69bc-58f4-b2b9-90341b43319e",
    "age": "child",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 2,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 12,
        "displayDurationMs": 1100,
        "startDegrees": 8,
        "targetDegrees": 18,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 35,
        "displayDurationMs": 1100,
        "startDegrees": 8,
        "targetDegrees": 18,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "59bc27fb-6e9d-5327-acab-0d77f925c104",
    "age": "child",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 3,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 14,
        "displayDurationMs": 1000,
        "startDegrees": 8,
        "targetDegrees": 20,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 40,
        "displayDurationMs": 1000,
        "startDegrees": 8,
        "targetDegrees": 20,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "dd072bd5-f52b-5d9f-9229-a9d50d305664",
    "age": "child",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 4,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 16,
        "displayDurationMs": 950,
        "startDegrees": 8,
        "targetDegrees": 22,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 45,
        "displayDurationMs": 950,
        "startDegrees": 8,
        "targetDegrees": 22,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "6dada25d-1303-5af9-8e5d-3a0bed836463",
    "age": "child",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 5,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 18,
        "displayDurationMs": 900,
        "startDegrees": 8,
        "targetDegrees": 24,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 50,
        "displayDurationMs": 900,
        "startDegrees": 8,
        "targetDegrees": 24,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "9b785bd0-3884-5708-9c08-4e913cdb3b93",
    "age": "teen",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 1,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 12,
        "displayDurationMs": 1100,
        "startDegrees": 8,
        "targetDegrees": 18,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 35,
        "displayDurationMs": 1100,
        "startDegrees": 8,
        "targetDegrees": 18,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "032fc96f-8326-5a6d-98c4-e1f5b9cf61b8",
    "age": "teen",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 2,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 14,
        "displayDurationMs": 1000,
        "startDegrees": 8,
        "targetDegrees": 20,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 40,
        "displayDurationMs": 1000,
        "startDegrees": 8,
        "targetDegrees": 20,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "ecada78f-008a-5c5d-a39f-8bd3fb3d0471",
    "age": "teen",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 3,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 16,
        "displayDurationMs": 950,
        "startDegrees": 8,
        "targetDegrees": 22,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 45,
        "displayDurationMs": 950,
        "startDegrees": 8,
        "targetDegrees": 22,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "c980c2be-7c63-59d7-8cc5-8e97d3d10496",
    "age": "teen",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 4,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 18,
        "displayDurationMs": 900,
        "startDegrees": 8,
        "targetDegrees": 24,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 50,
        "displayDurationMs": 900,
        "startDegrees": 8,
        "targetDegrees": 24,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "af60fd95-ed21-5892-b09c-db52e2317c1f",
    "age": "teen",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 5,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 20,
        "displayDurationMs": 850,
        "startDegrees": 8,
        "targetDegrees": 26,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 55,
        "displayDurationMs": 850,
        "startDegrees": 8,
        "targetDegrees": 26,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "383a2ffe-b397-59cf-a344-0d73a7159ab1",
    "age": "young-adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 1,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 12,
        "displayDurationMs": 1100,
        "startDegrees": 8,
        "targetDegrees": 18,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 40,
        "displayDurationMs": 1100,
        "startDegrees": 8,
        "targetDegrees": 18,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "92540438-4920-5168-a021-3b58011b275c",
    "age": "young-adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 2,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 14,
        "displayDurationMs": 1000,
        "startDegrees": 8,
        "targetDegrees": 20,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 45,
        "displayDurationMs": 1000,
        "startDegrees": 8,
        "targetDegrees": 20,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "9a09590a-0a4c-51d5-8b3d-ef884680bb38",
    "age": "young-adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 3,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 16,
        "displayDurationMs": 950,
        "startDegrees": 8,
        "targetDegrees": 22,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 50,
        "displayDurationMs": 950,
        "startDegrees": 8,
        "targetDegrees": 22,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "2b68f647-27da-522b-ac9a-292395fda5be",
    "age": "young-adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 4,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 18,
        "displayDurationMs": 900,
        "startDegrees": 8,
        "targetDegrees": 24,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 55,
        "displayDurationMs": 900,
        "startDegrees": 8,
        "targetDegrees": 24,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "53d8fbf9-dbf7-50ba-8226-90fd9937b6ca",
    "age": "young-adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 5,
    "previousConfiguration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 20,
        "displayDurationMs": 850,
        "startDegrees": 8,
        "targetDegrees": 26,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "visual_expansion",
      "engineConfig": {
        "mode": "horizontal",
        "rounds": 60,
        "displayDurationMs": 850,
        "startDegrees": 8,
        "targetDegrees": 26,
        "content": {
          "stimulusType": "letter"
        }
      },
      "difficultyLevel": 5
    }
  }
]$settings$::jsonb) item;
DO $$ BEGIN
  IF (SELECT count(*) FROM expansion_settings)<>20 OR (SELECT count(DISTINCT id) FROM expansion_settings)<>20
    THEN RAISE EXCEPTION 'Visual Expansion payload incomplete'; END IF;
  IF EXISTS(SELECT 1 FROM expansion_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE e.id IS NULL OR t.name IS DISTINCT FROM 'VisualExpansion' OR NOT t.is_active OR t.is_deleted
      OR NOT e.is_active OR e.is_deleted OR e.target_age_group_id IS DISTINCT FROM s.age_id
      OR e.difficulty_level<>s.level
      OR (e.configuration_json IS DISTINCT FROM s.old_config AND e.configuration_json IS DISTINCT FROM s.config))
    THEN RAISE EXCEPTION 'Visual Expansion baseline changed; review before applying'; END IF;
END $$;
UPDATE speed_reading.exercises e SET configuration_json=s.config,
  updated_at=now(),updated_by='visual-expansion-v1',version=e.version+1
FROM expansion_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.config;
DO $$ BEGIN
  IF (SELECT count(*) FROM expansion_settings s JOIN speed_reading.exercises e ON e.id=s.id
    WHERE e.configuration_json=s.config)<>20 THEN RAISE EXCEPTION 'Visual Expansion verification failed'; END IF;
END $$;
COMMIT;
