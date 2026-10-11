BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='120s';
SELECT pg_advisory_xact_lock(hashtext('vocabulary-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.exercise_types, speed_reading.vocabulary_items IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE vocabulary_settings ON COMMIT DROP AS
SELECT (s->>'id')::uuid id,(s->>'ageGroupId')::uuid age_id,(s->>'level')::int level,
  s->>'title' title,s->>'description' description,s->'previousConfiguration' old_config,s->'configuration' config
FROM jsonb_array_elements($settings$[
  {
    "id": "69916068-32f4-5353-8590-f1ba00e0e5a2",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 1,
    "title": "Kelime Bahçesi",
    "description": "Çocuk yaş grubunda seçilen seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "word_to_definition",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 5,
          "difficultyLevel": 1
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "word_to_definition",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 8,
          "difficultyLevel": 1,
          "requireFullPool": true
        },
        "learnThenQuiz": true
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "3adf7625-c354-5a19-ad67-6208613d9812",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 1,
    "title": "Kelimemi Seçiyorum",
    "description": "Çocuk yaş grubunda seçilen seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "word_to_definition",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 5,
          "difficultyLevel": 1
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "word_to_definition",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 8,
          "difficultyLevel": 1,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "817d398a-046b-5764-83de-4d2edcdeaeca",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 2,
    "title": "Bağlamdan Kelime",
    "description": "Çocuk yaş grubunda seçilen seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 6,
          "difficultyLevel": 2
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 10,
          "difficultyLevel": 2,
          "requireFullPool": true
        },
        "learnThenQuiz": true
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "60720e23-e801-5728-9637-4aad270786cb",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 2,
    "title": "Kelimeyi Hatırla",
    "description": "Çocuk yaş grubunda seçilen seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 6,
          "difficultyLevel": 2
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 10,
          "difficultyLevel": 2,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "a908385f-9984-5881-bb56-9448b5eebd29",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 3,
    "title": "Kelime Bağlantıları",
    "description": "Çocuk yaş grubunda seçilen seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 8,
          "difficultyLevel": 3
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 12,
          "difficultyLevel": 3,
          "requireFullPool": true
        },
        "learnThenQuiz": true
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "d281c883-b608-57bd-8d31-bfe196936d7c",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 3,
    "title": "Kelime Bağlantılarını Sına",
    "description": "Çocuk yaş grubunda seçilen seviyeye uygun çalışma.",
    "previousConfiguration": null,
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 12,
          "difficultyLevel": 3,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "7e84e43a-1ff7-5d58-b5e6-6b6864c0bc92",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 4,
    "title": "Kelime Anlamlarını Ayır",
    "description": "Çocuk yaş grubunda seçilen seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 8,
          "difficultyLevel": 4
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 14,
          "difficultyLevel": 4,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "0eef6d0b-f544-5111-ac12-deeb6a9d45f3",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 5,
    "title": "Kelime Ustası",
    "description": "Çocuk yaş grubunda seçilen seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 10,
          "difficultyLevel": 5
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 16,
          "difficultyLevel": 5,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "0947e3b3-ba2c-549b-a93d-66429ec135c6",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 1,
    "title": "Kelime Bilgisi Testi",
    "description": "13–16 yaş grubunda seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "word_to_definition",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 6,
          "difficultyLevel": 1
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "word_to_definition",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 10,
          "difficultyLevel": 1,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "f7f74df5-c0fc-5576-ba87-976fa4f44d7a",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 1,
    "title": "Kelime Öğrenme",
    "description": "13–16 yaş grubunda seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "word_to_definition",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 6,
          "difficultyLevel": 1
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "word_to_definition",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 10,
          "difficultyLevel": 1,
          "requireFullPool": true
        },
        "learnThenQuiz": true
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "b1ee8c6b-2c42-5cff-b376-e8c7e69c5be0",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 2,
    "title": "Kelime Bilgisi Testi",
    "description": "13–16 yaş grubunda seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 8,
          "difficultyLevel": 2
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 12,
          "difficultyLevel": 2,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "3a9c8a4b-2910-53b1-b2dc-321eff6394ba",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 2,
    "title": "Kelime Öğrenme",
    "description": "13–16 yaş grubunda seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 8,
          "difficultyLevel": 2
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 12,
          "difficultyLevel": 2,
          "requireFullPool": true
        },
        "learnThenQuiz": true
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "d8e27f97-097e-554c-8109-40de21b240d9",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 3,
    "title": "Kelime Bilgisi Testi",
    "description": "13–16 yaş grubunda seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 10,
          "difficultyLevel": 3
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 14,
          "difficultyLevel": 3,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "68403c8c-d80e-54b2-b540-bcac581dfb69",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 4,
    "title": "Kelime Bilgisi Testi",
    "description": "13–16 yaş grubunda seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 10,
          "difficultyLevel": 4
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 16,
          "difficultyLevel": 4,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "34a3484f-fea0-582a-a719-65ec47e5fe70",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 5,
    "title": "Kelime Bilgisi Testi",
    "description": "13–16 yaş grubunda seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 12,
          "difficultyLevel": 5
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 18,
          "difficultyLevel": 5,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "f3220e75-9250-5dd5-9471-176e81b1d206",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 1,
    "title": "Kelime Bilgisi Testi",
    "description": "17–21 yaş grubunda seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "word_to_definition",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 8,
          "difficultyLevel": 1
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "word_to_definition",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 12,
          "difficultyLevel": 1,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "846e4e83-2553-5a19-b9f6-a50f47f2953a",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 1,
    "title": "Kelime Öğrenme",
    "description": "17–21 yaş grubunda seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "word_to_definition",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 8,
          "difficultyLevel": 1
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "word_to_definition",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 12,
          "difficultyLevel": 1,
          "requireFullPool": true
        },
        "learnThenQuiz": true
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "6fd8ceba-ef93-59e3-8b30-37d6a5acf104",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 2,
    "title": "Kelime Bilgisi Testi",
    "description": "17–21 yaş grubunda seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 8,
          "difficultyLevel": 2
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 14,
          "difficultyLevel": 2,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "e250cf1a-7b31-53f0-ab8c-be6e6b573ba6",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 2,
    "title": "Kelime Öğrenme",
    "description": "17–21 yaş grubunda seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 8,
          "difficultyLevel": 2
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 14,
          "difficultyLevel": 2,
          "requireFullPool": true
        },
        "learnThenQuiz": true
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "bd994f7f-767d-5623-bb60-ba717dbe5bc9",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 3,
    "title": "Kelime Bilgisi Testi",
    "description": "17–21 yaş grubunda seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 10,
          "difficultyLevel": 3
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 16,
          "difficultyLevel": 3,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "b4f28155-de18-5a51-b489-eb95d9f023cb",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 4,
    "title": "Kelime Bilgisi Testi",
    "description": "17–21 yaş grubunda seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 12,
          "difficultyLevel": 4
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 18,
          "difficultyLevel": 4,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "4c518573-ab4c-506c-a016-1982c9ca4481",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 5,
    "title": "Kelime Bilgisi Testi",
    "description": "17–21 yaş grubunda seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 12,
          "difficultyLevel": 5
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 20,
          "difficultyLevel": 5,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 5
    }
  },
  {
    "id": "d1ba235f-93a0-5d1b-b80a-22e17b0af12b",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 1,
    "title": "Kelime Bilgisi Testi",
    "description": "22 yaş ve üzeri yetişkinlerde seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "word_to_definition",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 8,
          "difficultyLevel": 1
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "word_to_definition",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 12,
          "difficultyLevel": 1,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "8135706c-19b1-5c05-9cb5-916cb359a31d",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 1,
    "title": "Kelime Öğrenme",
    "description": "22 yaş ve üzeri yetişkinlerde seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "word_to_definition",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 8,
          "difficultyLevel": 1
        }
      },
      "difficultyLevel": 1
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "word_to_definition",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 12,
          "difficultyLevel": 1,
          "requireFullPool": true
        },
        "learnThenQuiz": true
      },
      "difficultyLevel": 1
    }
  },
  {
    "id": "828df04b-e9b2-5aab-8cd2-cdb0ea400d07",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 2,
    "title": "Kelime Bilgisi Testi",
    "description": "22 yaş ve üzeri yetişkinlerde seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 10,
          "difficultyLevel": 2
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 15,
          "difficultyLevel": 2,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "cdb0c711-8cba-5245-aa65-8e342827b7ce",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 2,
    "title": "Kelime Öğrenme",
    "description": "22 yaş ve üzeri yetişkinlerde seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 10,
          "difficultyLevel": 2
        }
      },
      "difficultyLevel": 2
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "learning",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 15,
          "difficultyLevel": 2,
          "requireFullPool": true
        },
        "learnThenQuiz": true
      },
      "difficultyLevel": 2
    }
  },
  {
    "id": "960294df-a771-51cf-a97f-c7fb90bcbb97",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 3,
    "title": "Kelime Bilgisi Testi",
    "description": "22 yaş ve üzeri yetişkinlerde seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 10,
          "difficultyLevel": 3
        }
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 18,
          "difficultyLevel": 3,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 3
    }
  },
  {
    "id": "aa3b33db-132c-570d-ba41-11ce07be54ca",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 4,
    "title": "Kelime Bilgisi Testi",
    "description": "22 yaş ve üzeri yetişkinlerde seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 12,
          "difficultyLevel": 4
        }
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 20,
          "difficultyLevel": 4,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 4
    }
  },
  {
    "id": "ee4a00d9-f170-5461-a671-6bc83f7951d3",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 5,
    "title": "Kelime Bilgisi Testi",
    "description": "22 yaş ve üzeri yetişkinlerde seviyeye uygun çalışma.",
    "previousConfiguration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 12,
          "difficultyLevel": 5
        }
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "vocabulary_builder",
      "engineConfig": {
        "mode": "quiz",
        "quizType": "mixed",
        "timeLimitPerWord": 0,
        "vocabulary": {
          "count": 24,
          "difficultyLevel": 5,
          "requireFullPool": true
        }
      },
      "difficultyLevel": 5
    }
  }
]$settings$::jsonb) s;
DO $$ BEGIN
  IF (SELECT count(*) FROM vocabulary_settings)<>29 OR (SELECT count(DISTINCT id) FROM vocabulary_settings)<>29
    OR (SELECT count(*) FROM vocabulary_settings WHERE old_config='null'::jsonb)<>1
    THEN RAISE EXCEPTION 'Vocabulary payload incomplete'; END IF;
  IF EXISTS(SELECT 1 FROM vocabulary_settings s WHERE s.level NOT BETWEEN 1 AND 5
    OR s.config->>'engineType' IS DISTINCT FROM 'vocabulary_builder'
    OR s.config#>'{engineConfig,timeLimitPerWord}' IS DISTINCT FROM '0'::jsonb
    OR s.config#>'{engineConfig,vocabulary,requireFullPool}' IS DISTINCT FROM 'true'::jsonb
    OR s.config#>'{engineConfig,vocabulary,difficultyLevel}' IS DISTINCT FROM to_jsonb(s.level)
    OR s.config#>>'{engineConfig,mode}' NOT IN ('learning','quiz')
    OR s.config#>>'{engineConfig,mode}' IS NULL
    OR (s.config#>>'{engineConfig,mode}'='learning' AND s.config#>'{engineConfig,learnThenQuiz}' IS DISTINCT FROM 'true'::jsonb)
    OR s.config#>'{engineConfig,vocabulary,count}' IS DISTINCT FROM to_jsonb((CASE s.age_id::text
      WHEN '10000000-0000-0000-0000-000000000001' THEN ARRAY[8,10,12,14,16]
      WHEN '10000000-0000-0000-0000-000000000002' THEN ARRAY[10,12,14,16,18]
      WHEN '10000000-0000-0000-0000-000000000004' THEN ARRAY[12,14,16,18,20]
      WHEN '10000000-0000-0000-0000-000000000003' THEN ARRAY[12,15,18,20,24]
      ELSE ARRAY[]::int[] END)[s.level]))
    THEN RAISE EXCEPTION 'Vocabulary payload differs from approved parameters'; END IF;
  IF (SELECT count(*) FROM speed_reading.exercise_types WHERE name='Vocabulary' AND engine_type='vocabulary_builder' AND is_active AND NOT is_deleted)<>1
    THEN RAISE EXCEPTION 'Vocabulary exercise type missing or ambiguous'; END IF;
  IF EXISTS(SELECT 1 FROM vocabulary_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE (s.old_config<>'null'::jsonb AND e.id IS NULL)
      OR (e.id IS NOT NULL AND (t.name IS DISTINCT FROM 'Vocabulary' OR t.engine_type IS DISTINCT FROM 'vocabulary_builder'
        OR NOT e.is_active OR e.is_deleted OR NOT t.is_active OR t.is_deleted
        OR e.target_age_group_id IS DISTINCT FROM s.age_id OR e.difficulty_level IS DISTINCT FROM s.level
        OR (e.configuration_json IS DISTINCT FROM s.old_config AND e.configuration_json IS DISTINCT FROM s.config))))
    THEN RAISE EXCEPTION 'Vocabulary baseline changed; review before applying'; END IF;
  IF EXISTS(SELECT 1 FROM vocabulary_settings s WHERE NOT EXISTS(
    SELECT 1 FROM (
      SELECT word,definition,count(*) OVER(PARTITION BY lower(trim(word))) word_uses,
        count(*) OVER(PARTITION BY lower(trim(definition))) definition_uses
      FROM speed_reading.vocabulary_items v WHERE NOT v.is_deleted
        AND (v.target_age_group_id IS NULL OR v.target_age_group_id=s.age_id) AND v.difficulty_level=s.level
    ) pool WHERE word_uses=1 AND definition_uses=1 AND trim(word)<>'' AND trim(definition)<>''
    HAVING count(*) >= (s.config#>>'{engineConfig,vocabulary,count}')::int))
    THEN RAISE EXCEPTION 'Vocabulary content pool insufficient'; END IF;
END $$;
UPDATE speed_reading.exercises e SET configuration_json=s.config,updated_at=now(),updated_by='vocabulary-v1',version=e.version+1
FROM vocabulary_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.config;
INSERT INTO speed_reading.exercises(id,exercise_type_id,title,description,type_code,difficulty_level,configuration_json,
  target_age_group_id,creator_id,is_active,created_at,created_by,version,is_deleted)
SELECT s.id,t.id,s.title,s.description,'Vocabulary',s.level,s.config,s.age_id,
  'b993e2f8-3948-49bd-9cbd-463e8771c20d'::uuid,true,now(),'vocabulary-v1',1,false
FROM vocabulary_settings s JOIN speed_reading.exercise_types t ON t.name='Vocabulary' AND t.is_active AND NOT t.is_deleted
WHERE s.old_config='null'::jsonb ON CONFLICT(id) DO NOTHING;
DO $$ BEGIN
  IF (SELECT count(*) FROM vocabulary_settings s JOIN speed_reading.exercises e ON e.id=s.id WHERE e.configuration_json=s.config)<>29
    THEN RAISE EXCEPTION 'Vocabulary verification failed'; END IF;
END $$;
COMMIT;
