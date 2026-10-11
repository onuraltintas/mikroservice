-- Generated with generate.mjs. Only Speed Reading DB, after its existing v2 program packs.
-- Take and verify a backup before publishing; no student schedules/history are changed.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
SELECT pg_advisory_xact_lock(hashtext('grouping-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.reading_texts, speed_reading.exercise_types,
  speed_reading.program_templates IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE grouping_settings ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->>'previousTitle' old_title, item->'previousConfiguration' old_config,
  item->>'title' title, item->'configuration' config
FROM jsonb_array_elements($settings$[
  {
    "id": "c034a403-0215-51c1-8727-c6e690c5226f",
    "age": "child",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 1,
    "previousTitle": "İkili Anlam Grupları",
    "previousConfiguration": {
      "engineType": "word_highlight",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "chunking",
        "pacer": {
          "speedWpm": 100,
          "chunkSize": 2
        },
        "readingPurpose": "practice"
      },
      "difficultyLevel": 1
    },
    "title": "Kelime Gruplarıyla Okuma — Seviye 1",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 1,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 150,
          "maxWordCount": 200
        },
        "pacer": {
          "chunkSize": 2,
          "speedWpm": 100
        }
      }
    }
  },
  {
    "id": "42296217-4ce6-5acd-88a3-fe636cbffb30",
    "age": "child",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 2,
    "previousTitle": "Üçlü Kelime Grupları",
    "previousConfiguration": {
      "engineType": "word_highlight",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "chunking",
        "pacer": {
          "speedWpm": 130,
          "chunkSize": 3
        },
        "readingPurpose": "practice"
      },
      "difficultyLevel": 2
    },
    "title": "Kelime Gruplarıyla Okuma — Seviye 2",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 2,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 195,
          "maxWordCount": 260
        },
        "pacer": {
          "chunkSize": 3,
          "speedWpm": 130
        }
      }
    }
  },
  {
    "id": "3e0c4ca7-b16d-5700-8e28-79724f53dd94",
    "age": "child",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 3,
    "previousTitle": "Dörtlü Kelime Grupları",
    "previousConfiguration": {
      "engineType": "word_highlight",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "chunking",
        "pacer": {
          "speedWpm": 160,
          "chunkSize": 4
        },
        "readingPurpose": "practice"
      },
      "difficultyLevel": 3
    },
    "title": "Kelime Gruplarıyla Okuma — Seviye 3",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 3,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 240,
          "maxWordCount": 320
        },
        "pacer": {
          "chunkSize": 4,
          "speedWpm": 160
        }
      }
    }
  },
  {
    "id": "158e91aa-b6b4-50f9-afbc-752d59c9cd07",
    "age": "child",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 4,
    "previousTitle": "Geniş Kelime Grupları",
    "previousConfiguration": {
      "engineType": "word_highlight",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "chunking",
        "pacer": {
          "speedWpm": 200,
          "chunkSize": 4
        },
        "readingPurpose": "practice"
      },
      "difficultyLevel": 4
    },
    "title": "Kelime Gruplarıyla Okuma — Seviye 4",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 4,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 300,
          "maxWordCount": 400
        },
        "pacer": {
          "chunkSize": 4,
          "speedWpm": 200
        }
      }
    }
  },
  {
    "id": "d7b10000-0000-5000-8000-000000000001",
    "age": "child",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 5,
    "previousTitle": null,
    "previousConfiguration": null,
    "title": "Kelime Gruplarıyla Okuma — Seviye 5",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 5,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 345,
          "maxWordCount": 460
        },
        "pacer": {
          "chunkSize": 4,
          "speedWpm": 230
        }
      }
    }
  },
  {
    "id": "89bedb19-f023-5d7f-9a3f-9a986f9d33c2",
    "age": "teen",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 1,
    "previousTitle": "Anlam Gruplarıyla Okuma",
    "previousConfiguration": {
      "engineType": "word_highlight",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "chunking",
        "pacer": {
          "speedWpm": 150,
          "chunkSize": 2
        },
        "readingPurpose": "practice"
      },
      "difficultyLevel": 1
    },
    "title": "Kelime Gruplarıyla Okuma — Seviye 1",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 1,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 300,
          "maxWordCount": 375
        },
        "pacer": {
          "chunkSize": 2,
          "speedWpm": 150
        }
      }
    }
  },
  {
    "id": "4d2d13e4-92b0-5201-b847-5c7f1bd0edbb",
    "age": "teen",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 2,
    "previousTitle": "Anlam Gruplarıyla Okuma",
    "previousConfiguration": {
      "engineType": "word_highlight",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "chunking",
        "pacer": {
          "speedWpm": 180,
          "chunkSize": 3
        },
        "readingPurpose": "practice"
      },
      "difficultyLevel": 2
    },
    "title": "Kelime Gruplarıyla Okuma — Seviye 2",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 2,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 360,
          "maxWordCount": 450
        },
        "pacer": {
          "chunkSize": 3,
          "speedWpm": 180
        }
      }
    }
  },
  {
    "id": "bc66a4fb-0b93-5fe5-9f99-7d96dbdd013f",
    "age": "teen",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 3,
    "previousTitle": "Anlam Gruplarıyla Okuma",
    "previousConfiguration": {
      "engineType": "word_highlight",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "chunking",
        "pacer": {
          "speedWpm": 210,
          "chunkSize": 3
        },
        "readingPurpose": "practice"
      },
      "difficultyLevel": 3
    },
    "title": "Kelime Gruplarıyla Okuma — Seviye 3",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 3,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 420,
          "maxWordCount": 525
        },
        "pacer": {
          "chunkSize": 3,
          "speedWpm": 210
        }
      }
    }
  },
  {
    "id": "c93c2dec-eb3b-50b6-890d-4c416037565d",
    "age": "teen",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 4,
    "previousTitle": "Anlam Gruplarıyla Okuma",
    "previousConfiguration": {
      "engineType": "word_highlight",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "chunking",
        "pacer": {
          "speedWpm": 240,
          "chunkSize": 4
        },
        "readingPurpose": "practice"
      },
      "difficultyLevel": 4
    },
    "title": "Kelime Gruplarıyla Okuma — Seviye 4",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 4,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 480,
          "maxWordCount": 600
        },
        "pacer": {
          "chunkSize": 4,
          "speedWpm": 240
        }
      }
    }
  },
  {
    "id": "d7b10000-0000-5000-8000-000000000002",
    "age": "teen",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 5,
    "previousTitle": null,
    "previousConfiguration": null,
    "title": "Kelime Gruplarıyla Okuma — Seviye 5",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 5,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 540,
          "maxWordCount": 675
        },
        "pacer": {
          "chunkSize": 4,
          "speedWpm": 270
        }
      }
    }
  },
  {
    "id": "617a2e9d-3aed-5afb-b20b-39bcbc2848ba",
    "age": "young-adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 1,
    "previousTitle": "Anlam Gruplarıyla Okuma",
    "previousConfiguration": {
      "engineType": "word_highlight",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "chunking",
        "pacer": {
          "speedWpm": 180,
          "chunkSize": 2
        },
        "readingPurpose": "practice"
      },
      "difficultyLevel": 1
    },
    "title": "Kelime Gruplarıyla Okuma — Seviye 1",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 1,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 360,
          "maxWordCount": 540
        },
        "pacer": {
          "chunkSize": 2,
          "speedWpm": 180
        }
      }
    }
  },
  {
    "id": "c53fae0b-d606-539d-b02a-60d080fc8e75",
    "age": "young-adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 2,
    "previousTitle": "Anlam Gruplarıyla Okuma",
    "previousConfiguration": {
      "engineType": "word_highlight",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "chunking",
        "pacer": {
          "speedWpm": 210,
          "chunkSize": 3
        },
        "readingPurpose": "practice"
      },
      "difficultyLevel": 2
    },
    "title": "Kelime Gruplarıyla Okuma — Seviye 2",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 2,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 420,
          "maxWordCount": 630
        },
        "pacer": {
          "chunkSize": 3,
          "speedWpm": 210
        }
      }
    }
  },
  {
    "id": "eb87713b-d508-5d54-8556-fe7f0765bd90",
    "age": "young-adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 3,
    "previousTitle": "Anlam Gruplarıyla Okuma",
    "previousConfiguration": {
      "engineType": "word_highlight",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "chunking",
        "pacer": {
          "speedWpm": 240,
          "chunkSize": 3
        },
        "readingPurpose": "practice"
      },
      "difficultyLevel": 3
    },
    "title": "Kelime Gruplarıyla Okuma — Seviye 3",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 3,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 480,
          "maxWordCount": 720
        },
        "pacer": {
          "chunkSize": 3,
          "speedWpm": 240
        }
      }
    }
  },
  {
    "id": "57d186e0-8756-5e69-aaa2-3dd457c931b9",
    "age": "young-adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 4,
    "previousTitle": "Anlam Gruplarıyla Okuma",
    "previousConfiguration": {
      "engineType": "word_highlight",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "chunking",
        "pacer": {
          "speedWpm": 270,
          "chunkSize": 4
        },
        "readingPurpose": "practice"
      },
      "difficultyLevel": 4
    },
    "title": "Kelime Gruplarıyla Okuma — Seviye 4",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 4,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 540,
          "maxWordCount": 810
        },
        "pacer": {
          "chunkSize": 4,
          "speedWpm": 270
        }
      }
    }
  },
  {
    "id": "d7b10000-0000-5000-8000-000000000004",
    "age": "young-adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 5,
    "previousTitle": null,
    "previousConfiguration": null,
    "title": "Kelime Gruplarıyla Okuma — Seviye 5",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 5,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 600,
          "maxWordCount": 900
        },
        "pacer": {
          "chunkSize": 5,
          "speedWpm": 300
        }
      }
    }
  },
  {
    "id": "02fc1e52-80d6-506e-9317-fd5eec33ea2d",
    "age": "adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 1,
    "previousTitle": "Anlam Gruplarıyla Okuma",
    "previousConfiguration": {
      "engineType": "word_highlight",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "chunking",
        "pacer": {
          "speedWpm": 200,
          "chunkSize": 2
        },
        "readingPurpose": "practice"
      },
      "difficultyLevel": 1
    },
    "title": "Kelime Gruplarıyla Okuma — Seviye 1",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 1,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 400,
          "maxWordCount": 600
        },
        "pacer": {
          "chunkSize": 2,
          "speedWpm": 200
        }
      }
    }
  },
  {
    "id": "8686e7b0-4d3c-5e51-9aa2-8ee87fbffd65",
    "age": "adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 2,
    "previousTitle": "Anlam Gruplarıyla Okuma",
    "previousConfiguration": {
      "engineType": "word_highlight",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "chunking",
        "pacer": {
          "speedWpm": 230,
          "chunkSize": 3
        },
        "readingPurpose": "practice"
      },
      "difficultyLevel": 2
    },
    "title": "Kelime Gruplarıyla Okuma — Seviye 2",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 2,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 460,
          "maxWordCount": 690
        },
        "pacer": {
          "chunkSize": 3,
          "speedWpm": 230
        }
      }
    }
  },
  {
    "id": "1c5d756b-2bef-58e8-99c2-1e32d32e8153",
    "age": "adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 3,
    "previousTitle": "Anlam Gruplarıyla Okuma",
    "previousConfiguration": {
      "engineType": "word_highlight",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "chunking",
        "pacer": {
          "speedWpm": 260,
          "chunkSize": 3
        },
        "readingPurpose": "practice"
      },
      "difficultyLevel": 3
    },
    "title": "Kelime Gruplarıyla Okuma — Seviye 3",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 3,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 520,
          "maxWordCount": 780
        },
        "pacer": {
          "chunkSize": 3,
          "speedWpm": 260
        }
      }
    }
  },
  {
    "id": "e4900566-c2ba-5254-bde7-215bb4ae5ca7",
    "age": "adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 4,
    "previousTitle": "Anlam Gruplarıyla Okuma",
    "previousConfiguration": {
      "engineType": "word_highlight",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "chunking",
        "pacer": {
          "speedWpm": 290,
          "chunkSize": 4
        },
        "readingPurpose": "practice"
      },
      "difficultyLevel": 4
    },
    "title": "Kelime Gruplarıyla Okuma — Seviye 4",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 4,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 580,
          "maxWordCount": 870
        },
        "pacer": {
          "chunkSize": 4,
          "speedWpm": 290
        }
      }
    }
  },
  {
    "id": "d7b10000-0000-5000-8000-000000000003",
    "age": "adult",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 5,
    "previousTitle": null,
    "previousConfiguration": null,
    "title": "Kelime Gruplarıyla Okuma — Seviye 5",
    "configuration": {
      "engineType": "word_highlight",
      "difficultyLevel": 5,
      "engineConfig": {
        "mode": "chunking",
        "readingPurpose": "practice",
        "content": {
          "source": "reading_text",
          "minWordCount": 640,
          "maxWordCount": 960
        },
        "pacer": {
          "chunkSize": 5,
          "speedWpm": 320
        }
      }
    }
  }
]$settings$::jsonb) item;
CREATE TEMP TABLE grouping_content ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'exerciseId')::uuid exercise_id,
  (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->>'title' title, item->>'content' content, (item->>'wordCount')::int word_count
FROM jsonb_array_elements($content$[
  {
    "id": "d7b20000-0000-5000-8000-000000000001",
    "exerciseId": "c034a403-0215-51c1-8727-c6e690c5226f",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 1,
    "title": "Mahalle kütüphanesinden notlar — child 1 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.",
    "wordCount": 162,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000002",
    "exerciseId": "c034a403-0215-51c1-8727-c6e690c5226f",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 1,
    "title": "Bahçede birlikte öğrenmek — child 1 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.",
    "wordCount": 169,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000003",
    "exerciseId": "c034a403-0215-51c1-8727-c6e690c5226f",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 1,
    "title": "Bir haritanın gelişimi — child 1 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.",
    "wordCount": 169,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000004",
    "exerciseId": "42296217-4ce6-5acd-88a3-fe636cbffb30",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 2,
    "title": "Mahalle kütüphanesinden notlar — child 2 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.",
    "wordCount": 228,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000005",
    "exerciseId": "42296217-4ce6-5acd-88a3-fe636cbffb30",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 2,
    "title": "Bahçede birlikte öğrenmek — child 2 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.",
    "wordCount": 233,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000006",
    "exerciseId": "42296217-4ce6-5acd-88a3-fe636cbffb30",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 2,
    "title": "Bir haritanın gelişimi — child 2 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.",
    "wordCount": 231,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000007",
    "exerciseId": "3e0c4ca7-b16d-5700-8e28-79724f53dd94",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 3,
    "title": "Mahalle kütüphanesinden notlar — child 3 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.",
    "wordCount": 295,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000008",
    "exerciseId": "3e0c4ca7-b16d-5700-8e28-79724f53dd94",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 3,
    "title": "Bahçede birlikte öğrenmek — child 3 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.",
    "wordCount": 264,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000009",
    "exerciseId": "3e0c4ca7-b16d-5700-8e28-79724f53dd94",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 3,
    "title": "Bir haritanın gelişimi — child 3 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.",
    "wordCount": 295,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000010",
    "exerciseId": "158e91aa-b6b4-50f9-afbc-752d59c9cd07",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 4,
    "title": "Mahalle kütüphanesinden notlar — child 4 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.",
    "wordCount": 359,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000011",
    "exerciseId": "158e91aa-b6b4-50f9-afbc-752d59c9cd07",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 4,
    "title": "Bahçede birlikte öğrenmek — child 4 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.\n\nSulama görevleri haftalık bir çizelgeye yazıldı. Çizelgede yalnız isimler değil, yapılacak işin kısa açıklaması da vardı. Bir kişi gelemediğinde başka biri görevi devralabiliyordu. Görevin nasıl yapılacağını bilmek, görevlinin adını bilmek kadar önemliydi.",
    "wordCount": 360,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000012",
    "exerciseId": "158e91aa-b6b4-50f9-afbc-752d59c9cd07",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 4,
    "title": "Bir haritanın gelişimi — child 4 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.\n\nYol çalışması bitince eski sokak yeniden açıldı. Deniz yeni haritasını hemen atmadı. Farklı yolların başka günlerde de işe yarayabileceğini düşündü. Ancak kapalı sokak işaretini kaldırdı ve haritanın hangi tarihte güncellendiğini yazdı.",
    "wordCount": 359,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000013",
    "exerciseId": "d7b10000-0000-5000-8000-000000000001",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 5,
    "title": "Mahalle kütüphanesinden notlar — child 5 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.",
    "wordCount": 393,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000014",
    "exerciseId": "d7b10000-0000-5000-8000-000000000001",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 5,
    "title": "Bahçede birlikte öğrenmek — child 5 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.\n\nSulama görevleri haftalık bir çizelgeye yazıldı. Çizelgede yalnız isimler değil, yapılacak işin kısa açıklaması da vardı. Bir kişi gelemediğinde başka biri görevi devralabiliyordu. Görevin nasıl yapılacağını bilmek, görevlinin adını bilmek kadar önemliydi.\n\nÇocuklar farklı sulama miktarlarını denemek istediğinde öğretmenleri önce küçük bir alan seçmelerini önerdi. Bütün bahçeyi aynı anda değiştirmek yerine, karşılaştırabilecekleri birkaç bitki ayırdılar. Ne değiştirdiklerini açıkça yazdılar ve diğer koşulları benzer tutmaya çalıştılar.",
    "wordCount": 393,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000015",
    "exerciseId": "d7b10000-0000-5000-8000-000000000001",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "level": 5,
    "title": "Bir haritanın gelişimi — child 5 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.\n\nYol çalışması bitince eski sokak yeniden açıldı. Deniz yeni haritasını hemen atmadı. Farklı yolların başka günlerde de işe yarayabileceğini düşündü. Ancak kapalı sokak işaretini kaldırdı ve haritanın hangi tarihte güncellendiğini yazdı.\n\nMahalledeki gençler, yürüyüş yollarını birlikte kaydetmek istedi. Herkes kendi bildiği köşeleri çizdi. Çizimler birleştirildiğinde bazı adların farklı yazıldığı görüldü. Aynı yerden söz edip etmediklerini birlikte kontrol ederek ortak bir ad listesi hazırladılar.",
    "wordCount": 391,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000016",
    "exerciseId": "89bedb19-f023-5d7f-9a3f-9a986f9d33c2",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 1,
    "title": "Mahalle kütüphanesinden notlar — teen 1 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.",
    "wordCount": 326,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000017",
    "exerciseId": "89bedb19-f023-5d7f-9a3f-9a986f9d33c2",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 1,
    "title": "Bahçede birlikte öğrenmek — teen 1 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.",
    "wordCount": 328,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000018",
    "exerciseId": "89bedb19-f023-5d7f-9a3f-9a986f9d33c2",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 1,
    "title": "Bir haritanın gelişimi — teen 1 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.",
    "wordCount": 328,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000019",
    "exerciseId": "4d2d13e4-92b0-5201-b847-5c7f1bd0edbb",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 2,
    "title": "Mahalle kütüphanesinden notlar — teen 2 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.",
    "wordCount": 393,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000020",
    "exerciseId": "4d2d13e4-92b0-5201-b847-5c7f1bd0edbb",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 2,
    "title": "Bahçede birlikte öğrenmek — teen 2 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.\n\nSulama görevleri haftalık bir çizelgeye yazıldı. Çizelgede yalnız isimler değil, yapılacak işin kısa açıklaması da vardı. Bir kişi gelemediğinde başka biri görevi devralabiliyordu. Görevin nasıl yapılacağını bilmek, görevlinin adını bilmek kadar önemliydi.\n\nÇocuklar farklı sulama miktarlarını denemek istediğinde öğretmenleri önce küçük bir alan seçmelerini önerdi. Bütün bahçeyi aynı anda değiştirmek yerine, karşılaştırabilecekleri birkaç bitki ayırdılar. Ne değiştirdiklerini açıkça yazdılar ve diğer koşulları benzer tutmaya çalıştılar.",
    "wordCount": 393,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000021",
    "exerciseId": "4d2d13e4-92b0-5201-b847-5c7f1bd0edbb",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 2,
    "title": "Bir haritanın gelişimi — teen 2 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.\n\nYol çalışması bitince eski sokak yeniden açıldı. Deniz yeni haritasını hemen atmadı. Farklı yolların başka günlerde de işe yarayabileceğini düşündü. Ancak kapalı sokak işaretini kaldırdı ve haritanın hangi tarihte güncellendiğini yazdı.\n\nMahalledeki gençler, yürüyüş yollarını birlikte kaydetmek istedi. Herkes kendi bildiği köşeleri çizdi. Çizimler birleştirildiğinde bazı adların farklı yazıldığı görüldü. Aynı yerden söz edip etmediklerini birlikte kontrol ederek ortak bir ad listesi hazırladılar.",
    "wordCount": 391,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000022",
    "exerciseId": "bc66a4fb-0b93-5fe5-9f99-7d96dbdd013f",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 3,
    "title": "Mahalle kütüphanesinden notlar — teen 3 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.",
    "wordCount": 458,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000023",
    "exerciseId": "bc66a4fb-0b93-5fe5-9f99-7d96dbdd013f",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 3,
    "title": "Bahçede birlikte öğrenmek — teen 3 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.\n\nSulama görevleri haftalık bir çizelgeye yazıldı. Çizelgede yalnız isimler değil, yapılacak işin kısa açıklaması da vardı. Bir kişi gelemediğinde başka biri görevi devralabiliyordu. Görevin nasıl yapılacağını bilmek, görevlinin adını bilmek kadar önemliydi.\n\nÇocuklar farklı sulama miktarlarını denemek istediğinde öğretmenleri önce küçük bir alan seçmelerini önerdi. Bütün bahçeyi aynı anda değiştirmek yerine, karşılaştırabilecekleri birkaç bitki ayırdılar. Ne değiştirdiklerini açıkça yazdılar ve diğer koşulları benzer tutmaya çalıştılar.\n\nDenemenin sonunda bazı bitkiler daha canlı görünüyordu. Ancak çocuklar bunun yalnız sulamadan kaynaklandığını hemen söylemedi. Toprağın ve güneşin de etkili olabileceğini konuştular. Sonucu bir gözlem olarak yazdılar; kesin bir kural gibi sunmadılar.\n\nBahçenin çevresine küçük bir yol yapıldı. Yol, bitkilere basmadan yaklaşmayı kolaylaştırıyordu. Önce çok dar çizilen geçit, sulama kabı taşıyanlara yetmedi. Çocuklar yürüyerek denedi ve yolu biraz genişletti. Çizim, kullanıldığında karşılaşılan duruma göre değişti.",
    "wordCount": 458,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000024",
    "exerciseId": "bc66a4fb-0b93-5fe5-9f99-7d96dbdd013f",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 3,
    "title": "Bir haritanın gelişimi — teen 3 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.\n\nYol çalışması bitince eski sokak yeniden açıldı. Deniz yeni haritasını hemen atmadı. Farklı yolların başka günlerde de işe yarayabileceğini düşündü. Ancak kapalı sokak işaretini kaldırdı ve haritanın hangi tarihte güncellendiğini yazdı.\n\nMahalledeki gençler, yürüyüş yollarını birlikte kaydetmek istedi. Herkes kendi bildiği köşeleri çizdi. Çizimler birleştirildiğinde bazı adların farklı yazıldığı görüldü. Aynı yerden söz edip etmediklerini birlikte kontrol ederek ortak bir ad listesi hazırladılar.\n\nGrup, haritaya bütün bilgileri aynı anda koymamaya karar verdi. Ana çizimde yönler ve önemli noktalar bulunacaktı. Ayrıntılar ayrı notlarda gösterilecekti. Çok fazla işaretin, aranan bilgiyi bulmayı zorlaştırabileceğini denemelerde görmüşlerdi.\n\nYürüyüş süresi için birkaç farklı günde kayıt tutuldu. Yağmurlu günlerin, kalabalık saatlerin ve yol çalışmalarının süreyi değiştirdiği görüldü. Sonuçlar tek bir sayı yerine küçük bir aralıkla anlatıldı. Bu aralık, beklenebilecek değişkenliği daha açık gösteriyordu.\n\nBazı sokaklar kısa görünse de yürümek için uygun değildi. Grup yalnız mesafeye bakarak öneri vermedi. Kaldırımın durumunu, geçiş noktalarını ve yolun açık olup olmadığını da gözledi. Bir ölçünün, bütün kararın yerine geçemeyeceğini konuştular.",
    "wordCount": 487,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000025",
    "exerciseId": "c93c2dec-eb3b-50b6-890d-4c416037565d",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 4,
    "title": "Mahalle kütüphanesinden notlar — teen 4 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.\n\nAy sonunda öneri kartları yeniden gözden geçirildi. Bazı istekler hemen yapılmış, bazıları bütçe bekliyordu. Pano, yalnız tamamlanan işleri değil, henüz çözülemeyen ihtiyaçları da gösteriyordu. Görevli, bir isteğin beklemesinin unutulduğu anlamına gelmediğini açıkladı.\n\nKütüphaneye yeni kitaplar geldiğinde herkesin sevdiği türlerden eşit sayıda kitap bulunmadı. Gönüllüler tek bir kişinin tercihine göre karar vermedi. Ödünç alma kayıtlarına, önerilere ve raflarda eksik kalan konulara birlikte baktılar. Kararın gerekçesini panoda paylaştılar.",
    "wordCount": 524,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000026",
    "exerciseId": "c93c2dec-eb3b-50b6-890d-4c416037565d",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 4,
    "title": "Bahçede birlikte öğrenmek — teen 4 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.\n\nSulama görevleri haftalık bir çizelgeye yazıldı. Çizelgede yalnız isimler değil, yapılacak işin kısa açıklaması da vardı. Bir kişi gelemediğinde başka biri görevi devralabiliyordu. Görevin nasıl yapılacağını bilmek, görevlinin adını bilmek kadar önemliydi.\n\nÇocuklar farklı sulama miktarlarını denemek istediğinde öğretmenleri önce küçük bir alan seçmelerini önerdi. Bütün bahçeyi aynı anda değiştirmek yerine, karşılaştırabilecekleri birkaç bitki ayırdılar. Ne değiştirdiklerini açıkça yazdılar ve diğer koşulları benzer tutmaya çalıştılar.\n\nDenemenin sonunda bazı bitkiler daha canlı görünüyordu. Ancak çocuklar bunun yalnız sulamadan kaynaklandığını hemen söylemedi. Toprağın ve güneşin de etkili olabileceğini konuştular. Sonucu bir gözlem olarak yazdılar; kesin bir kural gibi sunmadılar.\n\nBahçenin çevresine küçük bir yol yapıldı. Yol, bitkilere basmadan yaklaşmayı kolaylaştırıyordu. Önce çok dar çizilen geçit, sulama kabı taşıyanlara yetmedi. Çocuklar yürüyerek denedi ve yolu biraz genişletti. Çizim, kullanıldığında karşılaşılan duruma göre değişti.\n\nBazı ziyaretçiler bitkilerin yanında daha fazla bilgi istedi. Çocuklar uzun açıklamalar yerine kısa kartlar hazırladı. Kartlarda ad, ekim tarihi ve birkaç gözlem bulunuyordu. Ayrıntılı notları görmek isteyenler için defterin nerede bulunduğu ayrıca yazıldı.\n\nMevsim değiştikçe bahçenin görünümü de değişti. Çocuklar eski fotoğraflarla yeni fotoğrafları karşılaştırdı. Fotoğrafların benzer açıdan çekilmesi karşılaştırmayı kolaylaştırdı. Farklı açı ve ışıkla çekilen görüntülerde bir değişiklik varmış gibi görünmenin mümkün olduğunu fark ettiler.\n\nBir bitki kuruduğunda grup kimin hata yaptığını aramakla başlamadı. Önce bakım kayıtlarını ve toprağın durumunu inceledi. Her sorunun tek bir kişiden kaynaklanmayabileceğini konuştular. Sonraki ekimde neyi farklı yapabileceklerini birlikte yazdılar.",
    "wordCount": 554,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000027",
    "exerciseId": "c93c2dec-eb3b-50b6-890d-4c416037565d",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 4,
    "title": "Bir haritanın gelişimi — teen 4 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.\n\nYol çalışması bitince eski sokak yeniden açıldı. Deniz yeni haritasını hemen atmadı. Farklı yolların başka günlerde de işe yarayabileceğini düşündü. Ancak kapalı sokak işaretini kaldırdı ve haritanın hangi tarihte güncellendiğini yazdı.\n\nMahalledeki gençler, yürüyüş yollarını birlikte kaydetmek istedi. Herkes kendi bildiği köşeleri çizdi. Çizimler birleştirildiğinde bazı adların farklı yazıldığı görüldü. Aynı yerden söz edip etmediklerini birlikte kontrol ederek ortak bir ad listesi hazırladılar.\n\nGrup, haritaya bütün bilgileri aynı anda koymamaya karar verdi. Ana çizimde yönler ve önemli noktalar bulunacaktı. Ayrıntılar ayrı notlarda gösterilecekti. Çok fazla işaretin, aranan bilgiyi bulmayı zorlaştırabileceğini denemelerde görmüşlerdi.\n\nYürüyüş süresi için birkaç farklı günde kayıt tutuldu. Yağmurlu günlerin, kalabalık saatlerin ve yol çalışmalarının süreyi değiştirdiği görüldü. Sonuçlar tek bir sayı yerine küçük bir aralıkla anlatıldı. Bu aralık, beklenebilecek değişkenliği daha açık gösteriyordu.\n\nBazı sokaklar kısa görünse de yürümek için uygun değildi. Grup yalnız mesafeye bakarak öneri vermedi. Kaldırımın durumunu, geçiş noktalarını ve yolun açık olup olmadığını da gözledi. Bir ölçünün, bütün kararın yerine geçemeyeceğini konuştular.\n\nHaritadaki bilgiler zamanla eskiyebilirdi. Grup, yanlış bir işaret görenlerin bunu nasıl bildireceğini yazdı. Gelen önerilerin hemen doğru kabul edilmemesi gerektiğini de belirledi. Değişiklik yapılmadan önce yerin yeniden kontrol edilmesi planlandı.\n\nBir sokak adı değiştiğinde eski adla arayanlar yeni haritada zorlandı. Grup bir süre iki adı birlikte gösterdi. Böylece yeni bilgi, önceki bilgiyi bilenleri tamamen dışarıda bırakmadı. Güncelleme yapmak, eski kullanıcıların nasıl aradığını düşünmeyi de gerektiriyordu.",
    "wordCount": 552,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000028",
    "exerciseId": "d7b10000-0000-5000-8000-000000000002",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 5,
    "title": "Mahalle kütüphanesinden notlar — teen 5 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.\n\nAy sonunda öneri kartları yeniden gözden geçirildi. Bazı istekler hemen yapılmış, bazıları bütçe bekliyordu. Pano, yalnız tamamlanan işleri değil, henüz çözülemeyen ihtiyaçları da gösteriyordu. Görevli, bir isteğin beklemesinin unutulduğu anlamına gelmediğini açıkladı.\n\nKütüphaneye yeni kitaplar geldiğinde herkesin sevdiği türlerden eşit sayıda kitap bulunmadı. Gönüllüler tek bir kişinin tercihine göre karar vermedi. Ödünç alma kayıtlarına, önerilere ve raflarda eksik kalan konulara birlikte baktılar. Kararın gerekçesini panoda paylaştılar.\n\nBazı kitaplar çok sık isteniyordu. Bu kitaplar için sıraya girenlere tahmini dönüş tarihi söylendi. Sıra beklemek istemeyenlere benzer konudaki başka kitaplar gösterildi. Bir seçenek sunmak, aranan kitabın hemen bulunamamasını daha anlaşılır hâle getirdi.\n\nBir okuma buluşmasında aynı öykü hakkında farklı yorumlar yapıldı. Duru kahramanın cesur olduğunu düşünürken arkadaşı aceleci olduğunu söyledi. İkisi de görüşünü öyküdeki bir olayla açıkladı. Farklı yorumların, dikkatle dinlendiğinde konuşmayı zenginleştirebildiğini gördüler.\n\nBuluşmayı yöneten kişi, herkesi aynı görüşte birleştirmeye çalışmadı. Önce yorumların hangi ayrıntıya dayandığını sordu. Sonra öykünün başka bir bölümünü birlikte okudular. Yeni ayrıntılar bazı düşünceleri değiştirdi, bazılarını güçlendirdi. Konuşma, yalnız sonuçtan değil gerekçelerden oluştu.",
    "wordCount": 623,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000029",
    "exerciseId": "d7b10000-0000-5000-8000-000000000002",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 5,
    "title": "Bahçede birlikte öğrenmek — teen 5 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.\n\nSulama görevleri haftalık bir çizelgeye yazıldı. Çizelgede yalnız isimler değil, yapılacak işin kısa açıklaması da vardı. Bir kişi gelemediğinde başka biri görevi devralabiliyordu. Görevin nasıl yapılacağını bilmek, görevlinin adını bilmek kadar önemliydi.\n\nÇocuklar farklı sulama miktarlarını denemek istediğinde öğretmenleri önce küçük bir alan seçmelerini önerdi. Bütün bahçeyi aynı anda değiştirmek yerine, karşılaştırabilecekleri birkaç bitki ayırdılar. Ne değiştirdiklerini açıkça yazdılar ve diğer koşulları benzer tutmaya çalıştılar.\n\nDenemenin sonunda bazı bitkiler daha canlı görünüyordu. Ancak çocuklar bunun yalnız sulamadan kaynaklandığını hemen söylemedi. Toprağın ve güneşin de etkili olabileceğini konuştular. Sonucu bir gözlem olarak yazdılar; kesin bir kural gibi sunmadılar.\n\nBahçenin çevresine küçük bir yol yapıldı. Yol, bitkilere basmadan yaklaşmayı kolaylaştırıyordu. Önce çok dar çizilen geçit, sulama kabı taşıyanlara yetmedi. Çocuklar yürüyerek denedi ve yolu biraz genişletti. Çizim, kullanıldığında karşılaşılan duruma göre değişti.\n\nBazı ziyaretçiler bitkilerin yanında daha fazla bilgi istedi. Çocuklar uzun açıklamalar yerine kısa kartlar hazırladı. Kartlarda ad, ekim tarihi ve birkaç gözlem bulunuyordu. Ayrıntılı notları görmek isteyenler için defterin nerede bulunduğu ayrıca yazıldı.\n\nMevsim değiştikçe bahçenin görünümü de değişti. Çocuklar eski fotoğraflarla yeni fotoğrafları karşılaştırdı. Fotoğrafların benzer açıdan çekilmesi karşılaştırmayı kolaylaştırdı. Farklı açı ve ışıkla çekilen görüntülerde bir değişiklik varmış gibi görünmenin mümkün olduğunu fark ettiler.\n\nBir bitki kuruduğunda grup kimin hata yaptığını aramakla başlamadı. Önce bakım kayıtlarını ve toprağın durumunu inceledi. Her sorunun tek bir kişiden kaynaklanmayabileceğini konuştular. Sonraki ekimde neyi farklı yapabileceklerini birlikte yazdılar.\n\nBahçe büyüdükçe iş paylaşımı da değişti. Başlangıçta bütün işleri birlikte yapan grup, küçük sorumluluk alanları oluşturdu. Yine de herkes diğer alanların durumunu öğrenebiliyordu. Ortak defter, ayrı görevlerin birbirinden tamamen kopmasını önleyen bir bağlantı oldu.\n\nYıl sonunda yeni gelen öğrenciler bahçeyi devralacaktı. Eren ve arkadaşları yalnız güzel fotoğrafları seçmedi. Karışıklık yaşanan günleri ve çözüm yollarını da anlattı. Böylece yeni grup, aynı güçlükleri yeniden yaşamak yerine önceki denemelerden yararlanabilecekti.",
    "wordCount": 621,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000030",
    "exerciseId": "d7b10000-0000-5000-8000-000000000002",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "level": 5,
    "title": "Bir haritanın gelişimi — teen 5 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.\n\nYol çalışması bitince eski sokak yeniden açıldı. Deniz yeni haritasını hemen atmadı. Farklı yolların başka günlerde de işe yarayabileceğini düşündü. Ancak kapalı sokak işaretini kaldırdı ve haritanın hangi tarihte güncellendiğini yazdı.\n\nMahalledeki gençler, yürüyüş yollarını birlikte kaydetmek istedi. Herkes kendi bildiği köşeleri çizdi. Çizimler birleştirildiğinde bazı adların farklı yazıldığı görüldü. Aynı yerden söz edip etmediklerini birlikte kontrol ederek ortak bir ad listesi hazırladılar.\n\nGrup, haritaya bütün bilgileri aynı anda koymamaya karar verdi. Ana çizimde yönler ve önemli noktalar bulunacaktı. Ayrıntılar ayrı notlarda gösterilecekti. Çok fazla işaretin, aranan bilgiyi bulmayı zorlaştırabileceğini denemelerde görmüşlerdi.\n\nYürüyüş süresi için birkaç farklı günde kayıt tutuldu. Yağmurlu günlerin, kalabalık saatlerin ve yol çalışmalarının süreyi değiştirdiği görüldü. Sonuçlar tek bir sayı yerine küçük bir aralıkla anlatıldı. Bu aralık, beklenebilecek değişkenliği daha açık gösteriyordu.\n\nBazı sokaklar kısa görünse de yürümek için uygun değildi. Grup yalnız mesafeye bakarak öneri vermedi. Kaldırımın durumunu, geçiş noktalarını ve yolun açık olup olmadığını da gözledi. Bir ölçünün, bütün kararın yerine geçemeyeceğini konuştular.\n\nHaritadaki bilgiler zamanla eskiyebilirdi. Grup, yanlış bir işaret görenlerin bunu nasıl bildireceğini yazdı. Gelen önerilerin hemen doğru kabul edilmemesi gerektiğini de belirledi. Değişiklik yapılmadan önce yerin yeniden kontrol edilmesi planlandı.\n\nBir sokak adı değiştiğinde eski adla arayanlar yeni haritada zorlandı. Grup bir süre iki adı birlikte gösterdi. Böylece yeni bilgi, önceki bilgiyi bilenleri tamamen dışarıda bırakmadı. Güncelleme yapmak, eski kullanıcıların nasıl aradığını düşünmeyi de gerektiriyordu.\n\nHarita toplantısında bir anlaşmazlık çıktı. Bir kişi daha kısa rotayı, diğeri daha az basamak içeren rotayı öneriyordu. İkisinin farklı ihtiyaçları önemsediği anlaşıldı. Tek doğru rota seçmek yerine, seçeneklerin özelliklerini açıkça gösterdiler.\n\nHazırlanan çizim bir rehberdi; her koşulda değişmez bir söz vermiyordu. Yola çıkmadan önce geçici kapanmaların kontrol edilmesi gerektiği yazıldı. Grup bu açıklamayı küçük ve görünmez bir not yapmak istemedi. Bilginin sınırları, yön işaretleri kadar anlaşılır olmalıydı.",
    "wordCount": 619,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000031",
    "exerciseId": "617a2e9d-3aed-5afb-b20b-39bcbc2848ba",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 1,
    "title": "Mahalle kütüphanesinden notlar — young-adult 1 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.",
    "wordCount": 458,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000032",
    "exerciseId": "617a2e9d-3aed-5afb-b20b-39bcbc2848ba",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 1,
    "title": "Bahçede birlikte öğrenmek — young-adult 1 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.\n\nSulama görevleri haftalık bir çizelgeye yazıldı. Çizelgede yalnız isimler değil, yapılacak işin kısa açıklaması da vardı. Bir kişi gelemediğinde başka biri görevi devralabiliyordu. Görevin nasıl yapılacağını bilmek, görevlinin adını bilmek kadar önemliydi.\n\nÇocuklar farklı sulama miktarlarını denemek istediğinde öğretmenleri önce küçük bir alan seçmelerini önerdi. Bütün bahçeyi aynı anda değiştirmek yerine, karşılaştırabilecekleri birkaç bitki ayırdılar. Ne değiştirdiklerini açıkça yazdılar ve diğer koşulları benzer tutmaya çalıştılar.\n\nDenemenin sonunda bazı bitkiler daha canlı görünüyordu. Ancak çocuklar bunun yalnız sulamadan kaynaklandığını hemen söylemedi. Toprağın ve güneşin de etkili olabileceğini konuştular. Sonucu bir gözlem olarak yazdılar; kesin bir kural gibi sunmadılar.\n\nBahçenin çevresine küçük bir yol yapıldı. Yol, bitkilere basmadan yaklaşmayı kolaylaştırıyordu. Önce çok dar çizilen geçit, sulama kabı taşıyanlara yetmedi. Çocuklar yürüyerek denedi ve yolu biraz genişletti. Çizim, kullanıldığında karşılaşılan duruma göre değişti.",
    "wordCount": 458,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000033",
    "exerciseId": "617a2e9d-3aed-5afb-b20b-39bcbc2848ba",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 1,
    "title": "Bir haritanın gelişimi — young-adult 1 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.\n\nYol çalışması bitince eski sokak yeniden açıldı. Deniz yeni haritasını hemen atmadı. Farklı yolların başka günlerde de işe yarayabileceğini düşündü. Ancak kapalı sokak işaretini kaldırdı ve haritanın hangi tarihte güncellendiğini yazdı.\n\nMahalledeki gençler, yürüyüş yollarını birlikte kaydetmek istedi. Herkes kendi bildiği köşeleri çizdi. Çizimler birleştirildiğinde bazı adların farklı yazıldığı görüldü. Aynı yerden söz edip etmediklerini birlikte kontrol ederek ortak bir ad listesi hazırladılar.\n\nGrup, haritaya bütün bilgileri aynı anda koymamaya karar verdi. Ana çizimde yönler ve önemli noktalar bulunacaktı. Ayrıntılar ayrı notlarda gösterilecekti. Çok fazla işaretin, aranan bilgiyi bulmayı zorlaştırabileceğini denemelerde görmüşlerdi.\n\nYürüyüş süresi için birkaç farklı günde kayıt tutuldu. Yağmurlu günlerin, kalabalık saatlerin ve yol çalışmalarının süreyi değiştirdiği görüldü. Sonuçlar tek bir sayı yerine küçük bir aralıkla anlatıldı. Bu aralık, beklenebilecek değişkenliği daha açık gösteriyordu.",
    "wordCount": 454,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000034",
    "exerciseId": "c53fae0b-d606-539d-b02a-60d080fc8e75",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 2,
    "title": "Mahalle kütüphanesinden notlar — young-adult 2 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.\n\nAy sonunda öneri kartları yeniden gözden geçirildi. Bazı istekler hemen yapılmış, bazıları bütçe bekliyordu. Pano, yalnız tamamlanan işleri değil, henüz çözülemeyen ihtiyaçları da gösteriyordu. Görevli, bir isteğin beklemesinin unutulduğu anlamına gelmediğini açıkladı.\n\nKütüphaneye yeni kitaplar geldiğinde herkesin sevdiği türlerden eşit sayıda kitap bulunmadı. Gönüllüler tek bir kişinin tercihine göre karar vermedi. Ödünç alma kayıtlarına, önerilere ve raflarda eksik kalan konulara birlikte baktılar. Kararın gerekçesini panoda paylaştılar.",
    "wordCount": 524,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000035",
    "exerciseId": "c53fae0b-d606-539d-b02a-60d080fc8e75",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 2,
    "title": "Bahçede birlikte öğrenmek — young-adult 2 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.\n\nSulama görevleri haftalık bir çizelgeye yazıldı. Çizelgede yalnız isimler değil, yapılacak işin kısa açıklaması da vardı. Bir kişi gelemediğinde başka biri görevi devralabiliyordu. Görevin nasıl yapılacağını bilmek, görevlinin adını bilmek kadar önemliydi.\n\nÇocuklar farklı sulama miktarlarını denemek istediğinde öğretmenleri önce küçük bir alan seçmelerini önerdi. Bütün bahçeyi aynı anda değiştirmek yerine, karşılaştırabilecekleri birkaç bitki ayırdılar. Ne değiştirdiklerini açıkça yazdılar ve diğer koşulları benzer tutmaya çalıştılar.\n\nDenemenin sonunda bazı bitkiler daha canlı görünüyordu. Ancak çocuklar bunun yalnız sulamadan kaynaklandığını hemen söylemedi. Toprağın ve güneşin de etkili olabileceğini konuştular. Sonucu bir gözlem olarak yazdılar; kesin bir kural gibi sunmadılar.\n\nBahçenin çevresine küçük bir yol yapıldı. Yol, bitkilere basmadan yaklaşmayı kolaylaştırıyordu. Önce çok dar çizilen geçit, sulama kabı taşıyanlara yetmedi. Çocuklar yürüyerek denedi ve yolu biraz genişletti. Çizim, kullanıldığında karşılaşılan duruma göre değişti.\n\nBazı ziyaretçiler bitkilerin yanında daha fazla bilgi istedi. Çocuklar uzun açıklamalar yerine kısa kartlar hazırladı. Kartlarda ad, ekim tarihi ve birkaç gözlem bulunuyordu. Ayrıntılı notları görmek isteyenler için defterin nerede bulunduğu ayrıca yazıldı.\n\nMevsim değiştikçe bahçenin görünümü de değişti. Çocuklar eski fotoğraflarla yeni fotoğrafları karşılaştırdı. Fotoğrafların benzer açıdan çekilmesi karşılaştırmayı kolaylaştırdı. Farklı açı ve ışıkla çekilen görüntülerde bir değişiklik varmış gibi görünmenin mümkün olduğunu fark ettiler.",
    "wordCount": 524,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000036",
    "exerciseId": "c53fae0b-d606-539d-b02a-60d080fc8e75",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 2,
    "title": "Bir haritanın gelişimi — young-adult 2 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.\n\nYol çalışması bitince eski sokak yeniden açıldı. Deniz yeni haritasını hemen atmadı. Farklı yolların başka günlerde de işe yarayabileceğini düşündü. Ancak kapalı sokak işaretini kaldırdı ve haritanın hangi tarihte güncellendiğini yazdı.\n\nMahalledeki gençler, yürüyüş yollarını birlikte kaydetmek istedi. Herkes kendi bildiği köşeleri çizdi. Çizimler birleştirildiğinde bazı adların farklı yazıldığı görüldü. Aynı yerden söz edip etmediklerini birlikte kontrol ederek ortak bir ad listesi hazırladılar.\n\nGrup, haritaya bütün bilgileri aynı anda koymamaya karar verdi. Ana çizimde yönler ve önemli noktalar bulunacaktı. Ayrıntılar ayrı notlarda gösterilecekti. Çok fazla işaretin, aranan bilgiyi bulmayı zorlaştırabileceğini denemelerde görmüşlerdi.\n\nYürüyüş süresi için birkaç farklı günde kayıt tutuldu. Yağmurlu günlerin, kalabalık saatlerin ve yol çalışmalarının süreyi değiştirdiği görüldü. Sonuçlar tek bir sayı yerine küçük bir aralıkla anlatıldı. Bu aralık, beklenebilecek değişkenliği daha açık gösteriyordu.\n\nBazı sokaklar kısa görünse de yürümek için uygun değildi. Grup yalnız mesafeye bakarak öneri vermedi. Kaldırımın durumunu, geçiş noktalarını ve yolun açık olup olmadığını da gözledi. Bir ölçünün, bütün kararın yerine geçemeyeceğini konuştular.\n\nHaritadaki bilgiler zamanla eskiyebilirdi. Grup, yanlış bir işaret görenlerin bunu nasıl bildireceğini yazdı. Gelen önerilerin hemen doğru kabul edilmemesi gerektiğini de belirledi. Değişiklik yapılmadan önce yerin yeniden kontrol edilmesi planlandı.",
    "wordCount": 517,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000037",
    "exerciseId": "eb87713b-d508-5d54-8556-fe7f0765bd90",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 3,
    "title": "Mahalle kütüphanesinden notlar — young-adult 3 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.\n\nAy sonunda öneri kartları yeniden gözden geçirildi. Bazı istekler hemen yapılmış, bazıları bütçe bekliyordu. Pano, yalnız tamamlanan işleri değil, henüz çözülemeyen ihtiyaçları da gösteriyordu. Görevli, bir isteğin beklemesinin unutulduğu anlamına gelmediğini açıkladı.\n\nKütüphaneye yeni kitaplar geldiğinde herkesin sevdiği türlerden eşit sayıda kitap bulunmadı. Gönüllüler tek bir kişinin tercihine göre karar vermedi. Ödünç alma kayıtlarına, önerilere ve raflarda eksik kalan konulara birlikte baktılar. Kararın gerekçesini panoda paylaştılar.\n\nBazı kitaplar çok sık isteniyordu. Bu kitaplar için sıraya girenlere tahmini dönüş tarihi söylendi. Sıra beklemek istemeyenlere benzer konudaki başka kitaplar gösterildi. Bir seçenek sunmak, aranan kitabın hemen bulunamamasını daha anlaşılır hâle getirdi.\n\nBir okuma buluşmasında aynı öykü hakkında farklı yorumlar yapıldı. Duru kahramanın cesur olduğunu düşünürken arkadaşı aceleci olduğunu söyledi. İkisi de görüşünü öyküdeki bir olayla açıkladı. Farklı yorumların, dikkatle dinlendiğinde konuşmayı zenginleştirebildiğini gördüler.",
    "wordCount": 589,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000038",
    "exerciseId": "eb87713b-d508-5d54-8556-fe7f0765bd90",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 3,
    "title": "Bahçede birlikte öğrenmek — young-adult 3 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.\n\nSulama görevleri haftalık bir çizelgeye yazıldı. Çizelgede yalnız isimler değil, yapılacak işin kısa açıklaması da vardı. Bir kişi gelemediğinde başka biri görevi devralabiliyordu. Görevin nasıl yapılacağını bilmek, görevlinin adını bilmek kadar önemliydi.\n\nÇocuklar farklı sulama miktarlarını denemek istediğinde öğretmenleri önce küçük bir alan seçmelerini önerdi. Bütün bahçeyi aynı anda değiştirmek yerine, karşılaştırabilecekleri birkaç bitki ayırdılar. Ne değiştirdiklerini açıkça yazdılar ve diğer koşulları benzer tutmaya çalıştılar.\n\nDenemenin sonunda bazı bitkiler daha canlı görünüyordu. Ancak çocuklar bunun yalnız sulamadan kaynaklandığını hemen söylemedi. Toprağın ve güneşin de etkili olabileceğini konuştular. Sonucu bir gözlem olarak yazdılar; kesin bir kural gibi sunmadılar.\n\nBahçenin çevresine küçük bir yol yapıldı. Yol, bitkilere basmadan yaklaşmayı kolaylaştırıyordu. Önce çok dar çizilen geçit, sulama kabı taşıyanlara yetmedi. Çocuklar yürüyerek denedi ve yolu biraz genişletti. Çizim, kullanıldığında karşılaşılan duruma göre değişti.\n\nBazı ziyaretçiler bitkilerin yanında daha fazla bilgi istedi. Çocuklar uzun açıklamalar yerine kısa kartlar hazırladı. Kartlarda ad, ekim tarihi ve birkaç gözlem bulunuyordu. Ayrıntılı notları görmek isteyenler için defterin nerede bulunduğu ayrıca yazıldı.\n\nMevsim değiştikçe bahçenin görünümü de değişti. Çocuklar eski fotoğraflarla yeni fotoğrafları karşılaştırdı. Fotoğrafların benzer açıdan çekilmesi karşılaştırmayı kolaylaştırdı. Farklı açı ve ışıkla çekilen görüntülerde bir değişiklik varmış gibi görünmenin mümkün olduğunu fark ettiler.\n\nBir bitki kuruduğunda grup kimin hata yaptığını aramakla başlamadı. Önce bakım kayıtlarını ve toprağın durumunu inceledi. Her sorunun tek bir kişiden kaynaklanmayabileceğini konuştular. Sonraki ekimde neyi farklı yapabileceklerini birlikte yazdılar.\n\nBahçe büyüdükçe iş paylaşımı da değişti. Başlangıçta bütün işleri birlikte yapan grup, küçük sorumluluk alanları oluşturdu. Yine de herkes diğer alanların durumunu öğrenebiliyordu. Ortak defter, ayrı görevlerin birbirinden tamamen kopmasını önleyen bir bağlantı oldu.",
    "wordCount": 588,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000039",
    "exerciseId": "eb87713b-d508-5d54-8556-fe7f0765bd90",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 3,
    "title": "Bir haritanın gelişimi — young-adult 3 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.\n\nYol çalışması bitince eski sokak yeniden açıldı. Deniz yeni haritasını hemen atmadı. Farklı yolların başka günlerde de işe yarayabileceğini düşündü. Ancak kapalı sokak işaretini kaldırdı ve haritanın hangi tarihte güncellendiğini yazdı.\n\nMahalledeki gençler, yürüyüş yollarını birlikte kaydetmek istedi. Herkes kendi bildiği köşeleri çizdi. Çizimler birleştirildiğinde bazı adların farklı yazıldığı görüldü. Aynı yerden söz edip etmediklerini birlikte kontrol ederek ortak bir ad listesi hazırladılar.\n\nGrup, haritaya bütün bilgileri aynı anda koymamaya karar verdi. Ana çizimde yönler ve önemli noktalar bulunacaktı. Ayrıntılar ayrı notlarda gösterilecekti. Çok fazla işaretin, aranan bilgiyi bulmayı zorlaştırabileceğini denemelerde görmüşlerdi.\n\nYürüyüş süresi için birkaç farklı günde kayıt tutuldu. Yağmurlu günlerin, kalabalık saatlerin ve yol çalışmalarının süreyi değiştirdiği görüldü. Sonuçlar tek bir sayı yerine küçük bir aralıkla anlatıldı. Bu aralık, beklenebilecek değişkenliği daha açık gösteriyordu.\n\nBazı sokaklar kısa görünse de yürümek için uygun değildi. Grup yalnız mesafeye bakarak öneri vermedi. Kaldırımın durumunu, geçiş noktalarını ve yolun açık olup olmadığını da gözledi. Bir ölçünün, bütün kararın yerine geçemeyeceğini konuştular.\n\nHaritadaki bilgiler zamanla eskiyebilirdi. Grup, yanlış bir işaret görenlerin bunu nasıl bildireceğini yazdı. Gelen önerilerin hemen doğru kabul edilmemesi gerektiğini de belirledi. Değişiklik yapılmadan önce yerin yeniden kontrol edilmesi planlandı.\n\nBir sokak adı değiştiğinde eski adla arayanlar yeni haritada zorlandı. Grup bir süre iki adı birlikte gösterdi. Böylece yeni bilgi, önceki bilgiyi bilenleri tamamen dışarıda bırakmadı. Güncelleme yapmak, eski kullanıcıların nasıl aradığını düşünmeyi de gerektiriyordu.\n\nHarita toplantısında bir anlaşmazlık çıktı. Bir kişi daha kısa rotayı, diğeri daha az basamak içeren rotayı öneriyordu. İkisinin farklı ihtiyaçları önemsediği anlaşıldı. Tek doğru rota seçmek yerine, seçeneklerin özelliklerini açıkça gösterdiler.",
    "wordCount": 583,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000040",
    "exerciseId": "57d186e0-8756-5e69-aaa2-3dd457c931b9",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 4,
    "title": "Mahalle kütüphanesinden notlar — young-adult 4 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.\n\nAy sonunda öneri kartları yeniden gözden geçirildi. Bazı istekler hemen yapılmış, bazıları bütçe bekliyordu. Pano, yalnız tamamlanan işleri değil, henüz çözülemeyen ihtiyaçları da gösteriyordu. Görevli, bir isteğin beklemesinin unutulduğu anlamına gelmediğini açıkladı.\n\nKütüphaneye yeni kitaplar geldiğinde herkesin sevdiği türlerden eşit sayıda kitap bulunmadı. Gönüllüler tek bir kişinin tercihine göre karar vermedi. Ödünç alma kayıtlarına, önerilere ve raflarda eksik kalan konulara birlikte baktılar. Kararın gerekçesini panoda paylaştılar.\n\nBazı kitaplar çok sık isteniyordu. Bu kitaplar için sıraya girenlere tahmini dönüş tarihi söylendi. Sıra beklemek istemeyenlere benzer konudaki başka kitaplar gösterildi. Bir seçenek sunmak, aranan kitabın hemen bulunamamasını daha anlaşılır hâle getirdi.\n\nBir okuma buluşmasında aynı öykü hakkında farklı yorumlar yapıldı. Duru kahramanın cesur olduğunu düşünürken arkadaşı aceleci olduğunu söyledi. İkisi de görüşünü öyküdeki bir olayla açıkladı. Farklı yorumların, dikkatle dinlendiğinde konuşmayı zenginleştirebildiğini gördüler.\n\nBuluşmayı yöneten kişi, herkesi aynı görüşte birleştirmeye çalışmadı. Önce yorumların hangi ayrıntıya dayandığını sordu. Sonra öykünün başka bir bölümünü birlikte okudular. Yeni ayrıntılar bazı düşünceleri değiştirdi, bazılarını güçlendirdi. Konuşma, yalnız sonuçtan değil gerekçelerden oluştu.\n\nDuru artık kütüphaneye yalnız kitap almak için gelmiyordu. Bir soruyu nasıl soracağını, bilgiyi nerede arayacağını ve bulduğunu nasıl düzenleyeceğini de deniyordu. Kütüphane onun için bitmiş yanıtların bulunduğu bir yerden çok, merakın geliştiği bir çalışma alanıydı.",
    "wordCount": 658,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000041",
    "exerciseId": "57d186e0-8756-5e69-aaa2-3dd457c931b9",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 4,
    "title": "Bahçede birlikte öğrenmek — young-adult 4 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.\n\nSulama görevleri haftalık bir çizelgeye yazıldı. Çizelgede yalnız isimler değil, yapılacak işin kısa açıklaması da vardı. Bir kişi gelemediğinde başka biri görevi devralabiliyordu. Görevin nasıl yapılacağını bilmek, görevlinin adını bilmek kadar önemliydi.\n\nÇocuklar farklı sulama miktarlarını denemek istediğinde öğretmenleri önce küçük bir alan seçmelerini önerdi. Bütün bahçeyi aynı anda değiştirmek yerine, karşılaştırabilecekleri birkaç bitki ayırdılar. Ne değiştirdiklerini açıkça yazdılar ve diğer koşulları benzer tutmaya çalıştılar.\n\nDenemenin sonunda bazı bitkiler daha canlı görünüyordu. Ancak çocuklar bunun yalnız sulamadan kaynaklandığını hemen söylemedi. Toprağın ve güneşin de etkili olabileceğini konuştular. Sonucu bir gözlem olarak yazdılar; kesin bir kural gibi sunmadılar.\n\nBahçenin çevresine küçük bir yol yapıldı. Yol, bitkilere basmadan yaklaşmayı kolaylaştırıyordu. Önce çok dar çizilen geçit, sulama kabı taşıyanlara yetmedi. Çocuklar yürüyerek denedi ve yolu biraz genişletti. Çizim, kullanıldığında karşılaşılan duruma göre değişti.\n\nBazı ziyaretçiler bitkilerin yanında daha fazla bilgi istedi. Çocuklar uzun açıklamalar yerine kısa kartlar hazırladı. Kartlarda ad, ekim tarihi ve birkaç gözlem bulunuyordu. Ayrıntılı notları görmek isteyenler için defterin nerede bulunduğu ayrıca yazıldı.\n\nMevsim değiştikçe bahçenin görünümü de değişti. Çocuklar eski fotoğraflarla yeni fotoğrafları karşılaştırdı. Fotoğrafların benzer açıdan çekilmesi karşılaştırmayı kolaylaştırdı. Farklı açı ve ışıkla çekilen görüntülerde bir değişiklik varmış gibi görünmenin mümkün olduğunu fark ettiler.\n\nBir bitki kuruduğunda grup kimin hata yaptığını aramakla başlamadı. Önce bakım kayıtlarını ve toprağın durumunu inceledi. Her sorunun tek bir kişiden kaynaklanmayabileceğini konuştular. Sonraki ekimde neyi farklı yapabileceklerini birlikte yazdılar.\n\nBahçe büyüdükçe iş paylaşımı da değişti. Başlangıçta bütün işleri birlikte yapan grup, küçük sorumluluk alanları oluşturdu. Yine de herkes diğer alanların durumunu öğrenebiliyordu. Ortak defter, ayrı görevlerin birbirinden tamamen kopmasını önleyen bir bağlantı oldu.\n\nYıl sonunda yeni gelen öğrenciler bahçeyi devralacaktı. Eren ve arkadaşları yalnız güzel fotoğrafları seçmedi. Karışıklık yaşanan günleri ve çözüm yollarını da anlattı. Böylece yeni grup, aynı güçlükleri yeniden yaşamak yerine önceki denemelerden yararlanabilecekti.\n\nHazırladıkları dosyada tamamlanan işler kadar açık sorular da vardı. Hangi bitkinin başka köşede daha iyi büyüyeceği henüz bilinmiyordu. Dosya her konuda kesin yanıt vermiyordu. Yeni öğrencilerin kendi gözlemlerini ekleyebileceği bir başlangıç sunuyordu.\n\nEren son kez bahçedeki işaretlere baktığında küçük köşenin yalnız bitkilerden oluşmadığını düşündü. Orada ortak kararlar, ölçümler ve dikkatli notlar da birikmişti. Bahçeyi devralanların aynı yolu izlemesi şart değildi. Önemli olan yaptıklarını gözlemleyip birbirlerine anlatabilmeleriydi.",
    "wordCount": 687,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000042",
    "exerciseId": "57d186e0-8756-5e69-aaa2-3dd457c931b9",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 4,
    "title": "Bir haritanın gelişimi — young-adult 4 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.\n\nYol çalışması bitince eski sokak yeniden açıldı. Deniz yeni haritasını hemen atmadı. Farklı yolların başka günlerde de işe yarayabileceğini düşündü. Ancak kapalı sokak işaretini kaldırdı ve haritanın hangi tarihte güncellendiğini yazdı.\n\nMahalledeki gençler, yürüyüş yollarını birlikte kaydetmek istedi. Herkes kendi bildiği köşeleri çizdi. Çizimler birleştirildiğinde bazı adların farklı yazıldığı görüldü. Aynı yerden söz edip etmediklerini birlikte kontrol ederek ortak bir ad listesi hazırladılar.\n\nGrup, haritaya bütün bilgileri aynı anda koymamaya karar verdi. Ana çizimde yönler ve önemli noktalar bulunacaktı. Ayrıntılar ayrı notlarda gösterilecekti. Çok fazla işaretin, aranan bilgiyi bulmayı zorlaştırabileceğini denemelerde görmüşlerdi.\n\nYürüyüş süresi için birkaç farklı günde kayıt tutuldu. Yağmurlu günlerin, kalabalık saatlerin ve yol çalışmalarının süreyi değiştirdiği görüldü. Sonuçlar tek bir sayı yerine küçük bir aralıkla anlatıldı. Bu aralık, beklenebilecek değişkenliği daha açık gösteriyordu.\n\nBazı sokaklar kısa görünse de yürümek için uygun değildi. Grup yalnız mesafeye bakarak öneri vermedi. Kaldırımın durumunu, geçiş noktalarını ve yolun açık olup olmadığını da gözledi. Bir ölçünün, bütün kararın yerine geçemeyeceğini konuştular.\n\nHaritadaki bilgiler zamanla eskiyebilirdi. Grup, yanlış bir işaret görenlerin bunu nasıl bildireceğini yazdı. Gelen önerilerin hemen doğru kabul edilmemesi gerektiğini de belirledi. Değişiklik yapılmadan önce yerin yeniden kontrol edilmesi planlandı.\n\nBir sokak adı değiştiğinde eski adla arayanlar yeni haritada zorlandı. Grup bir süre iki adı birlikte gösterdi. Böylece yeni bilgi, önceki bilgiyi bilenleri tamamen dışarıda bırakmadı. Güncelleme yapmak, eski kullanıcıların nasıl aradığını düşünmeyi de gerektiriyordu.\n\nHarita toplantısında bir anlaşmazlık çıktı. Bir kişi daha kısa rotayı, diğeri daha az basamak içeren rotayı öneriyordu. İkisinin farklı ihtiyaçları önemsediği anlaşıldı. Tek doğru rota seçmek yerine, seçeneklerin özelliklerini açıkça gösterdiler.\n\nHazırlanan çizim bir rehberdi; her koşulda değişmez bir söz vermiyordu. Yola çıkmadan önce geçici kapanmaların kontrol edilmesi gerektiği yazıldı. Grup bu açıklamayı küçük ve görünmez bir not yapmak istemedi. Bilginin sınırları, yön işaretleri kadar anlaşılır olmalıydı.\n\nYeni gönüllüler çalışmaya katıldığında onlara yalnız son çizim verilmedi. İşaretlerin neden seçildiği ve hangi denemelerin yapıldığı da anlatıldı. Böylece bir simge değiştirileceğinde önceki kararın gerekçesi görülebiliyordu. Ortak çalışma, çizim kadar kayıtların paylaşılmasına da dayanıyordu.\n\nDeniz, ilk haritasını yeni çizimin yanına koydu. İkisi arasında büyük fark vardı ama ilk çizim gereksiz değildi. Küçük bir ihtiyacı görünür kılmış ve konuşmayı başlatmıştı. Yeni harita da son nokta değil, gözlemle geliştirilebilecek bir ortak başlangıçtı.",
    "wordCount": 689,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000043",
    "exerciseId": "d7b10000-0000-5000-8000-000000000004",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 5,
    "title": "Mahalle kütüphanesinden notlar — young-adult 5 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.\n\nAy sonunda öneri kartları yeniden gözden geçirildi. Bazı istekler hemen yapılmış, bazıları bütçe bekliyordu. Pano, yalnız tamamlanan işleri değil, henüz çözülemeyen ihtiyaçları da gösteriyordu. Görevli, bir isteğin beklemesinin unutulduğu anlamına gelmediğini açıkladı.\n\nKütüphaneye yeni kitaplar geldiğinde herkesin sevdiği türlerden eşit sayıda kitap bulunmadı. Gönüllüler tek bir kişinin tercihine göre karar vermedi. Ödünç alma kayıtlarına, önerilere ve raflarda eksik kalan konulara birlikte baktılar. Kararın gerekçesini panoda paylaştılar.\n\nBazı kitaplar çok sık isteniyordu. Bu kitaplar için sıraya girenlere tahmini dönüş tarihi söylendi. Sıra beklemek istemeyenlere benzer konudaki başka kitaplar gösterildi. Bir seçenek sunmak, aranan kitabın hemen bulunamamasını daha anlaşılır hâle getirdi.\n\nBir okuma buluşmasında aynı öykü hakkında farklı yorumlar yapıldı. Duru kahramanın cesur olduğunu düşünürken arkadaşı aceleci olduğunu söyledi. İkisi de görüşünü öyküdeki bir olayla açıkladı. Farklı yorumların, dikkatle dinlendiğinde konuşmayı zenginleştirebildiğini gördüler.\n\nBuluşmayı yöneten kişi, herkesi aynı görüşte birleştirmeye çalışmadı. Önce yorumların hangi ayrıntıya dayandığını sordu. Sonra öykünün başka bir bölümünü birlikte okudular. Yeni ayrıntılar bazı düşünceleri değiştirdi, bazılarını güçlendirdi. Konuşma, yalnız sonuçtan değil gerekçelerden oluştu.\n\nDuru artık kütüphaneye yalnız kitap almak için gelmiyordu. Bir soruyu nasıl soracağını, bilgiyi nerede arayacağını ve bulduğunu nasıl düzenleyeceğini de deniyordu. Kütüphane onun için bitmiş yanıtların bulunduğu bir yerden çok, merakın geliştiği bir çalışma alanıydı.\n\nYeni bir ziyaretçi kapıdaki haritaya bakıp öykü rafını kolayca bulduğunda Duru sevindi. Küçük bir önerinin işe yaradığı görülmüştü. Yine de haritanın sonsuza kadar değişmeden kalması gerekmiyordu. Raflar ve ihtiyaçlar değiştikçe çizim de yeniden gözden geçirilebilirdi.",
    "wordCount": 693,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000044",
    "exerciseId": "d7b10000-0000-5000-8000-000000000004",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 5,
    "title": "Bahçede birlikte öğrenmek — young-adult 5 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.\n\nSulama görevleri haftalık bir çizelgeye yazıldı. Çizelgede yalnız isimler değil, yapılacak işin kısa açıklaması da vardı. Bir kişi gelemediğinde başka biri görevi devralabiliyordu. Görevin nasıl yapılacağını bilmek, görevlinin adını bilmek kadar önemliydi.\n\nÇocuklar farklı sulama miktarlarını denemek istediğinde öğretmenleri önce küçük bir alan seçmelerini önerdi. Bütün bahçeyi aynı anda değiştirmek yerine, karşılaştırabilecekleri birkaç bitki ayırdılar. Ne değiştirdiklerini açıkça yazdılar ve diğer koşulları benzer tutmaya çalıştılar.\n\nDenemenin sonunda bazı bitkiler daha canlı görünüyordu. Ancak çocuklar bunun yalnız sulamadan kaynaklandığını hemen söylemedi. Toprağın ve güneşin de etkili olabileceğini konuştular. Sonucu bir gözlem olarak yazdılar; kesin bir kural gibi sunmadılar.\n\nBahçenin çevresine küçük bir yol yapıldı. Yol, bitkilere basmadan yaklaşmayı kolaylaştırıyordu. Önce çok dar çizilen geçit, sulama kabı taşıyanlara yetmedi. Çocuklar yürüyerek denedi ve yolu biraz genişletti. Çizim, kullanıldığında karşılaşılan duruma göre değişti.\n\nBazı ziyaretçiler bitkilerin yanında daha fazla bilgi istedi. Çocuklar uzun açıklamalar yerine kısa kartlar hazırladı. Kartlarda ad, ekim tarihi ve birkaç gözlem bulunuyordu. Ayrıntılı notları görmek isteyenler için defterin nerede bulunduğu ayrıca yazıldı.\n\nMevsim değiştikçe bahçenin görünümü de değişti. Çocuklar eski fotoğraflarla yeni fotoğrafları karşılaştırdı. Fotoğrafların benzer açıdan çekilmesi karşılaştırmayı kolaylaştırdı. Farklı açı ve ışıkla çekilen görüntülerde bir değişiklik varmış gibi görünmenin mümkün olduğunu fark ettiler.\n\nBir bitki kuruduğunda grup kimin hata yaptığını aramakla başlamadı. Önce bakım kayıtlarını ve toprağın durumunu inceledi. Her sorunun tek bir kişiden kaynaklanmayabileceğini konuştular. Sonraki ekimde neyi farklı yapabileceklerini birlikte yazdılar.\n\nBahçe büyüdükçe iş paylaşımı da değişti. Başlangıçta bütün işleri birlikte yapan grup, küçük sorumluluk alanları oluşturdu. Yine de herkes diğer alanların durumunu öğrenebiliyordu. Ortak defter, ayrı görevlerin birbirinden tamamen kopmasını önleyen bir bağlantı oldu.\n\nYıl sonunda yeni gelen öğrenciler bahçeyi devralacaktı. Eren ve arkadaşları yalnız güzel fotoğrafları seçmedi. Karışıklık yaşanan günleri ve çözüm yollarını da anlattı. Böylece yeni grup, aynı güçlükleri yeniden yaşamak yerine önceki denemelerden yararlanabilecekti.\n\nHazırladıkları dosyada tamamlanan işler kadar açık sorular da vardı. Hangi bitkinin başka köşede daha iyi büyüyeceği henüz bilinmiyordu. Dosya her konuda kesin yanıt vermiyordu. Yeni öğrencilerin kendi gözlemlerini ekleyebileceği bir başlangıç sunuyordu.\n\nEren son kez bahçedeki işaretlere baktığında küçük köşenin yalnız bitkilerden oluşmadığını düşündü. Orada ortak kararlar, ölçümler ve dikkatli notlar da birikmişti. Bahçeyi devralanların aynı yolu izlemesi şart değildi. Önemli olan yaptıklarını gözlemleyip birbirlerine anlatabilmeleriydi.",
    "wordCount": 687,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000045",
    "exerciseId": "d7b10000-0000-5000-8000-000000000004",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "level": 5,
    "title": "Bir haritanın gelişimi — young-adult 5 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.\n\nYol çalışması bitince eski sokak yeniden açıldı. Deniz yeni haritasını hemen atmadı. Farklı yolların başka günlerde de işe yarayabileceğini düşündü. Ancak kapalı sokak işaretini kaldırdı ve haritanın hangi tarihte güncellendiğini yazdı.\n\nMahalledeki gençler, yürüyüş yollarını birlikte kaydetmek istedi. Herkes kendi bildiği köşeleri çizdi. Çizimler birleştirildiğinde bazı adların farklı yazıldığı görüldü. Aynı yerden söz edip etmediklerini birlikte kontrol ederek ortak bir ad listesi hazırladılar.\n\nGrup, haritaya bütün bilgileri aynı anda koymamaya karar verdi. Ana çizimde yönler ve önemli noktalar bulunacaktı. Ayrıntılar ayrı notlarda gösterilecekti. Çok fazla işaretin, aranan bilgiyi bulmayı zorlaştırabileceğini denemelerde görmüşlerdi.\n\nYürüyüş süresi için birkaç farklı günde kayıt tutuldu. Yağmurlu günlerin, kalabalık saatlerin ve yol çalışmalarının süreyi değiştirdiği görüldü. Sonuçlar tek bir sayı yerine küçük bir aralıkla anlatıldı. Bu aralık, beklenebilecek değişkenliği daha açık gösteriyordu.\n\nBazı sokaklar kısa görünse de yürümek için uygun değildi. Grup yalnız mesafeye bakarak öneri vermedi. Kaldırımın durumunu, geçiş noktalarını ve yolun açık olup olmadığını da gözledi. Bir ölçünün, bütün kararın yerine geçemeyeceğini konuştular.\n\nHaritadaki bilgiler zamanla eskiyebilirdi. Grup, yanlış bir işaret görenlerin bunu nasıl bildireceğini yazdı. Gelen önerilerin hemen doğru kabul edilmemesi gerektiğini de belirledi. Değişiklik yapılmadan önce yerin yeniden kontrol edilmesi planlandı.\n\nBir sokak adı değiştiğinde eski adla arayanlar yeni haritada zorlandı. Grup bir süre iki adı birlikte gösterdi. Böylece yeni bilgi, önceki bilgiyi bilenleri tamamen dışarıda bırakmadı. Güncelleme yapmak, eski kullanıcıların nasıl aradığını düşünmeyi de gerektiriyordu.\n\nHarita toplantısında bir anlaşmazlık çıktı. Bir kişi daha kısa rotayı, diğeri daha az basamak içeren rotayı öneriyordu. İkisinin farklı ihtiyaçları önemsediği anlaşıldı. Tek doğru rota seçmek yerine, seçeneklerin özelliklerini açıkça gösterdiler.\n\nHazırlanan çizim bir rehberdi; her koşulda değişmez bir söz vermiyordu. Yola çıkmadan önce geçici kapanmaların kontrol edilmesi gerektiği yazıldı. Grup bu açıklamayı küçük ve görünmez bir not yapmak istemedi. Bilginin sınırları, yön işaretleri kadar anlaşılır olmalıydı.\n\nYeni gönüllüler çalışmaya katıldığında onlara yalnız son çizim verilmedi. İşaretlerin neden seçildiği ve hangi denemelerin yapıldığı da anlatıldı. Böylece bir simge değiştirileceğinde önceki kararın gerekçesi görülebiliyordu. Ortak çalışma, çizim kadar kayıtların paylaşılmasına da dayanıyordu.\n\nDeniz, ilk haritasını yeni çizimin yanına koydu. İkisi arasında büyük fark vardı ama ilk çizim gereksiz değildi. Küçük bir ihtiyacı görünür kılmış ve konuşmayı başlatmıştı. Yeni harita da son nokta değil, gözlemle geliştirilebilecek bir ortak başlangıçtı.",
    "wordCount": 689,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000046",
    "exerciseId": "02fc1e52-80d6-506e-9317-fd5eec33ea2d",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 1,
    "title": "Mahalle kütüphanesinden notlar — adult 1 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.\n\nAy sonunda öneri kartları yeniden gözden geçirildi. Bazı istekler hemen yapılmış, bazıları bütçe bekliyordu. Pano, yalnız tamamlanan işleri değil, henüz çözülemeyen ihtiyaçları da gösteriyordu. Görevli, bir isteğin beklemesinin unutulduğu anlamına gelmediğini açıkladı.",
    "wordCount": 490,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000047",
    "exerciseId": "02fc1e52-80d6-506e-9317-fd5eec33ea2d",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 1,
    "title": "Bahçede birlikte öğrenmek — adult 1 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.\n\nSulama görevleri haftalık bir çizelgeye yazıldı. Çizelgede yalnız isimler değil, yapılacak işin kısa açıklaması da vardı. Bir kişi gelemediğinde başka biri görevi devralabiliyordu. Görevin nasıl yapılacağını bilmek, görevlinin adını bilmek kadar önemliydi.\n\nÇocuklar farklı sulama miktarlarını denemek istediğinde öğretmenleri önce küçük bir alan seçmelerini önerdi. Bütün bahçeyi aynı anda değiştirmek yerine, karşılaştırabilecekleri birkaç bitki ayırdılar. Ne değiştirdiklerini açıkça yazdılar ve diğer koşulları benzer tutmaya çalıştılar.\n\nDenemenin sonunda bazı bitkiler daha canlı görünüyordu. Ancak çocuklar bunun yalnız sulamadan kaynaklandığını hemen söylemedi. Toprağın ve güneşin de etkili olabileceğini konuştular. Sonucu bir gözlem olarak yazdılar; kesin bir kural gibi sunmadılar.\n\nBahçenin çevresine küçük bir yol yapıldı. Yol, bitkilere basmadan yaklaşmayı kolaylaştırıyordu. Önce çok dar çizilen geçit, sulama kabı taşıyanlara yetmedi. Çocuklar yürüyerek denedi ve yolu biraz genişletti. Çizim, kullanıldığında karşılaşılan duruma göre değişti.\n\nBazı ziyaretçiler bitkilerin yanında daha fazla bilgi istedi. Çocuklar uzun açıklamalar yerine kısa kartlar hazırladı. Kartlarda ad, ekim tarihi ve birkaç gözlem bulunuyordu. Ayrıntılı notları görmek isteyenler için defterin nerede bulunduğu ayrıca yazıldı.",
    "wordCount": 491,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000048",
    "exerciseId": "02fc1e52-80d6-506e-9317-fd5eec33ea2d",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 1,
    "title": "Bir haritanın gelişimi — adult 1 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.\n\nYol çalışması bitince eski sokak yeniden açıldı. Deniz yeni haritasını hemen atmadı. Farklı yolların başka günlerde de işe yarayabileceğini düşündü. Ancak kapalı sokak işaretini kaldırdı ve haritanın hangi tarihte güncellendiğini yazdı.\n\nMahalledeki gençler, yürüyüş yollarını birlikte kaydetmek istedi. Herkes kendi bildiği köşeleri çizdi. Çizimler birleştirildiğinde bazı adların farklı yazıldığı görüldü. Aynı yerden söz edip etmediklerini birlikte kontrol ederek ortak bir ad listesi hazırladılar.\n\nGrup, haritaya bütün bilgileri aynı anda koymamaya karar verdi. Ana çizimde yönler ve önemli noktalar bulunacaktı. Ayrıntılar ayrı notlarda gösterilecekti. Çok fazla işaretin, aranan bilgiyi bulmayı zorlaştırabileceğini denemelerde görmüşlerdi.\n\nYürüyüş süresi için birkaç farklı günde kayıt tutuldu. Yağmurlu günlerin, kalabalık saatlerin ve yol çalışmalarının süreyi değiştirdiği görüldü. Sonuçlar tek bir sayı yerine küçük bir aralıkla anlatıldı. Bu aralık, beklenebilecek değişkenliği daha açık gösteriyordu.\n\nBazı sokaklar kısa görünse de yürümek için uygun değildi. Grup yalnız mesafeye bakarak öneri vermedi. Kaldırımın durumunu, geçiş noktalarını ve yolun açık olup olmadığını da gözledi. Bir ölçünün, bütün kararın yerine geçemeyeceğini konuştular.",
    "wordCount": 487,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000049",
    "exerciseId": "8686e7b0-4d3c-5e51-9aa2-8ee87fbffd65",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 2,
    "title": "Mahalle kütüphanesinden notlar — adult 2 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.\n\nAy sonunda öneri kartları yeniden gözden geçirildi. Bazı istekler hemen yapılmış, bazıları bütçe bekliyordu. Pano, yalnız tamamlanan işleri değil, henüz çözülemeyen ihtiyaçları da gösteriyordu. Görevli, bir isteğin beklemesinin unutulduğu anlamına gelmediğini açıkladı.\n\nKütüphaneye yeni kitaplar geldiğinde herkesin sevdiği türlerden eşit sayıda kitap bulunmadı. Gönüllüler tek bir kişinin tercihine göre karar vermedi. Ödünç alma kayıtlarına, önerilere ve raflarda eksik kalan konulara birlikte baktılar. Kararın gerekçesini panoda paylaştılar.\n\nBazı kitaplar çok sık isteniyordu. Bu kitaplar için sıraya girenlere tahmini dönüş tarihi söylendi. Sıra beklemek istemeyenlere benzer konudaki başka kitaplar gösterildi. Bir seçenek sunmak, aranan kitabın hemen bulunamamasını daha anlaşılır hâle getirdi.\n\nBir okuma buluşmasında aynı öykü hakkında farklı yorumlar yapıldı. Duru kahramanın cesur olduğunu düşünürken arkadaşı aceleci olduğunu söyledi. İkisi de görüşünü öyküdeki bir olayla açıkladı. Farklı yorumların, dikkatle dinlendiğinde konuşmayı zenginleştirebildiğini gördüler.",
    "wordCount": 589,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000050",
    "exerciseId": "8686e7b0-4d3c-5e51-9aa2-8ee87fbffd65",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 2,
    "title": "Bahçede birlikte öğrenmek — adult 2 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.\n\nSulama görevleri haftalık bir çizelgeye yazıldı. Çizelgede yalnız isimler değil, yapılacak işin kısa açıklaması da vardı. Bir kişi gelemediğinde başka biri görevi devralabiliyordu. Görevin nasıl yapılacağını bilmek, görevlinin adını bilmek kadar önemliydi.\n\nÇocuklar farklı sulama miktarlarını denemek istediğinde öğretmenleri önce küçük bir alan seçmelerini önerdi. Bütün bahçeyi aynı anda değiştirmek yerine, karşılaştırabilecekleri birkaç bitki ayırdılar. Ne değiştirdiklerini açıkça yazdılar ve diğer koşulları benzer tutmaya çalıştılar.\n\nDenemenin sonunda bazı bitkiler daha canlı görünüyordu. Ancak çocuklar bunun yalnız sulamadan kaynaklandığını hemen söylemedi. Toprağın ve güneşin de etkili olabileceğini konuştular. Sonucu bir gözlem olarak yazdılar; kesin bir kural gibi sunmadılar.\n\nBahçenin çevresine küçük bir yol yapıldı. Yol, bitkilere basmadan yaklaşmayı kolaylaştırıyordu. Önce çok dar çizilen geçit, sulama kabı taşıyanlara yetmedi. Çocuklar yürüyerek denedi ve yolu biraz genişletti. Çizim, kullanıldığında karşılaşılan duruma göre değişti.\n\nBazı ziyaretçiler bitkilerin yanında daha fazla bilgi istedi. Çocuklar uzun açıklamalar yerine kısa kartlar hazırladı. Kartlarda ad, ekim tarihi ve birkaç gözlem bulunuyordu. Ayrıntılı notları görmek isteyenler için defterin nerede bulunduğu ayrıca yazıldı.\n\nMevsim değiştikçe bahçenin görünümü de değişti. Çocuklar eski fotoğraflarla yeni fotoğrafları karşılaştırdı. Fotoğrafların benzer açıdan çekilmesi karşılaştırmayı kolaylaştırdı. Farklı açı ve ışıkla çekilen görüntülerde bir değişiklik varmış gibi görünmenin mümkün olduğunu fark ettiler.\n\nBir bitki kuruduğunda grup kimin hata yaptığını aramakla başlamadı. Önce bakım kayıtlarını ve toprağın durumunu inceledi. Her sorunun tek bir kişiden kaynaklanmayabileceğini konuştular. Sonraki ekimde neyi farklı yapabileceklerini birlikte yazdılar.\n\nBahçe büyüdükçe iş paylaşımı da değişti. Başlangıçta bütün işleri birlikte yapan grup, küçük sorumluluk alanları oluşturdu. Yine de herkes diğer alanların durumunu öğrenebiliyordu. Ortak defter, ayrı görevlerin birbirinden tamamen kopmasını önleyen bir bağlantı oldu.",
    "wordCount": 588,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000051",
    "exerciseId": "8686e7b0-4d3c-5e51-9aa2-8ee87fbffd65",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 2,
    "title": "Bir haritanın gelişimi — adult 2 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.\n\nYol çalışması bitince eski sokak yeniden açıldı. Deniz yeni haritasını hemen atmadı. Farklı yolların başka günlerde de işe yarayabileceğini düşündü. Ancak kapalı sokak işaretini kaldırdı ve haritanın hangi tarihte güncellendiğini yazdı.\n\nMahalledeki gençler, yürüyüş yollarını birlikte kaydetmek istedi. Herkes kendi bildiği köşeleri çizdi. Çizimler birleştirildiğinde bazı adların farklı yazıldığı görüldü. Aynı yerden söz edip etmediklerini birlikte kontrol ederek ortak bir ad listesi hazırladılar.\n\nGrup, haritaya bütün bilgileri aynı anda koymamaya karar verdi. Ana çizimde yönler ve önemli noktalar bulunacaktı. Ayrıntılar ayrı notlarda gösterilecekti. Çok fazla işaretin, aranan bilgiyi bulmayı zorlaştırabileceğini denemelerde görmüşlerdi.\n\nYürüyüş süresi için birkaç farklı günde kayıt tutuldu. Yağmurlu günlerin, kalabalık saatlerin ve yol çalışmalarının süreyi değiştirdiği görüldü. Sonuçlar tek bir sayı yerine küçük bir aralıkla anlatıldı. Bu aralık, beklenebilecek değişkenliği daha açık gösteriyordu.\n\nBazı sokaklar kısa görünse de yürümek için uygun değildi. Grup yalnız mesafeye bakarak öneri vermedi. Kaldırımın durumunu, geçiş noktalarını ve yolun açık olup olmadığını da gözledi. Bir ölçünün, bütün kararın yerine geçemeyeceğini konuştular.\n\nHaritadaki bilgiler zamanla eskiyebilirdi. Grup, yanlış bir işaret görenlerin bunu nasıl bildireceğini yazdı. Gelen önerilerin hemen doğru kabul edilmemesi gerektiğini de belirledi. Değişiklik yapılmadan önce yerin yeniden kontrol edilmesi planlandı.\n\nBir sokak adı değiştiğinde eski adla arayanlar yeni haritada zorlandı. Grup bir süre iki adı birlikte gösterdi. Böylece yeni bilgi, önceki bilgiyi bilenleri tamamen dışarıda bırakmadı. Güncelleme yapmak, eski kullanıcıların nasıl aradığını düşünmeyi de gerektiriyordu.\n\nHarita toplantısında bir anlaşmazlık çıktı. Bir kişi daha kısa rotayı, diğeri daha az basamak içeren rotayı öneriyordu. İkisinin farklı ihtiyaçları önemsediği anlaşıldı. Tek doğru rota seçmek yerine, seçeneklerin özelliklerini açıkça gösterdiler.",
    "wordCount": 583,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000052",
    "exerciseId": "1c5d756b-2bef-58e8-99c2-1e32d32e8153",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 3,
    "title": "Mahalle kütüphanesinden notlar — adult 3 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.\n\nAy sonunda öneri kartları yeniden gözden geçirildi. Bazı istekler hemen yapılmış, bazıları bütçe bekliyordu. Pano, yalnız tamamlanan işleri değil, henüz çözülemeyen ihtiyaçları da gösteriyordu. Görevli, bir isteğin beklemesinin unutulduğu anlamına gelmediğini açıkladı.\n\nKütüphaneye yeni kitaplar geldiğinde herkesin sevdiği türlerden eşit sayıda kitap bulunmadı. Gönüllüler tek bir kişinin tercihine göre karar vermedi. Ödünç alma kayıtlarına, önerilere ve raflarda eksik kalan konulara birlikte baktılar. Kararın gerekçesini panoda paylaştılar.\n\nBazı kitaplar çok sık isteniyordu. Bu kitaplar için sıraya girenlere tahmini dönüş tarihi söylendi. Sıra beklemek istemeyenlere benzer konudaki başka kitaplar gösterildi. Bir seçenek sunmak, aranan kitabın hemen bulunamamasını daha anlaşılır hâle getirdi.\n\nBir okuma buluşmasında aynı öykü hakkında farklı yorumlar yapıldı. Duru kahramanın cesur olduğunu düşünürken arkadaşı aceleci olduğunu söyledi. İkisi de görüşünü öyküdeki bir olayla açıkladı. Farklı yorumların, dikkatle dinlendiğinde konuşmayı zenginleştirebildiğini gördüler.\n\nBuluşmayı yöneten kişi, herkesi aynı görüşte birleştirmeye çalışmadı. Önce yorumların hangi ayrıntıya dayandığını sordu. Sonra öykünün başka bir bölümünü birlikte okudular. Yeni ayrıntılar bazı düşünceleri değiştirdi, bazılarını güçlendirdi. Konuşma, yalnız sonuçtan değil gerekçelerden oluştu.\n\nDuru artık kütüphaneye yalnız kitap almak için gelmiyordu. Bir soruyu nasıl soracağını, bilgiyi nerede arayacağını ve bulduğunu nasıl düzenleyeceğini de deniyordu. Kütüphane onun için bitmiş yanıtların bulunduğu bir yerden çok, merakın geliştiği bir çalışma alanıydı.",
    "wordCount": 658,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000053",
    "exerciseId": "1c5d756b-2bef-58e8-99c2-1e32d32e8153",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 3,
    "title": "Bahçede birlikte öğrenmek — adult 3 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.\n\nSulama görevleri haftalık bir çizelgeye yazıldı. Çizelgede yalnız isimler değil, yapılacak işin kısa açıklaması da vardı. Bir kişi gelemediğinde başka biri görevi devralabiliyordu. Görevin nasıl yapılacağını bilmek, görevlinin adını bilmek kadar önemliydi.\n\nÇocuklar farklı sulama miktarlarını denemek istediğinde öğretmenleri önce küçük bir alan seçmelerini önerdi. Bütün bahçeyi aynı anda değiştirmek yerine, karşılaştırabilecekleri birkaç bitki ayırdılar. Ne değiştirdiklerini açıkça yazdılar ve diğer koşulları benzer tutmaya çalıştılar.\n\nDenemenin sonunda bazı bitkiler daha canlı görünüyordu. Ancak çocuklar bunun yalnız sulamadan kaynaklandığını hemen söylemedi. Toprağın ve güneşin de etkili olabileceğini konuştular. Sonucu bir gözlem olarak yazdılar; kesin bir kural gibi sunmadılar.\n\nBahçenin çevresine küçük bir yol yapıldı. Yol, bitkilere basmadan yaklaşmayı kolaylaştırıyordu. Önce çok dar çizilen geçit, sulama kabı taşıyanlara yetmedi. Çocuklar yürüyerek denedi ve yolu biraz genişletti. Çizim, kullanıldığında karşılaşılan duruma göre değişti.\n\nBazı ziyaretçiler bitkilerin yanında daha fazla bilgi istedi. Çocuklar uzun açıklamalar yerine kısa kartlar hazırladı. Kartlarda ad, ekim tarihi ve birkaç gözlem bulunuyordu. Ayrıntılı notları görmek isteyenler için defterin nerede bulunduğu ayrıca yazıldı.\n\nMevsim değiştikçe bahçenin görünümü de değişti. Çocuklar eski fotoğraflarla yeni fotoğrafları karşılaştırdı. Fotoğrafların benzer açıdan çekilmesi karşılaştırmayı kolaylaştırdı. Farklı açı ve ışıkla çekilen görüntülerde bir değişiklik varmış gibi görünmenin mümkün olduğunu fark ettiler.\n\nBir bitki kuruduğunda grup kimin hata yaptığını aramakla başlamadı. Önce bakım kayıtlarını ve toprağın durumunu inceledi. Her sorunun tek bir kişiden kaynaklanmayabileceğini konuştular. Sonraki ekimde neyi farklı yapabileceklerini birlikte yazdılar.\n\nBahçe büyüdükçe iş paylaşımı da değişti. Başlangıçta bütün işleri birlikte yapan grup, küçük sorumluluk alanları oluşturdu. Yine de herkes diğer alanların durumunu öğrenebiliyordu. Ortak defter, ayrı görevlerin birbirinden tamamen kopmasını önleyen bir bağlantı oldu.\n\nYıl sonunda yeni gelen öğrenciler bahçeyi devralacaktı. Eren ve arkadaşları yalnız güzel fotoğrafları seçmedi. Karışıklık yaşanan günleri ve çözüm yollarını da anlattı. Böylece yeni grup, aynı güçlükleri yeniden yaşamak yerine önceki denemelerden yararlanabilecekti.\n\nHazırladıkları dosyada tamamlanan işler kadar açık sorular da vardı. Hangi bitkinin başka köşede daha iyi büyüyeceği henüz bilinmiyordu. Dosya her konuda kesin yanıt vermiyordu. Yeni öğrencilerin kendi gözlemlerini ekleyebileceği bir başlangıç sunuyordu.",
    "wordCount": 653,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000054",
    "exerciseId": "1c5d756b-2bef-58e8-99c2-1e32d32e8153",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 3,
    "title": "Bir haritanın gelişimi — adult 3 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.\n\nYol çalışması bitince eski sokak yeniden açıldı. Deniz yeni haritasını hemen atmadı. Farklı yolların başka günlerde de işe yarayabileceğini düşündü. Ancak kapalı sokak işaretini kaldırdı ve haritanın hangi tarihte güncellendiğini yazdı.\n\nMahalledeki gençler, yürüyüş yollarını birlikte kaydetmek istedi. Herkes kendi bildiği köşeleri çizdi. Çizimler birleştirildiğinde bazı adların farklı yazıldığı görüldü. Aynı yerden söz edip etmediklerini birlikte kontrol ederek ortak bir ad listesi hazırladılar.\n\nGrup, haritaya bütün bilgileri aynı anda koymamaya karar verdi. Ana çizimde yönler ve önemli noktalar bulunacaktı. Ayrıntılar ayrı notlarda gösterilecekti. Çok fazla işaretin, aranan bilgiyi bulmayı zorlaştırabileceğini denemelerde görmüşlerdi.\n\nYürüyüş süresi için birkaç farklı günde kayıt tutuldu. Yağmurlu günlerin, kalabalık saatlerin ve yol çalışmalarının süreyi değiştirdiği görüldü. Sonuçlar tek bir sayı yerine küçük bir aralıkla anlatıldı. Bu aralık, beklenebilecek değişkenliği daha açık gösteriyordu.\n\nBazı sokaklar kısa görünse de yürümek için uygun değildi. Grup yalnız mesafeye bakarak öneri vermedi. Kaldırımın durumunu, geçiş noktalarını ve yolun açık olup olmadığını da gözledi. Bir ölçünün, bütün kararın yerine geçemeyeceğini konuştular.\n\nHaritadaki bilgiler zamanla eskiyebilirdi. Grup, yanlış bir işaret görenlerin bunu nasıl bildireceğini yazdı. Gelen önerilerin hemen doğru kabul edilmemesi gerektiğini de belirledi. Değişiklik yapılmadan önce yerin yeniden kontrol edilmesi planlandı.\n\nBir sokak adı değiştiğinde eski adla arayanlar yeni haritada zorlandı. Grup bir süre iki adı birlikte gösterdi. Böylece yeni bilgi, önceki bilgiyi bilenleri tamamen dışarıda bırakmadı. Güncelleme yapmak, eski kullanıcıların nasıl aradığını düşünmeyi de gerektiriyordu.\n\nHarita toplantısında bir anlaşmazlık çıktı. Bir kişi daha kısa rotayı, diğeri daha az basamak içeren rotayı öneriyordu. İkisinin farklı ihtiyaçları önemsediği anlaşıldı. Tek doğru rota seçmek yerine, seçeneklerin özelliklerini açıkça gösterdiler.\n\nHazırlanan çizim bir rehberdi; her koşulda değişmez bir söz vermiyordu. Yola çıkmadan önce geçici kapanmaların kontrol edilmesi gerektiği yazıldı. Grup bu açıklamayı küçük ve görünmez bir not yapmak istemedi. Bilginin sınırları, yön işaretleri kadar anlaşılır olmalıydı.\n\nYeni gönüllüler çalışmaya katıldığında onlara yalnız son çizim verilmedi. İşaretlerin neden seçildiği ve hangi denemelerin yapıldığı da anlatıldı. Böylece bir simge değiştirileceğinde önceki kararın gerekçesi görülebiliyordu. Ortak çalışma, çizim kadar kayıtların paylaşılmasına da dayanıyordu.",
    "wordCount": 653,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000055",
    "exerciseId": "e4900566-c2ba-5254-bde7-215bb4ae5ca7",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 4,
    "title": "Mahalle kütüphanesinden notlar — adult 4 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.\n\nAy sonunda öneri kartları yeniden gözden geçirildi. Bazı istekler hemen yapılmış, bazıları bütçe bekliyordu. Pano, yalnız tamamlanan işleri değil, henüz çözülemeyen ihtiyaçları da gösteriyordu. Görevli, bir isteğin beklemesinin unutulduğu anlamına gelmediğini açıkladı.\n\nKütüphaneye yeni kitaplar geldiğinde herkesin sevdiği türlerden eşit sayıda kitap bulunmadı. Gönüllüler tek bir kişinin tercihine göre karar vermedi. Ödünç alma kayıtlarına, önerilere ve raflarda eksik kalan konulara birlikte baktılar. Kararın gerekçesini panoda paylaştılar.\n\nBazı kitaplar çok sık isteniyordu. Bu kitaplar için sıraya girenlere tahmini dönüş tarihi söylendi. Sıra beklemek istemeyenlere benzer konudaki başka kitaplar gösterildi. Bir seçenek sunmak, aranan kitabın hemen bulunamamasını daha anlaşılır hâle getirdi.\n\nBir okuma buluşmasında aynı öykü hakkında farklı yorumlar yapıldı. Duru kahramanın cesur olduğunu düşünürken arkadaşı aceleci olduğunu söyledi. İkisi de görüşünü öyküdeki bir olayla açıkladı. Farklı yorumların, dikkatle dinlendiğinde konuşmayı zenginleştirebildiğini gördüler.\n\nBuluşmayı yöneten kişi, herkesi aynı görüşte birleştirmeye çalışmadı. Önce yorumların hangi ayrıntıya dayandığını sordu. Sonra öykünün başka bir bölümünü birlikte okudular. Yeni ayrıntılar bazı düşünceleri değiştirdi, bazılarını güçlendirdi. Konuşma, yalnız sonuçtan değil gerekçelerden oluştu.\n\nDuru artık kütüphaneye yalnız kitap almak için gelmiyordu. Bir soruyu nasıl soracağını, bilgiyi nerede arayacağını ve bulduğunu nasıl düzenleyeceğini de deniyordu. Kütüphane onun için bitmiş yanıtların bulunduğu bir yerden çok, merakın geliştiği bir çalışma alanıydı.\n\nYeni bir ziyaretçi kapıdaki haritaya bakıp öykü rafını kolayca bulduğunda Duru sevindi. Küçük bir önerinin işe yaradığı görülmüştü. Yine de haritanın sonsuza kadar değişmeden kalması gerekmiyordu. Raflar ve ihtiyaçlar değiştikçe çizim de yeniden gözden geçirilebilirdi.",
    "wordCount": 693,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000056",
    "exerciseId": "e4900566-c2ba-5254-bde7-215bb4ae5ca7",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 4,
    "title": "Bahçede birlikte öğrenmek — adult 4 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.\n\nSulama görevleri haftalık bir çizelgeye yazıldı. Çizelgede yalnız isimler değil, yapılacak işin kısa açıklaması da vardı. Bir kişi gelemediğinde başka biri görevi devralabiliyordu. Görevin nasıl yapılacağını bilmek, görevlinin adını bilmek kadar önemliydi.\n\nÇocuklar farklı sulama miktarlarını denemek istediğinde öğretmenleri önce küçük bir alan seçmelerini önerdi. Bütün bahçeyi aynı anda değiştirmek yerine, karşılaştırabilecekleri birkaç bitki ayırdılar. Ne değiştirdiklerini açıkça yazdılar ve diğer koşulları benzer tutmaya çalıştılar.\n\nDenemenin sonunda bazı bitkiler daha canlı görünüyordu. Ancak çocuklar bunun yalnız sulamadan kaynaklandığını hemen söylemedi. Toprağın ve güneşin de etkili olabileceğini konuştular. Sonucu bir gözlem olarak yazdılar; kesin bir kural gibi sunmadılar.\n\nBahçenin çevresine küçük bir yol yapıldı. Yol, bitkilere basmadan yaklaşmayı kolaylaştırıyordu. Önce çok dar çizilen geçit, sulama kabı taşıyanlara yetmedi. Çocuklar yürüyerek denedi ve yolu biraz genişletti. Çizim, kullanıldığında karşılaşılan duruma göre değişti.\n\nBazı ziyaretçiler bitkilerin yanında daha fazla bilgi istedi. Çocuklar uzun açıklamalar yerine kısa kartlar hazırladı. Kartlarda ad, ekim tarihi ve birkaç gözlem bulunuyordu. Ayrıntılı notları görmek isteyenler için defterin nerede bulunduğu ayrıca yazıldı.\n\nMevsim değiştikçe bahçenin görünümü de değişti. Çocuklar eski fotoğraflarla yeni fotoğrafları karşılaştırdı. Fotoğrafların benzer açıdan çekilmesi karşılaştırmayı kolaylaştırdı. Farklı açı ve ışıkla çekilen görüntülerde bir değişiklik varmış gibi görünmenin mümkün olduğunu fark ettiler.\n\nBir bitki kuruduğunda grup kimin hata yaptığını aramakla başlamadı. Önce bakım kayıtlarını ve toprağın durumunu inceledi. Her sorunun tek bir kişiden kaynaklanmayabileceğini konuştular. Sonraki ekimde neyi farklı yapabileceklerini birlikte yazdılar.\n\nBahçe büyüdükçe iş paylaşımı da değişti. Başlangıçta bütün işleri birlikte yapan grup, küçük sorumluluk alanları oluşturdu. Yine de herkes diğer alanların durumunu öğrenebiliyordu. Ortak defter, ayrı görevlerin birbirinden tamamen kopmasını önleyen bir bağlantı oldu.\n\nYıl sonunda yeni gelen öğrenciler bahçeyi devralacaktı. Eren ve arkadaşları yalnız güzel fotoğrafları seçmedi. Karışıklık yaşanan günleri ve çözüm yollarını da anlattı. Böylece yeni grup, aynı güçlükleri yeniden yaşamak yerine önceki denemelerden yararlanabilecekti.\n\nHazırladıkları dosyada tamamlanan işler kadar açık sorular da vardı. Hangi bitkinin başka köşede daha iyi büyüyeceği henüz bilinmiyordu. Dosya her konuda kesin yanıt vermiyordu. Yeni öğrencilerin kendi gözlemlerini ekleyebileceği bir başlangıç sunuyordu.\n\nEren son kez bahçedeki işaretlere baktığında küçük köşenin yalnız bitkilerden oluşmadığını düşündü. Orada ortak kararlar, ölçümler ve dikkatli notlar da birikmişti. Bahçeyi devralanların aynı yolu izlemesi şart değildi. Önemli olan yaptıklarını gözlemleyip birbirlerine anlatabilmeleriydi.",
    "wordCount": 687,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000057",
    "exerciseId": "e4900566-c2ba-5254-bde7-215bb4ae5ca7",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 4,
    "title": "Bir haritanın gelişimi — adult 4 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.\n\nYol çalışması bitince eski sokak yeniden açıldı. Deniz yeni haritasını hemen atmadı. Farklı yolların başka günlerde de işe yarayabileceğini düşündü. Ancak kapalı sokak işaretini kaldırdı ve haritanın hangi tarihte güncellendiğini yazdı.\n\nMahalledeki gençler, yürüyüş yollarını birlikte kaydetmek istedi. Herkes kendi bildiği köşeleri çizdi. Çizimler birleştirildiğinde bazı adların farklı yazıldığı görüldü. Aynı yerden söz edip etmediklerini birlikte kontrol ederek ortak bir ad listesi hazırladılar.\n\nGrup, haritaya bütün bilgileri aynı anda koymamaya karar verdi. Ana çizimde yönler ve önemli noktalar bulunacaktı. Ayrıntılar ayrı notlarda gösterilecekti. Çok fazla işaretin, aranan bilgiyi bulmayı zorlaştırabileceğini denemelerde görmüşlerdi.\n\nYürüyüş süresi için birkaç farklı günde kayıt tutuldu. Yağmurlu günlerin, kalabalık saatlerin ve yol çalışmalarının süreyi değiştirdiği görüldü. Sonuçlar tek bir sayı yerine küçük bir aralıkla anlatıldı. Bu aralık, beklenebilecek değişkenliği daha açık gösteriyordu.\n\nBazı sokaklar kısa görünse de yürümek için uygun değildi. Grup yalnız mesafeye bakarak öneri vermedi. Kaldırımın durumunu, geçiş noktalarını ve yolun açık olup olmadığını da gözledi. Bir ölçünün, bütün kararın yerine geçemeyeceğini konuştular.\n\nHaritadaki bilgiler zamanla eskiyebilirdi. Grup, yanlış bir işaret görenlerin bunu nasıl bildireceğini yazdı. Gelen önerilerin hemen doğru kabul edilmemesi gerektiğini de belirledi. Değişiklik yapılmadan önce yerin yeniden kontrol edilmesi planlandı.\n\nBir sokak adı değiştiğinde eski adla arayanlar yeni haritada zorlandı. Grup bir süre iki adı birlikte gösterdi. Böylece yeni bilgi, önceki bilgiyi bilenleri tamamen dışarıda bırakmadı. Güncelleme yapmak, eski kullanıcıların nasıl aradığını düşünmeyi de gerektiriyordu.\n\nHarita toplantısında bir anlaşmazlık çıktı. Bir kişi daha kısa rotayı, diğeri daha az basamak içeren rotayı öneriyordu. İkisinin farklı ihtiyaçları önemsediği anlaşıldı. Tek doğru rota seçmek yerine, seçeneklerin özelliklerini açıkça gösterdiler.\n\nHazırlanan çizim bir rehberdi; her koşulda değişmez bir söz vermiyordu. Yola çıkmadan önce geçici kapanmaların kontrol edilmesi gerektiği yazıldı. Grup bu açıklamayı küçük ve görünmez bir not yapmak istemedi. Bilginin sınırları, yön işaretleri kadar anlaşılır olmalıydı.\n\nYeni gönüllüler çalışmaya katıldığında onlara yalnız son çizim verilmedi. İşaretlerin neden seçildiği ve hangi denemelerin yapıldığı da anlatıldı. Böylece bir simge değiştirileceğinde önceki kararın gerekçesi görülebiliyordu. Ortak çalışma, çizim kadar kayıtların paylaşılmasına da dayanıyordu.\n\nDeniz, ilk haritasını yeni çizimin yanına koydu. İkisi arasında büyük fark vardı ama ilk çizim gereksiz değildi. Küçük bir ihtiyacı görünür kılmış ve konuşmayı başlatmıştı. Yeni harita da son nokta değil, gözlemle geliştirilebilecek bir ortak başlangıçtı.",
    "wordCount": 689,
    "source": "texts/map.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000058",
    "exerciseId": "d7b10000-0000-5000-8000-000000000003",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 5,
    "title": "Mahalle kütüphanesinden notlar — adult 5 seçkisi",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.\n\nAy sonunda öneri kartları yeniden gözden geçirildi. Bazı istekler hemen yapılmış, bazıları bütçe bekliyordu. Pano, yalnız tamamlanan işleri değil, henüz çözülemeyen ihtiyaçları da gösteriyordu. Görevli, bir isteğin beklemesinin unutulduğu anlamına gelmediğini açıkladı.\n\nKütüphaneye yeni kitaplar geldiğinde herkesin sevdiği türlerden eşit sayıda kitap bulunmadı. Gönüllüler tek bir kişinin tercihine göre karar vermedi. Ödünç alma kayıtlarına, önerilere ve raflarda eksik kalan konulara birlikte baktılar. Kararın gerekçesini panoda paylaştılar.\n\nBazı kitaplar çok sık isteniyordu. Bu kitaplar için sıraya girenlere tahmini dönüş tarihi söylendi. Sıra beklemek istemeyenlere benzer konudaki başka kitaplar gösterildi. Bir seçenek sunmak, aranan kitabın hemen bulunamamasını daha anlaşılır hâle getirdi.\n\nBir okuma buluşmasında aynı öykü hakkında farklı yorumlar yapıldı. Duru kahramanın cesur olduğunu düşünürken arkadaşı aceleci olduğunu söyledi. İkisi de görüşünü öyküdeki bir olayla açıkladı. Farklı yorumların, dikkatle dinlendiğinde konuşmayı zenginleştirebildiğini gördüler.\n\nBuluşmayı yöneten kişi, herkesi aynı görüşte birleştirmeye çalışmadı. Önce yorumların hangi ayrıntıya dayandığını sordu. Sonra öykünün başka bir bölümünü birlikte okudular. Yeni ayrıntılar bazı düşünceleri değiştirdi, bazılarını güçlendirdi. Konuşma, yalnız sonuçtan değil gerekçelerden oluştu.\n\nDuru artık kütüphaneye yalnız kitap almak için gelmiyordu. Bir soruyu nasıl soracağını, bilgiyi nerede arayacağını ve bulduğunu nasıl düzenleyeceğini de deniyordu. Kütüphane onun için bitmiş yanıtların bulunduğu bir yerden çok, merakın geliştiği bir çalışma alanıydı.\n\nYeni bir ziyaretçi kapıdaki haritaya bakıp öykü rafını kolayca bulduğunda Duru sevindi. Küçük bir önerinin işe yaradığı görülmüştü. Yine de haritanın sonsuza kadar değişmeden kalması gerekmiyordu. Raflar ve ihtiyaçlar değiştikçe çizim de yeniden gözden geçirilebilirdi.",
    "wordCount": 693,
    "source": "texts/library.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000059",
    "exerciseId": "d7b10000-0000-5000-8000-000000000003",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 5,
    "title": "Bahçede birlikte öğrenmek — adult 5 seçkisi",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.\n\nSulama görevleri haftalık bir çizelgeye yazıldı. Çizelgede yalnız isimler değil, yapılacak işin kısa açıklaması da vardı. Bir kişi gelemediğinde başka biri görevi devralabiliyordu. Görevin nasıl yapılacağını bilmek, görevlinin adını bilmek kadar önemliydi.\n\nÇocuklar farklı sulama miktarlarını denemek istediğinde öğretmenleri önce küçük bir alan seçmelerini önerdi. Bütün bahçeyi aynı anda değiştirmek yerine, karşılaştırabilecekleri birkaç bitki ayırdılar. Ne değiştirdiklerini açıkça yazdılar ve diğer koşulları benzer tutmaya çalıştılar.\n\nDenemenin sonunda bazı bitkiler daha canlı görünüyordu. Ancak çocuklar bunun yalnız sulamadan kaynaklandığını hemen söylemedi. Toprağın ve güneşin de etkili olabileceğini konuştular. Sonucu bir gözlem olarak yazdılar; kesin bir kural gibi sunmadılar.\n\nBahçenin çevresine küçük bir yol yapıldı. Yol, bitkilere basmadan yaklaşmayı kolaylaştırıyordu. Önce çok dar çizilen geçit, sulama kabı taşıyanlara yetmedi. Çocuklar yürüyerek denedi ve yolu biraz genişletti. Çizim, kullanıldığında karşılaşılan duruma göre değişti.\n\nBazı ziyaretçiler bitkilerin yanında daha fazla bilgi istedi. Çocuklar uzun açıklamalar yerine kısa kartlar hazırladı. Kartlarda ad, ekim tarihi ve birkaç gözlem bulunuyordu. Ayrıntılı notları görmek isteyenler için defterin nerede bulunduğu ayrıca yazıldı.\n\nMevsim değiştikçe bahçenin görünümü de değişti. Çocuklar eski fotoğraflarla yeni fotoğrafları karşılaştırdı. Fotoğrafların benzer açıdan çekilmesi karşılaştırmayı kolaylaştırdı. Farklı açı ve ışıkla çekilen görüntülerde bir değişiklik varmış gibi görünmenin mümkün olduğunu fark ettiler.\n\nBir bitki kuruduğunda grup kimin hata yaptığını aramakla başlamadı. Önce bakım kayıtlarını ve toprağın durumunu inceledi. Her sorunun tek bir kişiden kaynaklanmayabileceğini konuştular. Sonraki ekimde neyi farklı yapabileceklerini birlikte yazdılar.\n\nBahçe büyüdükçe iş paylaşımı da değişti. Başlangıçta bütün işleri birlikte yapan grup, küçük sorumluluk alanları oluşturdu. Yine de herkes diğer alanların durumunu öğrenebiliyordu. Ortak defter, ayrı görevlerin birbirinden tamamen kopmasını önleyen bir bağlantı oldu.\n\nYıl sonunda yeni gelen öğrenciler bahçeyi devralacaktı. Eren ve arkadaşları yalnız güzel fotoğrafları seçmedi. Karışıklık yaşanan günleri ve çözüm yollarını da anlattı. Böylece yeni grup, aynı güçlükleri yeniden yaşamak yerine önceki denemelerden yararlanabilecekti.\n\nHazırladıkları dosyada tamamlanan işler kadar açık sorular da vardı. Hangi bitkinin başka köşede daha iyi büyüyeceği henüz bilinmiyordu. Dosya her konuda kesin yanıt vermiyordu. Yeni öğrencilerin kendi gözlemlerini ekleyebileceği bir başlangıç sunuyordu.\n\nEren son kez bahçedeki işaretlere baktığında küçük köşenin yalnız bitkilerden oluşmadığını düşündü. Orada ortak kararlar, ölçümler ve dikkatli notlar da birikmişti. Bahçeyi devralanların aynı yolu izlemesi şart değildi. Önemli olan yaptıklarını gözlemleyip birbirlerine anlatabilmeleriydi.",
    "wordCount": 687,
    "source": "texts/garden.txt"
  },
  {
    "id": "d7b20000-0000-5000-8000-000000000060",
    "exerciseId": "d7b10000-0000-5000-8000-000000000003",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "level": 5,
    "title": "Bir haritanın gelişimi — adult 5 seçkisi",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.\n\nYol çalışması bitince eski sokak yeniden açıldı. Deniz yeni haritasını hemen atmadı. Farklı yolların başka günlerde de işe yarayabileceğini düşündü. Ancak kapalı sokak işaretini kaldırdı ve haritanın hangi tarihte güncellendiğini yazdı.\n\nMahalledeki gençler, yürüyüş yollarını birlikte kaydetmek istedi. Herkes kendi bildiği köşeleri çizdi. Çizimler birleştirildiğinde bazı adların farklı yazıldığı görüldü. Aynı yerden söz edip etmediklerini birlikte kontrol ederek ortak bir ad listesi hazırladılar.\n\nGrup, haritaya bütün bilgileri aynı anda koymamaya karar verdi. Ana çizimde yönler ve önemli noktalar bulunacaktı. Ayrıntılar ayrı notlarda gösterilecekti. Çok fazla işaretin, aranan bilgiyi bulmayı zorlaştırabileceğini denemelerde görmüşlerdi.\n\nYürüyüş süresi için birkaç farklı günde kayıt tutuldu. Yağmurlu günlerin, kalabalık saatlerin ve yol çalışmalarının süreyi değiştirdiği görüldü. Sonuçlar tek bir sayı yerine küçük bir aralıkla anlatıldı. Bu aralık, beklenebilecek değişkenliği daha açık gösteriyordu.\n\nBazı sokaklar kısa görünse de yürümek için uygun değildi. Grup yalnız mesafeye bakarak öneri vermedi. Kaldırımın durumunu, geçiş noktalarını ve yolun açık olup olmadığını da gözledi. Bir ölçünün, bütün kararın yerine geçemeyeceğini konuştular.\n\nHaritadaki bilgiler zamanla eskiyebilirdi. Grup, yanlış bir işaret görenlerin bunu nasıl bildireceğini yazdı. Gelen önerilerin hemen doğru kabul edilmemesi gerektiğini de belirledi. Değişiklik yapılmadan önce yerin yeniden kontrol edilmesi planlandı.\n\nBir sokak adı değiştiğinde eski adla arayanlar yeni haritada zorlandı. Grup bir süre iki adı birlikte gösterdi. Böylece yeni bilgi, önceki bilgiyi bilenleri tamamen dışarıda bırakmadı. Güncelleme yapmak, eski kullanıcıların nasıl aradığını düşünmeyi de gerektiriyordu.\n\nHarita toplantısında bir anlaşmazlık çıktı. Bir kişi daha kısa rotayı, diğeri daha az basamak içeren rotayı öneriyordu. İkisinin farklı ihtiyaçları önemsediği anlaşıldı. Tek doğru rota seçmek yerine, seçeneklerin özelliklerini açıkça gösterdiler.\n\nHazırlanan çizim bir rehberdi; her koşulda değişmez bir söz vermiyordu. Yola çıkmadan önce geçici kapanmaların kontrol edilmesi gerektiği yazıldı. Grup bu açıklamayı küçük ve görünmez bir not yapmak istemedi. Bilginin sınırları, yön işaretleri kadar anlaşılır olmalıydı.\n\nYeni gönüllüler çalışmaya katıldığında onlara yalnız son çizim verilmedi. İşaretlerin neden seçildiği ve hangi denemelerin yapıldığı da anlatıldı. Böylece bir simge değiştirileceğinde önceki kararın gerekçesi görülebiliyordu. Ortak çalışma, çizim kadar kayıtların paylaşılmasına da dayanıyordu.\n\nDeniz, ilk haritasını yeni çizimin yanına koydu. İkisi arasında büyük fark vardı ama ilk çizim gereksiz değildi. Küçük bir ihtiyacı görünür kılmış ve konuşmayı başlatmıştı. Yeni harita da son nokta değil, gözlemle geliştirilebilecek bir ortak başlangıçtı.",
    "wordCount": 689,
    "source": "texts/map.txt"
  }
]$content$::jsonb) item;
CREATE TEMP TABLE grouping_programs ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id,
  item->'previousPattern' old_pattern, item->'pattern' pattern
FROM jsonb_array_elements($programs$[
  {
    "id": "e2c01001-8d9a-4e6c-a428-000000000005",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "previousPattern": {
      "placementLevel": 5,
      "week1": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week2": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week3": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week4": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ]
      }
    },
    "pattern": {
      "placementLevel": 5,
      "week1": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week2": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week3": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week4": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "FreeReading",
            "count": 1,
            "difficulty": 5
          }
        ]
      }
    }
  },
  {
    "id": "e2c01002-8d9a-4e6c-a428-000000000005",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "previousPattern": {
      "placementLevel": 5,
      "week1": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week2": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week3": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "RSVP",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "TextFading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ErrorAnalysis",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week4": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Skimming",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ErrorAnalysis",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ExamSimulation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ]
      }
    },
    "pattern": {
      "placementLevel": 5,
      "week1": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week2": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week3": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "RSVP",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "TextFading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ErrorAnalysis",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week4": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Skimming",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ErrorAnalysis",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ExamSimulation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ]
      }
    }
  },
  {
    "id": "e2c01004-8d9a-4e6c-a428-000000000005",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "previousPattern": {
      "placementLevel": 5,
      "week1": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week2": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week3": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "RSVP",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "TextFading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ErrorAnalysis",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week4": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Skimming",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ErrorAnalysis",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ExamSimulation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ]
      }
    },
    "pattern": {
      "placementLevel": 5,
      "week1": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week2": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week3": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "RSVP",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "TextFading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ErrorAnalysis",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week4": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Skimming",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ErrorAnalysis",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ExamSimulation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ]
      }
    }
  },
  {
    "id": "e2c01003-8d9a-4e6c-a428-000000000005",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "previousPattern": {
      "placementLevel": 5,
      "week1": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week2": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week3": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "RSVP",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "TextFading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ErrorAnalysis",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week4": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Skimming",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ErrorAnalysis",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 4
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ExamSimulation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ]
      }
    },
    "pattern": {
      "placementLevel": 5,
      "week1": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week2": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week3": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "RSVP",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "TextFading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ErrorAnalysis",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ]
      },
      "week4": {
        "day1": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Skimming",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day2": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day3": [
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Fixation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day4": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "EyeTracking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Vocabulary",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day5": [
          {
            "type": "VisualExpansion",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Scanning",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day6": [
          {
            "type": "SchulteTable",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Saccade",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ErrorAnalysis",
            "count": 1,
            "difficulty": 5
          }
        ],
        "day7": [
          {
            "type": "Focus",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Chunking",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "SpeedReading",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Tachistoscope",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "ExamSimulation",
            "count": 1,
            "difficulty": 5
          },
          {
            "type": "Comprehension",
            "count": 1,
            "difficulty": 5
          }
        ]
      }
    }
  }
]$programs$::jsonb) item;
DO $$ BEGIN
  IF (SELECT count(*) FROM grouping_settings)<>20 OR (SELECT count(DISTINCT id) FROM grouping_settings)<>20
    OR (SELECT count(*) FROM grouping_content)<>60 OR (SELECT count(DISTINCT id) FROM grouping_content)<>60
    OR (SELECT count(*) FROM grouping_programs)<>4 THEN RAISE EXCEPTION 'Grouping payload incomplete'; END IF;
  IF (SELECT count(*) FROM speed_reading.exercise_types WHERE name='Chunking' AND is_active AND NOT is_deleted)<>1
    THEN RAISE EXCEPTION 'Active Chunking type missing or ambiguous'; END IF;
  IF EXISTS(SELECT 1 FROM grouping_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    LEFT JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE (s.level<5 AND e.id IS NULL) OR (e.id IS NOT NULL AND
      (t.name IS DISTINCT FROM 'Chunking' OR e.target_age_group_id IS DISTINCT FROM s.age_id
       OR e.difficulty_level<>s.level OR NOT e.is_active OR e.is_deleted
       OR (e.configuration_json IS DISTINCT FROM s.config AND e.configuration_json IS DISTINCT FROM s.old_config)
       OR (e.title IS DISTINCT FROM s.title AND e.title IS DISTINCT FROM s.old_title))))
    THEN RAISE EXCEPTION 'Grouping catalog changed; review before applying'; END IF;
  IF EXISTS(SELECT 1 FROM speed_reading.exercises e JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
    WHERE t.name='Chunking' AND e.is_active AND NOT e.is_deleted
      AND e.target_age_group_id IN (SELECT age_id FROM grouping_settings)
      AND NOT EXISTS(SELECT 1 FROM grouping_settings s WHERE s.id=e.id))
    THEN RAISE EXCEPTION 'Unexpected age-specific Chunking entry'; END IF;
  IF EXISTS(SELECT 1 FROM grouping_content c LEFT JOIN grouping_settings s ON s.id=c.exercise_id
    WHERE s.id IS NULL OR s.age_id<>c.age_id OR s.level<>c.level
      OR cardinality(regexp_split_to_array(btrim(c.content), E'\\s+'))<>c.word_count
      OR c.word_count<(s.config->'engineConfig'->'content'->>'minWordCount')::int
      OR c.word_count>(s.config->'engineConfig'->'content'->>'maxWordCount')::int)
    THEN RAISE EXCEPTION 'Grouping content band mismatch'; END IF;
  IF EXISTS(SELECT 1 FROM grouping_settings s LEFT JOIN grouping_content c ON c.exercise_id=s.id
    GROUP BY s.id HAVING count(c.id)<>3 OR count(DISTINCT c.content)<>3)
    THEN RAISE EXCEPTION 'Grouping needs three distinct texts per age/level'; END IF;
  IF EXISTS(SELECT 1 FROM grouping_content c JOIN speed_reading.reading_texts t ON t.id=c.id
    WHERE t.title<>c.title OR t.content<>c.content OR t.word_count<>c.word_count
      OR t.exercise_id IS DISTINCT FROM c.exercise_id OR t.target_age_group_id IS DISTINCT FROM c.age_id
      OR t.difficulty_level<>c.level OR NOT t.is_active OR t.is_deleted)
    THEN RAISE EXCEPTION 'Grouping text collision or later edit'; END IF;
  IF EXISTS(SELECT 1 FROM grouping_programs g LEFT JOIN speed_reading.program_templates p ON p.id=g.id
    WHERE p.id IS NULL OR NOT p."IsActive" OR p."IsDeleted" OR p."IsAssessment"
      OR p."InitialDifficultyLevel"<>5 OR p."TotalDays"<>28 OR p."TotalWeeks"<>4
      OR p."TargetAgeGroupConfigurationId" IS DISTINCT FROM g.age_id
      OR (p."WeeklyPatternJson"::jsonb IS DISTINCT FROM g.old_pattern AND p."WeeklyPatternJson"::jsonb IS DISTINCT FROM g.pattern))
    THEN RAISE EXCEPTION 'Grouping program baseline changed'; END IF;
