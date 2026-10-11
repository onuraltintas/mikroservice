-- Only the 20 explicitly identified training exercises; assessments/history stay intact.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('fixation-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE fixation_settings ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->'previousConfiguration' old_config, item->'configuration' config
FROM jsonb_array_elements($settings$[
  {
    "id": "c94edaba-0cec-522a-9752-3830c4c9be22",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 1,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 1000,
          "durationSeconds": 30
        },
        "content": {
          "type": "letter",
          "points": 3,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 1000
        },
        "content": {
          "type": "letter",
          "points": 20,
          "peripheralCount": 1,
          "pointSize": 44,
          "maxPeripheralCount": 2
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "d2d2c9f8-a6e0-5e64-9a85-1134b85be859",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 2,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 900,
          "durationSeconds": 35
        },
        "content": {
          "type": "letter",
          "points": 3,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 900
        },
        "content": {
          "type": "letter",
          "points": 25,
          "peripheralCount": 1,
          "pointSize": 44,
          "maxPeripheralCount": 2
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "d9eaa96f-ea25-508b-8ec2-df4775e187e7",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 3,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 850,
          "durationSeconds": 40
        },
        "content": {
          "type": "letter",
          "points": 4,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 850
        },
        "content": {
          "type": "letter",
          "points": 30,
          "peripheralCount": 2,
          "pointSize": 44,
          "maxPeripheralCount": 2
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "71a7618f-ea2f-524a-afc4-a6b902f5efe9",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 4,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 800,
          "durationSeconds": 45
        },
        "content": {
          "type": "letter",
          "points": 4,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 800
        },
        "content": {
          "type": "letter",
          "points": 35,
          "peripheralCount": 2,
          "pointSize": 44,
          "maxPeripheralCount": 2
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "113dce1d-f33d-5711-92e4-249f4af376cc",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 5,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 750,
          "durationSeconds": 45
        },
        "content": {
          "type": "letter",
          "points": 5,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 750
        },
        "content": {
          "type": "letter",
          "points": 40,
          "peripheralCount": 2,
          "pointSize": 44,
          "maxPeripheralCount": 2
        }
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "01aa6406-59ab-55e2-9ab9-0be5da1170c2",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 1,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 900,
          "durationSeconds": 35
        },
        "content": {
          "type": "letter",
          "points": 3,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 900
        },
        "content": {
          "type": "letter",
          "points": 25,
          "peripheralCount": 1,
          "pointSize": 44,
          "maxPeripheralCount": 3
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "a1eabc8f-3212-5b2f-aab8-9faaa0656760",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 2,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 850,
          "durationSeconds": 40
        },
        "content": {
          "type": "letter",
          "points": 4,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 850
        },
        "content": {
          "type": "letter",
          "points": 30,
          "peripheralCount": 2,
          "pointSize": 44,
          "maxPeripheralCount": 3
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "f9f288ed-76f9-50c9-9c0b-5a98308b4414",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 3,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 800,
          "durationSeconds": 45
        },
        "content": {
          "type": "letter",
          "points": 4,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 800
        },
        "content": {
          "type": "letter",
          "points": 35,
          "peripheralCount": 2,
          "pointSize": 44,
          "maxPeripheralCount": 3
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "cd487941-ba90-5100-8375-09b63ce65101",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 4,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 750,
          "durationSeconds": 45
        },
        "content": {
          "type": "letter",
          "points": 5,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 750
        },
        "content": {
          "type": "letter",
          "points": 40,
          "peripheralCount": 3,
          "pointSize": 44,
          "maxPeripheralCount": 3
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "c61d0f03-ea26-5579-937c-4b7507941e86",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 5,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 700,
          "durationSeconds": 45
        },
        "content": {
          "type": "letter",
          "points": 5,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 700
        },
        "content": {
          "type": "letter",
          "points": 45,
          "peripheralCount": 3,
          "pointSize": 44,
          "maxPeripheralCount": 3
        }
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "8b7b8e25-9274-5233-9f16-f06f06075be8",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 1,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 900,
          "durationSeconds": 35
        },
        "content": {
          "type": "letter",
          "points": 3,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 900
        },
        "content": {
          "type": "letter",
          "points": 30,
          "peripheralCount": 2,
          "pointSize": 44,
          "maxPeripheralCount": 4
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "74094d00-1fce-5bb3-bb2b-c9a6f07b6ed4",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 2,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 850,
          "durationSeconds": 40
        },
        "content": {
          "type": "letter",
          "points": 4,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 850
        },
        "content": {
          "type": "letter",
          "points": 35,
          "peripheralCount": 2,
          "pointSize": 44,
          "maxPeripheralCount": 4
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "bee53326-518d-5271-a48e-30f3b987b78b",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 3,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 800,
          "durationSeconds": 45
        },
        "content": {
          "type": "letter",
          "points": 4,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 800
        },
        "content": {
          "type": "letter",
          "points": 40,
          "peripheralCount": 3,
          "pointSize": 44,
          "maxPeripheralCount": 4
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "44fa6fce-c58a-55ad-9f4c-5571d25a8148",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 4,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 750,
          "durationSeconds": 45
        },
        "content": {
          "type": "letter",
          "points": 5,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 750
        },
        "content": {
          "type": "letter",
          "points": 45,
          "peripheralCount": 3,
          "pointSize": 44,
          "maxPeripheralCount": 4
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "52388c32-c1ec-58e4-9e87-ecb3c663a699",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 5,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 700,
          "durationSeconds": 45
        },
        "content": {
          "type": "letter",
          "points": 5,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 700
        },
        "content": {
          "type": "letter",
          "points": 50,
          "peripheralCount": 4,
          "pointSize": 44,
          "maxPeripheralCount": 4
        }
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "e46c49df-f099-576e-9d97-1ada74853026",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 1,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 900,
          "durationSeconds": 35
        },
        "content": {
          "type": "letter",
          "points": 3,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 900
        },
        "content": {
          "type": "letter",
          "points": 35,
          "peripheralCount": 2,
          "pointSize": 44,
          "maxPeripheralCount": 4
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "b4aa48a9-dfd6-5605-bd79-173af01d2aba",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 2,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 850,
          "durationSeconds": 40
        },
        "content": {
          "type": "letter",
          "points": 4,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 850
        },
        "content": {
          "type": "letter",
          "points": 40,
          "peripheralCount": 3,
          "pointSize": 44,
          "maxPeripheralCount": 4
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "72df579c-cce8-56ba-a06b-5979e54f188d",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 3,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 800,
          "durationSeconds": 45
        },
        "content": {
          "type": "letter",
          "points": 4,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 800
        },
        "content": {
          "type": "letter",
          "points": 45,
          "peripheralCount": 3,
          "pointSize": 44,
          "maxPeripheralCount": 4
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "a0d609e7-583d-5968-a655-fdd70e69689b",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 4,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 750,
          "durationSeconds": 45
        },
        "content": {
          "type": "letter",
          "points": 5,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 750
        },
        "content": {
          "type": "letter",
          "points": 50,
          "peripheralCount": 4,
          "pointSize": 44,
          "maxPeripheralCount": 4
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "5b39a28a-df2b-5af1-8ac2-2b2a6722314a",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 5,
    "previousConfiguration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 700,
          "durationSeconds": 45
        },
        "content": {
          "type": "letter",
          "points": 5,
          "peripheralCount": 1,
          "pointSize": 44
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "motion_path",
      "engineConfig": {
        "mode": "fixation",
        "timing": {
          "holdMs": 700
        },
        "content": {
          "type": "letter",
          "points": 55,
          "peripheralCount": 4,
          "pointSize": 44,
          "maxPeripheralCount": 4
        }
      },
      "difficultyLevel": 5
    }
  }
]$settings$::jsonb) item;
DO $$ BEGIN
  IF (SELECT count(*) FROM fixation_settings)<>20 OR (SELECT count(DISTINCT id) FROM fixation_settings)<>20
    THEN RAISE EXCEPTION 'Fixation payload incomplete'; END IF;
  IF EXISTS(SELECT 1 FROM fixation_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE e.id IS NULL OR t.name IS DISTINCT FROM 'Fixation' OR NOT t.is_active OR t.is_deleted
      OR NOT e.is_active OR e.is_deleted OR e.target_age_group_id IS DISTINCT FROM s.age_id
      OR e.difficulty_level<>s.level
      OR (e.configuration_json IS DISTINCT FROM s.old_config AND e.configuration_json IS DISTINCT FROM s.config))
    THEN RAISE EXCEPTION 'Fixation baseline changed; review before applying'; END IF;
END $$;
UPDATE speed_reading.exercises e SET configuration_json=s.config,
  updated_at=now(),updated_by='fixation-v1',version=e.version+1
FROM fixation_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.config;
DO $$ BEGIN
  IF (SELECT count(*) FROM fixation_settings s JOIN speed_reading.exercises e ON e.id=s.id
    WHERE e.configuration_json=s.config)<>20 THEN RAISE EXCEPTION 'Fixation verification failed'; END IF;
END $$;
COMMIT;