END $$;
INSERT INTO speed_reading.exercises(id,exercise_type_id,title,description,type_code,difficulty_level,
  configuration_json,target_age_group_id,creator_id,is_active,created_at,created_by,version,is_deleted)
SELECT s.id,t.id,s.title,'Sabit tempoyla kelime gruplarını izleme alıştırması; anlama ölçümü değildir.',
  'reading',s.level,s.config,s.age_id,e.creator_id,true,now(),'grouping-v1',1,false
FROM grouping_settings s CROSS JOIN speed_reading.exercise_types t
JOIN speed_reading.exercises e ON e.exercise_type_id=t.id AND e.difficulty_level=1
WHERE t.name='Chunking' AND e.target_age_group_id=s.age_id AND s.level=5
ON CONFLICT(id) DO NOTHING;
INSERT INTO speed_reading.reading_texts(id,title,content,word_count,category,difficulty_level,target_age_group_id,
  language,is_active,tags,recommended_min_level,recommended_max_level,average_comprehension_score,times_read,
  average_reading_time_seconds,exercise_id,created_at,created_by,version,is_deleted)
SELECT id,title,content,word_count,'grouping',level,age_id,'tr',true,'grouping-v1;original-selection',level,level,
  0,0,0,exercise_id,now(),'grouping-v1',1,false FROM grouping_content ON CONFLICT(id) DO NOTHING;
UPDATE speed_reading.exercises e SET configuration_json=s.config,title=s.title,updated_at=now(),
  updated_by='grouping-v1',version=e.version+1 FROM grouping_settings s
WHERE e.id=s.id AND (e.configuration_json IS DISTINCT FROM s.config OR e.title IS DISTINCT FROM s.title);
UPDATE speed_reading.exercise_types SET display_name='Kelime Gruplarıyla Okuma',updated_at=now(),
  updated_by='grouping-v1',version=version+1 WHERE name='Chunking' AND display_name<>'Kelime Gruplarıyla Okuma';
UPDATE speed_reading.program_templates p SET "WeeklyPatternJson"=g.pattern,
  updated_at=now(),updated_by='grouping-v1',version=p.version+1 FROM grouping_programs g
WHERE p.id=g.id AND p."WeeklyPatternJson"::jsonb IS DISTINCT FROM g.pattern;
COMMIT;
