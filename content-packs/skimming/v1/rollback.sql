-- Restore settings and archive ONLY this package's texts. Never delete results or questions.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='120s';
SELECT pg_advisory_xact_lock(hashtext('skimming-v1'));
LOCK TABLE speed_reading.exercises, speed_reading.reading_texts, speed_reading.reading_questions IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE skimming_settings ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'ageGroupId')::uuid age_id, (item->>'level')::int level,
  item->'previousConfiguration' old_config, item->'configuration' config
FROM jsonb_array_elements($settings$[
  {
    "id": "2f5ccf40-b4c8-5f5f-a472-88bd03480aca",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "age": "child",
    "level": 3,
    "previousConfiguration": {
      "engineType": "skimming",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "skimming",
        "rules": {
          "timeLimit": 120
        },
        "timing": {
          "minReadingTimeMs": 3000
        },
        "readingPurpose": "evaluation"
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "skimming",
      "difficultyLevel": 3,
      "engineConfig": {
        "mode": "skimming",
        "readingPurpose": "evaluation",
        "content": {
          "source": "reading_text",
          "minWordCount": 200,
          "maxWordCount": 280,
          "questionCount": 4
        },
        "timing": {
          "minReadingTimeMs": 10000,
          "maxReadingTimeMs": 75000
        },
        "visuals": {
          "fontSize": "24px"
        }
      }
    }
  },
  {
    "id": "d1a4fac5-42db-587b-a429-015f922fa8d2",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "age": "child",
    "level": 4,
    "previousConfiguration": {
      "engineType": "skimming",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "skimming",
        "rules": {
          "timeLimit": 150
        },
        "timing": {
          "minReadingTimeMs": 3000
        },
        "readingPurpose": "evaluation"
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "skimming",
      "difficultyLevel": 4,
      "engineConfig": {
        "mode": "skimming",
        "readingPurpose": "evaluation",
        "content": {
          "source": "reading_text",
          "minWordCount": 280,
          "maxWordCount": 360,
          "questionCount": 4
        },
        "timing": {
          "minReadingTimeMs": 12000,
          "maxReadingTimeMs": 90000
        },
        "visuals": {
          "fontSize": "24px"
        }
      }
    }
  },
  {
    "id": "00a29320-c7cf-5ac0-99e4-62226e911a2b",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "age": "child",
    "level": 5,
    "previousConfiguration": {
      "engineType": "skimming",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "skimming",
        "rules": {
          "timeLimit": 180
        },
        "timing": {
          "minReadingTimeMs": 3000
        },
        "readingPurpose": "evaluation"
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "skimming",
      "difficultyLevel": 5,
      "engineConfig": {
        "mode": "skimming",
        "readingPurpose": "evaluation",
        "content": {
          "source": "reading_text",
          "minWordCount": 360,
          "maxWordCount": 450,
          "questionCount": 4
        },
        "timing": {
          "minReadingTimeMs": 15000,
          "maxReadingTimeMs": 105000
        },
        "visuals": {
          "fontSize": "24px"
        }
      }
    }
  },
  {
    "id": "9242e9ad-d5d5-5b01-8a4c-86a6f68923e8",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "age": "teen",
    "level": 3,
    "previousConfiguration": {
      "engineType": "skimming",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "skimming",
        "rules": {
          "timeLimit": 150
        },
        "timing": {
          "minReadingTimeMs": 3000
        },
        "readingPurpose": "evaluation"
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "skimming",
      "difficultyLevel": 3,
      "engineConfig": {
        "mode": "skimming",
        "readingPurpose": "evaluation",
        "content": {
          "source": "reading_text",
          "minWordCount": 300,
          "maxWordCount": 400,
          "questionCount": 4
        },
        "timing": {
          "minReadingTimeMs": 12000,
          "maxReadingTimeMs": 90000
        },
        "visuals": {
          "fontSize": "22px"
        }
      }
    }
  },
  {
    "id": "f72341f8-d778-5065-a7a4-687a1567b547",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "age": "teen",
    "level": 4,
    "previousConfiguration": {
      "engineType": "skimming",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "skimming",
        "rules": {
          "timeLimit": 180
        },
        "timing": {
          "minReadingTimeMs": 3000
        },
        "readingPurpose": "evaluation"
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "skimming",
      "difficultyLevel": 4,
      "engineConfig": {
        "mode": "skimming",
        "readingPurpose": "evaluation",
        "content": {
          "source": "reading_text",
          "minWordCount": 400,
          "maxWordCount": 520,
          "questionCount": 4
        },
        "timing": {
          "minReadingTimeMs": 15000,
          "maxReadingTimeMs": 105000
        },
        "visuals": {
          "fontSize": "22px"
        }
      }
    }
  },
  {
    "id": "af019d2e-728c-585a-bb33-85615c0eaccf",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "age": "teen",
    "level": 5,
    "previousConfiguration": {
      "engineType": "skimming",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "skimming",
        "rules": {
          "timeLimit": 210
        },
        "timing": {
          "minReadingTimeMs": 3000
        },
        "readingPurpose": "evaluation"
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "skimming",
      "difficultyLevel": 5,
      "engineConfig": {
        "mode": "skimming",
        "readingPurpose": "evaluation",
        "content": {
          "source": "reading_text",
          "minWordCount": 520,
          "maxWordCount": 650,
          "questionCount": 4
        },
        "timing": {
          "minReadingTimeMs": 18000,
          "maxReadingTimeMs": 120000
        },
        "visuals": {
          "fontSize": "22px"
        }
      }
    }
  },
  {
    "id": "5b407543-34d2-55c2-9be3-fb11ca93c2ca",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "age": "young-adult",
    "level": 3,
    "previousConfiguration": {
      "engineType": "skimming",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "skimming",
        "rules": {
          "timeLimit": 180
        },
        "timing": {
          "minReadingTimeMs": 3000
        },
        "readingPurpose": "evaluation"
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "skimming",
      "difficultyLevel": 3,
      "engineConfig": {
        "mode": "skimming",
        "readingPurpose": "evaluation",
        "content": {
          "source": "reading_text",
          "minWordCount": 450,
          "maxWordCount": 600,
          "questionCount": 4
        },
        "timing": {
          "minReadingTimeMs": 15000,
          "maxReadingTimeMs": 105000
        },
        "visuals": {
          "fontSize": "20px"
        }
      }
    }
  },
  {
    "id": "4062647b-988d-566f-8f34-8d880bfe14a0",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "age": "young-adult",
    "level": 4,
    "previousConfiguration": {
      "engineType": "skimming",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "skimming",
        "rules": {
          "timeLimit": 150
        },
        "timing": {
          "minReadingTimeMs": 3000
        },
        "readingPurpose": "evaluation"
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "skimming",
      "difficultyLevel": 4,
      "engineConfig": {
        "mode": "skimming",
        "readingPurpose": "evaluation",
        "content": {
          "source": "reading_text",
          "minWordCount": 600,
          "maxWordCount": 750,
          "questionCount": 4
        },
        "timing": {
          "minReadingTimeMs": 18000,
          "maxReadingTimeMs": 120000
        },
        "visuals": {
          "fontSize": "20px"
        }
      }
    }
  },
  {
    "id": "a1dc447b-f5ce-557c-bd63-852040fb5ca6",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "age": "young-adult",
    "level": 5,
    "previousConfiguration": {
      "engineType": "skimming",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "skimming",
        "rules": {
          "timeLimit": 150
        },
        "timing": {
          "minReadingTimeMs": 3000
        },
        "readingPurpose": "evaluation"
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "skimming",
      "difficultyLevel": 5,
      "engineConfig": {
        "mode": "skimming",
        "readingPurpose": "evaluation",
        "content": {
          "source": "reading_text",
          "minWordCount": 750,
          "maxWordCount": 900,
          "questionCount": 4
        },
        "timing": {
          "minReadingTimeMs": 20000,
          "maxReadingTimeMs": 135000
        },
        "visuals": {
          "fontSize": "20px"
        }
      }
    }
  },
  {
    "id": "b0eef605-99a1-52f6-95af-62d48476c4d6",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "age": "adult",
    "level": 3,
    "previousConfiguration": {
      "engineType": "skimming",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "skimming",
        "rules": {
          "timeLimit": 180
        },
        "timing": {
          "minReadingTimeMs": 3000
        },
        "readingPurpose": "evaluation"
      },
      "difficultyLevel": 3
    },
    "configuration": {
      "engineType": "skimming",
      "difficultyLevel": 3,
      "engineConfig": {
        "mode": "skimming",
        "readingPurpose": "evaluation",
        "content": {
          "source": "reading_text",
          "minWordCount": 500,
          "maxWordCount": 650,
          "questionCount": 4
        },
        "timing": {
          "minReadingTimeMs": 15000,
          "maxReadingTimeMs": 120000
        },
        "visuals": {
          "fontSize": "20px"
        }
      }
    }
  },
  {
    "id": "a4d35a8a-6f3c-57ba-a5a1-d8519105fb70",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "age": "adult",
    "level": 4,
    "previousConfiguration": {
      "engineType": "skimming",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "skimming",
        "rules": {
          "timeLimit": 240
        },
        "timing": {
          "minReadingTimeMs": 3000
        },
        "readingPurpose": "evaluation"
      },
      "difficultyLevel": 4
    },
    "configuration": {
      "engineType": "skimming",
      "difficultyLevel": 4,
      "engineConfig": {
        "mode": "skimming",
        "readingPurpose": "evaluation",
        "content": {
          "source": "reading_text",
          "minWordCount": 650,
          "maxWordCount": 850,
          "questionCount": 4
        },
        "timing": {
          "minReadingTimeMs": 20000,
          "maxReadingTimeMs": 135000
        },
        "visuals": {
          "fontSize": "20px"
        }
      }
    }
  },
  {
    "id": "8f6c9722-b784-54c2-bf22-98c32bb9716f",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "age": "adult",
    "level": 5,
    "previousConfiguration": {
      "engineType": "skimming",
      "engineConfig": {
        "content": {
          "source": "reading_text"
        },
        "mode": "skimming",
        "rules": {
          "timeLimit": 270
        },
        "timing": {
          "minReadingTimeMs": 3000
        },
        "readingPurpose": "evaluation"
      },
      "difficultyLevel": 5
    },
    "configuration": {
      "engineType": "skimming",
      "difficultyLevel": 5,
      "engineConfig": {
        "mode": "skimming",
        "readingPurpose": "evaluation",
        "content": {
          "source": "reading_text",
          "minWordCount": 850,
          "maxWordCount": 1050,
          "questionCount": 4
        },
        "timing": {
          "minReadingTimeMs": 25000,
          "maxReadingTimeMs": 150000
        },
        "visuals": {
          "fontSize": "20px"
        }
      }
    }
  }
]$settings$::jsonb) item;
CREATE TEMP TABLE skimming_content ON COMMIT DROP AS
SELECT (item->>'id')::uuid id, (item->>'exerciseId')::uuid exercise_id, (item->>'ageGroupId')::uuid age_id,
  (item->>'level')::int level, item->>'title' title, item->>'content' content,
  (item->>'wordCount')::int word_count, item->'questions' questions
FROM jsonb_array_elements($content$[
  {
    "id": "be2e5b66-9146-599e-8b0d-9b960fd87a50",
    "exerciseId": "2f5ccf40-b4c8-5f5f-a472-88bd03480aca",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "age": "child",
    "level": 3,
    "title": "Bir bahçede birlikte öğrenmek — Seviye 3",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nBu çalışmada önemli olan yalnız bitkilerin büyümesi değildi. Çocuklar koşulları gözleyerek, notlarını karşılaştırarak ve görevleri paylaşarak karar vermeyi denedi. Bahçe, bir işi hemen bitirmekten çok birlikte öğrenmenin ve gerektiğinde düzenlemeyi değiştirmenin örneği oldu.",
    "wordCount": 235,
    "source": "../../grouping/v1/texts/garden.txt",
    "sourceSha256": "0d7ce30627e06b33e7c9ff46cf4804e53fdf3cab138d39819f02146874592a02",
    "questions": [
      {
        "id": "6d62bde2-ca04-5dc2-823b-4d398036df6c",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Bir bitki yarışmasının kazananını belirlemek",
        "optionB": "Bir bitki köşesini gözlem ve görev paylaşımıyla geliştirmek",
        "optionC": "Her bitkiye aynı miktarda su vermeyi savunmak",
        "optionD": "Bahçedeki tüm canlıları uzaklaştırma yöntemini öğretmek",
        "correctAnswer": "B",
        "evidence": "Bu çalışmada önemli olan yalnız bitkilerin büyümesi değildi. Çocuklar koşulları gözleyerek, notlarını karşılaştırarak ve görevleri paylaşarak karar vermeyi denedi. Bahçe, bir işi hemen bitirmekten çok birlikte öğrenmenin ve gerektiğinde düzenlemeyi değiştirmenin örneği oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "84fb2f87-3618-5282-8a6f-4506bd3eb12b",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Bitkilerin gelişmesini yalnız sulama miktarı belirler",
        "optionB": "İşlerin hızlı bitmesi için bütün görevler tek kişiye bırakılmalıdır",
        "optionC": "Ortak gözlem, kayıt ve görev paylaşımı bahçede öğrenmeyi destekler",
        "optionD": "Bir gözlem her koşul için kesin sonuç vermeye yeter",
        "correctAnswer": "C",
        "evidence": "Bu çalışmada önemli olan yalnız bitkilerin büyümesi değildi. Çocuklar koşulları gözleyerek, notlarını karşılaştırarak ve görevleri paylaşarak karar vermeyi denedi. Bahçe, bir işi hemen bitirmekten çok birlikte öğrenmenin ve gerektiğinde düzenlemeyi değiştirmenin örneği oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "4412d679-5cd8-57c7-8e8a-90185b2c0c7d",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Bir tohum markasının satın alınmasını sağlamak",
        "optionB": "Bütün bitki türlerinin özelliklerini eksiksiz listelemek",
        "optionC": "Öğrencileri yetiştirdikleri bitkilere göre sıralamak",
        "optionD": "Bahçe çalışması üzerinden birlikte gözlem yapmanın önemini anlatmak",
        "correctAnswer": "D",
        "evidence": "Bu çalışmada önemli olan yalnız bitkilerin büyümesi değildi. Çocuklar koşulları gözleyerek, notlarını karşılaştırarak ve görevleri paylaşarak karar vermeyi denedi. Bahçe, bir işi hemen bitirmekten çok birlikte öğrenmenin ve gerektiğinde düzenlemeyi değiştirmenin örneği oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "42d529c8-7b40-56b9-80c0-8f49118ad452",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Bir bahçede birlikte öğrenmek",
        "optionB": "En Hızlı Büyüyen Tohum Yarışı",
        "optionC": "Bahçeyi Bir Günde Tamamlamak",
        "optionD": "Her Bitkiye Tek Bir Kural",
        "correctAnswer": "A",
        "evidence": "Bu çalışmada önemli olan yalnız bitkilerin büyümesi değildi. Çocuklar koşulları gözleyerek, notlarını karşılaştırarak ve görevleri paylaşarak karar vermeyi denedi. Bahçe, bir işi hemen bitirmekten çok birlikte öğrenmenin ve gerektiğinde düzenlemeyi değiştirmenin örneği oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "9750002d-3964-5e9d-8b40-ddbad183a93f",
    "exerciseId": "2f5ccf40-b4c8-5f5f-a472-88bd03480aca",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "age": "child",
    "level": 3,
    "title": "İhtiyaca göre gelişen harita — Seviye 3",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nHaritanın değeri bütün sokakları kusursuz çizmesinden gelmiyordu. Kullanıcıların neye ihtiyaç duyduğunu anlamak, karışan işaretleri denemek ve değişen bilgileri güncellemek onu yararlı kılıyordu. Deniz için çizim, bitmiş bir resimden çok birlikte geliştirilebilen bir yol gösterici oldu.",
    "wordCount": 236,
    "source": "../../grouping/v1/texts/map.txt",
    "sourceSha256": "0ab7639c5911d2578e4403703ce59e9c4fb107f2fe964e2feee6c817da006345",
    "questions": [
      {
        "id": "e5e136eb-86fa-5b63-8158-642c57743219",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Bir koşu yarışında en hızlı kişiyi seçmek",
        "optionB": "Bir haritayı kullanım deneyimleriyle anlaşılır hale getirmek",
        "optionC": "Mahalledeki bütün binaların yaşını hesaplamak",
        "optionD": "Parkın yerini başka bir mahalleye taşımak",
        "correctAnswer": "B",
        "evidence": "Haritanın değeri bütün sokakları kusursuz çizmesinden gelmiyordu. Kullanıcıların neye ihtiyaç duyduğunu anlamak, karışan işaretleri denemek ve değişen bilgileri güncellemek onu yararlı kılıyordu. Deniz için çizim, bitmiş bir resimden çok birlikte geliştirilebilen bir yol gösterici oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "5ba6e50c-8b17-524f-89ef-b26d93af5aaf",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Bir çizimi yalnız hazırlayan kişinin anlaması yeterlidir",
        "optionB": "Bir harita ilk çizildiğinde bir daha değiştirilmemelidir",
        "optionC": "Harita, kullanıcı ihtiyaçlarına göre denenip güncellendiğinde yararlı olur",
        "optionD": "En kısa yol her kullanıcı için daima en uygun yoldur",
        "correctAnswer": "C",
        "evidence": "Haritanın değeri bütün sokakları kusursuz çizmesinden gelmiyordu. Kullanıcıların neye ihtiyaç duyduğunu anlamak, karışan işaretleri denemek ve değişen bilgileri güncellemek onu yararlı kılıyordu. Deniz için çizim, bitmiş bir resimden çok birlikte geliştirilebilen bir yol gösterici oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "d0743e0c-d816-5297-87e5-6334d17d8935",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Her yürüyüşün süresinin aynı olduğunu kanıtlamak",
        "optionB": "Bütün kullanıcıların aynı yolu seçmesini zorunlu tutmak",
        "optionC": "Mahalledeki tüm yolların teknik ölçülerini vermek",
        "optionD": "Haritanın kullanıcıların ihtiyaçlarına göre geliştirilebileceğini göstermek",
        "correctAnswer": "D",
        "evidence": "Haritanın değeri bütün sokakları kusursuz çizmesinden gelmiyordu. Kullanıcıların neye ihtiyaç duyduğunu anlamak, karışan işaretleri denemek ve değişen bilgileri güncellemek onu yararlı kılıyordu. Deniz için çizim, bitmiş bir resimden çok birlikte geliştirilebilen bir yol gösterici oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "3168024d-5e85-5e04-8d4b-6d6280ed409c",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "İhtiyaca göre gelişen harita",
        "optionB": "Değişmeyen Kusursuz Çizim",
        "optionC": "Parktaki Hız Yarışı",
        "optionD": "Bütün Yollar Aynıdır",
        "correctAnswer": "A",
        "evidence": "Haritanın değeri bütün sokakları kusursuz çizmesinden gelmiyordu. Kullanıcıların neye ihtiyaç duyduğunu anlamak, karışan işaretleri denemek ve değişen bilgileri güncellemek onu yararlı kılıyordu. Deniz için çizim, bitmiş bir resimden çok birlikte geliştirilebilen bir yol gösterici oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "9ebc74b9-4823-5485-808c-4e66b6e98ace",
    "exerciseId": "2f5ccf40-b4c8-5f5f-a472-88bd03480aca",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "age": "child",
    "level": 3,
    "title": "Merakı destekleyen kütüphane — Seviye 3",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
    "wordCount": 234,
    "source": "../../free-reading/v1/texts/library-long.txt",
    "sourceSha256": "a856f71c5d3b8ff5966873de7e39ab894e018d3d7198980d5f09bb1dbf2fe774",
    "questions": [
      {
        "id": "c7944dac-5137-5a23-8bf9-0a150997f729",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Kütüphanede bilgiye ulaşmayı ve öğrenmeyi birlikte geliştirmek",
        "optionB": "Kitap kapaklarını yalnız renklerine göre sıralamak",
        "optionC": "Bütün okuyuculara aynı kitabı zorunlu seçtirmek",
        "optionD": "En çok sayfa okuyan kişiyi ödüllendirmek",
        "correctAnswer": "A",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "c586675c-ee48-5863-801c-7f0749f1c3a8",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Kitap seçiminde bütün okuyucuların ihtiyacı aynıdır",
        "optionB": "Kütüphane, merak ve bilgiye erişimi ortak çalışmayla destekler",
        "optionC": "Bir kaynağa bakmak bütün soruları kesin olarak çözer",
        "optionD": "Kütüphanede yalnız ödünç alınan kitap sayısı önemlidir",
        "correctAnswer": "B",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "a2a9ccbb-e8b3-56cb-87cd-447add2ddd11",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Bir okuma hızının herkese uygun olduğunu kanıtlamak",
        "optionB": "Kitapları yalnız kalınlıklarına göre değerlendirmek",
        "optionC": "Kütüphanenin merakı ve bilgiye erişimi destekleyen rolünü anlatmak",
        "optionD": "Bütün ziyaretçileri aynı görüşte birleştirmek",
        "correctAnswer": "C",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "50da61bf-7b65-532e-85a1-88d4d9749947",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Herkese Tek Kitap",
        "optionB": "Sayfa Sayısına Göre Yarış",
        "optionC": "Yalnız Kapaklara Bakmak",
        "optionD": "Merakı destekleyen kütüphane",
        "correctAnswer": "D",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "b77e8e0f-fcdf-5478-8d8d-9c49e79122cb",
    "exerciseId": "d1a4fac5-42db-587b-a429-015f922fa8d2",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "age": "child",
    "level": 4,
    "title": "Bir bahçede birlikte öğrenmek — Seviye 4",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBu çalışmada önemli olan yalnız bitkilerin büyümesi değildi. Çocuklar koşulları gözleyerek, notlarını karşılaştırarak ve görevleri paylaşarak karar vermeyi denedi. Bahçe, bir işi hemen bitirmekten çok birlikte öğrenmenin ve gerektiğinde düzenlemeyi değiştirmenin örneği oldu.",
    "wordCount": 330,
    "source": "../../grouping/v1/texts/garden.txt",
    "sourceSha256": "0d7ce30627e06b33e7c9ff46cf4804e53fdf3cab138d39819f02146874592a02",
    "questions": [
      {
        "id": "2fa8c977-9c49-57aa-8c50-66b62eccd5a3",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Bir bitki yarışmasının kazananını belirlemek",
        "optionB": "Bir bitki köşesini gözlem ve görev paylaşımıyla geliştirmek",
        "optionC": "Her bitkiye aynı miktarda su vermeyi savunmak",
        "optionD": "Bahçedeki tüm canlıları uzaklaştırma yöntemini öğretmek",
        "correctAnswer": "B",
        "evidence": "Bu çalışmada önemli olan yalnız bitkilerin büyümesi değildi. Çocuklar koşulları gözleyerek, notlarını karşılaştırarak ve görevleri paylaşarak karar vermeyi denedi. Bahçe, bir işi hemen bitirmekten çok birlikte öğrenmenin ve gerektiğinde düzenlemeyi değiştirmenin örneği oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "17104eeb-839a-5ab3-86c2-c806cb5a4574",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Bitkilerin gelişmesini yalnız sulama miktarı belirler",
        "optionB": "İşlerin hızlı bitmesi için bütün görevler tek kişiye bırakılmalıdır",
        "optionC": "Ortak gözlem, kayıt ve görev paylaşımı bahçede öğrenmeyi destekler",
        "optionD": "Bir gözlem her koşul için kesin sonuç vermeye yeter",
        "correctAnswer": "C",
        "evidence": "Bu çalışmada önemli olan yalnız bitkilerin büyümesi değildi. Çocuklar koşulları gözleyerek, notlarını karşılaştırarak ve görevleri paylaşarak karar vermeyi denedi. Bahçe, bir işi hemen bitirmekten çok birlikte öğrenmenin ve gerektiğinde düzenlemeyi değiştirmenin örneği oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "c283e603-98fc-5e2d-884a-f596c62bc2ec",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Bir tohum markasının satın alınmasını sağlamak",
        "optionB": "Bütün bitki türlerinin özelliklerini eksiksiz listelemek",
        "optionC": "Öğrencileri yetiştirdikleri bitkilere göre sıralamak",
        "optionD": "Bahçe çalışması üzerinden birlikte gözlem yapmanın önemini anlatmak",
        "correctAnswer": "D",
        "evidence": "Bu çalışmada önemli olan yalnız bitkilerin büyümesi değildi. Çocuklar koşulları gözleyerek, notlarını karşılaştırarak ve görevleri paylaşarak karar vermeyi denedi. Bahçe, bir işi hemen bitirmekten çok birlikte öğrenmenin ve gerektiğinde düzenlemeyi değiştirmenin örneği oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "69122d42-789c-5427-8f4f-dcf474466515",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Bir bahçede birlikte öğrenmek",
        "optionB": "En Hızlı Büyüyen Tohum Yarışı",
        "optionC": "Bahçeyi Bir Günde Tamamlamak",
        "optionD": "Her Bitkiye Tek Bir Kural",
        "correctAnswer": "A",
        "evidence": "Bu çalışmada önemli olan yalnız bitkilerin büyümesi değildi. Çocuklar koşulları gözleyerek, notlarını karşılaştırarak ve görevleri paylaşarak karar vermeyi denedi. Bahçe, bir işi hemen bitirmekten çok birlikte öğrenmenin ve gerektiğinde düzenlemeyi değiştirmenin örneği oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "6e3aa09b-1d54-5abc-89be-e8a5970db972",
    "exerciseId": "d1a4fac5-42db-587b-a429-015f922fa8d2",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "age": "child",
    "level": 4,
    "title": "İhtiyaca göre gelişen harita — Seviye 4",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nHaritanın değeri bütün sokakları kusursuz çizmesinden gelmiyordu. Kullanıcıların neye ihtiyaç duyduğunu anlamak, karışan işaretleri denemek ve değişen bilgileri güncellemek onu yararlı kılıyordu. Deniz için çizim, bitmiş bir resimden çok birlikte geliştirilebilen bir yol gösterici oldu.",
    "wordCount": 330,
    "source": "../../grouping/v1/texts/map.txt",
    "sourceSha256": "0ab7639c5911d2578e4403703ce59e9c4fb107f2fe964e2feee6c817da006345",
    "questions": [
      {
        "id": "82ef5cb1-f762-5ca6-816b-637cc3ed51d9",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Bir koşu yarışında en hızlı kişiyi seçmek",
        "optionB": "Mahalledeki bütün binaların yaşını hesaplamak",
        "optionC": "Parkın yerini başka bir mahalleye taşımak",
        "optionD": "Bir haritayı kullanım deneyimleriyle anlaşılır hale getirmek",
        "correctAnswer": "D",
        "evidence": "Haritanın değeri bütün sokakları kusursuz çizmesinden gelmiyordu. Kullanıcıların neye ihtiyaç duyduğunu anlamak, karışan işaretleri denemek ve değişen bilgileri güncellemek onu yararlı kılıyordu. Deniz için çizim, bitmiş bir resimden çok birlikte geliştirilebilen bir yol gösterici oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "839f7932-5259-59d8-82c9-9f2432ed6d75",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Harita, kullanıcı ihtiyaçlarına göre denenip güncellendiğinde yararlı olur",
        "optionB": "Bir çizimi yalnız hazırlayan kişinin anlaması yeterlidir",
        "optionC": "Bir harita ilk çizildiğinde bir daha değiştirilmemelidir",
        "optionD": "En kısa yol her kullanıcı için daima en uygun yoldur",
        "correctAnswer": "A",
        "evidence": "Haritanın değeri bütün sokakları kusursuz çizmesinden gelmiyordu. Kullanıcıların neye ihtiyaç duyduğunu anlamak, karışan işaretleri denemek ve değişen bilgileri güncellemek onu yararlı kılıyordu. Deniz için çizim, bitmiş bir resimden çok birlikte geliştirilebilen bir yol gösterici oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "6a752014-84df-544f-8cf1-504913af8a1b",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Her yürüyüşün süresinin aynı olduğunu kanıtlamak",
        "optionB": "Haritanın kullanıcıların ihtiyaçlarına göre geliştirilebileceğini göstermek",
        "optionC": "Bütün kullanıcıların aynı yolu seçmesini zorunlu tutmak",
        "optionD": "Mahalledeki tüm yolların teknik ölçülerini vermek",
        "correctAnswer": "B",
        "evidence": "Haritanın değeri bütün sokakları kusursuz çizmesinden gelmiyordu. Kullanıcıların neye ihtiyaç duyduğunu anlamak, karışan işaretleri denemek ve değişen bilgileri güncellemek onu yararlı kılıyordu. Deniz için çizim, bitmiş bir resimden çok birlikte geliştirilebilen bir yol gösterici oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "f1d33567-9530-5881-8e57-0c7de2be90bf",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Değişmeyen Kusursuz Çizim",
        "optionB": "Parktaki Hız Yarışı",
        "optionC": "İhtiyaca göre gelişen harita",
        "optionD": "Bütün Yollar Aynıdır",
        "correctAnswer": "C",
        "evidence": "Haritanın değeri bütün sokakları kusursuz çizmesinden gelmiyordu. Kullanıcıların neye ihtiyaç duyduğunu anlamak, karışan işaretleri denemek ve değişen bilgileri güncellemek onu yararlı kılıyordu. Deniz için çizim, bitmiş bir resimden çok birlikte geliştirilebilen bir yol gösterici oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "321b6970-0fb5-5312-82a0-4196df3dfc9a",
    "exerciseId": "d1a4fac5-42db-587b-a429-015f922fa8d2",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "age": "child",
    "level": 4,
    "title": "Merakı destekleyen kütüphane — Seviye 4",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nKütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
    "wordCount": 333,
    "source": "../../free-reading/v1/texts/library-long.txt",
    "sourceSha256": "a856f71c5d3b8ff5966873de7e39ab894e018d3d7198980d5f09bb1dbf2fe774",
    "questions": [
      {
        "id": "1aaf47e6-5751-5e8e-86a3-2bd81f61f5ab",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Kitap kapaklarını yalnız renklerine göre sıralamak",
        "optionB": "Bütün okuyuculara aynı kitabı zorunlu seçtirmek",
        "optionC": "Kütüphanede bilgiye ulaşmayı ve öğrenmeyi birlikte geliştirmek",
        "optionD": "En çok sayfa okuyan kişiyi ödüllendirmek",
        "correctAnswer": "C",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "fe1e04e1-3a66-553f-8433-52959a48536c",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Kitap seçiminde bütün okuyucuların ihtiyacı aynıdır",
        "optionB": "Bir kaynağa bakmak bütün soruları kesin olarak çözer",
        "optionC": "Kütüphanede yalnız ödünç alınan kitap sayısı önemlidir",
        "optionD": "Kütüphane, merak ve bilgiye erişimi ortak çalışmayla destekler",
        "correctAnswer": "D",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "f85428a6-f2e5-54cd-8522-1a71d143b3a9",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Kütüphanenin merakı ve bilgiye erişimi destekleyen rolünü anlatmak",
        "optionB": "Bir okuma hızının herkese uygun olduğunu kanıtlamak",
        "optionC": "Kitapları yalnız kalınlıklarına göre değerlendirmek",
        "optionD": "Bütün ziyaretçileri aynı görüşte birleştirmek",
        "correctAnswer": "A",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "b242c2b2-ed83-530d-879b-02f663f99857",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Herkese Tek Kitap",
        "optionB": "Merakı destekleyen kütüphane",
        "optionC": "Sayfa Sayısına Göre Yarış",
        "optionD": "Yalnız Kapaklara Bakmak",
        "correctAnswer": "B",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "e1350d82-82f1-5130-8ce0-d76acc983f0c",
    "exerciseId": "00a29320-c7cf-5ac0-99e4-62226e911a2b",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "age": "child",
    "level": 5,
    "title": "Bir bahçede birlikte öğrenmek — Seviye 5",
    "content": "Okul bahçesinin bir köşesinde boş bir toprak alan vardı. Eren, arkadaşlarıyla birlikte burada küçük bir bitki köşesi kurmayı düşündü. İlk işleri tohum almak olmadı. Önce alanı gezdiler, güneşin nereden geldiğini ve suyun nerede biriktiğini gözlemlediler.\n\nÖğretmenleri, her bitkinin aynı yerde aynı biçimde büyümeyeceğini söyledi. Çocuklar sabah ve öğleden sonra bahçeye baktı. Gölgenin gün içinde yer değiştirdiğini fark ettiler. Bir deftere tarih ve saat yazarak gördüklerini kısa cümlelerle kaydettiler.\n\nBahçenin kenarında kuru yapraklar ve birkaç taş bulunuyordu. Çocuklar toprağı temizlerken küçük canlıları rahatsız etmemeye çalıştı. Buldukları her şeyi çöp saymadılar. Bazı yaprakların ve dalların toprağın üzerinde doğal olarak bulunabileceğini öğretmenlerine sordular.\n\nTohumları seçerken yetişme sürelerini de okudular. Bazı bitkilerin daha çok zaman istediğini gördüler. Küçük alan için birkaç türle başlamaya karar verdiler. Her sıranın başına bir işaret koydular. İşaretlerde bitkinin adı ve ekildiği gün yazıyordu.\n\nİlk sulamada herkes su vermek istedi. Ancak her çocuğun ayrı ayrı sulaması toprağı fazla ıslattı. Ertesi gün görevleri paylaşmaya karar verdiler. Bir kişi suluyor, bir kişi toprağın durumunu gözlüyor, bir kişi de deftere not alıyordu.\n\nBir hafta sonra ilk küçük yapraklar göründü. Eren, bütün tohumların aynı gün filizlenmediğini fark etti. Henüz görünmeyen tohumları hemen başarısız saymadı. Paket üzerindeki bilgilerle defterdeki tarihleri karşılaştırdı. Gözlem yapmanın bazen sabır gerektirdiğini anladı.\n\nÇocuklar ölçüm için basit bir cetvel kullandı. Her bitkinin aynı noktasından ölçmeye çalıştılar. Bir arkadaşları yaprağın ucunu, diğeri gövdenin tepesini ölçünce sonuçlar farklı çıktı. Sonraki ölçümlerde hangi noktaya bakacaklarını birlikte belirlediler.\n\nDefterdeki sayılar tek başına bahçede olanları anlatmıyordu. Yağmur yağdığı günleri ve yaprakların rengini de kaydettiler. Böylece bir değişiklik gördüklerinde önceki günlerin koşullarına bakabiliyorlardı. Notlar, hatırlanan bilgiyi daha düzenli bir gözleme dönüştürüyordu.\n\nBahçeye gelen bir ziyaretçi, hangi sıranın daha hızlı büyüdüğünü sordu. Çocuklar yalnız en uzun bitkiyi göstermedi. Aynı türdeki birkaç bitkiyi karşılaştırdılar. Bir örneğe bakarak bütün sıra hakkında kesin karar vermenin yanıltıcı olabileceğini düşündüler.\n\nBir gün işaretlerden biri kayboldu. O sıradaki bitkilerin adını herkes farklı hatırlıyordu. Defterdeki çizime bakarak sırayı yeniden buldular. Bilginin yalnız bir kişinin belleğinde durmaması, küçük bir karışıklığın daha kolay çözülmesini sağladı.\n\nSulama görevleri haftalık bir çizelgeye yazıldı. Çizelgede yalnız isimler değil, yapılacak işin kısa açıklaması da vardı. Bir kişi gelemediğinde başka biri görevi devralabiliyordu. Görevin nasıl yapılacağını bilmek, görevlinin adını bilmek kadar önemliydi.\n\nBu çalışmada önemli olan yalnız bitkilerin büyümesi değildi. Çocuklar koşulları gözleyerek, notlarını karşılaştırarak ve görevleri paylaşarak karar vermeyi denedi. Bahçe, bir işi hemen bitirmekten çok birlikte öğrenmenin ve gerektiğinde düzenlemeyi değiştirmenin örneği oldu.",
    "wordCount": 393,
    "source": "../../grouping/v1/texts/garden.txt",
    "sourceSha256": "0d7ce30627e06b33e7c9ff46cf4804e53fdf3cab138d39819f02146874592a02",
    "questions": [
      {
        "id": "f2e508c5-bc99-5510-8fd8-ffbde1b0efb3",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Bir bitki yarışmasının kazananını belirlemek",
        "optionB": "Her bitkiye aynı miktarda su vermeyi savunmak",
        "optionC": "Bir bitki köşesini gözlem ve görev paylaşımıyla geliştirmek",
        "optionD": "Bahçedeki tüm canlıları uzaklaştırma yöntemini öğretmek",
        "correctAnswer": "C",
        "evidence": "Bu çalışmada önemli olan yalnız bitkilerin büyümesi değildi. Çocuklar koşulları gözleyerek, notlarını karşılaştırarak ve görevleri paylaşarak karar vermeyi denedi. Bahçe, bir işi hemen bitirmekten çok birlikte öğrenmenin ve gerektiğinde düzenlemeyi değiştirmenin örneği oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "b3ce0d80-a07f-58bb-8c13-c1be56758c8b",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Bitkilerin gelişmesini yalnız sulama miktarı belirler",
        "optionB": "İşlerin hızlı bitmesi için bütün görevler tek kişiye bırakılmalıdır",
        "optionC": "Bir gözlem her koşul için kesin sonuç vermeye yeter",
        "optionD": "Ortak gözlem, kayıt ve görev paylaşımı bahçede öğrenmeyi destekler",
        "correctAnswer": "D",
        "evidence": "Bu çalışmada önemli olan yalnız bitkilerin büyümesi değildi. Çocuklar koşulları gözleyerek, notlarını karşılaştırarak ve görevleri paylaşarak karar vermeyi denedi. Bahçe, bir işi hemen bitirmekten çok birlikte öğrenmenin ve gerektiğinde düzenlemeyi değiştirmenin örneği oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "75ec8e44-8adf-5ab8-880f-0d2204f9988a",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Bahçe çalışması üzerinden birlikte gözlem yapmanın önemini anlatmak",
        "optionB": "Bir tohum markasının satın alınmasını sağlamak",
        "optionC": "Bütün bitki türlerinin özelliklerini eksiksiz listelemek",
        "optionD": "Öğrencileri yetiştirdikleri bitkilere göre sıralamak",
        "correctAnswer": "A",
        "evidence": "Bu çalışmada önemli olan yalnız bitkilerin büyümesi değildi. Çocuklar koşulları gözleyerek, notlarını karşılaştırarak ve görevleri paylaşarak karar vermeyi denedi. Bahçe, bir işi hemen bitirmekten çok birlikte öğrenmenin ve gerektiğinde düzenlemeyi değiştirmenin örneği oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "84c46e0c-cc8e-57f6-81b3-b0b761966310",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "En Hızlı Büyüyen Tohum Yarışı",
        "optionB": "Bir bahçede birlikte öğrenmek",
        "optionC": "Bahçeyi Bir Günde Tamamlamak",
        "optionD": "Her Bitkiye Tek Bir Kural",
        "correctAnswer": "B",
        "evidence": "Bu çalışmada önemli olan yalnız bitkilerin büyümesi değildi. Çocuklar koşulları gözleyerek, notlarını karşılaştırarak ve görevleri paylaşarak karar vermeyi denedi. Bahçe, bir işi hemen bitirmekten çok birlikte öğrenmenin ve gerektiğinde düzenlemeyi değiştirmenin örneği oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "a7a478e1-b170-5674-8d2d-4d87cdb996b9",
    "exerciseId": "00a29320-c7cf-5ac0-99e4-62226e911a2b",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "age": "child",
    "level": 5,
    "title": "İhtiyaca göre gelişen harita — Seviye 5",
    "content": "Deniz, evinden parka giderken her zaman aynı yolu kullanıyordu. Bir gün yolun bir bölümü kapandı. Başka bir sokaktan yürümek zorunda kaldı. Dönüşte basit bir harita çizdi. Haritada bildiği yerleri, köşeleri ve geçtiği sokakları işaretledi.\n\nHaritası gerçek sokakların tam ölçüsünü göstermiyordu. Bazı yolları uzun, bazılarını kısa çizmişti. Yine de nerede döneceğini hatırlamasına yardım ediyordu. Deniz, bir çizimin bütün ayrıntıları göstermeden de belirli bir iş için yararlı olabileceğini gördü.\n\nArkadaşı haritaya baktığında fırının işaretini hemen tanıdı. Ancak iki oyun alanını birbirine karıştırdı. Deniz, aynı şekli iki farklı yer için kullanmıştı. İşaretlerin yanına kısa adlar ekledi. Böylece çizim, yalnız kendisinin anlayacağı bir not olmaktan çıktı.\n\nBir sonraki yürüyüşte yolun süresini de tuttu. Hızlı yürüdüğü günle arkadaşlarıyla konuşarak yürüdüğü gün aynı sonucu vermedi. Tek bir süreyi kesin bir ölçü saymadı. Haritanın yanına, sürenin yürüyüş biçimine göre değişebileceğini yazdı.\n\nParkın kapısında başka bir harita vardı. Bu harita yürüyüş yollarını, çeşmeleri ve oturma alanlarını gösteriyordu. Deniz iki haritanın aynı şey olmadığını fark etti. Kendi haritası parka ulaşmaya, kapıdaki harita ise parkın içinde dolaşmaya yardım ediyordu.\n\nParkta küçük bir keşif oyunu hazırladılar. Arkadaşları üç yeri bulacak ve oralardaki işaretleri not edecekti. Oyunda koşmak zorunlu değildi. Deniz, yolların açık olduğundan emin olmak için oyunu önce bir arkadaşıyla yavaşça denedi.\n\nDenemede bir okun yanlış yere baktığı görüldü. Deniz oku düzeltti ama bununla yetinmedi. Yanındaki açıklamayı da yeniden okudu. Bir sorunun yalnız işaretten değil, işaretle yazının birlikte kullanılmasından doğabileceğini fark etti.\n\nKeşif oyunu başladığında herkes aynı yolu seçmedi. Bazıları önce yakındaki yeri, bazıları daha uzak noktayı buldu. Deniz farklı yolları haritaya çizdi. Aynı sonuca ulaşmak için birden fazla uygun yol bulunabileceğini gördüler.\n\nOyundan sonra en hızlı bitireni ilan etmek yerine hangi işaretlerin kolay anlaşıldığını konuştular. Bir arkadaşları büyük ağaç çiziminin işe yaradığını söyledi. Başka biri çeşmenin yerini yazıdan bulmuştu. Harita farklı kişilere farklı ipuçları sunuyordu.\n\nBir ziyaretçi merdiven kullanmadan ilerleyebileceği yolu sordu. Deniz haritasında basamakları göstermediğini fark etti. Yeni çizime merdiven ve eğimli yol işaretleri ekledi. Herkes için aynı yolun uygun olmadığını bilmek, haritayı daha yararlı hâle getirdi.\n\nYol çalışması bitince eski sokak yeniden açıldı. Deniz yeni haritasını hemen atmadı. Farklı yolların başka günlerde de işe yarayabileceğini düşündü. Ancak kapalı sokak işaretini kaldırdı ve haritanın hangi tarihte güncellendiğini yazdı.\n\nHaritanın değeri bütün sokakları kusursuz çizmesinden gelmiyordu. Kullanıcıların neye ihtiyaç duyduğunu anlamak, karışan işaretleri denemek ve değişen bilgileri güncellemek onu yararlı kılıyordu. Deniz için çizim, bitmiş bir resimden çok birlikte geliştirilebilen bir yol gösterici oldu.",
    "wordCount": 394,
    "source": "../../grouping/v1/texts/map.txt",
    "sourceSha256": "0ab7639c5911d2578e4403703ce59e9c4fb107f2fe964e2feee6c817da006345",
    "questions": [
      {
        "id": "f278772e-f5ca-5144-850d-6f3f46d360ce",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Bir koşu yarışında en hızlı kişiyi seçmek",
        "optionB": "Bir haritayı kullanım deneyimleriyle anlaşılır hale getirmek",
        "optionC": "Mahalledeki bütün binaların yaşını hesaplamak",
        "optionD": "Parkın yerini başka bir mahalleye taşımak",
        "correctAnswer": "B",
        "evidence": "Haritanın değeri bütün sokakları kusursuz çizmesinden gelmiyordu. Kullanıcıların neye ihtiyaç duyduğunu anlamak, karışan işaretleri denemek ve değişen bilgileri güncellemek onu yararlı kılıyordu. Deniz için çizim, bitmiş bir resimden çok birlikte geliştirilebilen bir yol gösterici oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "a585be63-db09-5afd-84da-662e344f06fb",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Bir çizimi yalnız hazırlayan kişinin anlaması yeterlidir",
        "optionB": "Bir harita ilk çizildiğinde bir daha değiştirilmemelidir",
        "optionC": "Harita, kullanıcı ihtiyaçlarına göre denenip güncellendiğinde yararlı olur",
        "optionD": "En kısa yol her kullanıcı için daima en uygun yoldur",
        "correctAnswer": "C",
        "evidence": "Haritanın değeri bütün sokakları kusursuz çizmesinden gelmiyordu. Kullanıcıların neye ihtiyaç duyduğunu anlamak, karışan işaretleri denemek ve değişen bilgileri güncellemek onu yararlı kılıyordu. Deniz için çizim, bitmiş bir resimden çok birlikte geliştirilebilen bir yol gösterici oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "27f09125-932b-5af4-8030-0205aaa128b4",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Her yürüyüşün süresinin aynı olduğunu kanıtlamak",
        "optionB": "Bütün kullanıcıların aynı yolu seçmesini zorunlu tutmak",
        "optionC": "Mahalledeki tüm yolların teknik ölçülerini vermek",
        "optionD": "Haritanın kullanıcıların ihtiyaçlarına göre geliştirilebileceğini göstermek",
        "correctAnswer": "D",
        "evidence": "Haritanın değeri bütün sokakları kusursuz çizmesinden gelmiyordu. Kullanıcıların neye ihtiyaç duyduğunu anlamak, karışan işaretleri denemek ve değişen bilgileri güncellemek onu yararlı kılıyordu. Deniz için çizim, bitmiş bir resimden çok birlikte geliştirilebilen bir yol gösterici oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "4ea93222-910e-57a6-8f6f-17592bb721c9",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "İhtiyaca göre gelişen harita",
        "optionB": "Değişmeyen Kusursuz Çizim",
        "optionC": "Parktaki Hız Yarışı",
        "optionD": "Bütün Yollar Aynıdır",
        "correctAnswer": "A",
        "evidence": "Haritanın değeri bütün sokakları kusursuz çizmesinden gelmiyordu. Kullanıcıların neye ihtiyaç duyduğunu anlamak, karışan işaretleri denemek ve değişen bilgileri güncellemek onu yararlı kılıyordu. Deniz için çizim, bitmiş bir resimden çok birlikte geliştirilebilen bir yol gösterici oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "ad5d5bba-a6fa-5a0a-8908-a8b77b660e6b",
    "exerciseId": "00a29320-c7cf-5ac0-99e4-62226e911a2b",
    "ageGroupId": "10000000-0000-0000-0000-000000000001",
    "age": "child",
    "level": 5,
    "title": "Merakı destekleyen kütüphane — Seviye 5",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
    "wordCount": 397,
    "source": "../../free-reading/v1/texts/library-long.txt",
    "sourceSha256": "a856f71c5d3b8ff5966873de7e39ab894e018d3d7198980d5f09bb1dbf2fe774",
    "questions": [
      {
        "id": "6ec1f21f-bc9f-51b9-88bf-0326597676a7",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Kitap kapaklarını yalnız renklerine göre sıralamak",
        "optionB": "Bütün okuyuculara aynı kitabı zorunlu seçtirmek",
        "optionC": "En çok sayfa okuyan kişiyi ödüllendirmek",
        "optionD": "Kütüphanede bilgiye ulaşmayı ve öğrenmeyi birlikte geliştirmek",
        "correctAnswer": "D",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "418335e5-26ad-57e6-8a26-7129effcd1ba",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Kütüphane, merak ve bilgiye erişimi ortak çalışmayla destekler",
        "optionB": "Kitap seçiminde bütün okuyucuların ihtiyacı aynıdır",
        "optionC": "Bir kaynağa bakmak bütün soruları kesin olarak çözer",
        "optionD": "Kütüphanede yalnız ödünç alınan kitap sayısı önemlidir",
        "correctAnswer": "A",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "47769d25-6a63-55c7-8d60-f7efe2d34c3b",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Bir okuma hızının herkese uygun olduğunu kanıtlamak",
        "optionB": "Kütüphanenin merakı ve bilgiye erişimi destekleyen rolünü anlatmak",
        "optionC": "Kitapları yalnız kalınlıklarına göre değerlendirmek",
        "optionD": "Bütün ziyaretçileri aynı görüşte birleştirmek",
        "correctAnswer": "B",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "8dd6373a-edef-50f1-838b-1c46cc027904",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Herkese Tek Kitap",
        "optionB": "Sayfa Sayısına Göre Yarış",
        "optionC": "Merakı destekleyen kütüphane",
        "optionD": "Yalnız Kapaklara Bakmak",
        "correctAnswer": "C",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "eec01a9c-0e34-5fa1-8ad6-e86f2888d5a3",
    "exerciseId": "9242e9ad-d5d5-5b01-8a4c-86a6f68923e8",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "age": "teen",
    "level": 3,
    "title": "Gözlemden ortak karara — Seviye 3",
    "content": "Okulun öğrenci meclisi, sabah girişinde oluşan kalabalığı incelemek için bir çalışma grubu kurdu. İlk toplantıda herkes farklı bir çözüm önerdi. Bazıları kapının genişletilmesini, bazıları servislerin başka bir noktada durmasını istiyordu. Ancak önerilerin çoğu kişisel izlenimlere dayanıyordu. Grup, bir çözümü savunmadan önce sorunun hangi saatlerde ve nerelerde ortaya çıktığını anlamaya karar verdi. Öğretmenlerinin gözetiminde, yalnızca güvenli noktalardan ve kimlik bilgisi toplamadan gözlem yapacaklardı. Araştırmanın sınırlarını baştan yazmaları, sonraki kararları daha açık tartışmalarına yardımcı oldu.\n\nİlk hafta gözlemler üç farklı sabah yapıldı. Öğrenciler gelen kişilerin adlarını veya fotoğraflarını kaydetmedi. Bunun yerine belirli aralıklarla giriş önündeki yaklaşık yoğunluğu not etti. Havanın yağışlı olup olmadığını, bir servis aracının geç kalıp kalmadığını ve girişte özel bir etkinlik bulunup bulunmadığını da yazdılar. Böylece yalnız kişi sayısına bakılarak anlaşılmayabilecek durumları kayda geçirdiler. Bir ölçümün yanında koşulların bulunması, sayının tek başına her şeyi açıklamadığını gösteriyordu. Grup, eksik kalan gözlemleri tahmin ederek doldurmadı; bunları açıkça boş bıraktı.\n\nVeriler bir tabloya aktarıldığında ilk bakışta belirgin bir sonuç ortaya çıktı. En yoğun saat aralığı üç günün ikisinde aynıydı. Fakat üçüncü gün tablo farklı görünüyordu. Bir öğrenci o günü hatalı sayıp çıkarmayı önerdi. Diğerleri, farklılığın gerçekten önemli bir durumu göstermiş olabileceğini düşündü. Notları yeniden incelediklerinde o sabah bir yol çalışması nedeniyle servislerin farklı zamanda geldiğini gördüler. Bu kayıt silinmek yerine ayrı bir açıklamayla korundu. Normal koşullar ile beklenmeyen koşulların birlikte görülmesi, önerilecek düzenlemenin farklı günlerde nasıl işleyeceği hakkında yeni sorular doğurdu.\n\nGrup daha sonra okulun çevresine ilişkin yayımlanmış ulaşım bilgilerini incelemek istedi. Buldukları haritanın üzerinde güncelleme tarihi vardı, fakat bazı duraklar eski adlarıyla görünüyordu. Öğrenciler kaynağı kullanmadan önce hangi kısmının güncel olduğunu kontrol etti. İnternette kolayca bulunan bir bilgi, sırf bir haritada yer aldığı için doğru kabul edilemezdi. Özellikle güvenli ulaşım hakkında öneri hazırlanırken hatalı bir ayrıntı önemli sonuçlar doğurabilirdi. Çalışma grubunun hazırladığı belge bir yol güvenliği talimatı olmayacak; okul yönetiminin değerlendireceği gözlemleri ve soruları içerecekti.\n\nGrubun çalışması bir çözümü peşinen doğru ilan etmeye değil, sorunu koşullarıyla anlamaya dayanıyordu. Farklı günlerin kayıtlarını karşılaştırmak ve eksikleri açıkça belirtmek ortak kararları daha anlaşılır kıldı. Gözlem, kişileri sıralamak için değil uygulanabilir bir düzenleme tartışmak için kullanıldı.",
    "wordCount": 341,
    "source": "../../guided-reading/v1/texts/teen-5-route.txt",
    "sourceSha256": "4bbe18d1c6fe7783492d66cd84c5d4e2b8424a7dfa02b2e152a1c5958ddf2b18",
    "questions": [
      {
        "id": "a9b6b2c4-cb1d-57f8-81b8-f076d3961309",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Öğrencileri giriş sırasına göre başarıyla sıralamak",
        "optionB": "Bir servis şirketinin reklamını hazırlamak",
        "optionC": "Okulun bütün ders saatlerini değiştirmek",
        "optionD": "Okul girişindeki kalabalığı gözlemlerle incelemek",
        "correctAnswer": "D",
        "evidence": "Grubun çalışması bir çözümü peşinen doğru ilan etmeye değil, sorunu koşullarıyla anlamaya dayanıyordu. Farklı günlerin kayıtlarını karşılaştırmak ve eksikleri açıkça belirtmek ortak kararları daha anlaşılır kıldı. Gözlem, kişileri sıralamak için değil uygulanabilir bir düzenleme tartışmak için kullanıldı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "79b2fd30-8d25-517a-8312-0e0b760e3f9d",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Sorunu koşullarıyla gözlemek, ortak çözüm kararlarını destekler",
        "optionB": "İlk kişisel izlenim bütün kararlar için yeterlidir",
        "optionC": "Farklı çıkan gözlemler açıklanmadan silinmelidir",
        "optionD": "Eksik değerler ölçülmüş gibi tahminle doldurulmalıdır",
        "correctAnswer": "A",
        "evidence": "Grubun çalışması bir çözümü peşinen doğru ilan etmeye değil, sorunu koşullarıyla anlamaya dayanıyordu. Farklı günlerin kayıtlarını karşılaştırmak ve eksikleri açıkça belirtmek ortak kararları daha anlaşılır kıldı. Gözlem, kişileri sıralamak için değil uygulanabilir bir düzenleme tartışmak için kullanıldı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "11eba514-91f3-55bf-861b-cc03b6d9cc77",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Tek bir kapı genişliğinin her sorunu çözdüğünü kanıtlamak",
        "optionB": "Bir soruna çözüm ararken gözlem ve koşulların birlikte değerlendirilmesini göstermek",
        "optionC": "Öğrencilerin kimliklerini ve hareketlerini izlemek",
        "optionD": "En kalabalık günü bütün günlerin değişmez örneği saymak",
        "correctAnswer": "B",
        "evidence": "Grubun çalışması bir çözümü peşinen doğru ilan etmeye değil, sorunu koşullarıyla anlamaya dayanıyordu. Farklı günlerin kayıtlarını karşılaştırmak ve eksikleri açıkça belirtmek ortak kararları daha anlaşılır kıldı. Gözlem, kişileri sıralamak için değil uygulanabilir bir düzenleme tartışmak için kullanıldı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "646ec432-4652-5ab9-8bfc-73a14b3ef5cb",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "İlk Tahmin Her Zaman Doğrudur",
        "optionB": "Giriş Sırasına Göre Başarı",
        "optionC": "Gözlemden ortak karara",
        "optionD": "Her Gün Aynı Kalabalık",
        "correctAnswer": "C",
        "evidence": "Grubun çalışması bir çözümü peşinen doğru ilan etmeye değil, sorunu koşullarıyla anlamaya dayanıyordu. Farklı günlerin kayıtlarını karşılaştırmak ve eksikleri açıkça belirtmek ortak kararları daha anlaşılır kıldı. Gözlem, kişileri sıralamak için değil uygulanabilir bir düzenleme tartışmak için kullanıldı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "4a5f1c43-7b94-5a30-8c50-a036108ed8d5",
    "exerciseId": "9242e9ad-d5d5-5b01-8a4c-86a6f68923e8",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "age": "teen",
    "level": 3,
    "title": "Nesnelerin hikâyelerini araştırmak — Seviye 3",
    "content": "Kent kütüphanesinde düzenlenecek öğrenci sergisinin konusu gündelik nesnelerin hikâyeleriydi. Katılımcılardan eski bir eşya seçmeleri, nereden geldiğini araştırmaları ve ziyaretçilere kısa bir anlatı sunmaları istenmişti. Defne büyükannesinin kullandığı bir radyo, Aras ise ailesinin yıllardır sakladığı bir tamir kutusu üzerinde çalışacaktı. İkisi de ilk toplantıda nesneleri hakkında çok şey bildiğini düşünüyordu. Fakat sergi metnini yazmaya başladıklarında hatırlanan olaylar, kişisel yorumlar ve doğrulanabilir bilgiler arasında önemli farklar bulunduğunu gördüler. Her cümlenin dayanağı aynı türden değildi.\n\nDefne, radyonun satın alındığı yılı öğrenmek için büyükannesiyle konuştu. Büyükannesi belirli bir yaz tatilini hatırlıyor, fakat tam tarihi söyleyemiyordu. Radyonun üzerindeki model adı başka bir başlangıç noktası sağladı. Defne bulduğu farklı kaynaklarda benzer görünümlü cihazların birbirine karıştırıldığını fark etti. Bu nedenle yalnız fotoğraf benzerliğine dayanarak tarih vermedi. Model bilgisini ve hatırayı ayrı ayrı yazdı. Kesinleşmeyen noktalar için yaklaşık ifadeler kullandı. Sergiyi gezen birinin, anlatının hangi kısmında belirsizlik bulunduğunu anlayabilmesi onun için önemliydi.\n\nAras'ın tamir kutusunda eksik parçalar vardı. Kutunun ilk hâlini gösteren bir fotoğraf bulunmadığı için hangi parçanın ne zaman kaybolduğunu bilmiyordu. Babası, kutuyu farklı zamanlarda farklı işlerde kullandıklarını anlattı. Aras başlangıçta kutuyu bir mesleğin eksiksiz temsilcisi olarak sunmayı planlamıştı. Ancak aileye ait tek bir örnekten bütün bir meslek hakkında sonuç çıkaramayacağını anladı. Anlatısını daha dar bir çerçeveye yerleştirdi: Bu kutunun aile içindeki kullanım hikâyesini gösterecekti. Kapsamı küçültmek, metnin daha dürüst ve anlaşılır olmasını sağladı.\n\nSergi hazırlık ekibi nesnelerin yanına konacak açıklamalar için ortak bir uzunluk önerdi. Amaç bütün ziyaretçilerin ayrıntılı bir araştırma dosyası okumasını beklemek değildi. Kısa bir ana metin, isteyenler için ise daha uzun bir kaynak dosyası hazırlanacaktı. Defne ilk taslağındaki bazı cümleleri çıkarırken yalnız metni kısaltmaya çalışmadı. Hangi bilginin ziyaretçinin nesneyi anlamasına gerçekten yardım ettiğini düşündü. Dikkat çekici ama konuyla ilgisi zayıf ayrıntıları ayrı dosyaya taşıdı. Başlıkların ve paragraf düzeninin de anlatının anlaşılmasını etkilediğini gördü.\n\nSerginin değeri her nesne için kesin bir tarih bulmakta değil, anlatının nasıl oluştuğunu açıkça göstermekteydi. Hatıraları, yorumları ve doğrulanabilen bilgileri ayırmak ziyaretçinin anlatıyı daha dikkatli değerlendirmesini sağlıyordu. Bir nesnenin hikâyesi, kanıt ve belirsizlik birlikte görülebildiğinde daha anlaşılır hale geldi.",
    "wordCount": 329,
    "source": "../../guided-reading/v1/texts/teen-5-exhibition.txt",
    "sourceSha256": "6c1c8694a47c5514ddf23cf34b98a4e99e04dab84cabfc10cea90a1ad7caa033",
    "questions": [
      {
        "id": "1cfe9bff-f0f0-5a1f-84fc-5b6698b6d1ce",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Radyoların bütün teknik parçalarını onarmak",
        "optionB": "Bir tamir kutusunun satış fiyatını hesaplamak",
        "optionC": "Gündelik nesneler hakkında dayanağı açık sergi anlatıları hazırlamak",
        "optionD": "En eski eşyayı getiren kişiyi seçmek",
        "correctAnswer": "C",
        "evidence": "Serginin değeri her nesne için kesin bir tarih bulmakta değil, anlatının nasıl oluştuğunu açıkça göstermekteydi. Hatıraları, yorumları ve doğrulanabilen bilgileri ayırmak ziyaretçinin anlatıyı daha dikkatli değerlendirmesini sağlıyordu. Bir nesnenin hikâyesi, kanıt ve belirsizlik birlikte görülebildiğinde daha anlaşılır hale geldi.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "110bf2d4-6d4a-5fdd-89d0-df78b85e9fdf",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Her hatıra kesin tarih ve eksiksiz kanıt sayılmalıdır",
        "optionB": "Tek bir aile eşyası bütün bir mesleği temsil etmeye yeter",
        "optionC": "Belirsiz bilgiler kesinmiş gibi yazılmalıdır",
        "optionD": "Sergi anlatısında bilgi, yorum ve belirsizlik açıkça ayrılmalıdır",
        "correctAnswer": "D",
        "evidence": "Serginin değeri her nesne için kesin bir tarih bulmakta değil, anlatının nasıl oluştuğunu açıkça göstermekteydi. Hatıraları, yorumları ve doğrulanabilen bilgileri ayırmak ziyaretçinin anlatıyı daha dikkatli değerlendirmesini sağlıyordu. Bir nesnenin hikâyesi, kanıt ve belirsizlik birlikte görülebildiğinde daha anlaşılır hale geldi.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "790f59be-47d2-5cf1-8704-71fd0df1e6b5",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Bir nesnenin hikâyesini aktarırken bilgi ve belirsizliği ayırmanın önemini göstermek",
        "optionB": "Nesnelerin yalnız ekonomik değerini karşılaştırmak",
        "optionC": "Görünüş benzerliğinin kesin kanıt olduğunu savunmak",
        "optionD": "Katılımcıları eşyalarının yaşına göre sıralamak",
        "correctAnswer": "A",
        "evidence": "Serginin değeri her nesne için kesin bir tarih bulmakta değil, anlatının nasıl oluştuğunu açıkça göstermekteydi. Hatıraları, yorumları ve doğrulanabilen bilgileri ayırmak ziyaretçinin anlatıyı daha dikkatli değerlendirmesini sağlıyordu. Bir nesnenin hikâyesi, kanıt ve belirsizlik birlikte görülebildiğinde daha anlaşılır hale geldi.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "794c1998-deb3-592a-8a4a-d21f86c64977",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "En Pahalı Eşya Yarışı",
        "optionB": "Nesnelerin hikâyelerini araştırmak",
        "optionC": "Her Hatıra Kesin Kanıttır",
        "optionD": "Yalnız Görünüş Yeter",
        "correctAnswer": "B",
        "evidence": "Serginin değeri her nesne için kesin bir tarih bulmakta değil, anlatının nasıl oluştuğunu açıkça göstermekteydi. Hatıraları, yorumları ve doğrulanabilen bilgileri ayırmak ziyaretçinin anlatıyı daha dikkatli değerlendirmesini sağlıyordu. Bir nesnenin hikâyesi, kanıt ve belirsizlik birlikte görülebildiğinde daha anlaşılır hale geldi.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "31f6a61d-3168-57b7-8349-26fd7ff51994",
    "exerciseId": "9242e9ad-d5d5-5b01-8a4c-86a6f68923e8",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "age": "teen",
    "level": 3,
    "title": "Köprü maketinde karşılaştırmalı denemeler — Seviye 3",
    "content": "Bilim kulübündeki öğrenciler, küçük bir köprü maketi tasarlamak için iki gruba ayrıldı. Masanın iki ucuna yerleştirilen desteklerin arasındaki boşluk herkes için aynıydı. Kullanılabilecek karton miktarı da sınırlıydı. Öğretmen, yarışmanın yalnız en çok ağırlığı taşıyan köprüyü bulmak olmadığını açıkladı. Gruplar, hangi değişikliğin neyi etkilediğini de anlatmalıydı. Bu nedenle çizimler, deneme sonuçları ve başarısız tasarımlar çalışma boyunca saklanacaktı.\n\nDuru'nun grubu önce düz bir karton parçasını desteklerin üzerine koydu. İlk küçük yükte karton aşağı doğru eğildi. Kerem hemen ikinci bir karton eklemek istedi. Duru ise önce ilk denemenin sonucunu kaydetmelerini önerdi. Yeni malzeme eklediklerinde ne kadar fark oluştuğunu ancak böyle anlayabilirlerdi. Defterde kullanılan karton miktarı, destekler arasındaki uzaklık ve yükün konduğu yer için ayrı satırlar açtılar.\n\nSonraki denemede aynı miktarda kartonu katlayarak kullandılar. Katların biçimi değişince köprünün görünümü de değişti. Ancak yükün yerini önceki denemeden farklı seçmişlerdi. Sonuç daha iyi görünse de bunun yalnız katlama biçiminden kaynaklandığını söyleyemediler. Öğretmenleri, bir karşılaştırmada koşulları mümkün olduğunca aynı tutmanın neden önemli olduğunu sordu. Öğrenciler ikinci tasarımı aynı noktadan yükleyerek yeniden denedi.\n\nDiğer grup kartondan üçgen destekler hazırlıyordu. Kerem onların tasarımına bakınca kendi köprülerinin yanlış olduğunu düşündü. Duru, farklı bir tasarımın görülmesinin ilk çalışmayı değersiz yapmadığını söyledi. İki grup farklı yöntemlerle aynı soruna yaklaşıyordu. Sonuçları karşılaştırmak için malzeme miktarının ve deneme düzeninin açık olması gerekiyordu. Yalnız köprülerin fotoğraflarına bakarak hangisinin daha başarılı olduğunu anlamak mümkün değildi.\n\nÜçüncü tasarım hazırlanırken grubun içinde bir anlaşmazlık çıktı. Kerem, çok sayıda ince katın daha iyi olacağını savunuyordu. Duru ise az sayıda geniş katı denemek istiyordu. Tartışmayı tahminlerle sürdürmek yerine iki küçük örnek hazırladılar. Her örnek için aynı tür karton kullandılar ve yükü aynı noktaya koydular. Sonuçlardan biri beklentilerine uymadı. Bu şaşırtıcı farkı gizlemek yerine nedenini incelemek için işaretlediler.\n\nÖrneklerden birinin kenarı önceden yıpranmıştı. Ayrıca katlardan biri diğerlerine göre daha gevşekti. Çocuklar bu ayrıntıların sonucu etkileyebileceğini düşündü. Denemeyi yenilediklerinde fark azaldı. Böylece tek bir ölçümün her zaman yeterli olmadığını gördüler. Yine de sınıftaki küçük deneyden bütün köprüler için geçerli bir kural çıkaramazlardı. Kullandıkları malzeme, boyut ve yükler gerçek köprülerden çok farklıydı.\n\nMaket çalışmasının amacı yalnız en çok ağırlığı taşıyan köprüyü seçmek değildi. Malzeme miktarını ve yükün konduğu yeri kaydetmek, tasarım değişikliklerini karşılaştırmayı mümkün kılıyordu. Gruplar bir sonucun nedenini açıklayabilmek için görünüşten çok deneme koşullarına ve kayıtlarına bakmaları gerektiğini gördü.",
    "wordCount": 367,
    "source": "../../guided-reading/v1/texts/child-5-bridge.txt",
    "sourceSha256": "23eab41f3b9508c43da5368b86518b863d07de672d84f74164cce2d24a7a21aa",
    "questions": [
      {
        "id": "42fb3a27-f16b-5702-8447-2e27c646d1d0",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Bir köprü resminin satış fiyatını belirlemek",
        "optionB": "Köprü maketlerini kayıtlı ve karşılaştırılabilir denemelerle geliştirmek",
        "optionC": "En hızlı yürüyen kişiyi ödüllendirmek",
        "optionD": "Bütün köprülerin aynı biçimde yapılmasını zorunlu tutmak",
        "correctAnswer": "B",
        "evidence": "Maket çalışmasının amacı yalnız en çok ağırlığı taşıyan köprüyü seçmek değildi. Malzeme miktarını ve yükün konduğu yeri kaydetmek, tasarım değişikliklerini karşılaştırmayı mümkün kılıyordu. Gruplar bir sonucun nedenini açıklayabilmek için görünüşten çok deneme koşullarına ve kayıtlarına bakmaları gerektiğini gördü.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "a0222b8a-441d-5e8c-85a7-7fcc84854177",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Tek bir yük denemesi her tasarım için kesin sonuç verir",
        "optionB": "Maketin güzel görünmesi taşıma gücünü belirlemeye yeterlidir",
        "optionC": "Tasarım kararları, koşulları açık ve karşılaştırılabilir denemelerle değerlendirilmelidir",
        "optionD": "Malzeme miktarı ve yük konumu denemeyi etkilemez",
        "correctAnswer": "C",
        "evidence": "Maket çalışmasının amacı yalnız en çok ağırlığı taşıyan köprüyü seçmek değildi. Malzeme miktarını ve yükün konduğu yeri kaydetmek, tasarım değişikliklerini karşılaştırmayı mümkün kılıyordu. Gruplar bir sonucun nedenini açıklayabilmek için görünüşten çok deneme koşullarına ve kayıtlarına bakmaları gerektiğini gördü.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "b6dd330d-5003-5580-8b9a-4b4ca6045ea8",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Bir maketin gerçek köprüler için kesin güvenlik kanıtı olduğunu göstermek",
        "optionB": "Deneme koşullarını kaydetmeyi gereksiz göstermek",
        "optionC": "Maketleri yalnız görünüşlerine göre sıralamak",
        "optionD": "Tasarım denemelerinde koşulları ve kayıtları karşılaştırmanın önemini anlatmak",
        "correctAnswer": "D",
        "evidence": "Maket çalışmasının amacı yalnız en çok ağırlığı taşıyan köprüyü seçmek değildi. Malzeme miktarını ve yükün konduğu yeri kaydetmek, tasarım değişikliklerini karşılaştırmayı mümkün kılıyordu. Gruplar bir sonucun nedenini açıklayabilmek için görünüşten çok deneme koşullarına ve kayıtlarına bakmaları gerektiğini gördü.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "58534148-ff0b-51a5-8799-466e03ab3565",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Köprü maketinde karşılaştırmalı denemeler",
        "optionB": "Yalnız Görünüşe Göre Karar",
        "optionC": "Tek Kişinin Köprüsü",
        "optionD": "En Hızlı Geçiş Yarışı",
        "correctAnswer": "A",
        "evidence": "Maket çalışmasının amacı yalnız en çok ağırlığı taşıyan köprüyü seçmek değildi. Malzeme miktarını ve yükün konduğu yeri kaydetmek, tasarım değişikliklerini karşılaştırmayı mümkün kılıyordu. Gruplar bir sonucun nedenini açıklayabilmek için görünüşten çok deneme koşullarına ve kayıtlarına bakmaları gerektiğini gördü.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "a0da61c2-08f2-5d80-84b8-14aa95093b4b",
    "exerciseId": "f72341f8-d778-5065-a7a4-687a1567b547",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "age": "teen",
    "level": 4,
    "title": "Gözlemden ortak karara — Seviye 4",
    "content": "Okulun öğrenci meclisi, sabah girişinde oluşan kalabalığı incelemek için bir çalışma grubu kurdu. İlk toplantıda herkes farklı bir çözüm önerdi. Bazıları kapının genişletilmesini, bazıları servislerin başka bir noktada durmasını istiyordu. Ancak önerilerin çoğu kişisel izlenimlere dayanıyordu. Grup, bir çözümü savunmadan önce sorunun hangi saatlerde ve nerelerde ortaya çıktığını anlamaya karar verdi. Öğretmenlerinin gözetiminde, yalnızca güvenli noktalardan ve kimlik bilgisi toplamadan gözlem yapacaklardı. Araştırmanın sınırlarını baştan yazmaları, sonraki kararları daha açık tartışmalarına yardımcı oldu.\n\nİlk hafta gözlemler üç farklı sabah yapıldı. Öğrenciler gelen kişilerin adlarını veya fotoğraflarını kaydetmedi. Bunun yerine belirli aralıklarla giriş önündeki yaklaşık yoğunluğu not etti. Havanın yağışlı olup olmadığını, bir servis aracının geç kalıp kalmadığını ve girişte özel bir etkinlik bulunup bulunmadığını da yazdılar. Böylece yalnız kişi sayısına bakılarak anlaşılmayabilecek durumları kayda geçirdiler. Bir ölçümün yanında koşulların bulunması, sayının tek başına her şeyi açıklamadığını gösteriyordu. Grup, eksik kalan gözlemleri tahmin ederek doldurmadı; bunları açıkça boş bıraktı.\n\nVeriler bir tabloya aktarıldığında ilk bakışta belirgin bir sonuç ortaya çıktı. En yoğun saat aralığı üç günün ikisinde aynıydı. Fakat üçüncü gün tablo farklı görünüyordu. Bir öğrenci o günü hatalı sayıp çıkarmayı önerdi. Diğerleri, farklılığın gerçekten önemli bir durumu göstermiş olabileceğini düşündü. Notları yeniden incelediklerinde o sabah bir yol çalışması nedeniyle servislerin farklı zamanda geldiğini gördüler. Bu kayıt silinmek yerine ayrı bir açıklamayla korundu. Normal koşullar ile beklenmeyen koşulların birlikte görülmesi, önerilecek düzenlemenin farklı günlerde nasıl işleyeceği hakkında yeni sorular doğurdu.\n\nGrup daha sonra okulun çevresine ilişkin yayımlanmış ulaşım bilgilerini incelemek istedi. Buldukları haritanın üzerinde güncelleme tarihi vardı, fakat bazı duraklar eski adlarıyla görünüyordu. Öğrenciler kaynağı kullanmadan önce hangi kısmının güncel olduğunu kontrol etti. İnternette kolayca bulunan bir bilgi, sırf bir haritada yer aldığı için doğru kabul edilemezdi. Özellikle güvenli ulaşım hakkında öneri hazırlanırken hatalı bir ayrıntı önemli sonuçlar doğurabilirdi. Çalışma grubunun hazırladığı belge bir yol güvenliği talimatı olmayacak; okul yönetiminin değerlendireceği gözlemleri ve soruları içerecekti.\n\nAnket hazırlama fikri gündeme geldiğinde yeni bir tartışma başladı. Öğrenciler herkesin ev adresini sorarak güzergâhları anlamayı düşündü. Öğretmenleri bunun amaç için gereksiz ayrıntı olabileceğini açıkladı. Bunun yerine yürüyerek, toplu taşımayla veya servisle gelenlerin genel deneyimleri sorulabilirdi. Yanıt vermek isteğe bağlı olmalı, kişisel bilgiler gereksiz yere toplanmamalıydı. Grup anketi kısalttı ve her sorunun neyi anlamaya yardım ettiğini yanına yazdı. Açık bir gerekçesi bulunmayan sorular çıkarıldı. Böylece daha çok bilgi toplamanın her zaman daha iyi araştırma anlamına gelmediğini fark ettiler.\n\nAnket sonuçlarıyla gözlemler aynı belgeye eklendiğinde iki veri türünün farklı şeyler anlattığı görüldü. Gözlemler belirli sabahların durumunu, anket ise yanıt verenlerin deneyimlerini gösteriyordu. Bunları aynı kesinlikte sonuçlar olarak sunmak doğru olmazdı. Örneğin ankete katılanların çoğu yürüyerek gelmişti; servis kullananların görüşleri daha az temsil ediliyordu. Öğrenciler bu sınırlılığı raporun başına yazdı. Sonuçları okulun tamamına ait değişmez bir gerçek gibi anlatmak yerine, mevcut çalışmanın kapsamını ve eksik kalan yönlerini açıkladılar. Belgenin anlaşılır olması için grafiklerin altına kısa açıklamalar eklediler.\n\nGrubun çalışması bir çözümü peşinen doğru ilan etmeye değil, sorunu koşullarıyla anlamaya dayanıyordu. Farklı günlerin kayıtlarını karşılaştırmak ve eksikleri açıkça belirtmek ortak kararları daha anlaşılır kıldı. Gözlem, kişileri sıralamak için değil uygulanabilir bir düzenleme tartışmak için kullanıldı.",
    "wordCount": 496,
    "source": "../../guided-reading/v1/texts/teen-5-route.txt",
    "sourceSha256": "4bbe18d1c6fe7783492d66cd84c5d4e2b8424a7dfa02b2e152a1c5958ddf2b18",
    "questions": [
      {
        "id": "184bebf9-1f71-5623-8e46-02abe1cd2763",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Okul girişindeki kalabalığı gözlemlerle incelemek",
        "optionB": "Öğrencileri giriş sırasına göre başarıyla sıralamak",
        "optionC": "Bir servis şirketinin reklamını hazırlamak",
        "optionD": "Okulun bütün ders saatlerini değiştirmek",
        "correctAnswer": "A",
        "evidence": "Grubun çalışması bir çözümü peşinen doğru ilan etmeye değil, sorunu koşullarıyla anlamaya dayanıyordu. Farklı günlerin kayıtlarını karşılaştırmak ve eksikleri açıkça belirtmek ortak kararları daha anlaşılır kıldı. Gözlem, kişileri sıralamak için değil uygulanabilir bir düzenleme tartışmak için kullanıldı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "953ec911-be46-58c3-8d92-86d35dc5e0a9",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "İlk kişisel izlenim bütün kararlar için yeterlidir",
        "optionB": "Sorunu koşullarıyla gözlemek, ortak çözüm kararlarını destekler",
        "optionC": "Farklı çıkan gözlemler açıklanmadan silinmelidir",
        "optionD": "Eksik değerler ölçülmüş gibi tahminle doldurulmalıdır",
        "correctAnswer": "B",
        "evidence": "Grubun çalışması bir çözümü peşinen doğru ilan etmeye değil, sorunu koşullarıyla anlamaya dayanıyordu. Farklı günlerin kayıtlarını karşılaştırmak ve eksikleri açıkça belirtmek ortak kararları daha anlaşılır kıldı. Gözlem, kişileri sıralamak için değil uygulanabilir bir düzenleme tartışmak için kullanıldı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "69aa4c92-bc17-5da3-83fa-cd7b8f1b83d4",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Tek bir kapı genişliğinin her sorunu çözdüğünü kanıtlamak",
        "optionB": "Öğrencilerin kimliklerini ve hareketlerini izlemek",
        "optionC": "Bir soruna çözüm ararken gözlem ve koşulların birlikte değerlendirilmesini göstermek",
        "optionD": "En kalabalık günü bütün günlerin değişmez örneği saymak",
        "correctAnswer": "C",
        "evidence": "Grubun çalışması bir çözümü peşinen doğru ilan etmeye değil, sorunu koşullarıyla anlamaya dayanıyordu. Farklı günlerin kayıtlarını karşılaştırmak ve eksikleri açıkça belirtmek ortak kararları daha anlaşılır kıldı. Gözlem, kişileri sıralamak için değil uygulanabilir bir düzenleme tartışmak için kullanıldı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "154d51d1-406e-5b1f-8ee6-ad558c61829c",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "İlk Tahmin Her Zaman Doğrudur",
        "optionB": "Giriş Sırasına Göre Başarı",
        "optionC": "Her Gün Aynı Kalabalık",
        "optionD": "Gözlemden ortak karara",
        "correctAnswer": "D",
        "evidence": "Grubun çalışması bir çözümü peşinen doğru ilan etmeye değil, sorunu koşullarıyla anlamaya dayanıyordu. Farklı günlerin kayıtlarını karşılaştırmak ve eksikleri açıkça belirtmek ortak kararları daha anlaşılır kıldı. Gözlem, kişileri sıralamak için değil uygulanabilir bir düzenleme tartışmak için kullanıldı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "6500f513-58fd-5a32-831f-2d6faf792475",
    "exerciseId": "f72341f8-d778-5065-a7a4-687a1567b547",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "age": "teen",
    "level": 4,
    "title": "Nesnelerin hikâyelerini araştırmak — Seviye 4",
    "content": "Kent kütüphanesinde düzenlenecek öğrenci sergisinin konusu gündelik nesnelerin hikâyeleriydi. Katılımcılardan eski bir eşya seçmeleri, nereden geldiğini araştırmaları ve ziyaretçilere kısa bir anlatı sunmaları istenmişti. Defne büyükannesinin kullandığı bir radyo, Aras ise ailesinin yıllardır sakladığı bir tamir kutusu üzerinde çalışacaktı. İkisi de ilk toplantıda nesneleri hakkında çok şey bildiğini düşünüyordu. Fakat sergi metnini yazmaya başladıklarında hatırlanan olaylar, kişisel yorumlar ve doğrulanabilir bilgiler arasında önemli farklar bulunduğunu gördüler. Her cümlenin dayanağı aynı türden değildi.\n\nDefne, radyonun satın alındığı yılı öğrenmek için büyükannesiyle konuştu. Büyükannesi belirli bir yaz tatilini hatırlıyor, fakat tam tarihi söyleyemiyordu. Radyonun üzerindeki model adı başka bir başlangıç noktası sağladı. Defne bulduğu farklı kaynaklarda benzer görünümlü cihazların birbirine karıştırıldığını fark etti. Bu nedenle yalnız fotoğraf benzerliğine dayanarak tarih vermedi. Model bilgisini ve hatırayı ayrı ayrı yazdı. Kesinleşmeyen noktalar için yaklaşık ifadeler kullandı. Sergiyi gezen birinin, anlatının hangi kısmında belirsizlik bulunduğunu anlayabilmesi onun için önemliydi.\n\nAras'ın tamir kutusunda eksik parçalar vardı. Kutunun ilk hâlini gösteren bir fotoğraf bulunmadığı için hangi parçanın ne zaman kaybolduğunu bilmiyordu. Babası, kutuyu farklı zamanlarda farklı işlerde kullandıklarını anlattı. Aras başlangıçta kutuyu bir mesleğin eksiksiz temsilcisi olarak sunmayı planlamıştı. Ancak aileye ait tek bir örnekten bütün bir meslek hakkında sonuç çıkaramayacağını anladı. Anlatısını daha dar bir çerçeveye yerleştirdi: Bu kutunun aile içindeki kullanım hikâyesini gösterecekti. Kapsamı küçültmek, metnin daha dürüst ve anlaşılır olmasını sağladı.\n\nSergi hazırlık ekibi nesnelerin yanına konacak açıklamalar için ortak bir uzunluk önerdi. Amaç bütün ziyaretçilerin ayrıntılı bir araştırma dosyası okumasını beklemek değildi. Kısa bir ana metin, isteyenler için ise daha uzun bir kaynak dosyası hazırlanacaktı. Defne ilk taslağındaki bazı cümleleri çıkarırken yalnız metni kısaltmaya çalışmadı. Hangi bilginin ziyaretçinin nesneyi anlamasına gerçekten yardım ettiğini düşündü. Dikkat çekici ama konuyla ilgisi zayıf ayrıntıları ayrı dosyaya taşıdı. Başlıkların ve paragraf düzeninin de anlatının anlaşılmasını etkilediğini gördü.\n\nTasarım aşamasında görseller için izin konusu gündeme geldi. Öğrenciler aile fotoğraflarını kullanmak istiyordu, fakat fotoğrafta görünen herkes sergiye katılmamıştı. Öğretmenleri, bir fotoğrafın ellerinde bulunmasının onu herkese açık şekilde sergilemek için tek başına yeterli olmadığını hatırlattı. İzin verilmeyen görseller kullanılmadı; bazı nesneler yeniden fotoğraflandı. Yeni çekimlerde yalnız sergilenecek eşya yer aldı. Kaynak ve izin bilgileri hazırlık dosyasına eklendi. Böylece ziyaretçi açısından basit görünen bir görsel seçiminin arkasında da dikkatli kararlar bulunabileceği anlaşıldı.\n\nİlk deneme sergisine başka sınıflardan öğrenciler çağrıldı. Defne, ziyaretçilerin radyoya bakarken sorduğu soruları not etti. Bazıları teknik ayrıntıları merak ederken bazıları radyonun ailede kimler tarafından kullanıldığını öğrenmek istiyordu. Aras'ın açıklaması ise araçların adlarını bilmeyen ziyaretçiler için zor görünüyordu. İki öğrenci metinlerini yeniden düzenledi. Bilinmeyen sözcükler kısa açıklamalarla desteklendi. Ancak metni kolaylaştırmak için doğrulanmış bir bilgiyi değiştirip daha heyecanlı bir hikâye oluşturmadılar. Anlaşılır anlatım ile bilgiyi çarpıtmak arasındaki ayrımı korumaya çalıştılar.\n\nSerginin değeri her nesne için kesin bir tarih bulmakta değil, anlatının nasıl oluştuğunu açıkça göstermekteydi. Hatıraları, yorumları ve doğrulanabilen bilgileri ayırmak ziyaretçinin anlatıyı daha dikkatli değerlendirmesini sağlıyordu. Bir nesnenin hikâyesi, kanıt ve belirsizlik birlikte görülebildiğinde daha anlaşılır hale geldi.",
    "wordCount": 470,
    "source": "../../guided-reading/v1/texts/teen-5-exhibition.txt",
    "sourceSha256": "6c1c8694a47c5514ddf23cf34b98a4e99e04dab84cabfc10cea90a1ad7caa033",
    "questions": [
      {
        "id": "85f2bf1d-1d09-5c92-88c4-c0a70edaeef3",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Radyoların bütün teknik parçalarını onarmak",
        "optionB": "Bir tamir kutusunun satış fiyatını hesaplamak",
        "optionC": "Gündelik nesneler hakkında dayanağı açık sergi anlatıları hazırlamak",
        "optionD": "En eski eşyayı getiren kişiyi seçmek",
        "correctAnswer": "C",
        "evidence": "Serginin değeri her nesne için kesin bir tarih bulmakta değil, anlatının nasıl oluştuğunu açıkça göstermekteydi. Hatıraları, yorumları ve doğrulanabilen bilgileri ayırmak ziyaretçinin anlatıyı daha dikkatli değerlendirmesini sağlıyordu. Bir nesnenin hikâyesi, kanıt ve belirsizlik birlikte görülebildiğinde daha anlaşılır hale geldi.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "6746f397-7093-5755-83a3-d0a4eedc819b",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Her hatıra kesin tarih ve eksiksiz kanıt sayılmalıdır",
        "optionB": "Tek bir aile eşyası bütün bir mesleği temsil etmeye yeter",
        "optionC": "Belirsiz bilgiler kesinmiş gibi yazılmalıdır",
        "optionD": "Sergi anlatısında bilgi, yorum ve belirsizlik açıkça ayrılmalıdır",
        "correctAnswer": "D",
        "evidence": "Serginin değeri her nesne için kesin bir tarih bulmakta değil, anlatının nasıl oluştuğunu açıkça göstermekteydi. Hatıraları, yorumları ve doğrulanabilen bilgileri ayırmak ziyaretçinin anlatıyı daha dikkatli değerlendirmesini sağlıyordu. Bir nesnenin hikâyesi, kanıt ve belirsizlik birlikte görülebildiğinde daha anlaşılır hale geldi.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "b767edb2-65dc-5c42-8d9f-917b4ab00fa6",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Bir nesnenin hikâyesini aktarırken bilgi ve belirsizliği ayırmanın önemini göstermek",
        "optionB": "Nesnelerin yalnız ekonomik değerini karşılaştırmak",
        "optionC": "Görünüş benzerliğinin kesin kanıt olduğunu savunmak",
        "optionD": "Katılımcıları eşyalarının yaşına göre sıralamak",
        "correctAnswer": "A",
        "evidence": "Serginin değeri her nesne için kesin bir tarih bulmakta değil, anlatının nasıl oluştuğunu açıkça göstermekteydi. Hatıraları, yorumları ve doğrulanabilen bilgileri ayırmak ziyaretçinin anlatıyı daha dikkatli değerlendirmesini sağlıyordu. Bir nesnenin hikâyesi, kanıt ve belirsizlik birlikte görülebildiğinde daha anlaşılır hale geldi.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "ce7cbd68-e656-5149-8a73-02ad85144025",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "En Pahalı Eşya Yarışı",
        "optionB": "Nesnelerin hikâyelerini araştırmak",
        "optionC": "Her Hatıra Kesin Kanıttır",
        "optionD": "Yalnız Görünüş Yeter",
        "correctAnswer": "B",
        "evidence": "Serginin değeri her nesne için kesin bir tarih bulmakta değil, anlatının nasıl oluştuğunu açıkça göstermekteydi. Hatıraları, yorumları ve doğrulanabilen bilgileri ayırmak ziyaretçinin anlatıyı daha dikkatli değerlendirmesini sağlıyordu. Bir nesnenin hikâyesi, kanıt ve belirsizlik birlikte görülebildiğinde daha anlaşılır hale geldi.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "38576dd5-71b2-52a1-89fd-d2d340395c08",
    "exerciseId": "f72341f8-d778-5065-a7a4-687a1567b547",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "age": "teen",
    "level": 4,
    "title": "Köprü maketinde karşılaştırmalı denemeler — Seviye 4",
    "content": "Bilim kulübündeki öğrenciler, küçük bir köprü maketi tasarlamak için iki gruba ayrıldı. Masanın iki ucuna yerleştirilen desteklerin arasındaki boşluk herkes için aynıydı. Kullanılabilecek karton miktarı da sınırlıydı. Öğretmen, yarışmanın yalnız en çok ağırlığı taşıyan köprüyü bulmak olmadığını açıkladı. Gruplar, hangi değişikliğin neyi etkilediğini de anlatmalıydı. Bu nedenle çizimler, deneme sonuçları ve başarısız tasarımlar çalışma boyunca saklanacaktı.\n\nDuru'nun grubu önce düz bir karton parçasını desteklerin üzerine koydu. İlk küçük yükte karton aşağı doğru eğildi. Kerem hemen ikinci bir karton eklemek istedi. Duru ise önce ilk denemenin sonucunu kaydetmelerini önerdi. Yeni malzeme eklediklerinde ne kadar fark oluştuğunu ancak böyle anlayabilirlerdi. Defterde kullanılan karton miktarı, destekler arasındaki uzaklık ve yükün konduğu yer için ayrı satırlar açtılar.\n\nSonraki denemede aynı miktarda kartonu katlayarak kullandılar. Katların biçimi değişince köprünün görünümü de değişti. Ancak yükün yerini önceki denemeden farklı seçmişlerdi. Sonuç daha iyi görünse de bunun yalnız katlama biçiminden kaynaklandığını söyleyemediler. Öğretmenleri, bir karşılaştırmada koşulları mümkün olduğunca aynı tutmanın neden önemli olduğunu sordu. Öğrenciler ikinci tasarımı aynı noktadan yükleyerek yeniden denedi.\n\nDiğer grup kartondan üçgen destekler hazırlıyordu. Kerem onların tasarımına bakınca kendi köprülerinin yanlış olduğunu düşündü. Duru, farklı bir tasarımın görülmesinin ilk çalışmayı değersiz yapmadığını söyledi. İki grup farklı yöntemlerle aynı soruna yaklaşıyordu. Sonuçları karşılaştırmak için malzeme miktarının ve deneme düzeninin açık olması gerekiyordu. Yalnız köprülerin fotoğraflarına bakarak hangisinin daha başarılı olduğunu anlamak mümkün değildi.\n\nÜçüncü tasarım hazırlanırken grubun içinde bir anlaşmazlık çıktı. Kerem, çok sayıda ince katın daha iyi olacağını savunuyordu. Duru ise az sayıda geniş katı denemek istiyordu. Tartışmayı tahminlerle sürdürmek yerine iki küçük örnek hazırladılar. Her örnek için aynı tür karton kullandılar ve yükü aynı noktaya koydular. Sonuçlardan biri beklentilerine uymadı. Bu şaşırtıcı farkı gizlemek yerine nedenini incelemek için işaretlediler.\n\nÖrneklerden birinin kenarı önceden yıpranmıştı. Ayrıca katlardan biri diğerlerine göre daha gevşekti. Çocuklar bu ayrıntıların sonucu etkileyebileceğini düşündü. Denemeyi yenilediklerinde fark azaldı. Böylece tek bir ölçümün her zaman yeterli olmadığını gördüler. Yine de sınıftaki küçük deneyden bütün köprüler için geçerli bir kural çıkaramazlardı. Kullandıkları malzeme, boyut ve yükler gerçek köprülerden çok farklıydı.\n\nSunum günü her grup yalnız son maketini değil, çalışma defterini de getirdi. Duru, ilk başarısız köprünün fotoğrafını özellikle gösterdi. Bu deneme, sonraki soruların oluşmasını sağlamıştı. Kerem ise karşılaştırma sırasında değiştirdikleri koşulları anlattı. En yüksek yükü taşıyan tasarımın neden işe yaradığını henüz bütünüyle açıklayamadıklarını söylediler. Açık bıraktıkları soruları sunumun sonuna eklediler.\n\nSunumdan sonra öğrenciler birbirlerinin defterlerini inceledi. Bazı çizimlerde ölçülerin eksik olduğunu, bazılarında ise deneme tarihinin yazılmadığını gördüler. Her grup, kendi kaydını başka birinin anlayıp anlayamadığını kontrol etti. Duru bir çizime yükün yerini gösteren ok ekledi. Kerem de kullanılan kartonun daha önce katlanıp katlanmadığını belirten yeni bir sütun hazırladı.\n\nMaket çalışmasının amacı yalnız en çok ağırlığı taşıyan köprüyü seçmek değildi. Malzeme miktarını ve yükün konduğu yeri kaydetmek, tasarım değişikliklerini karşılaştırmayı mümkün kılıyordu. Gruplar bir sonucun nedenini açıklayabilmek için görünüşten çok deneme koşullarına ve kayıtlarına bakmaları gerektiğini gördü.",
    "wordCount": 465,
    "source": "../../guided-reading/v1/texts/child-5-bridge.txt",
    "sourceSha256": "23eab41f3b9508c43da5368b86518b863d07de672d84f74164cce2d24a7a21aa",
    "questions": [
      {
        "id": "fe777942-b0e0-560b-801a-11135eb608d3",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Köprü maketlerini kayıtlı ve karşılaştırılabilir denemelerle geliştirmek",
        "optionB": "Bir köprü resminin satış fiyatını belirlemek",
        "optionC": "En hızlı yürüyen kişiyi ödüllendirmek",
        "optionD": "Bütün köprülerin aynı biçimde yapılmasını zorunlu tutmak",
        "correctAnswer": "A",
        "evidence": "Maket çalışmasının amacı yalnız en çok ağırlığı taşıyan köprüyü seçmek değildi. Malzeme miktarını ve yükün konduğu yeri kaydetmek, tasarım değişikliklerini karşılaştırmayı mümkün kılıyordu. Gruplar bir sonucun nedenini açıklayabilmek için görünüşten çok deneme koşullarına ve kayıtlarına bakmaları gerektiğini gördü.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "4686ad1f-e5dc-5f99-8353-76f60c457d2a",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Tek bir yük denemesi her tasarım için kesin sonuç verir",
        "optionB": "Tasarım kararları, koşulları açık ve karşılaştırılabilir denemelerle değerlendirilmelidir",
        "optionC": "Maketin güzel görünmesi taşıma gücünü belirlemeye yeterlidir",
        "optionD": "Malzeme miktarı ve yük konumu denemeyi etkilemez",
        "correctAnswer": "B",
        "evidence": "Maket çalışmasının amacı yalnız en çok ağırlığı taşıyan köprüyü seçmek değildi. Malzeme miktarını ve yükün konduğu yeri kaydetmek, tasarım değişikliklerini karşılaştırmayı mümkün kılıyordu. Gruplar bir sonucun nedenini açıklayabilmek için görünüşten çok deneme koşullarına ve kayıtlarına bakmaları gerektiğini gördü.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "a6ac09f1-c36a-5e86-8792-d1a97a1aea08",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Bir maketin gerçek köprüler için kesin güvenlik kanıtı olduğunu göstermek",
        "optionB": "Deneme koşullarını kaydetmeyi gereksiz göstermek",
        "optionC": "Tasarım denemelerinde koşulları ve kayıtları karşılaştırmanın önemini anlatmak",
        "optionD": "Maketleri yalnız görünüşlerine göre sıralamak",
        "correctAnswer": "C",
        "evidence": "Maket çalışmasının amacı yalnız en çok ağırlığı taşıyan köprüyü seçmek değildi. Malzeme miktarını ve yükün konduğu yeri kaydetmek, tasarım değişikliklerini karşılaştırmayı mümkün kılıyordu. Gruplar bir sonucun nedenini açıklayabilmek için görünüşten çok deneme koşullarına ve kayıtlarına bakmaları gerektiğini gördü.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "a63c4d67-3f9c-5da3-832d-4a72923bd46a",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Yalnız Görünüşe Göre Karar",
        "optionB": "Tek Kişinin Köprüsü",
        "optionC": "En Hızlı Geçiş Yarışı",
        "optionD": "Köprü maketinde karşılaştırmalı denemeler",
        "correctAnswer": "D",
        "evidence": "Maket çalışmasının amacı yalnız en çok ağırlığı taşıyan köprüyü seçmek değildi. Malzeme miktarını ve yükün konduğu yeri kaydetmek, tasarım değişikliklerini karşılaştırmayı mümkün kılıyordu. Gruplar bir sonucun nedenini açıklayabilmek için görünüşten çok deneme koşullarına ve kayıtlarına bakmaları gerektiğini gördü.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "68888d2a-6c49-51ff-8c7d-4379b7d77f25",
    "exerciseId": "af019d2e-728c-585a-bb33-85615c0eaccf",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "age": "teen",
    "level": 5,
    "title": "Gözlemden ortak karara — Seviye 5",
    "content": "Okulun öğrenci meclisi, sabah girişinde oluşan kalabalığı incelemek için bir çalışma grubu kurdu. İlk toplantıda herkes farklı bir çözüm önerdi. Bazıları kapının genişletilmesini, bazıları servislerin başka bir noktada durmasını istiyordu. Ancak önerilerin çoğu kişisel izlenimlere dayanıyordu. Grup, bir çözümü savunmadan önce sorunun hangi saatlerde ve nerelerde ortaya çıktığını anlamaya karar verdi. Öğretmenlerinin gözetiminde, yalnızca güvenli noktalardan ve kimlik bilgisi toplamadan gözlem yapacaklardı. Araştırmanın sınırlarını baştan yazmaları, sonraki kararları daha açık tartışmalarına yardımcı oldu.\n\nİlk hafta gözlemler üç farklı sabah yapıldı. Öğrenciler gelen kişilerin adlarını veya fotoğraflarını kaydetmedi. Bunun yerine belirli aralıklarla giriş önündeki yaklaşık yoğunluğu not etti. Havanın yağışlı olup olmadığını, bir servis aracının geç kalıp kalmadığını ve girişte özel bir etkinlik bulunup bulunmadığını da yazdılar. Böylece yalnız kişi sayısına bakılarak anlaşılmayabilecek durumları kayda geçirdiler. Bir ölçümün yanında koşulların bulunması, sayının tek başına her şeyi açıklamadığını gösteriyordu. Grup, eksik kalan gözlemleri tahmin ederek doldurmadı; bunları açıkça boş bıraktı.\n\nVeriler bir tabloya aktarıldığında ilk bakışta belirgin bir sonuç ortaya çıktı. En yoğun saat aralığı üç günün ikisinde aynıydı. Fakat üçüncü gün tablo farklı görünüyordu. Bir öğrenci o günü hatalı sayıp çıkarmayı önerdi. Diğerleri, farklılığın gerçekten önemli bir durumu göstermiş olabileceğini düşündü. Notları yeniden incelediklerinde o sabah bir yol çalışması nedeniyle servislerin farklı zamanda geldiğini gördüler. Bu kayıt silinmek yerine ayrı bir açıklamayla korundu. Normal koşullar ile beklenmeyen koşulların birlikte görülmesi, önerilecek düzenlemenin farklı günlerde nasıl işleyeceği hakkında yeni sorular doğurdu.\n\nGrup daha sonra okulun çevresine ilişkin yayımlanmış ulaşım bilgilerini incelemek istedi. Buldukları haritanın üzerinde güncelleme tarihi vardı, fakat bazı duraklar eski adlarıyla görünüyordu. Öğrenciler kaynağı kullanmadan önce hangi kısmının güncel olduğunu kontrol etti. İnternette kolayca bulunan bir bilgi, sırf bir haritada yer aldığı için doğru kabul edilemezdi. Özellikle güvenli ulaşım hakkında öneri hazırlanırken hatalı bir ayrıntı önemli sonuçlar doğurabilirdi. Çalışma grubunun hazırladığı belge bir yol güvenliği talimatı olmayacak; okul yönetiminin değerlendireceği gözlemleri ve soruları içerecekti.\n\nAnket hazırlama fikri gündeme geldiğinde yeni bir tartışma başladı. Öğrenciler herkesin ev adresini sorarak güzergâhları anlamayı düşündü. Öğretmenleri bunun amaç için gereksiz ayrıntı olabileceğini açıkladı. Bunun yerine yürüyerek, toplu taşımayla veya servisle gelenlerin genel deneyimleri sorulabilirdi. Yanıt vermek isteğe bağlı olmalı, kişisel bilgiler gereksiz yere toplanmamalıydı. Grup anketi kısalttı ve her sorunun neyi anlamaya yardım ettiğini yanına yazdı. Açık bir gerekçesi bulunmayan sorular çıkarıldı. Böylece daha çok bilgi toplamanın her zaman daha iyi araştırma anlamına gelmediğini fark ettiler.\n\nAnket sonuçlarıyla gözlemler aynı belgeye eklendiğinde iki veri türünün farklı şeyler anlattığı görüldü. Gözlemler belirli sabahların durumunu, anket ise yanıt verenlerin deneyimlerini gösteriyordu. Bunları aynı kesinlikte sonuçlar olarak sunmak doğru olmazdı. Örneğin ankete katılanların çoğu yürüyerek gelmişti; servis kullananların görüşleri daha az temsil ediliyordu. Öğrenciler bu sınırlılığı raporun başına yazdı. Sonuçları okulun tamamına ait değişmez bir gerçek gibi anlatmak yerine, mevcut çalışmanın kapsamını ve eksik kalan yönlerini açıkladılar. Belgenin anlaşılır olması için grafiklerin altına kısa açıklamalar eklediler.\n\nÖneri bölümünde tek bir çözüm yerine iki küçük deneme seçeneği hazırlandı. Bunların uygulanabilirliği okul yönetimi ve ilgili görevliler tarafından değerlendirilecekti. Her seçenek için beklenen yarar, olası güçlük ve tekrar gözlenmesi gereken durum yazıldı. Öğrenciler hangi ölçütün başarı sayılacağını da tartıştı. Kalabalığın azalması kadar, girişin herkes için erişilebilir kalması ve güvenliğin korunması da önemliydi. Yalnız en hızlı geçişi sağlayan düzen, diğer ihtiyaçları karşılamıyorsa yeterli olmayabilirdi. Bu nedenle çözümün tek bir sayıyla değerlendirilmemesi gerektiğini raporlarına eklediler.\n\nGrubun çalışması bir çözümü peşinen doğru ilan etmeye değil, sorunu koşullarıyla anlamaya dayanıyordu. Farklı günlerin kayıtlarını karşılaştırmak ve eksikleri açıkça belirtmek ortak kararları daha anlaşılır kıldı. Gözlem, kişileri sıralamak için değil uygulanabilir bir düzenleme tartışmak için kullanıldı.",
    "wordCount": 570,
    "source": "../../guided-reading/v1/texts/teen-5-route.txt",
    "sourceSha256": "4bbe18d1c6fe7783492d66cd84c5d4e2b8424a7dfa02b2e152a1c5958ddf2b18",
    "questions": [
      {
        "id": "906d52f0-3b97-5ce4-818d-7dddfa2cdd86",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Okul girişindeki kalabalığı gözlemlerle incelemek",
        "optionB": "Öğrencileri giriş sırasına göre başarıyla sıralamak",
        "optionC": "Bir servis şirketinin reklamını hazırlamak",
        "optionD": "Okulun bütün ders saatlerini değiştirmek",
        "correctAnswer": "A",
        "evidence": "Grubun çalışması bir çözümü peşinen doğru ilan etmeye değil, sorunu koşullarıyla anlamaya dayanıyordu. Farklı günlerin kayıtlarını karşılaştırmak ve eksikleri açıkça belirtmek ortak kararları daha anlaşılır kıldı. Gözlem, kişileri sıralamak için değil uygulanabilir bir düzenleme tartışmak için kullanıldı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "a2464016-7296-57a8-83e0-93b6cd73788e",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "İlk kişisel izlenim bütün kararlar için yeterlidir",
        "optionB": "Sorunu koşullarıyla gözlemek, ortak çözüm kararlarını destekler",
        "optionC": "Farklı çıkan gözlemler açıklanmadan silinmelidir",
        "optionD": "Eksik değerler ölçülmüş gibi tahminle doldurulmalıdır",
        "correctAnswer": "B",
        "evidence": "Grubun çalışması bir çözümü peşinen doğru ilan etmeye değil, sorunu koşullarıyla anlamaya dayanıyordu. Farklı günlerin kayıtlarını karşılaştırmak ve eksikleri açıkça belirtmek ortak kararları daha anlaşılır kıldı. Gözlem, kişileri sıralamak için değil uygulanabilir bir düzenleme tartışmak için kullanıldı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "778bf9ac-d771-5e19-866b-139940d4f974",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Tek bir kapı genişliğinin her sorunu çözdüğünü kanıtlamak",
        "optionB": "Öğrencilerin kimliklerini ve hareketlerini izlemek",
        "optionC": "Bir soruna çözüm ararken gözlem ve koşulların birlikte değerlendirilmesini göstermek",
        "optionD": "En kalabalık günü bütün günlerin değişmez örneği saymak",
        "correctAnswer": "C",
        "evidence": "Grubun çalışması bir çözümü peşinen doğru ilan etmeye değil, sorunu koşullarıyla anlamaya dayanıyordu. Farklı günlerin kayıtlarını karşılaştırmak ve eksikleri açıkça belirtmek ortak kararları daha anlaşılır kıldı. Gözlem, kişileri sıralamak için değil uygulanabilir bir düzenleme tartışmak için kullanıldı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "e53fa5c3-4958-583b-8047-3e61d184b650",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "İlk Tahmin Her Zaman Doğrudur",
        "optionB": "Giriş Sırasına Göre Başarı",
        "optionC": "Her Gün Aynı Kalabalık",
        "optionD": "Gözlemden ortak karara",
        "correctAnswer": "D",
        "evidence": "Grubun çalışması bir çözümü peşinen doğru ilan etmeye değil, sorunu koşullarıyla anlamaya dayanıyordu. Farklı günlerin kayıtlarını karşılaştırmak ve eksikleri açıkça belirtmek ortak kararları daha anlaşılır kıldı. Gözlem, kişileri sıralamak için değil uygulanabilir bir düzenleme tartışmak için kullanıldı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "d39e6ca0-78f3-532d-8fcc-987d0383e376",
    "exerciseId": "af019d2e-728c-585a-bb33-85615c0eaccf",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "age": "teen",
    "level": 5,
    "title": "Nesnelerin hikâyelerini araştırmak — Seviye 5",
    "content": "Kent kütüphanesinde düzenlenecek öğrenci sergisinin konusu gündelik nesnelerin hikâyeleriydi. Katılımcılardan eski bir eşya seçmeleri, nereden geldiğini araştırmaları ve ziyaretçilere kısa bir anlatı sunmaları istenmişti. Defne büyükannesinin kullandığı bir radyo, Aras ise ailesinin yıllardır sakladığı bir tamir kutusu üzerinde çalışacaktı. İkisi de ilk toplantıda nesneleri hakkında çok şey bildiğini düşünüyordu. Fakat sergi metnini yazmaya başladıklarında hatırlanan olaylar, kişisel yorumlar ve doğrulanabilir bilgiler arasında önemli farklar bulunduğunu gördüler. Her cümlenin dayanağı aynı türden değildi.\n\nDefne, radyonun satın alındığı yılı öğrenmek için büyükannesiyle konuştu. Büyükannesi belirli bir yaz tatilini hatırlıyor, fakat tam tarihi söyleyemiyordu. Radyonun üzerindeki model adı başka bir başlangıç noktası sağladı. Defne bulduğu farklı kaynaklarda benzer görünümlü cihazların birbirine karıştırıldığını fark etti. Bu nedenle yalnız fotoğraf benzerliğine dayanarak tarih vermedi. Model bilgisini ve hatırayı ayrı ayrı yazdı. Kesinleşmeyen noktalar için yaklaşık ifadeler kullandı. Sergiyi gezen birinin, anlatının hangi kısmında belirsizlik bulunduğunu anlayabilmesi onun için önemliydi.\n\nAras'ın tamir kutusunda eksik parçalar vardı. Kutunun ilk hâlini gösteren bir fotoğraf bulunmadığı için hangi parçanın ne zaman kaybolduğunu bilmiyordu. Babası, kutuyu farklı zamanlarda farklı işlerde kullandıklarını anlattı. Aras başlangıçta kutuyu bir mesleğin eksiksiz temsilcisi olarak sunmayı planlamıştı. Ancak aileye ait tek bir örnekten bütün bir meslek hakkında sonuç çıkaramayacağını anladı. Anlatısını daha dar bir çerçeveye yerleştirdi: Bu kutunun aile içindeki kullanım hikâyesini gösterecekti. Kapsamı küçültmek, metnin daha dürüst ve anlaşılır olmasını sağladı.\n\nSergi hazırlık ekibi nesnelerin yanına konacak açıklamalar için ortak bir uzunluk önerdi. Amaç bütün ziyaretçilerin ayrıntılı bir araştırma dosyası okumasını beklemek değildi. Kısa bir ana metin, isteyenler için ise daha uzun bir kaynak dosyası hazırlanacaktı. Defne ilk taslağındaki bazı cümleleri çıkarırken yalnız metni kısaltmaya çalışmadı. Hangi bilginin ziyaretçinin nesneyi anlamasına gerçekten yardım ettiğini düşündü. Dikkat çekici ama konuyla ilgisi zayıf ayrıntıları ayrı dosyaya taşıdı. Başlıkların ve paragraf düzeninin de anlatının anlaşılmasını etkilediğini gördü.\n\nTasarım aşamasında görseller için izin konusu gündeme geldi. Öğrenciler aile fotoğraflarını kullanmak istiyordu, fakat fotoğrafta görünen herkes sergiye katılmamıştı. Öğretmenleri, bir fotoğrafın ellerinde bulunmasının onu herkese açık şekilde sergilemek için tek başına yeterli olmadığını hatırlattı. İzin verilmeyen görseller kullanılmadı; bazı nesneler yeniden fotoğraflandı. Yeni çekimlerde yalnız sergilenecek eşya yer aldı. Kaynak ve izin bilgileri hazırlık dosyasına eklendi. Böylece ziyaretçi açısından basit görünen bir görsel seçiminin arkasında da dikkatli kararlar bulunabileceği anlaşıldı.\n\nİlk deneme sergisine başka sınıflardan öğrenciler çağrıldı. Defne, ziyaretçilerin radyoya bakarken sorduğu soruları not etti. Bazıları teknik ayrıntıları merak ederken bazıları radyonun ailede kimler tarafından kullanıldığını öğrenmek istiyordu. Aras'ın açıklaması ise araçların adlarını bilmeyen ziyaretçiler için zor görünüyordu. İki öğrenci metinlerini yeniden düzenledi. Bilinmeyen sözcükler kısa açıklamalarla desteklendi. Ancak metni kolaylaştırmak için doğrulanmış bir bilgiyi değiştirip daha heyecanlı bir hikâye oluşturmadılar. Anlaşılır anlatım ile bilgiyi çarpıtmak arasındaki ayrımı korumaya çalıştılar.\n\nDeneme sırasında bir ziyaretçi, radyonun üzerinde yazan modelin açıklama dosyasındaki modelden farklı olduğunu söyledi. Defne ilk anda bütün araştırmasının yanlış olduğunu düşündü. Sonra kaynaklara geri dönerek küçük bir yazım hatası yaptığını buldu. Düzeltmeyi yalnız sergi kartında değil, kaynak dosyasında da yaptı. Değişikliğin nedenini not etti. Hatanın fark edilmesi, çalışmanın bütünüyle değersiz olduğu anlamına gelmiyordu. Kontrol edilebilir bir kayıt tuttuğu için hangi bilginin değişmesi gerektiğini bulabilmişti. Sonraki kontrollerde model adlarının yazımına daha fazla dikkat etti.\n\nSerginin açılışından önce ekip bir son kontrol listesi hazırladı. Nesnelerin güvenli şekilde yerleştirilmesi, yazıların rahat okunması ve dolaşım alanının açık kalması bu listede yer aldı. Eski radyonun çalıştırılması düşünülmedi; güvenliği uzman tarafından değerlendirilmemiş bir cihazı denemek serginin amacı değildi. Aras'ın kutusundaki araçlar da ziyaretçilerin gelişigüzel eline alabileceği biçimde bırakılmadı. Anlatının ilgi çekici olması kadar, insanların sergiyi güvenli ve erişilebilir biçimde gezebilmesi önemliydi. Ekip, tasarımın farklı ihtiyaçları birlikte gözetmesi gerektiğini konuştu.\n\nSerginin değeri her nesne için kesin bir tarih bulmakta değil, anlatının nasıl oluştuğunu açıkça göstermekteydi. Hatıraları, yorumları ve doğrulanabilen bilgileri ayırmak ziyaretçinin anlatıyı daha dikkatli değerlendirmesini sağlıyordu. Bir nesnenin hikâyesi, kanıt ve belirsizlik birlikte görülebildiğinde daha anlaşılır hale geldi.",
    "wordCount": 614,
    "source": "../../guided-reading/v1/texts/teen-5-exhibition.txt",
    "sourceSha256": "6c1c8694a47c5514ddf23cf34b98a4e99e04dab84cabfc10cea90a1ad7caa033",
    "questions": [
      {
        "id": "438464bd-739f-50ff-84d1-74615bfa8a0f",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Radyoların bütün teknik parçalarını onarmak",
        "optionB": "Bir tamir kutusunun satış fiyatını hesaplamak",
        "optionC": "En eski eşyayı getiren kişiyi seçmek",
        "optionD": "Gündelik nesneler hakkında dayanağı açık sergi anlatıları hazırlamak",
        "correctAnswer": "D",
        "evidence": "Serginin değeri her nesne için kesin bir tarih bulmakta değil, anlatının nasıl oluştuğunu açıkça göstermekteydi. Hatıraları, yorumları ve doğrulanabilen bilgileri ayırmak ziyaretçinin anlatıyı daha dikkatli değerlendirmesini sağlıyordu. Bir nesnenin hikâyesi, kanıt ve belirsizlik birlikte görülebildiğinde daha anlaşılır hale geldi.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "8c00ca3e-1418-5aef-867e-b6a08a59f32c",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Sergi anlatısında bilgi, yorum ve belirsizlik açıkça ayrılmalıdır",
        "optionB": "Her hatıra kesin tarih ve eksiksiz kanıt sayılmalıdır",
        "optionC": "Tek bir aile eşyası bütün bir mesleği temsil etmeye yeter",
        "optionD": "Belirsiz bilgiler kesinmiş gibi yazılmalıdır",
        "correctAnswer": "A",
        "evidence": "Serginin değeri her nesne için kesin bir tarih bulmakta değil, anlatının nasıl oluştuğunu açıkça göstermekteydi. Hatıraları, yorumları ve doğrulanabilen bilgileri ayırmak ziyaretçinin anlatıyı daha dikkatli değerlendirmesini sağlıyordu. Bir nesnenin hikâyesi, kanıt ve belirsizlik birlikte görülebildiğinde daha anlaşılır hale geldi.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "94fb92bf-7a3c-5dd7-89d3-d1c8b26c4636",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Nesnelerin yalnız ekonomik değerini karşılaştırmak",
        "optionB": "Bir nesnenin hikâyesini aktarırken bilgi ve belirsizliği ayırmanın önemini göstermek",
        "optionC": "Görünüş benzerliğinin kesin kanıt olduğunu savunmak",
        "optionD": "Katılımcıları eşyalarının yaşına göre sıralamak",
        "correctAnswer": "B",
        "evidence": "Serginin değeri her nesne için kesin bir tarih bulmakta değil, anlatının nasıl oluştuğunu açıkça göstermekteydi. Hatıraları, yorumları ve doğrulanabilen bilgileri ayırmak ziyaretçinin anlatıyı daha dikkatli değerlendirmesini sağlıyordu. Bir nesnenin hikâyesi, kanıt ve belirsizlik birlikte görülebildiğinde daha anlaşılır hale geldi.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "7170d4eb-dd20-586a-8da1-056250cc170a",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "En Pahalı Eşya Yarışı",
        "optionB": "Her Hatıra Kesin Kanıttır",
        "optionC": "Nesnelerin hikâyelerini araştırmak",
        "optionD": "Yalnız Görünüş Yeter",
        "correctAnswer": "C",
        "evidence": "Serginin değeri her nesne için kesin bir tarih bulmakta değil, anlatının nasıl oluştuğunu açıkça göstermekteydi. Hatıraları, yorumları ve doğrulanabilen bilgileri ayırmak ziyaretçinin anlatıyı daha dikkatli değerlendirmesini sağlıyordu. Bir nesnenin hikâyesi, kanıt ve belirsizlik birlikte görülebildiğinde daha anlaşılır hale geldi.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "14737ace-77f3-58f3-8ff8-8b441e71cc1a",
    "exerciseId": "af019d2e-728c-585a-bb33-85615c0eaccf",
    "ageGroupId": "10000000-0000-0000-0000-000000000002",
    "age": "teen",
    "level": 5,
    "title": "Köprü maketinde karşılaştırmalı denemeler — Seviye 5",
    "content": "Bilim kulübündeki öğrenciler, küçük bir köprü maketi tasarlamak için iki gruba ayrıldı. Masanın iki ucuna yerleştirilen desteklerin arasındaki boşluk herkes için aynıydı. Kullanılabilecek karton miktarı da sınırlıydı. Öğretmen, yarışmanın yalnız en çok ağırlığı taşıyan köprüyü bulmak olmadığını açıkladı. Gruplar, hangi değişikliğin neyi etkilediğini de anlatmalıydı. Bu nedenle çizimler, deneme sonuçları ve başarısız tasarımlar çalışma boyunca saklanacaktı.\n\nDuru'nun grubu önce düz bir karton parçasını desteklerin üzerine koydu. İlk küçük yükte karton aşağı doğru eğildi. Kerem hemen ikinci bir karton eklemek istedi. Duru ise önce ilk denemenin sonucunu kaydetmelerini önerdi. Yeni malzeme eklediklerinde ne kadar fark oluştuğunu ancak böyle anlayabilirlerdi. Defterde kullanılan karton miktarı, destekler arasındaki uzaklık ve yükün konduğu yer için ayrı satırlar açtılar.\n\nSonraki denemede aynı miktarda kartonu katlayarak kullandılar. Katların biçimi değişince köprünün görünümü de değişti. Ancak yükün yerini önceki denemeden farklı seçmişlerdi. Sonuç daha iyi görünse de bunun yalnız katlama biçiminden kaynaklandığını söyleyemediler. Öğretmenleri, bir karşılaştırmada koşulları mümkün olduğunca aynı tutmanın neden önemli olduğunu sordu. Öğrenciler ikinci tasarımı aynı noktadan yükleyerek yeniden denedi.\n\nDiğer grup kartondan üçgen destekler hazırlıyordu. Kerem onların tasarımına bakınca kendi köprülerinin yanlış olduğunu düşündü. Duru, farklı bir tasarımın görülmesinin ilk çalışmayı değersiz yapmadığını söyledi. İki grup farklı yöntemlerle aynı soruna yaklaşıyordu. Sonuçları karşılaştırmak için malzeme miktarının ve deneme düzeninin açık olması gerekiyordu. Yalnız köprülerin fotoğraflarına bakarak hangisinin daha başarılı olduğunu anlamak mümkün değildi.\n\nÜçüncü tasarım hazırlanırken grubun içinde bir anlaşmazlık çıktı. Kerem, çok sayıda ince katın daha iyi olacağını savunuyordu. Duru ise az sayıda geniş katı denemek istiyordu. Tartışmayı tahminlerle sürdürmek yerine iki küçük örnek hazırladılar. Her örnek için aynı tür karton kullandılar ve yükü aynı noktaya koydular. Sonuçlardan biri beklentilerine uymadı. Bu şaşırtıcı farkı gizlemek yerine nedenini incelemek için işaretlediler.\n\nÖrneklerden birinin kenarı önceden yıpranmıştı. Ayrıca katlardan biri diğerlerine göre daha gevşekti. Çocuklar bu ayrıntıların sonucu etkileyebileceğini düşündü. Denemeyi yenilediklerinde fark azaldı. Böylece tek bir ölçümün her zaman yeterli olmadığını gördüler. Yine de sınıftaki küçük deneyden bütün köprüler için geçerli bir kural çıkaramazlardı. Kullandıkları malzeme, boyut ve yükler gerçek köprülerden çok farklıydı.\n\nSunum günü her grup yalnız son maketini değil, çalışma defterini de getirdi. Duru, ilk başarısız köprünün fotoğrafını özellikle gösterdi. Bu deneme, sonraki soruların oluşmasını sağlamıştı. Kerem ise karşılaştırma sırasında değiştirdikleri koşulları anlattı. En yüksek yükü taşıyan tasarımın neden işe yaradığını henüz bütünüyle açıklayamadıklarını söylediler. Açık bıraktıkları soruları sunumun sonuna eklediler.\n\nSunumdan sonra öğrenciler birbirlerinin defterlerini inceledi. Bazı çizimlerde ölçülerin eksik olduğunu, bazılarında ise deneme tarihinin yazılmadığını gördüler. Her grup, kendi kaydını başka birinin anlayıp anlayamadığını kontrol etti. Duru bir çizime yükün yerini gösteren ok ekledi. Kerem de kullanılan kartonun daha önce katlanıp katlanmadığını belirten yeni bir sütun hazırladı.\n\nKulüp çalışması sona erdiğinde masanın üzerinde birbirinden farklı köprüler vardı. Bazıları sağlam, bazıları hafif, bazıları ise kolay kuruluyordu. Öğrenciler bir tasarımı değerlendirirken tek bir özelliğe bakmanın yetersiz kalabileceğini fark etti. Bir amacın sınırlarını belirlemek, denemeyi doğru kurmak ve sonuçları dürüstçe anlatmak da yapılan işin parçasıydı. Duru defterini kapatmadan önce yeni bir soru yazdı: Aynı yöntem başka bir malzemeyle denenseydi sonuç nasıl değişirdi?\n\nMaket çalışmasının amacı yalnız en çok ağırlığı taşıyan köprüyü seçmek değildi. Malzeme miktarını ve yükün konduğu yeri kaydetmek, tasarım değişikliklerini karşılaştırmayı mümkün kılıyordu. Gruplar bir sonucun nedenini açıklayabilmek için görünüşten çok deneme koşullarına ve kayıtlarına bakmaları gerektiğini gördü.",
    "wordCount": 527,
    "source": "../../guided-reading/v1/texts/child-5-bridge.txt",
    "sourceSha256": "23eab41f3b9508c43da5368b86518b863d07de672d84f74164cce2d24a7a21aa",
    "questions": [
      {
        "id": "04a557c9-5446-52da-8007-4e21049cd05e",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Köprü maketlerini kayıtlı ve karşılaştırılabilir denemelerle geliştirmek",
        "optionB": "Bir köprü resminin satış fiyatını belirlemek",
        "optionC": "En hızlı yürüyen kişiyi ödüllendirmek",
        "optionD": "Bütün köprülerin aynı biçimde yapılmasını zorunlu tutmak",
        "correctAnswer": "A",
        "evidence": "Maket çalışmasının amacı yalnız en çok ağırlığı taşıyan köprüyü seçmek değildi. Malzeme miktarını ve yükün konduğu yeri kaydetmek, tasarım değişikliklerini karşılaştırmayı mümkün kılıyordu. Gruplar bir sonucun nedenini açıklayabilmek için görünüşten çok deneme koşullarına ve kayıtlarına bakmaları gerektiğini gördü.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "f578828c-0da6-5267-8bd7-26d760d8ea59",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Tek bir yük denemesi her tasarım için kesin sonuç verir",
        "optionB": "Tasarım kararları, koşulları açık ve karşılaştırılabilir denemelerle değerlendirilmelidir",
        "optionC": "Maketin güzel görünmesi taşıma gücünü belirlemeye yeterlidir",
        "optionD": "Malzeme miktarı ve yük konumu denemeyi etkilemez",
        "correctAnswer": "B",
        "evidence": "Maket çalışmasının amacı yalnız en çok ağırlığı taşıyan köprüyü seçmek değildi. Malzeme miktarını ve yükün konduğu yeri kaydetmek, tasarım değişikliklerini karşılaştırmayı mümkün kılıyordu. Gruplar bir sonucun nedenini açıklayabilmek için görünüşten çok deneme koşullarına ve kayıtlarına bakmaları gerektiğini gördü.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "d792f10a-ddf2-5037-8aa8-275015878305",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Bir maketin gerçek köprüler için kesin güvenlik kanıtı olduğunu göstermek",
        "optionB": "Deneme koşullarını kaydetmeyi gereksiz göstermek",
        "optionC": "Tasarım denemelerinde koşulları ve kayıtları karşılaştırmanın önemini anlatmak",
        "optionD": "Maketleri yalnız görünüşlerine göre sıralamak",
        "correctAnswer": "C",
        "evidence": "Maket çalışmasının amacı yalnız en çok ağırlığı taşıyan köprüyü seçmek değildi. Malzeme miktarını ve yükün konduğu yeri kaydetmek, tasarım değişikliklerini karşılaştırmayı mümkün kılıyordu. Gruplar bir sonucun nedenini açıklayabilmek için görünüşten çok deneme koşullarına ve kayıtlarına bakmaları gerektiğini gördü.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "d30745d3-65fe-5615-896a-b62f6db4cee6",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Yalnız Görünüşe Göre Karar",
        "optionB": "Tek Kişinin Köprüsü",
        "optionC": "En Hızlı Geçiş Yarışı",
        "optionD": "Köprü maketinde karşılaştırmalı denemeler",
        "correctAnswer": "D",
        "evidence": "Maket çalışmasının amacı yalnız en çok ağırlığı taşıyan köprüyü seçmek değildi. Malzeme miktarını ve yükün konduğu yeri kaydetmek, tasarım değişikliklerini karşılaştırmayı mümkün kılıyordu. Gruplar bir sonucun nedenini açıklayabilmek için görünüşten çok deneme koşullarına ve kayıtlarına bakmaları gerektiğini gördü.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "156444d9-3310-50b5-84f5-6fa63c16ceb5",
    "exerciseId": "5b407543-34d2-55c2-9be3-fb11ca93c2ca",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "age": "young-adult",
    "level": 3,
    "title": "Sınırları açık bir ortak arşiv — Seviye 3",
    "content": "Kentteki bir kültür derneği, mahallenin geçmişini anlatan küçük bir dijital arşiv hazırlamak istedi. Amaç bütün kentin tarihini eksiksiz yazmak değil, gönüllülerin paylaşmak istediği sınırlı sayıdaki belgeyi açıklamalarıyla birlikte düzenlemekti. Eski dükkân fotoğrafları, etkinlik afişleri ve birkaç kişisel anlatı başlangıç malzemesi olarak seçildi. Projeyi yürüten Elif, ilk toplantıda arşivin neyi kapsamadığını da konuşmayı önerdi. Ellerindeki belgeler her dönemi ve her topluluğu eşit biçimde temsil etmiyordu. Bu durum sonradan küçük bir dipnotla geçiştirilmeyecekti. Arşivin giriş sayfasında kapsam açıkça belirtilecekti. Katılımcılar bir dosyanın ellerinde bulunmasını, onu herkesle paylaşma yetkisine sahip oldukları anlamında kabul etmedi. Kayıt ile yayın için ayrı kararlar verilmesi gerektiğini kararlaştırdılar.\n\nİlk getirilen fotoğraf eski bir fırının önünü gösteriyordu. Fotoğrafı getiren kişi görüntünün çocukluğunda çekildiğini hatırlıyor, fakat kesin bir yıl söyleyemiyordu. Elif dosyanın adına tahmini bir yıl yazmak yerine tarih bilinmiyor notunu ekledi. Fotoğraftaki tabelanın biçimi başka belgelerle karşılaştırılabilirdi, ancak bu henüz yapılmış bir doğrulama değildi. Kayıtta belgenin sahibi tarafından söylenenler ile ekip tarafından kontrol edilenler ayrı alanlara yazıldı. Böylece bir ziyaretçi bilgilerin aynı kesinlik düzeyinde olmadığını anlayabilecekti. Fotoğrafın güzel görünmesi, tarihi hakkında kesin bir cümle kurulmasını gerektirmiyordu. Daha sonra yeni bir belge bulunursa kayıt güncellenebilecek, değişikliğin dayanağı da açıklanacaktı. Belirsizliği korumak dosyayı kullanılamaz yapmadı; hangi sorunun henüz cevaplanmadığını görünür kıldı.\n\nBelgelerin taranması sırasında ekip aynı görüntünün farklı kopyalarını buldu. Bunlardan biri kenarları kesilmiş, diğeri daha düşük çözünürlüklüydü. İki dosyanın aynı fotoğraf olması, her bakımdan eşdeğer oldukları anlamına gelmiyordu. Kenardaki bir yazı kopyaların yalnız birinde okunabiliyordu. Ekip en ayrıntılı kopyayı sakladı, diğerinin nereden geldiğini de kayıtla ilişkilendirdi. Dosyalar gelişigüzel silinmedi. Ziyaretçiye gösterilecek görüntü ile çalışma için korunan asıl kopya ayrı tutuldu. Parlaklık ayarı yapılan bir görüntüye düzenleme notu eklendi. Görüntünün eksik kısmını hayal ederek tamamlamak veya görünmeyen bir sözcüğü kesinmiş gibi yazmak uygun bulunmadı. Amaç daha çekici bir sahne üretmek değil, elde bulunan belgenin anlaşılmasını sağlamaktı.\n\nArşivde arama yapılabilmesi için konu etiketleri hazırlandı. Bir afiş hem spor etkinliği hem mahalle buluşması başlığıyla ilgiliydi. İlk taslakta yalnız bir konu seçilmesi isteniyordu. Gönüllüler bunun bazı belgeleri dar bir başlık içinde saklayabileceğini fark etti. Birden fazla anlamlı etiket kullanılmasına izin verildi, fakat aynı şey için çok sayıda farklı ad üretmemek amacıyla ortak bir liste tutuldu. Listenin kapalı ve değişmez olması gerekmiyordu. Yeni bir ihtiyaç çıktığında ekleme önerilebilirdi. Önce birkaç kişi aradıkları belgeyi etiketlerle bulmayı denedi. Kullanılmayan veya karışıklık yaratan terimler yeniden gözden geçirildi. Etiket sayısının artması kendi başına başarı değildi; aranan belgenin doğru bağlamla bulunmasına yardımcı olması önemliydi.\n\nKişisel anlatıların eklenmesi proje için yeni sorular doğurdu. Aynı meydandaki bir etkinliği iki kişi farklı biçimde hatırlıyordu. Ekip hangisinin daha canlı anlattığına bakarak birini doğru ilan etmedi. Her anlatı, anlatanın hatırası olarak ve paylaşım izni kapsamında kaydedildi. Ayrıntılar doğrulanmadığında böyle olduğu belirtildi. Bir anlatının başka bir kişiden söz etmesi de otomatik yayın izni sayılmadı. Gerekli izin bulunmayan bölümler kamuya açık metne alınmadı. Arşiv yalnız bilgi toplamaya değil, hangi bilginin hangi bağlamda paylaşılabileceğini açıklamaya dayanıyordu. Farklı hatıralar her zaman çözülmesi gereken bir hata değildi. Bazen aynı olayın farklı kişiler için ne ifade ettiğini gösteriyor, okuyucuya yeni araştırma soruları sunuyordu.\n\nArşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
    "wordCount": 531,
    "source": "../../comprehension/v1/texts/community-archive.txt",
    "sourceSha256": "5e8947a4afc100c16cbee52de105ab360490c728c36747b9748bfbec9f92ebda",
    "questions": [
      {
        "id": "b76800fc-7ea1-5ff2-82a2-5a6e167e8979",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Mahalle belgelerini kapsamı ve paylaşım yetkisi açık bir arşivde düzenlemek",
        "optionB": "Kentin bütün tarihini eksiksiz ve kesin biçimde yazmak",
        "optionC": "Fotoğrafları yalnız estetik değerlerine göre sıralamak",
        "optionD": "Eldeki bütün dosyaları izin incelemeden yayımlamak",
        "correctAnswer": "A",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "5af172f8-4aff-58b7-8437-62096ecfe20a",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Bir dosyanın elde bulunması onu yayımlama yetkisini otomatik verir",
        "optionB": "Arşiv kayıtları kaynakları, belirsizlikleri ve paylaşım sınırlarıyla sunulmalıdır",
        "optionC": "Belirsiz kayıtlar kesin bilgiyle tamamlanmış gibi sunulmalıdır",
        "optionD": "Arşiv bütün toplulukları eşit temsil etmek zorundadır ve bunu zaten başarır",
        "correctAnswer": "B",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "b865f926-49a8-5621-8839-f4359f8ab29d",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "En eski belgenin her zaman en doğru olduğunu kanıtlamak",
        "optionB": "Paylaşım yetkisini kayıt sayısına göre belirlemek",
        "optionC": "Bir arşiv oluştururken kaynak, kapsam ve paylaşım sınırlarını açıklamanın önemini göstermek",
        "optionD": "Bütün kişisel anlatıları aynı kesinlikte sunmak",
        "correctAnswer": "C",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "4fc5fed9-f2c9-5057-852b-b853fc25f584",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Kentin Eksiksiz Tarihi",
        "optionB": "Her Dosya Herkese Açıktır",
        "optionC": "Belirsizlikleri Silmek",
        "optionD": "Sınırları açık bir ortak arşiv",
        "correctAnswer": "D",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "03b39d56-2c48-516e-8805-e18b17903df9",
    "exerciseId": "5b407543-34d2-55c2-9be3-fb11ca93c2ca",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "age": "young-adult",
    "level": 3,
    "title": "Tamir atölyesinde gerekçeli kararlar — Seviye 3",
    "content": "Mahalle kültür merkezinde açılan tamir atölyesi, kullanılmayan eşyaları hemen yenileriyle değiştirmek yerine önce sorunlarını anlamak için kurulmuştu. Gönüllüler ilk toplantıda kırık masa lambaları, fermuarı açılan çantalar ve gevşeyen sandalye ayakları hakkında konuştu. Ancak her eşyanın atölyede güvenle onarılabileceğini varsaymadılar. Elektrik bağlantısı bulunan cihazları yalnız yetkin bir uzmanın incelemesi, diğer katılımcıların ise gözlem ve kayıt işleriyle başlaması kararlaştırıldı. Atölyenin amacı herkesi bir günde usta yapmak değil, bakım kararlarının nasıl verildiğini görünür kılmaktı. Selin kayıt masasını, Bora malzeme dolabını düzenlemeyi üstlendi. İlk buluşmada hiçbir elektrikli cihaz çalıştırılmadı. Katılımcılara, bir sorunu tanımak ile onu onarmaya yetkili olmak arasındaki fark açıklandı. Bu ayrım hazırlık listesinin ilk satırına yazıldı.\n\nSelin her eşya için bir kabul kartı hazırladı. Kartta eşyanın türü, sahibinin anlattığı sorun ve inceleme sonucu için ayrı alanlar vardı. Bir çantanın sahibi, fermuarın geçen haftadan beri takıldığını söyledi. Selin bunu doğrudan arızanın nedeni olarak kaydetmedi; sahibinin gözlemi olarak yazdı. İnceleme sırasında kumaşın fermuara sıkıştığı görüldü. Kartın ikinci alanına bu bulgu eklendi. Böylece anlatılan belirti ile kontrol edilerek bulunan neden birbirine karışmadı. Sahiplerin bütün geçmişini veya ev adresini kaydetmek gerekmiyordu. Geri dönüş için istenen iletişim bilgisi ayrı tutuldu ve yalnız görevlilerin erişimine bırakıldı. Katılımcılar hangi bilginin neden toplandığını okuyabiliyordu. Bir eşyanın fotoğrafı çekilecekse bunun için ayrıca izin soruluyordu.\n\nMalzeme dolabı hazırlanırken Bora bütün parçaları dış görünüşlerine göre aynı kutuya koymak istedi. Deneyimli bir gönüllü, birbirine benzeyen vidaların farklı ölçülerde olabileceğini gösterdi. Kutular ölçü ve kullanım bilgisiyle etiketlendi. Etiketi okunmayan bir parçaya tahminle ad verilmedi; kontrol bekleyen bölüme kondu. Sonra herkes dolaptan örnek bir parça bulmayı denedi. Bazı etiketler doğru olsa da rafın arkasında kaldığı için görülmüyordu. Yerleri değiştirildi ve yazılar büyütüldü. Düzenin başarısı yalnız kutuların düzgün görünmesiyle değil, aranan parçanın doğru bulunmasıyla değerlendirildi. İlk düzenleme fotoğrafı saklandı; sonraki değişikliğin nedeni kısa bir notla açıklandı. Böylece yeni gelen bir gönüllü de etiketlerin hangi ihtiyaçtan doğduğunu anlayabilecekti.\n\nİlk üç buluşmanın sonunda ekip onarılan eşya sayısını karşılaştırdı. Birinci buluşmada on, ikinci buluşmada yedi, üçüncüde on iki eşya tamamlanmıştı. Bora üçüncü buluşmanın kesinlikle en verimli gün olduğunu söyledi. Selin kabul kartlarına bakmayı önerdi. İkinci gün gelen eşyalardan birkaçının incelemesi daha uzun sürmüş, bazıları için parça beklenmişti. Üçüncü gün ise çok sayıda küçük kumaş onarımı yapılmıştı. Sayılar gerçekti, fakat işlerin güçlüğünü tek başına göstermiyordu. Ekip toplam sayının yanına işlem türlerini ve bekleyen işleri de ekledi. Karşılaştırmayı kaldırmadılar; hangi soruya cevap verdiğini daralttılar. Artık tablo, gönüllülerin becerisini sıralayan bir puan değil, farklı günlerde hangi işlerin yapıldığını anlatan bir kayıt olarak kullanılıyordu.\n\nBir katılımcı eski bir lambasının onarılmasını istediğinde güvenlik sınırı yeniden gündeme geldi. Uzman o gün atölyede değildi. Gönüllüler lambayı denemek yerine kabul kartını doldurup inceleme bekleyen bölüme kaldırdı. Lambanın o gün geri verilmemesi katılımcıyı üzdü, fakat neden açıkça anlatıldı. Beklemek, eşyanın önemsiz görüldüğü anlamına gelmiyordu. Uzman değerlendirmesi olmadan yapılan bir deneme başka riskler doğurabilirdi. Ekip bu olayı sonraki toplantıda konuştu ve atölye duyurusuna hizmet sınırlarını ekledi. Her eşya aynı gün onarılacakmış gibi bir söz verilmemeliydi. Tamir edilemeyen veya güvenle incelenemeyen eşya için de anlaşılır bir yönlendirme yapılması gerekiyordu. Bir görevin ertelenmesi, gerekçesi ve sonraki adımıyla birlikte kaydedildiğinde daha kolay anlaşılabiliyordu.\n\nAtölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
    "wordCount": 538,
    "source": "../../comprehension/v1/texts/repair-workshop.txt",
    "sourceSha256": "4f11e94e54fd624803f7d4759adf273eaef86a8fa04028446fa2339a4d27e479",
    "questions": [
      {
        "id": "03cdeb0d-d3e2-5ec8-8302-e258e323b0b7",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Tamir atölyesinde güvenlik ve kayıtlarla ortak kararlar vermek",
        "optionB": "Her elektrikli cihazı herkesin hemen onarmasını sağlamak",
        "optionC": "Gönüllüleri yalnız tamamladıkları eşya sayısıyla sıralamak",
        "optionD": "Bütün eşyaları incelemeden yenileriyle değiştirmek",
        "correctAnswer": "A",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "84d29ad9-ca42-586b-8102-4f4ef9b2c2e7",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "En çok eşya tamamlanan gün bütün açılardan en başarılı gündür",
        "optionB": "Atölye kararları, işlem sayısıyla birlikte güvenlik ve gerekçelerle değerlendirilmelidir",
        "optionC": "Uzman bulunmasa da her cihazı çalıştırarak denemek gerekir",
        "optionD": "Sahibin anlattığı belirti kontrol edilmeden kesin neden sayılmalıdır",
        "correctAnswer": "B",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "c1ea1648-41a3-5d28-8d38-cdeb23579148",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Onarılan eşya sayısını ölçülmemiş çevresel tasarrufla eşitlemek",
        "optionB": "Her işi aynı gün bitirme sözü vermek",
        "optionC": "Tamir kararlarını güvenlik, kayıt ve kapsamıyla birlikte değerlendirmeyi göstermek",
        "optionD": "Kayıt tutmak yerine yalnız gönüllülerin belleğine güvenmek",
        "correctAnswer": "C",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "6065fca6-7aaf-5023-87b5-00c86d6781f1",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Her Eşya Aynı Gün Biter",
        "optionB": "Sadece Sayılar Önemlidir",
        "optionC": "İncelemeden Kesin Sonuç",
        "optionD": "Tamir atölyesinde gerekçeli kararlar",
        "correctAnswer": "D",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "a3d008d9-fe9b-5546-8a4a-4c6de9a7b26b",
    "exerciseId": "5b407543-34d2-55c2-9be3-fb11ca93c2ca",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "age": "young-adult",
    "level": 3,
    "title": "Merakı destekleyen kütüphane — Seviye 3",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.\n\nAy sonunda öneri kartları yeniden gözden geçirildi. Bazı istekler hemen yapılmış, bazıları bütçe bekliyordu. Pano, yalnız tamamlanan işleri değil, henüz çözülemeyen ihtiyaçları da gösteriyordu. Görevli, bir isteğin beklemesinin unutulduğu anlamına gelmediğini açıkladı.\n\nKütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
    "wordCount": 528,
    "source": "../../free-reading/v1/texts/library-long.txt",
    "sourceSha256": "a856f71c5d3b8ff5966873de7e39ab894e018d3d7198980d5f09bb1dbf2fe774",
    "questions": [
      {
        "id": "5732022e-6693-5fb5-856c-4b185a117620",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Kütüphanede bilgiye ulaşmayı ve öğrenmeyi birlikte geliştirmek",
        "optionB": "Kitap kapaklarını yalnız renklerine göre sıralamak",
        "optionC": "Bütün okuyuculara aynı kitabı zorunlu seçtirmek",
        "optionD": "En çok sayfa okuyan kişiyi ödüllendirmek",
        "correctAnswer": "A",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "ad4988dc-667e-5712-82f9-c219796d01aa",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Kitap seçiminde bütün okuyucuların ihtiyacı aynıdır",
        "optionB": "Kütüphane, merak ve bilgiye erişimi ortak çalışmayla destekler",
        "optionC": "Bir kaynağa bakmak bütün soruları kesin olarak çözer",
        "optionD": "Kütüphanede yalnız ödünç alınan kitap sayısı önemlidir",
        "correctAnswer": "B",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "1cb0fc9b-15c1-5e84-8436-d28920bf410e",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Bir okuma hızının herkese uygun olduğunu kanıtlamak",
        "optionB": "Kitapları yalnız kalınlıklarına göre değerlendirmek",
        "optionC": "Kütüphanenin merakı ve bilgiye erişimi destekleyen rolünü anlatmak",
        "optionD": "Bütün ziyaretçileri aynı görüşte birleştirmek",
        "correctAnswer": "C",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "0143aab9-da12-574b-8aeb-9ace184c3c3a",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Herkese Tek Kitap",
        "optionB": "Sayfa Sayısına Göre Yarış",
        "optionC": "Yalnız Kapaklara Bakmak",
        "optionD": "Merakı destekleyen kütüphane",
        "correctAnswer": "D",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "8f71006d-1c88-5dc7-853c-5a78a9c23aba",
    "exerciseId": "4062647b-988d-566f-8f34-8d880bfe14a0",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "age": "young-adult",
    "level": 4,
    "title": "Sınırları açık bir ortak arşiv — Seviye 4",
    "content": "Kentteki bir kültür derneği, mahallenin geçmişini anlatan küçük bir dijital arşiv hazırlamak istedi. Amaç bütün kentin tarihini eksiksiz yazmak değil, gönüllülerin paylaşmak istediği sınırlı sayıdaki belgeyi açıklamalarıyla birlikte düzenlemekti. Eski dükkân fotoğrafları, etkinlik afişleri ve birkaç kişisel anlatı başlangıç malzemesi olarak seçildi. Projeyi yürüten Elif, ilk toplantıda arşivin neyi kapsamadığını da konuşmayı önerdi. Ellerindeki belgeler her dönemi ve her topluluğu eşit biçimde temsil etmiyordu. Bu durum sonradan küçük bir dipnotla geçiştirilmeyecekti. Arşivin giriş sayfasında kapsam açıkça belirtilecekti. Katılımcılar bir dosyanın ellerinde bulunmasını, onu herkesle paylaşma yetkisine sahip oldukları anlamında kabul etmedi. Kayıt ile yayın için ayrı kararlar verilmesi gerektiğini kararlaştırdılar.\n\nİlk getirilen fotoğraf eski bir fırının önünü gösteriyordu. Fotoğrafı getiren kişi görüntünün çocukluğunda çekildiğini hatırlıyor, fakat kesin bir yıl söyleyemiyordu. Elif dosyanın adına tahmini bir yıl yazmak yerine tarih bilinmiyor notunu ekledi. Fotoğraftaki tabelanın biçimi başka belgelerle karşılaştırılabilirdi, ancak bu henüz yapılmış bir doğrulama değildi. Kayıtta belgenin sahibi tarafından söylenenler ile ekip tarafından kontrol edilenler ayrı alanlara yazıldı. Böylece bir ziyaretçi bilgilerin aynı kesinlik düzeyinde olmadığını anlayabilecekti. Fotoğrafın güzel görünmesi, tarihi hakkında kesin bir cümle kurulmasını gerektirmiyordu. Daha sonra yeni bir belge bulunursa kayıt güncellenebilecek, değişikliğin dayanağı da açıklanacaktı. Belirsizliği korumak dosyayı kullanılamaz yapmadı; hangi sorunun henüz cevaplanmadığını görünür kıldı.\n\nBelgelerin taranması sırasında ekip aynı görüntünün farklı kopyalarını buldu. Bunlardan biri kenarları kesilmiş, diğeri daha düşük çözünürlüklüydü. İki dosyanın aynı fotoğraf olması, her bakımdan eşdeğer oldukları anlamına gelmiyordu. Kenardaki bir yazı kopyaların yalnız birinde okunabiliyordu. Ekip en ayrıntılı kopyayı sakladı, diğerinin nereden geldiğini de kayıtla ilişkilendirdi. Dosyalar gelişigüzel silinmedi. Ziyaretçiye gösterilecek görüntü ile çalışma için korunan asıl kopya ayrı tutuldu. Parlaklık ayarı yapılan bir görüntüye düzenleme notu eklendi. Görüntünün eksik kısmını hayal ederek tamamlamak veya görünmeyen bir sözcüğü kesinmiş gibi yazmak uygun bulunmadı. Amaç daha çekici bir sahne üretmek değil, elde bulunan belgenin anlaşılmasını sağlamaktı.\n\nArşivde arama yapılabilmesi için konu etiketleri hazırlandı. Bir afiş hem spor etkinliği hem mahalle buluşması başlığıyla ilgiliydi. İlk taslakta yalnız bir konu seçilmesi isteniyordu. Gönüllüler bunun bazı belgeleri dar bir başlık içinde saklayabileceğini fark etti. Birden fazla anlamlı etiket kullanılmasına izin verildi, fakat aynı şey için çok sayıda farklı ad üretmemek amacıyla ortak bir liste tutuldu. Listenin kapalı ve değişmez olması gerekmiyordu. Yeni bir ihtiyaç çıktığında ekleme önerilebilirdi. Önce birkaç kişi aradıkları belgeyi etiketlerle bulmayı denedi. Kullanılmayan veya karışıklık yaratan terimler yeniden gözden geçirildi. Etiket sayısının artması kendi başına başarı değildi; aranan belgenin doğru bağlamla bulunmasına yardımcı olması önemliydi.\n\nKişisel anlatıların eklenmesi proje için yeni sorular doğurdu. Aynı meydandaki bir etkinliği iki kişi farklı biçimde hatırlıyordu. Ekip hangisinin daha canlı anlattığına bakarak birini doğru ilan etmedi. Her anlatı, anlatanın hatırası olarak ve paylaşım izni kapsamında kaydedildi. Ayrıntılar doğrulanmadığında böyle olduğu belirtildi. Bir anlatının başka bir kişiden söz etmesi de otomatik yayın izni sayılmadı. Gerekli izin bulunmayan bölümler kamuya açık metne alınmadı. Arşiv yalnız bilgi toplamaya değil, hangi bilginin hangi bağlamda paylaşılabileceğini açıklamaya dayanıyordu. Farklı hatıralar her zaman çözülmesi gereken bir hata değildi. Bazen aynı olayın farklı kişiler için ne ifade ettiğini gösteriyor, okuyucuya yeni araştırma soruları sunuyordu.\n\nİlk deneme sayfasını kullananlar fotoğrafların altındaki açıklamalara baktı. Bazı ziyaretçiler kimin fotoğrafı olduğunu, bazıları ise görüntünün hangi sokağa ait olduğunu arıyordu. İlk tasarımda kaynak bilgisi uzun bir metnin sonunda kalmıştı. Elif bu bilgiyi başlığa yakın, kısa bir alana taşıdı. Ayrıntılı inceleme notu isteyenler için ayrı bir bağlantı korundu. Metni sadeleştirmek, belirsizlik ifadelerini çıkarmak anlamına gelmedi. Büyük yazı ve düzenli başlıklar, aynı açıklamayı daha kolay izlemeyi sağladı. Ekip yalnız bilgisayardan değil, küçük ekranlı cihazlardan da sayfayı denedi. Bir tasarımın hazırlayan kişiye anlaşılır gelmesi yeterli değildi; farklı okuyucuların ihtiyaçlarına göre sınanması gerekiyordu. Denemeden sonra değiştirilen alanlar ayrı bir listede belirtildi.\n\nArşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
    "wordCount": 629,
    "source": "../../comprehension/v1/texts/community-archive.txt",
    "sourceSha256": "5e8947a4afc100c16cbee52de105ab360490c728c36747b9748bfbec9f92ebda",
    "questions": [
      {
        "id": "a9e64855-2fbf-56c8-89a6-bcdb765faac0",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Mahalle belgelerini kapsamı ve paylaşım yetkisi açık bir arşivde düzenlemek",
        "optionB": "Kentin bütün tarihini eksiksiz ve kesin biçimde yazmak",
        "optionC": "Fotoğrafları yalnız estetik değerlerine göre sıralamak",
        "optionD": "Eldeki bütün dosyaları izin incelemeden yayımlamak",
        "correctAnswer": "A",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "70356798-4ce8-5c87-8962-9838c5a6a355",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Bir dosyanın elde bulunması onu yayımlama yetkisini otomatik verir",
        "optionB": "Arşiv kayıtları kaynakları, belirsizlikleri ve paylaşım sınırlarıyla sunulmalıdır",
        "optionC": "Belirsiz kayıtlar kesin bilgiyle tamamlanmış gibi sunulmalıdır",
        "optionD": "Arşiv bütün toplulukları eşit temsil etmek zorundadır ve bunu zaten başarır",
        "correctAnswer": "B",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "6a5f973d-eb41-508c-8013-a142ecb72a79",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "En eski belgenin her zaman en doğru olduğunu kanıtlamak",
        "optionB": "Paylaşım yetkisini kayıt sayısına göre belirlemek",
        "optionC": "Bir arşiv oluştururken kaynak, kapsam ve paylaşım sınırlarını açıklamanın önemini göstermek",
        "optionD": "Bütün kişisel anlatıları aynı kesinlikte sunmak",
        "correctAnswer": "C",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "b1171749-d146-5a34-82e8-4d57ee952025",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Kentin Eksiksiz Tarihi",
        "optionB": "Her Dosya Herkese Açıktır",
        "optionC": "Belirsizlikleri Silmek",
        "optionD": "Sınırları açık bir ortak arşiv",
        "correctAnswer": "D",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "873a7a4d-0e8e-57cf-8bf4-8e83d0e173ee",
    "exerciseId": "4062647b-988d-566f-8f34-8d880bfe14a0",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "age": "young-adult",
    "level": 4,
    "title": "Tamir atölyesinde gerekçeli kararlar — Seviye 4",
    "content": "Mahalle kültür merkezinde açılan tamir atölyesi, kullanılmayan eşyaları hemen yenileriyle değiştirmek yerine önce sorunlarını anlamak için kurulmuştu. Gönüllüler ilk toplantıda kırık masa lambaları, fermuarı açılan çantalar ve gevşeyen sandalye ayakları hakkında konuştu. Ancak her eşyanın atölyede güvenle onarılabileceğini varsaymadılar. Elektrik bağlantısı bulunan cihazları yalnız yetkin bir uzmanın incelemesi, diğer katılımcıların ise gözlem ve kayıt işleriyle başlaması kararlaştırıldı. Atölyenin amacı herkesi bir günde usta yapmak değil, bakım kararlarının nasıl verildiğini görünür kılmaktı. Selin kayıt masasını, Bora malzeme dolabını düzenlemeyi üstlendi. İlk buluşmada hiçbir elektrikli cihaz çalıştırılmadı. Katılımcılara, bir sorunu tanımak ile onu onarmaya yetkili olmak arasındaki fark açıklandı. Bu ayrım hazırlık listesinin ilk satırına yazıldı.\n\nSelin her eşya için bir kabul kartı hazırladı. Kartta eşyanın türü, sahibinin anlattığı sorun ve inceleme sonucu için ayrı alanlar vardı. Bir çantanın sahibi, fermuarın geçen haftadan beri takıldığını söyledi. Selin bunu doğrudan arızanın nedeni olarak kaydetmedi; sahibinin gözlemi olarak yazdı. İnceleme sırasında kumaşın fermuara sıkıştığı görüldü. Kartın ikinci alanına bu bulgu eklendi. Böylece anlatılan belirti ile kontrol edilerek bulunan neden birbirine karışmadı. Sahiplerin bütün geçmişini veya ev adresini kaydetmek gerekmiyordu. Geri dönüş için istenen iletişim bilgisi ayrı tutuldu ve yalnız görevlilerin erişimine bırakıldı. Katılımcılar hangi bilginin neden toplandığını okuyabiliyordu. Bir eşyanın fotoğrafı çekilecekse bunun için ayrıca izin soruluyordu.\n\nMalzeme dolabı hazırlanırken Bora bütün parçaları dış görünüşlerine göre aynı kutuya koymak istedi. Deneyimli bir gönüllü, birbirine benzeyen vidaların farklı ölçülerde olabileceğini gösterdi. Kutular ölçü ve kullanım bilgisiyle etiketlendi. Etiketi okunmayan bir parçaya tahminle ad verilmedi; kontrol bekleyen bölüme kondu. Sonra herkes dolaptan örnek bir parça bulmayı denedi. Bazı etiketler doğru olsa da rafın arkasında kaldığı için görülmüyordu. Yerleri değiştirildi ve yazılar büyütüldü. Düzenin başarısı yalnız kutuların düzgün görünmesiyle değil, aranan parçanın doğru bulunmasıyla değerlendirildi. İlk düzenleme fotoğrafı saklandı; sonraki değişikliğin nedeni kısa bir notla açıklandı. Böylece yeni gelen bir gönüllü de etiketlerin hangi ihtiyaçtan doğduğunu anlayabilecekti.\n\nİlk üç buluşmanın sonunda ekip onarılan eşya sayısını karşılaştırdı. Birinci buluşmada on, ikinci buluşmada yedi, üçüncüde on iki eşya tamamlanmıştı. Bora üçüncü buluşmanın kesinlikle en verimli gün olduğunu söyledi. Selin kabul kartlarına bakmayı önerdi. İkinci gün gelen eşyalardan birkaçının incelemesi daha uzun sürmüş, bazıları için parça beklenmişti. Üçüncü gün ise çok sayıda küçük kumaş onarımı yapılmıştı. Sayılar gerçekti, fakat işlerin güçlüğünü tek başına göstermiyordu. Ekip toplam sayının yanına işlem türlerini ve bekleyen işleri de ekledi. Karşılaştırmayı kaldırmadılar; hangi soruya cevap verdiğini daralttılar. Artık tablo, gönüllülerin becerisini sıralayan bir puan değil, farklı günlerde hangi işlerin yapıldığını anlatan bir kayıt olarak kullanılıyordu.\n\nBir katılımcı eski bir lambasının onarılmasını istediğinde güvenlik sınırı yeniden gündeme geldi. Uzman o gün atölyede değildi. Gönüllüler lambayı denemek yerine kabul kartını doldurup inceleme bekleyen bölüme kaldırdı. Lambanın o gün geri verilmemesi katılımcıyı üzdü, fakat neden açıkça anlatıldı. Beklemek, eşyanın önemsiz görüldüğü anlamına gelmiyordu. Uzman değerlendirmesi olmadan yapılan bir deneme başka riskler doğurabilirdi. Ekip bu olayı sonraki toplantıda konuştu ve atölye duyurusuna hizmet sınırlarını ekledi. Her eşya aynı gün onarılacakmış gibi bir söz verilmemeliydi. Tamir edilemeyen veya güvenle incelenemeyen eşya için de anlaşılır bir yönlendirme yapılması gerekiyordu. Bir görevin ertelenmesi, gerekçesi ve sonraki adımıyla birlikte kaydedildiğinde daha kolay anlaşılabiliyordu.\n\nAy sonunda malzeme alımı için sınırlı bir bütçe ayrıldı. Gönüllüler yalnız en çok kullanılan parçaları satın almayı önerdi. Ancak kabul kayıtlarında, eksik bir parça yüzünden sürekli bekleyen birkaç iş de bulunuyordu. Sık kullanım ile eksiklikten doğan bekleme aynı ölçüt değildi. Ekip iki listeyi birlikte inceledi. Önce temel sarf malzemeleri, ardından birden fazla işi tamamlayacak belirli parçalar seçildi. Her talep karşılanamadığı için alınmayanların gerekçesi de yazıldı. Kararı açıklamak, herkesin aynı tercihi yapacağı anlamına gelmiyordu; farklı tercihlerin hangi sınırlar içinde tartışıldığını gösteriyordu. Liste daha sonra değiştirilebilirdi. Bütçe artırılırsa veya eşya türleri değişirse önceki sıralama yeniden değerlendirilecekti. Böylece satın alma kararı tek bir kişinin alışkanlığına bağlı kalmadı.\n\nAtölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
    "wordCount": 642,
    "source": "../../comprehension/v1/texts/repair-workshop.txt",
    "sourceSha256": "4f11e94e54fd624803f7d4759adf273eaef86a8fa04028446fa2339a4d27e479",
    "questions": [
      {
        "id": "d4191429-9c36-5bdf-8c5d-c9007a4f04c9",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Her elektrikli cihazı herkesin hemen onarmasını sağlamak",
        "optionB": "Gönüllüleri yalnız tamamladıkları eşya sayısıyla sıralamak",
        "optionC": "Tamir atölyesinde güvenlik ve kayıtlarla ortak kararlar vermek",
        "optionD": "Bütün eşyaları incelemeden yenileriyle değiştirmek",
        "correctAnswer": "C",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "df762683-24a2-5cd2-8740-fa7d8fff7b67",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "En çok eşya tamamlanan gün bütün açılardan en başarılı gündür",
        "optionB": "Uzman bulunmasa da her cihazı çalıştırarak denemek gerekir",
        "optionC": "Sahibin anlattığı belirti kontrol edilmeden kesin neden sayılmalıdır",
        "optionD": "Atölye kararları, işlem sayısıyla birlikte güvenlik ve gerekçelerle değerlendirilmelidir",
        "correctAnswer": "D",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "3d56d751-6e11-5e87-84b8-d275ca4c82ee",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Tamir kararlarını güvenlik, kayıt ve kapsamıyla birlikte değerlendirmeyi göstermek",
        "optionB": "Onarılan eşya sayısını ölçülmemiş çevresel tasarrufla eşitlemek",
        "optionC": "Her işi aynı gün bitirme sözü vermek",
        "optionD": "Kayıt tutmak yerine yalnız gönüllülerin belleğine güvenmek",
        "correctAnswer": "A",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "f0285b0a-6b8b-5693-8cb6-95a68379f5f8",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Her Eşya Aynı Gün Biter",
        "optionB": "Tamir atölyesinde gerekçeli kararlar",
        "optionC": "Sadece Sayılar Önemlidir",
        "optionD": "İncelemeden Kesin Sonuç",
        "correctAnswer": "B",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "fca7b39d-8493-55f9-889a-1f8a1cf61acd",
    "exerciseId": "4062647b-988d-566f-8f34-8d880bfe14a0",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "age": "young-adult",
    "level": 4,
    "title": "Merakı destekleyen kütüphane — Seviye 4",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.\n\nAy sonunda öneri kartları yeniden gözden geçirildi. Bazı istekler hemen yapılmış, bazıları bütçe bekliyordu. Pano, yalnız tamamlanan işleri değil, henüz çözülemeyen ihtiyaçları da gösteriyordu. Görevli, bir isteğin beklemesinin unutulduğu anlamına gelmediğini açıkladı.\n\nKütüphaneye yeni kitaplar geldiğinde herkesin sevdiği türlerden eşit sayıda kitap bulunmadı. Gönüllüler tek bir kişinin tercihine göre karar vermedi. Ödünç alma kayıtlarına, önerilere ve raflarda eksik kalan konulara birlikte baktılar. Kararın gerekçesini panoda paylaştılar.\n\nBazı kitaplar çok sık isteniyordu. Bu kitaplar için sıraya girenlere tahmini dönüş tarihi söylendi. Sıra beklemek istemeyenlere benzer konudaki başka kitaplar gösterildi. Bir seçenek sunmak, aranan kitabın hemen bulunamamasını daha anlaşılır hâle getirdi.\n\nBir okuma buluşmasında aynı öykü hakkında farklı yorumlar yapıldı. Duru kahramanın cesur olduğunu düşünürken arkadaşı aceleci olduğunu söyledi. İkisi de görüşünü öyküdeki bir olayla açıkladı. Farklı yorumların, dikkatle dinlendiğinde konuşmayı zenginleştirebildiğini gördüler.\n\nBuluşmayı yöneten kişi, herkesi aynı görüşte birleştirmeye çalışmadı. Önce yorumların hangi ayrıntıya dayandığını sordu. Sonra öykünün başka bir bölümünü birlikte okudular. Yeni ayrıntılar bazı düşünceleri değiştirdi, bazılarını güçlendirdi. Konuşma, yalnız sonuçtan değil gerekçelerden oluştu.\n\nKütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
    "wordCount": 661,
    "source": "../../free-reading/v1/texts/library-long.txt",
    "sourceSha256": "a856f71c5d3b8ff5966873de7e39ab894e018d3d7198980d5f09bb1dbf2fe774",
    "questions": [
      {
        "id": "0c836168-7a1d-5f7b-81a1-41d11973e9b9",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Kitap kapaklarını yalnız renklerine göre sıralamak",
        "optionB": "Bütün okuyuculara aynı kitabı zorunlu seçtirmek",
        "optionC": "Kütüphanede bilgiye ulaşmayı ve öğrenmeyi birlikte geliştirmek",
        "optionD": "En çok sayfa okuyan kişiyi ödüllendirmek",
        "correctAnswer": "C",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "158e550d-1af4-5a67-81d4-63af4c0cc4fa",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Kitap seçiminde bütün okuyucuların ihtiyacı aynıdır",
        "optionB": "Bir kaynağa bakmak bütün soruları kesin olarak çözer",
        "optionC": "Kütüphanede yalnız ödünç alınan kitap sayısı önemlidir",
        "optionD": "Kütüphane, merak ve bilgiye erişimi ortak çalışmayla destekler",
        "correctAnswer": "D",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "7befbf03-ac9d-5f58-80ac-43a668c571c1",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Kütüphanenin merakı ve bilgiye erişimi destekleyen rolünü anlatmak",
        "optionB": "Bir okuma hızının herkese uygun olduğunu kanıtlamak",
        "optionC": "Kitapları yalnız kalınlıklarına göre değerlendirmek",
        "optionD": "Bütün ziyaretçileri aynı görüşte birleştirmek",
        "correctAnswer": "A",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "a982211b-5db8-5533-85eb-d6627a5b81fd",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Herkese Tek Kitap",
        "optionB": "Merakı destekleyen kütüphane",
        "optionC": "Sayfa Sayısına Göre Yarış",
        "optionD": "Yalnız Kapaklara Bakmak",
        "correctAnswer": "B",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "6124f21c-0137-5227-8c2a-7eb05c8b0187",
    "exerciseId": "a1dc447b-f5ce-557c-bd63-852040fb5ca6",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "age": "young-adult",
    "level": 5,
    "title": "Sınırları açık bir ortak arşiv — Seviye 5",
    "content": "Kentteki bir kültür derneği, mahallenin geçmişini anlatan küçük bir dijital arşiv hazırlamak istedi. Amaç bütün kentin tarihini eksiksiz yazmak değil, gönüllülerin paylaşmak istediği sınırlı sayıdaki belgeyi açıklamalarıyla birlikte düzenlemekti. Eski dükkân fotoğrafları, etkinlik afişleri ve birkaç kişisel anlatı başlangıç malzemesi olarak seçildi. Projeyi yürüten Elif, ilk toplantıda arşivin neyi kapsamadığını da konuşmayı önerdi. Ellerindeki belgeler her dönemi ve her topluluğu eşit biçimde temsil etmiyordu. Bu durum sonradan küçük bir dipnotla geçiştirilmeyecekti. Arşivin giriş sayfasında kapsam açıkça belirtilecekti. Katılımcılar bir dosyanın ellerinde bulunmasını, onu herkesle paylaşma yetkisine sahip oldukları anlamında kabul etmedi. Kayıt ile yayın için ayrı kararlar verilmesi gerektiğini kararlaştırdılar.\n\nİlk getirilen fotoğraf eski bir fırının önünü gösteriyordu. Fotoğrafı getiren kişi görüntünün çocukluğunda çekildiğini hatırlıyor, fakat kesin bir yıl söyleyemiyordu. Elif dosyanın adına tahmini bir yıl yazmak yerine tarih bilinmiyor notunu ekledi. Fotoğraftaki tabelanın biçimi başka belgelerle karşılaştırılabilirdi, ancak bu henüz yapılmış bir doğrulama değildi. Kayıtta belgenin sahibi tarafından söylenenler ile ekip tarafından kontrol edilenler ayrı alanlara yazıldı. Böylece bir ziyaretçi bilgilerin aynı kesinlik düzeyinde olmadığını anlayabilecekti. Fotoğrafın güzel görünmesi, tarihi hakkında kesin bir cümle kurulmasını gerektirmiyordu. Daha sonra yeni bir belge bulunursa kayıt güncellenebilecek, değişikliğin dayanağı da açıklanacaktı. Belirsizliği korumak dosyayı kullanılamaz yapmadı; hangi sorunun henüz cevaplanmadığını görünür kıldı.\n\nBelgelerin taranması sırasında ekip aynı görüntünün farklı kopyalarını buldu. Bunlardan biri kenarları kesilmiş, diğeri daha düşük çözünürlüklüydü. İki dosyanın aynı fotoğraf olması, her bakımdan eşdeğer oldukları anlamına gelmiyordu. Kenardaki bir yazı kopyaların yalnız birinde okunabiliyordu. Ekip en ayrıntılı kopyayı sakladı, diğerinin nereden geldiğini de kayıtla ilişkilendirdi. Dosyalar gelişigüzel silinmedi. Ziyaretçiye gösterilecek görüntü ile çalışma için korunan asıl kopya ayrı tutuldu. Parlaklık ayarı yapılan bir görüntüye düzenleme notu eklendi. Görüntünün eksik kısmını hayal ederek tamamlamak veya görünmeyen bir sözcüğü kesinmiş gibi yazmak uygun bulunmadı. Amaç daha çekici bir sahne üretmek değil, elde bulunan belgenin anlaşılmasını sağlamaktı.\n\nArşivde arama yapılabilmesi için konu etiketleri hazırlandı. Bir afiş hem spor etkinliği hem mahalle buluşması başlığıyla ilgiliydi. İlk taslakta yalnız bir konu seçilmesi isteniyordu. Gönüllüler bunun bazı belgeleri dar bir başlık içinde saklayabileceğini fark etti. Birden fazla anlamlı etiket kullanılmasına izin verildi, fakat aynı şey için çok sayıda farklı ad üretmemek amacıyla ortak bir liste tutuldu. Listenin kapalı ve değişmez olması gerekmiyordu. Yeni bir ihtiyaç çıktığında ekleme önerilebilirdi. Önce birkaç kişi aradıkları belgeyi etiketlerle bulmayı denedi. Kullanılmayan veya karışıklık yaratan terimler yeniden gözden geçirildi. Etiket sayısının artması kendi başına başarı değildi; aranan belgenin doğru bağlamla bulunmasına yardımcı olması önemliydi.\n\nKişisel anlatıların eklenmesi proje için yeni sorular doğurdu. Aynı meydandaki bir etkinliği iki kişi farklı biçimde hatırlıyordu. Ekip hangisinin daha canlı anlattığına bakarak birini doğru ilan etmedi. Her anlatı, anlatanın hatırası olarak ve paylaşım izni kapsamında kaydedildi. Ayrıntılar doğrulanmadığında böyle olduğu belirtildi. Bir anlatının başka bir kişiden söz etmesi de otomatik yayın izni sayılmadı. Gerekli izin bulunmayan bölümler kamuya açık metne alınmadı. Arşiv yalnız bilgi toplamaya değil, hangi bilginin hangi bağlamda paylaşılabileceğini açıklamaya dayanıyordu. Farklı hatıralar her zaman çözülmesi gereken bir hata değildi. Bazen aynı olayın farklı kişiler için ne ifade ettiğini gösteriyor, okuyucuya yeni araştırma soruları sunuyordu.\n\nİlk deneme sayfasını kullananlar fotoğrafların altındaki açıklamalara baktı. Bazı ziyaretçiler kimin fotoğrafı olduğunu, bazıları ise görüntünün hangi sokağa ait olduğunu arıyordu. İlk tasarımda kaynak bilgisi uzun bir metnin sonunda kalmıştı. Elif bu bilgiyi başlığa yakın, kısa bir alana taşıdı. Ayrıntılı inceleme notu isteyenler için ayrı bir bağlantı korundu. Metni sadeleştirmek, belirsizlik ifadelerini çıkarmak anlamına gelmedi. Büyük yazı ve düzenli başlıklar, aynı açıklamayı daha kolay izlemeyi sağladı. Ekip yalnız bilgisayardan değil, küçük ekranlı cihazlardan da sayfayı denedi. Bir tasarımın hazırlayan kişiye anlaşılır gelmesi yeterli değildi; farklı okuyucuların ihtiyaçlarına göre sınanması gerekiyordu. Denemeden sonra değiştirilen alanlar ayrı bir listede belirtildi.\n\nProjenin tanıtımı için toplam belge sayısını vermek kolaydı. Ancak sayı, arşivin kapsamını tek başına anlatmıyordu. Aynı etkinlikten onlarca fotoğraf varken başka bir dönemden yalnız bir afiş bulunuyordu. Çok sayıda dosyanın olması bütün mahalle geçmişinin dengeli temsil edildiğini göstermiyordu. Ekip sayının yanına belge türleri ve dönemlere göre dağılımı da ekledi. Eksik kalan dönemler açıklandı. Yeni katkı çağrısı özellikle bu boşluklara odaklandı, fakat çağrının onları mutlaka dolduracağı sözü verilmedi. Mevcut arşivin değeri ile eksiksiz olma iddiası birbirinden ayrıldı. Ziyaretçilerin sayıyı ne anlama geldiğini bilerek kullanması amaçlandı. Bir ölçünün görünür olması, onun başka bütün nitelikleri temsil ettiği anlamına gelmezdi.\n\nBir ziyaretçi bir fotoğraftaki sokak adının yanlış yazıldığını bildirdi. Ekip öneriyi hemen kabul etmek veya reddetmek yerine dayanağını sordu. Ziyaretçinin gönderdiği başka bir afiş, aynı köşeyi eski bir adla gösteriyordu. Kaynaklar yeniden karşılaştırıldı ve hatanın bulunduğu alan düzeltildi. Eski kayıt tamamen yok edilmedi; düzeltme tarihçesinde önceki ad ve değişikliğin nedeni korundu. Böylece daha önce arşivi kullanan birinin farklı bilgiyle karşılaşması açıklanabilirdi. Her öneri doğru kabul edilse kayıtlar kolayca karışabilirdi. Hiçbir öneri dikkate alınmasa hatalar sürerdi. Ekip bu iki uç arasında kontrol edilebilir bir inceleme adımı belirledi. Başvurunun alındığı ve incelendiği durumlar da katkı sahibine anlaşılır biçimde bildirildi.\n\nArşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
    "wordCount": 822,
    "source": "../../comprehension/v1/texts/community-archive.txt",
    "sourceSha256": "5e8947a4afc100c16cbee52de105ab360490c728c36747b9748bfbec9f92ebda",
    "questions": [
      {
        "id": "74a19e94-7ad4-5638-8bd0-f37741fb42c0",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Kentin bütün tarihini eksiksiz ve kesin biçimde yazmak",
        "optionB": "Mahalle belgelerini kapsamı ve paylaşım yetkisi açık bir arşivde düzenlemek",
        "optionC": "Fotoğrafları yalnız estetik değerlerine göre sıralamak",
        "optionD": "Eldeki bütün dosyaları izin incelemeden yayımlamak",
        "correctAnswer": "B",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "fec84337-9db4-5f67-8749-52d12c25edf8",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Bir dosyanın elde bulunması onu yayımlama yetkisini otomatik verir",
        "optionB": "Belirsiz kayıtlar kesin bilgiyle tamamlanmış gibi sunulmalıdır",
        "optionC": "Arşiv kayıtları kaynakları, belirsizlikleri ve paylaşım sınırlarıyla sunulmalıdır",
        "optionD": "Arşiv bütün toplulukları eşit temsil etmek zorundadır ve bunu zaten başarır",
        "correctAnswer": "C",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "9c9a77bf-2041-5923-810a-26b865d2f42e",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "En eski belgenin her zaman en doğru olduğunu kanıtlamak",
        "optionB": "Paylaşım yetkisini kayıt sayısına göre belirlemek",
        "optionC": "Bütün kişisel anlatıları aynı kesinlikte sunmak",
        "optionD": "Bir arşiv oluştururken kaynak, kapsam ve paylaşım sınırlarını açıklamanın önemini göstermek",
        "correctAnswer": "D",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "9386fd63-34f8-5b09-8b3c-e4a6941afeb2",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Sınırları açık bir ortak arşiv",
        "optionB": "Kentin Eksiksiz Tarihi",
        "optionC": "Her Dosya Herkese Açıktır",
        "optionD": "Belirsizlikleri Silmek",
        "correctAnswer": "A",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "fc29eac3-67f2-5dfb-8c64-4207d813f70d",
    "exerciseId": "a1dc447b-f5ce-557c-bd63-852040fb5ca6",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "age": "young-adult",
    "level": 5,
    "title": "Tamir atölyesinde gerekçeli kararlar — Seviye 5",
    "content": "Mahalle kültür merkezinde açılan tamir atölyesi, kullanılmayan eşyaları hemen yenileriyle değiştirmek yerine önce sorunlarını anlamak için kurulmuştu. Gönüllüler ilk toplantıda kırık masa lambaları, fermuarı açılan çantalar ve gevşeyen sandalye ayakları hakkında konuştu. Ancak her eşyanın atölyede güvenle onarılabileceğini varsaymadılar. Elektrik bağlantısı bulunan cihazları yalnız yetkin bir uzmanın incelemesi, diğer katılımcıların ise gözlem ve kayıt işleriyle başlaması kararlaştırıldı. Atölyenin amacı herkesi bir günde usta yapmak değil, bakım kararlarının nasıl verildiğini görünür kılmaktı. Selin kayıt masasını, Bora malzeme dolabını düzenlemeyi üstlendi. İlk buluşmada hiçbir elektrikli cihaz çalıştırılmadı. Katılımcılara, bir sorunu tanımak ile onu onarmaya yetkili olmak arasındaki fark açıklandı. Bu ayrım hazırlık listesinin ilk satırına yazıldı.\n\nSelin her eşya için bir kabul kartı hazırladı. Kartta eşyanın türü, sahibinin anlattığı sorun ve inceleme sonucu için ayrı alanlar vardı. Bir çantanın sahibi, fermuarın geçen haftadan beri takıldığını söyledi. Selin bunu doğrudan arızanın nedeni olarak kaydetmedi; sahibinin gözlemi olarak yazdı. İnceleme sırasında kumaşın fermuara sıkıştığı görüldü. Kartın ikinci alanına bu bulgu eklendi. Böylece anlatılan belirti ile kontrol edilerek bulunan neden birbirine karışmadı. Sahiplerin bütün geçmişini veya ev adresini kaydetmek gerekmiyordu. Geri dönüş için istenen iletişim bilgisi ayrı tutuldu ve yalnız görevlilerin erişimine bırakıldı. Katılımcılar hangi bilginin neden toplandığını okuyabiliyordu. Bir eşyanın fotoğrafı çekilecekse bunun için ayrıca izin soruluyordu.\n\nMalzeme dolabı hazırlanırken Bora bütün parçaları dış görünüşlerine göre aynı kutuya koymak istedi. Deneyimli bir gönüllü, birbirine benzeyen vidaların farklı ölçülerde olabileceğini gösterdi. Kutular ölçü ve kullanım bilgisiyle etiketlendi. Etiketi okunmayan bir parçaya tahminle ad verilmedi; kontrol bekleyen bölüme kondu. Sonra herkes dolaptan örnek bir parça bulmayı denedi. Bazı etiketler doğru olsa da rafın arkasında kaldığı için görülmüyordu. Yerleri değiştirildi ve yazılar büyütüldü. Düzenin başarısı yalnız kutuların düzgün görünmesiyle değil, aranan parçanın doğru bulunmasıyla değerlendirildi. İlk düzenleme fotoğrafı saklandı; sonraki değişikliğin nedeni kısa bir notla açıklandı. Böylece yeni gelen bir gönüllü de etiketlerin hangi ihtiyaçtan doğduğunu anlayabilecekti.\n\nİlk üç buluşmanın sonunda ekip onarılan eşya sayısını karşılaştırdı. Birinci buluşmada on, ikinci buluşmada yedi, üçüncüde on iki eşya tamamlanmıştı. Bora üçüncü buluşmanın kesinlikle en verimli gün olduğunu söyledi. Selin kabul kartlarına bakmayı önerdi. İkinci gün gelen eşyalardan birkaçının incelemesi daha uzun sürmüş, bazıları için parça beklenmişti. Üçüncü gün ise çok sayıda küçük kumaş onarımı yapılmıştı. Sayılar gerçekti, fakat işlerin güçlüğünü tek başına göstermiyordu. Ekip toplam sayının yanına işlem türlerini ve bekleyen işleri de ekledi. Karşılaştırmayı kaldırmadılar; hangi soruya cevap verdiğini daralttılar. Artık tablo, gönüllülerin becerisini sıralayan bir puan değil, farklı günlerde hangi işlerin yapıldığını anlatan bir kayıt olarak kullanılıyordu.\n\nBir katılımcı eski bir lambasının onarılmasını istediğinde güvenlik sınırı yeniden gündeme geldi. Uzman o gün atölyede değildi. Gönüllüler lambayı denemek yerine kabul kartını doldurup inceleme bekleyen bölüme kaldırdı. Lambanın o gün geri verilmemesi katılımcıyı üzdü, fakat neden açıkça anlatıldı. Beklemek, eşyanın önemsiz görüldüğü anlamına gelmiyordu. Uzman değerlendirmesi olmadan yapılan bir deneme başka riskler doğurabilirdi. Ekip bu olayı sonraki toplantıda konuştu ve atölye duyurusuna hizmet sınırlarını ekledi. Her eşya aynı gün onarılacakmış gibi bir söz verilmemeliydi. Tamir edilemeyen veya güvenle incelenemeyen eşya için de anlaşılır bir yönlendirme yapılması gerekiyordu. Bir görevin ertelenmesi, gerekçesi ve sonraki adımıyla birlikte kaydedildiğinde daha kolay anlaşılabiliyordu.\n\nAy sonunda malzeme alımı için sınırlı bir bütçe ayrıldı. Gönüllüler yalnız en çok kullanılan parçaları satın almayı önerdi. Ancak kabul kayıtlarında, eksik bir parça yüzünden sürekli bekleyen birkaç iş de bulunuyordu. Sık kullanım ile eksiklikten doğan bekleme aynı ölçüt değildi. Ekip iki listeyi birlikte inceledi. Önce temel sarf malzemeleri, ardından birden fazla işi tamamlayacak belirli parçalar seçildi. Her talep karşılanamadığı için alınmayanların gerekçesi de yazıldı. Kararı açıklamak, herkesin aynı tercihi yapacağı anlamına gelmiyordu; farklı tercihlerin hangi sınırlar içinde tartışıldığını gösteriyordu. Liste daha sonra değiştirilebilirdi. Bütçe artırılırsa veya eşya türleri değişirse önceki sıralama yeniden değerlendirilecekti. Böylece satın alma kararı tek bir kişinin alışkanlığına bağlı kalmadı.\n\nAtölyenin açık kaldığı saatleri değiştirmek isteyenler bir katılım çizelgesi hazırladı. Sabah gelenlerle akşam gelebilenlerin aynı kişiler olmadığı görüldü. Yalnız mevcut ziyaretçilere soru sorulursa hiç gelemeyenlerin ihtiyaçları görünmeyebilirdi. Selin bu sınırlılığı önerinin başına yazdı. Kültür merkezinin başka etkinliklerine katılanlara da uygun saatler soruldu, fakat yanıt vermeleri zorunlu tutulmadı. Çizelge belirli insanların gün içindeki hareketlerini takip etmek için kullanılmadı. Amaç, birkaç olası zaman aralığını karşılaştırmaktı. Ekip tek bir saati herkes için ideal ilan etmedi. İki farklı saat aralığını kısa süreli denemeyi ve katılımı yeniden değerlendirmeyi önerdi. Yeni saatler uygulanmadan önce görevli bulunabilirliği ve merkezin diğer etkinlikleri de kontrol edilecekti.\n\nİşlem kayıtlarının sonraki ekip tarafından anlaşılması için küçük bir terim listesi oluşturuldu. Bazı kartlarda bitmiş bir onarım için tamamlandı, bazılarında ise kontrol edildi yazıyordu. Bu ifadeler eşdeğer değildi. Kontrol edilen bir eşya henüz onarılmamış olabilirdi. Terim listesinde kabul edildi, inceleme bekliyor, parça bekliyor ve tamamlandı durumları ayrı tanımlandı. Eski kartlar körlemesine yeniden etiketlenmedi. Belirsiz olanlar ilgili gönüllüye soruldu; yanıt alınamayanlarda belirsizlik korundu. Daha düzenli bir tablo elde etmek için gerçekte bilinmeyen bir durumu kesinleştirmek uygun bulunmadı. Yeni kayıtlar ortak tanımlarla yazılmaya başladı. Durum değiştiğinde tarih ve değişikliğin nedeni eklendi; eski notların üzerine yazılarak önceki süreç görünmez hâle getirilmedi.\n\nAtölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
    "wordCount": 837,
    "source": "../../comprehension/v1/texts/repair-workshop.txt",
    "sourceSha256": "4f11e94e54fd624803f7d4759adf273eaef86a8fa04028446fa2339a4d27e479",
    "questions": [
      {
        "id": "0438b26e-fac2-5333-8951-3f5cf5beec5c",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Her elektrikli cihazı herkesin hemen onarmasını sağlamak",
        "optionB": "Gönüllüleri yalnız tamamladıkları eşya sayısıyla sıralamak",
        "optionC": "Tamir atölyesinde güvenlik ve kayıtlarla ortak kararlar vermek",
        "optionD": "Bütün eşyaları incelemeden yenileriyle değiştirmek",
        "correctAnswer": "C",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "92ac694c-55c9-5730-8381-c993c29831ba",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "En çok eşya tamamlanan gün bütün açılardan en başarılı gündür",
        "optionB": "Uzman bulunmasa da her cihazı çalıştırarak denemek gerekir",
        "optionC": "Sahibin anlattığı belirti kontrol edilmeden kesin neden sayılmalıdır",
        "optionD": "Atölye kararları, işlem sayısıyla birlikte güvenlik ve gerekçelerle değerlendirilmelidir",
        "correctAnswer": "D",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "cf20ef81-55ad-5599-8e2c-c1623a7b1374",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Tamir kararlarını güvenlik, kayıt ve kapsamıyla birlikte değerlendirmeyi göstermek",
        "optionB": "Onarılan eşya sayısını ölçülmemiş çevresel tasarrufla eşitlemek",
        "optionC": "Her işi aynı gün bitirme sözü vermek",
        "optionD": "Kayıt tutmak yerine yalnız gönüllülerin belleğine güvenmek",
        "correctAnswer": "A",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "7a7ae171-f68c-5f18-8408-ecc7ced715a3",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Her Eşya Aynı Gün Biter",
        "optionB": "Tamir atölyesinde gerekçeli kararlar",
        "optionC": "Sadece Sayılar Önemlidir",
        "optionD": "İncelemeden Kesin Sonuç",
        "correctAnswer": "B",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "4cadc0bb-f580-547a-86c9-0adf3419bef0",
    "exerciseId": "a1dc447b-f5ce-557c-bd63-852040fb5ca6",
    "ageGroupId": "10000000-0000-0000-0000-000000000004",
    "age": "young-adult",
    "level": 5,
    "title": "Merakı destekleyen kütüphane — Seviye 5",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.\n\nAy sonunda öneri kartları yeniden gözden geçirildi. Bazı istekler hemen yapılmış, bazıları bütçe bekliyordu. Pano, yalnız tamamlanan işleri değil, henüz çözülemeyen ihtiyaçları da gösteriyordu. Görevli, bir isteğin beklemesinin unutulduğu anlamına gelmediğini açıkladı.\n\nKütüphaneye yeni kitaplar geldiğinde herkesin sevdiği türlerden eşit sayıda kitap bulunmadı. Gönüllüler tek bir kişinin tercihine göre karar vermedi. Ödünç alma kayıtlarına, önerilere ve raflarda eksik kalan konulara birlikte baktılar. Kararın gerekçesini panoda paylaştılar.\n\nBazı kitaplar çok sık isteniyordu. Bu kitaplar için sıraya girenlere tahmini dönüş tarihi söylendi. Sıra beklemek istemeyenlere benzer konudaki başka kitaplar gösterildi. Bir seçenek sunmak, aranan kitabın hemen bulunamamasını daha anlaşılır hâle getirdi.\n\nBir okuma buluşmasında aynı öykü hakkında farklı yorumlar yapıldı. Duru kahramanın cesur olduğunu düşünürken arkadaşı aceleci olduğunu söyledi. İkisi de görüşünü öyküdeki bir olayla açıkladı. Farklı yorumların, dikkatle dinlendiğinde konuşmayı zenginleştirebildiğini gördüler.\n\nBuluşmayı yöneten kişi, herkesi aynı görüşte birleştirmeye çalışmadı. Önce yorumların hangi ayrıntıya dayandığını sordu. Sonra öykünün başka bir bölümünü birlikte okudular. Yeni ayrıntılar bazı düşünceleri değiştirdi, bazılarını güçlendirdi. Konuşma, yalnız sonuçtan değil gerekçelerden oluştu.\n\nDuru artık kütüphaneye yalnız kitap almak için gelmiyordu. Bir soruyu nasıl soracağını, bilgiyi nerede arayacağını ve bulduğunu nasıl düzenleyeceğini de deniyordu. Kütüphane onun için bitmiş yanıtların bulunduğu bir yerden çok, merakın geliştiği bir çalışma alanıydı.\n\nYeni bir ziyaretçi kapıdaki haritaya bakıp öykü rafını kolayca bulduğunda Duru sevindi. Küçük bir önerinin işe yaradığı görülmüştü. Yine de haritanın sonsuza kadar değişmeden kalması gerekmiyordu. Raflar ve ihtiyaçlar değiştikçe çizim de yeniden gözden geçirilebilirdi.\n\nHarita yenilendikten sonra kütüphane ekibi ziyaretçilerin sorularını kısa notlarla toplamaya başladı. Bir rafın nerede olduğunu soranlarla belirli bir konu hakkında kaynak arayanların ihtiyaçları aynı değildi. Görevliler soruları yalnız saymak yerine türlerine göre ayırdı. Böylece kapıdaki yönlendirme çizimiyle danışma masasındaki açıklamaların farklı sorunları çözebileceğini gördüler. Tek bir değişiklik bütün ihtiyaçları karşılamak zorunda değildi.\n\nYeni bir okuma grubunda herkes aynı kitabı seçmedi. Katılımcılar önce ilgilendikleri konuları anlattı, ardından erişilebilir birkaç eser önerildi. Bir kitabın kalınlığı onun herkes için zor olduğu anlamına gelmiyordu. Konuya aşinalık, anlatım biçimi ve kullanılan sözcükler de etkiliydi. Duru bir seçimin uygunluğunu yalnız kapaktaki bilgiye bakarak değil, birkaç sayfayı dikkatle okuyarak değerlendirmeyi denedi.\n\nKütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
    "wordCount": 835,
    "source": "../../free-reading/v1/texts/library-long.txt",
    "sourceSha256": "a856f71c5d3b8ff5966873de7e39ab894e018d3d7198980d5f09bb1dbf2fe774",
    "questions": [
      {
        "id": "ed01df90-890f-5193-8dd1-685b27a9bba6",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Kitap kapaklarını yalnız renklerine göre sıralamak",
        "optionB": "Bütün okuyuculara aynı kitabı zorunlu seçtirmek",
        "optionC": "En çok sayfa okuyan kişiyi ödüllendirmek",
        "optionD": "Kütüphanede bilgiye ulaşmayı ve öğrenmeyi birlikte geliştirmek",
        "correctAnswer": "D",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "60d1f4e5-baca-5aa9-8eff-d2983fcd11ce",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Kütüphane, merak ve bilgiye erişimi ortak çalışmayla destekler",
        "optionB": "Kitap seçiminde bütün okuyucuların ihtiyacı aynıdır",
        "optionC": "Bir kaynağa bakmak bütün soruları kesin olarak çözer",
        "optionD": "Kütüphanede yalnız ödünç alınan kitap sayısı önemlidir",
        "correctAnswer": "A",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "61c3a02a-f202-510b-8d46-4d3662d99c47",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Bir okuma hızının herkese uygun olduğunu kanıtlamak",
        "optionB": "Kütüphanenin merakı ve bilgiye erişimi destekleyen rolünü anlatmak",
        "optionC": "Kitapları yalnız kalınlıklarına göre değerlendirmek",
        "optionD": "Bütün ziyaretçileri aynı görüşte birleştirmek",
        "correctAnswer": "B",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "a19fddfa-959f-5860-8a61-ae36737fe746",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Herkese Tek Kitap",
        "optionB": "Sayfa Sayısına Göre Yarış",
        "optionC": "Merakı destekleyen kütüphane",
        "optionD": "Yalnız Kapaklara Bakmak",
        "correctAnswer": "C",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "2ebfd16a-60ba-5d85-8a57-b59cc7c04466",
    "exerciseId": "b0eef605-99a1-52f6-95af-62d48476c4d6",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "age": "adult",
    "level": 3,
    "title": "Sınırları açık bir ortak arşiv — Seviye 3",
    "content": "Kentteki bir kültür derneği, mahallenin geçmişini anlatan küçük bir dijital arşiv hazırlamak istedi. Amaç bütün kentin tarihini eksiksiz yazmak değil, gönüllülerin paylaşmak istediği sınırlı sayıdaki belgeyi açıklamalarıyla birlikte düzenlemekti. Eski dükkân fotoğrafları, etkinlik afişleri ve birkaç kişisel anlatı başlangıç malzemesi olarak seçildi. Projeyi yürüten Elif, ilk toplantıda arşivin neyi kapsamadığını da konuşmayı önerdi. Ellerindeki belgeler her dönemi ve her topluluğu eşit biçimde temsil etmiyordu. Bu durum sonradan küçük bir dipnotla geçiştirilmeyecekti. Arşivin giriş sayfasında kapsam açıkça belirtilecekti. Katılımcılar bir dosyanın ellerinde bulunmasını, onu herkesle paylaşma yetkisine sahip oldukları anlamında kabul etmedi. Kayıt ile yayın için ayrı kararlar verilmesi gerektiğini kararlaştırdılar.\n\nİlk getirilen fotoğraf eski bir fırının önünü gösteriyordu. Fotoğrafı getiren kişi görüntünün çocukluğunda çekildiğini hatırlıyor, fakat kesin bir yıl söyleyemiyordu. Elif dosyanın adına tahmini bir yıl yazmak yerine tarih bilinmiyor notunu ekledi. Fotoğraftaki tabelanın biçimi başka belgelerle karşılaştırılabilirdi, ancak bu henüz yapılmış bir doğrulama değildi. Kayıtta belgenin sahibi tarafından söylenenler ile ekip tarafından kontrol edilenler ayrı alanlara yazıldı. Böylece bir ziyaretçi bilgilerin aynı kesinlik düzeyinde olmadığını anlayabilecekti. Fotoğrafın güzel görünmesi, tarihi hakkında kesin bir cümle kurulmasını gerektirmiyordu. Daha sonra yeni bir belge bulunursa kayıt güncellenebilecek, değişikliğin dayanağı da açıklanacaktı. Belirsizliği korumak dosyayı kullanılamaz yapmadı; hangi sorunun henüz cevaplanmadığını görünür kıldı.\n\nBelgelerin taranması sırasında ekip aynı görüntünün farklı kopyalarını buldu. Bunlardan biri kenarları kesilmiş, diğeri daha düşük çözünürlüklüydü. İki dosyanın aynı fotoğraf olması, her bakımdan eşdeğer oldukları anlamına gelmiyordu. Kenardaki bir yazı kopyaların yalnız birinde okunabiliyordu. Ekip en ayrıntılı kopyayı sakladı, diğerinin nereden geldiğini de kayıtla ilişkilendirdi. Dosyalar gelişigüzel silinmedi. Ziyaretçiye gösterilecek görüntü ile çalışma için korunan asıl kopya ayrı tutuldu. Parlaklık ayarı yapılan bir görüntüye düzenleme notu eklendi. Görüntünün eksik kısmını hayal ederek tamamlamak veya görünmeyen bir sözcüğü kesinmiş gibi yazmak uygun bulunmadı. Amaç daha çekici bir sahne üretmek değil, elde bulunan belgenin anlaşılmasını sağlamaktı.\n\nArşivde arama yapılabilmesi için konu etiketleri hazırlandı. Bir afiş hem spor etkinliği hem mahalle buluşması başlığıyla ilgiliydi. İlk taslakta yalnız bir konu seçilmesi isteniyordu. Gönüllüler bunun bazı belgeleri dar bir başlık içinde saklayabileceğini fark etti. Birden fazla anlamlı etiket kullanılmasına izin verildi, fakat aynı şey için çok sayıda farklı ad üretmemek amacıyla ortak bir liste tutuldu. Listenin kapalı ve değişmez olması gerekmiyordu. Yeni bir ihtiyaç çıktığında ekleme önerilebilirdi. Önce birkaç kişi aradıkları belgeyi etiketlerle bulmayı denedi. Kullanılmayan veya karışıklık yaratan terimler yeniden gözden geçirildi. Etiket sayısının artması kendi başına başarı değildi; aranan belgenin doğru bağlamla bulunmasına yardımcı olması önemliydi.\n\nKişisel anlatıların eklenmesi proje için yeni sorular doğurdu. Aynı meydandaki bir etkinliği iki kişi farklı biçimde hatırlıyordu. Ekip hangisinin daha canlı anlattığına bakarak birini doğru ilan etmedi. Her anlatı, anlatanın hatırası olarak ve paylaşım izni kapsamında kaydedildi. Ayrıntılar doğrulanmadığında böyle olduğu belirtildi. Bir anlatının başka bir kişiden söz etmesi de otomatik yayın izni sayılmadı. Gerekli izin bulunmayan bölümler kamuya açık metne alınmadı. Arşiv yalnız bilgi toplamaya değil, hangi bilginin hangi bağlamda paylaşılabileceğini açıklamaya dayanıyordu. Farklı hatıralar her zaman çözülmesi gereken bir hata değildi. Bazen aynı olayın farklı kişiler için ne ifade ettiğini gösteriyor, okuyucuya yeni araştırma soruları sunuyordu.\n\nArşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
    "wordCount": 531,
    "source": "../../comprehension/v1/texts/community-archive.txt",
    "sourceSha256": "5e8947a4afc100c16cbee52de105ab360490c728c36747b9748bfbec9f92ebda",
    "questions": [
      {
        "id": "35554405-1a4b-56e4-85f7-05ba2e5ff6c8",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Kentin bütün tarihini eksiksiz ve kesin biçimde yazmak",
        "optionB": "Fotoğrafları yalnız estetik değerlerine göre sıralamak",
        "optionC": "Eldeki bütün dosyaları izin incelemeden yayımlamak",
        "optionD": "Mahalle belgelerini kapsamı ve paylaşım yetkisi açık bir arşivde düzenlemek",
        "correctAnswer": "D",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "123905e8-37d5-5fdd-88f4-e221b66c8622",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Arşiv kayıtları kaynakları, belirsizlikleri ve paylaşım sınırlarıyla sunulmalıdır",
        "optionB": "Bir dosyanın elde bulunması onu yayımlama yetkisini otomatik verir",
        "optionC": "Belirsiz kayıtlar kesin bilgiyle tamamlanmış gibi sunulmalıdır",
        "optionD": "Arşiv bütün toplulukları eşit temsil etmek zorundadır ve bunu zaten başarır",
        "correctAnswer": "A",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "66de9d9b-ac4b-5ca1-8e93-86a069a5e974",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "En eski belgenin her zaman en doğru olduğunu kanıtlamak",
        "optionB": "Bir arşiv oluştururken kaynak, kapsam ve paylaşım sınırlarını açıklamanın önemini göstermek",
        "optionC": "Paylaşım yetkisini kayıt sayısına göre belirlemek",
        "optionD": "Bütün kişisel anlatıları aynı kesinlikte sunmak",
        "correctAnswer": "B",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "d99d0757-c9e6-5107-8ab6-c8d93a511909",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Kentin Eksiksiz Tarihi",
        "optionB": "Her Dosya Herkese Açıktır",
        "optionC": "Sınırları açık bir ortak arşiv",
        "optionD": "Belirsizlikleri Silmek",
        "correctAnswer": "C",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "cc4f059f-49b8-5fcd-8b32-8d5b817bbb1d",
    "exerciseId": "b0eef605-99a1-52f6-95af-62d48476c4d6",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "age": "adult",
    "level": 3,
    "title": "Tamir atölyesinde gerekçeli kararlar — Seviye 3",
    "content": "Mahalle kültür merkezinde açılan tamir atölyesi, kullanılmayan eşyaları hemen yenileriyle değiştirmek yerine önce sorunlarını anlamak için kurulmuştu. Gönüllüler ilk toplantıda kırık masa lambaları, fermuarı açılan çantalar ve gevşeyen sandalye ayakları hakkında konuştu. Ancak her eşyanın atölyede güvenle onarılabileceğini varsaymadılar. Elektrik bağlantısı bulunan cihazları yalnız yetkin bir uzmanın incelemesi, diğer katılımcıların ise gözlem ve kayıt işleriyle başlaması kararlaştırıldı. Atölyenin amacı herkesi bir günde usta yapmak değil, bakım kararlarının nasıl verildiğini görünür kılmaktı. Selin kayıt masasını, Bora malzeme dolabını düzenlemeyi üstlendi. İlk buluşmada hiçbir elektrikli cihaz çalıştırılmadı. Katılımcılara, bir sorunu tanımak ile onu onarmaya yetkili olmak arasındaki fark açıklandı. Bu ayrım hazırlık listesinin ilk satırına yazıldı.\n\nSelin her eşya için bir kabul kartı hazırladı. Kartta eşyanın türü, sahibinin anlattığı sorun ve inceleme sonucu için ayrı alanlar vardı. Bir çantanın sahibi, fermuarın geçen haftadan beri takıldığını söyledi. Selin bunu doğrudan arızanın nedeni olarak kaydetmedi; sahibinin gözlemi olarak yazdı. İnceleme sırasında kumaşın fermuara sıkıştığı görüldü. Kartın ikinci alanına bu bulgu eklendi. Böylece anlatılan belirti ile kontrol edilerek bulunan neden birbirine karışmadı. Sahiplerin bütün geçmişini veya ev adresini kaydetmek gerekmiyordu. Geri dönüş için istenen iletişim bilgisi ayrı tutuldu ve yalnız görevlilerin erişimine bırakıldı. Katılımcılar hangi bilginin neden toplandığını okuyabiliyordu. Bir eşyanın fotoğrafı çekilecekse bunun için ayrıca izin soruluyordu.\n\nMalzeme dolabı hazırlanırken Bora bütün parçaları dış görünüşlerine göre aynı kutuya koymak istedi. Deneyimli bir gönüllü, birbirine benzeyen vidaların farklı ölçülerde olabileceğini gösterdi. Kutular ölçü ve kullanım bilgisiyle etiketlendi. Etiketi okunmayan bir parçaya tahminle ad verilmedi; kontrol bekleyen bölüme kondu. Sonra herkes dolaptan örnek bir parça bulmayı denedi. Bazı etiketler doğru olsa da rafın arkasında kaldığı için görülmüyordu. Yerleri değiştirildi ve yazılar büyütüldü. Düzenin başarısı yalnız kutuların düzgün görünmesiyle değil, aranan parçanın doğru bulunmasıyla değerlendirildi. İlk düzenleme fotoğrafı saklandı; sonraki değişikliğin nedeni kısa bir notla açıklandı. Böylece yeni gelen bir gönüllü de etiketlerin hangi ihtiyaçtan doğduğunu anlayabilecekti.\n\nİlk üç buluşmanın sonunda ekip onarılan eşya sayısını karşılaştırdı. Birinci buluşmada on, ikinci buluşmada yedi, üçüncüde on iki eşya tamamlanmıştı. Bora üçüncü buluşmanın kesinlikle en verimli gün olduğunu söyledi. Selin kabul kartlarına bakmayı önerdi. İkinci gün gelen eşyalardan birkaçının incelemesi daha uzun sürmüş, bazıları için parça beklenmişti. Üçüncü gün ise çok sayıda küçük kumaş onarımı yapılmıştı. Sayılar gerçekti, fakat işlerin güçlüğünü tek başına göstermiyordu. Ekip toplam sayının yanına işlem türlerini ve bekleyen işleri de ekledi. Karşılaştırmayı kaldırmadılar; hangi soruya cevap verdiğini daralttılar. Artık tablo, gönüllülerin becerisini sıralayan bir puan değil, farklı günlerde hangi işlerin yapıldığını anlatan bir kayıt olarak kullanılıyordu.\n\nBir katılımcı eski bir lambasının onarılmasını istediğinde güvenlik sınırı yeniden gündeme geldi. Uzman o gün atölyede değildi. Gönüllüler lambayı denemek yerine kabul kartını doldurup inceleme bekleyen bölüme kaldırdı. Lambanın o gün geri verilmemesi katılımcıyı üzdü, fakat neden açıkça anlatıldı. Beklemek, eşyanın önemsiz görüldüğü anlamına gelmiyordu. Uzman değerlendirmesi olmadan yapılan bir deneme başka riskler doğurabilirdi. Ekip bu olayı sonraki toplantıda konuştu ve atölye duyurusuna hizmet sınırlarını ekledi. Her eşya aynı gün onarılacakmış gibi bir söz verilmemeliydi. Tamir edilemeyen veya güvenle incelenemeyen eşya için de anlaşılır bir yönlendirme yapılması gerekiyordu. Bir görevin ertelenmesi, gerekçesi ve sonraki adımıyla birlikte kaydedildiğinde daha kolay anlaşılabiliyordu.\n\nAtölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
    "wordCount": 538,
    "source": "../../comprehension/v1/texts/repair-workshop.txt",
    "sourceSha256": "4f11e94e54fd624803f7d4759adf273eaef86a8fa04028446fa2339a4d27e479",
    "questions": [
      {
        "id": "afc13d21-44f4-5643-84bb-23e2f6849dbe",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Her elektrikli cihazı herkesin hemen onarmasını sağlamak",
        "optionB": "Gönüllüleri yalnız tamamladıkları eşya sayısıyla sıralamak",
        "optionC": "Bütün eşyaları incelemeden yenileriyle değiştirmek",
        "optionD": "Tamir atölyesinde güvenlik ve kayıtlarla ortak kararlar vermek",
        "correctAnswer": "D",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "9e8320de-ddf2-5365-81be-6ae4929a311b",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Atölye kararları, işlem sayısıyla birlikte güvenlik ve gerekçelerle değerlendirilmelidir",
        "optionB": "En çok eşya tamamlanan gün bütün açılardan en başarılı gündür",
        "optionC": "Uzman bulunmasa da her cihazı çalıştırarak denemek gerekir",
        "optionD": "Sahibin anlattığı belirti kontrol edilmeden kesin neden sayılmalıdır",
        "correctAnswer": "A",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "e0e11388-0998-5ac6-880b-7776f98819c5",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Onarılan eşya sayısını ölçülmemiş çevresel tasarrufla eşitlemek",
        "optionB": "Tamir kararlarını güvenlik, kayıt ve kapsamıyla birlikte değerlendirmeyi göstermek",
        "optionC": "Her işi aynı gün bitirme sözü vermek",
        "optionD": "Kayıt tutmak yerine yalnız gönüllülerin belleğine güvenmek",
        "correctAnswer": "B",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "666d3186-e9e6-5a5f-8dea-2b08a5abadef",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Her Eşya Aynı Gün Biter",
        "optionB": "Sadece Sayılar Önemlidir",
        "optionC": "Tamir atölyesinde gerekçeli kararlar",
        "optionD": "İncelemeden Kesin Sonuç",
        "correctAnswer": "C",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "573c4de6-0475-58a0-890e-0f8b126cbf5e",
    "exerciseId": "b0eef605-99a1-52f6-95af-62d48476c4d6",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "age": "adult",
    "level": 3,
    "title": "Merakı destekleyen kütüphane — Seviye 3",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.\n\nAy sonunda öneri kartları yeniden gözden geçirildi. Bazı istekler hemen yapılmış, bazıları bütçe bekliyordu. Pano, yalnız tamamlanan işleri değil, henüz çözülemeyen ihtiyaçları da gösteriyordu. Görevli, bir isteğin beklemesinin unutulduğu anlamına gelmediğini açıkladı.\n\nKütüphaneye yeni kitaplar geldiğinde herkesin sevdiği türlerden eşit sayıda kitap bulunmadı. Gönüllüler tek bir kişinin tercihine göre karar vermedi. Ödünç alma kayıtlarına, önerilere ve raflarda eksik kalan konulara birlikte baktılar. Kararın gerekçesini panoda paylaştılar.\n\nKütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
    "wordCount": 562,
    "source": "../../free-reading/v1/texts/library-long.txt",
    "sourceSha256": "a856f71c5d3b8ff5966873de7e39ab894e018d3d7198980d5f09bb1dbf2fe774",
    "questions": [
      {
        "id": "c40e246b-7e7e-5004-820d-be5604a918a8",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Kitap kapaklarını yalnız renklerine göre sıralamak",
        "optionB": "Kütüphanede bilgiye ulaşmayı ve öğrenmeyi birlikte geliştirmek",
        "optionC": "Bütün okuyuculara aynı kitabı zorunlu seçtirmek",
        "optionD": "En çok sayfa okuyan kişiyi ödüllendirmek",
        "correctAnswer": "B",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "c6246129-c9a1-564b-8f8e-ceed3adc9893",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Kitap seçiminde bütün okuyucuların ihtiyacı aynıdır",
        "optionB": "Bir kaynağa bakmak bütün soruları kesin olarak çözer",
        "optionC": "Kütüphane, merak ve bilgiye erişimi ortak çalışmayla destekler",
        "optionD": "Kütüphanede yalnız ödünç alınan kitap sayısı önemlidir",
        "correctAnswer": "C",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "7bcee9c9-8221-53ca-831a-268cccf94e0c",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Bir okuma hızının herkese uygun olduğunu kanıtlamak",
        "optionB": "Kitapları yalnız kalınlıklarına göre değerlendirmek",
        "optionC": "Bütün ziyaretçileri aynı görüşte birleştirmek",
        "optionD": "Kütüphanenin merakı ve bilgiye erişimi destekleyen rolünü anlatmak",
        "correctAnswer": "D",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "d823aa7c-c1b7-5816-8fad-19469f38ba21",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Merakı destekleyen kütüphane",
        "optionB": "Herkese Tek Kitap",
        "optionC": "Sayfa Sayısına Göre Yarış",
        "optionD": "Yalnız Kapaklara Bakmak",
        "correctAnswer": "A",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "161ed105-d66f-5a99-82e1-7739ac626894",
    "exerciseId": "a4d35a8a-6f3c-57ba-a5a1-d8519105fb70",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "age": "adult",
    "level": 4,
    "title": "Sınırları açık bir ortak arşiv — Seviye 4",
    "content": "Kentteki bir kültür derneği, mahallenin geçmişini anlatan küçük bir dijital arşiv hazırlamak istedi. Amaç bütün kentin tarihini eksiksiz yazmak değil, gönüllülerin paylaşmak istediği sınırlı sayıdaki belgeyi açıklamalarıyla birlikte düzenlemekti. Eski dükkân fotoğrafları, etkinlik afişleri ve birkaç kişisel anlatı başlangıç malzemesi olarak seçildi. Projeyi yürüten Elif, ilk toplantıda arşivin neyi kapsamadığını da konuşmayı önerdi. Ellerindeki belgeler her dönemi ve her topluluğu eşit biçimde temsil etmiyordu. Bu durum sonradan küçük bir dipnotla geçiştirilmeyecekti. Arşivin giriş sayfasında kapsam açıkça belirtilecekti. Katılımcılar bir dosyanın ellerinde bulunmasını, onu herkesle paylaşma yetkisine sahip oldukları anlamında kabul etmedi. Kayıt ile yayın için ayrı kararlar verilmesi gerektiğini kararlaştırdılar.\n\nİlk getirilen fotoğraf eski bir fırının önünü gösteriyordu. Fotoğrafı getiren kişi görüntünün çocukluğunda çekildiğini hatırlıyor, fakat kesin bir yıl söyleyemiyordu. Elif dosyanın adına tahmini bir yıl yazmak yerine tarih bilinmiyor notunu ekledi. Fotoğraftaki tabelanın biçimi başka belgelerle karşılaştırılabilirdi, ancak bu henüz yapılmış bir doğrulama değildi. Kayıtta belgenin sahibi tarafından söylenenler ile ekip tarafından kontrol edilenler ayrı alanlara yazıldı. Böylece bir ziyaretçi bilgilerin aynı kesinlik düzeyinde olmadığını anlayabilecekti. Fotoğrafın güzel görünmesi, tarihi hakkında kesin bir cümle kurulmasını gerektirmiyordu. Daha sonra yeni bir belge bulunursa kayıt güncellenebilecek, değişikliğin dayanağı da açıklanacaktı. Belirsizliği korumak dosyayı kullanılamaz yapmadı; hangi sorunun henüz cevaplanmadığını görünür kıldı.\n\nBelgelerin taranması sırasında ekip aynı görüntünün farklı kopyalarını buldu. Bunlardan biri kenarları kesilmiş, diğeri daha düşük çözünürlüklüydü. İki dosyanın aynı fotoğraf olması, her bakımdan eşdeğer oldukları anlamına gelmiyordu. Kenardaki bir yazı kopyaların yalnız birinde okunabiliyordu. Ekip en ayrıntılı kopyayı sakladı, diğerinin nereden geldiğini de kayıtla ilişkilendirdi. Dosyalar gelişigüzel silinmedi. Ziyaretçiye gösterilecek görüntü ile çalışma için korunan asıl kopya ayrı tutuldu. Parlaklık ayarı yapılan bir görüntüye düzenleme notu eklendi. Görüntünün eksik kısmını hayal ederek tamamlamak veya görünmeyen bir sözcüğü kesinmiş gibi yazmak uygun bulunmadı. Amaç daha çekici bir sahne üretmek değil, elde bulunan belgenin anlaşılmasını sağlamaktı.\n\nArşivde arama yapılabilmesi için konu etiketleri hazırlandı. Bir afiş hem spor etkinliği hem mahalle buluşması başlığıyla ilgiliydi. İlk taslakta yalnız bir konu seçilmesi isteniyordu. Gönüllüler bunun bazı belgeleri dar bir başlık içinde saklayabileceğini fark etti. Birden fazla anlamlı etiket kullanılmasına izin verildi, fakat aynı şey için çok sayıda farklı ad üretmemek amacıyla ortak bir liste tutuldu. Listenin kapalı ve değişmez olması gerekmiyordu. Yeni bir ihtiyaç çıktığında ekleme önerilebilirdi. Önce birkaç kişi aradıkları belgeyi etiketlerle bulmayı denedi. Kullanılmayan veya karışıklık yaratan terimler yeniden gözden geçirildi. Etiket sayısının artması kendi başına başarı değildi; aranan belgenin doğru bağlamla bulunmasına yardımcı olması önemliydi.\n\nKişisel anlatıların eklenmesi proje için yeni sorular doğurdu. Aynı meydandaki bir etkinliği iki kişi farklı biçimde hatırlıyordu. Ekip hangisinin daha canlı anlattığına bakarak birini doğru ilan etmedi. Her anlatı, anlatanın hatırası olarak ve paylaşım izni kapsamında kaydedildi. Ayrıntılar doğrulanmadığında böyle olduğu belirtildi. Bir anlatının başka bir kişiden söz etmesi de otomatik yayın izni sayılmadı. Gerekli izin bulunmayan bölümler kamuya açık metne alınmadı. Arşiv yalnız bilgi toplamaya değil, hangi bilginin hangi bağlamda paylaşılabileceğini açıklamaya dayanıyordu. Farklı hatıralar her zaman çözülmesi gereken bir hata değildi. Bazen aynı olayın farklı kişiler için ne ifade ettiğini gösteriyor, okuyucuya yeni araştırma soruları sunuyordu.\n\nİlk deneme sayfasını kullananlar fotoğrafların altındaki açıklamalara baktı. Bazı ziyaretçiler kimin fotoğrafı olduğunu, bazıları ise görüntünün hangi sokağa ait olduğunu arıyordu. İlk tasarımda kaynak bilgisi uzun bir metnin sonunda kalmıştı. Elif bu bilgiyi başlığa yakın, kısa bir alana taşıdı. Ayrıntılı inceleme notu isteyenler için ayrı bir bağlantı korundu. Metni sadeleştirmek, belirsizlik ifadelerini çıkarmak anlamına gelmedi. Büyük yazı ve düzenli başlıklar, aynı açıklamayı daha kolay izlemeyi sağladı. Ekip yalnız bilgisayardan değil, küçük ekranlı cihazlardan da sayfayı denedi. Bir tasarımın hazırlayan kişiye anlaşılır gelmesi yeterli değildi; farklı okuyucuların ihtiyaçlarına göre sınanması gerekiyordu. Denemeden sonra değiştirilen alanlar ayrı bir listede belirtildi.\n\nProjenin tanıtımı için toplam belge sayısını vermek kolaydı. Ancak sayı, arşivin kapsamını tek başına anlatmıyordu. Aynı etkinlikten onlarca fotoğraf varken başka bir dönemden yalnız bir afiş bulunuyordu. Çok sayıda dosyanın olması bütün mahalle geçmişinin dengeli temsil edildiğini göstermiyordu. Ekip sayının yanına belge türleri ve dönemlere göre dağılımı da ekledi. Eksik kalan dönemler açıklandı. Yeni katkı çağrısı özellikle bu boşluklara odaklandı, fakat çağrının onları mutlaka dolduracağı sözü verilmedi. Mevcut arşivin değeri ile eksiksiz olma iddiası birbirinden ayrıldı. Ziyaretçilerin sayıyı ne anlama geldiğini bilerek kullanması amaçlandı. Bir ölçünün görünür olması, onun başka bütün nitelikleri temsil ettiği anlamına gelmezdi.\n\nArşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
    "wordCount": 725,
    "source": "../../comprehension/v1/texts/community-archive.txt",
    "sourceSha256": "5e8947a4afc100c16cbee52de105ab360490c728c36747b9748bfbec9f92ebda",
    "questions": [
      {
        "id": "c870e169-bced-5abc-87cf-df12052c211f",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Mahalle belgelerini kapsamı ve paylaşım yetkisi açık bir arşivde düzenlemek",
        "optionB": "Kentin bütün tarihini eksiksiz ve kesin biçimde yazmak",
        "optionC": "Fotoğrafları yalnız estetik değerlerine göre sıralamak",
        "optionD": "Eldeki bütün dosyaları izin incelemeden yayımlamak",
        "correctAnswer": "A",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "eadf35db-9c62-5028-815c-a6c3cb7e5c8f",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Bir dosyanın elde bulunması onu yayımlama yetkisini otomatik verir",
        "optionB": "Arşiv kayıtları kaynakları, belirsizlikleri ve paylaşım sınırlarıyla sunulmalıdır",
        "optionC": "Belirsiz kayıtlar kesin bilgiyle tamamlanmış gibi sunulmalıdır",
        "optionD": "Arşiv bütün toplulukları eşit temsil etmek zorundadır ve bunu zaten başarır",
        "correctAnswer": "B",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "0a38c32f-72cd-57a7-8758-c5ef5c3e36e6",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "En eski belgenin her zaman en doğru olduğunu kanıtlamak",
        "optionB": "Paylaşım yetkisini kayıt sayısına göre belirlemek",
        "optionC": "Bir arşiv oluştururken kaynak, kapsam ve paylaşım sınırlarını açıklamanın önemini göstermek",
        "optionD": "Bütün kişisel anlatıları aynı kesinlikte sunmak",
        "correctAnswer": "C",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "fcab9b6d-a72b-5c6a-8517-6e35a812e5bc",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Kentin Eksiksiz Tarihi",
        "optionB": "Her Dosya Herkese Açıktır",
        "optionC": "Belirsizlikleri Silmek",
        "optionD": "Sınırları açık bir ortak arşiv",
        "correctAnswer": "D",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "9de768de-5685-5ff2-8459-841de49bb796",
    "exerciseId": "a4d35a8a-6f3c-57ba-a5a1-d8519105fb70",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "age": "adult",
    "level": 4,
    "title": "Tamir atölyesinde gerekçeli kararlar — Seviye 4",
    "content": "Mahalle kültür merkezinde açılan tamir atölyesi, kullanılmayan eşyaları hemen yenileriyle değiştirmek yerine önce sorunlarını anlamak için kurulmuştu. Gönüllüler ilk toplantıda kırık masa lambaları, fermuarı açılan çantalar ve gevşeyen sandalye ayakları hakkında konuştu. Ancak her eşyanın atölyede güvenle onarılabileceğini varsaymadılar. Elektrik bağlantısı bulunan cihazları yalnız yetkin bir uzmanın incelemesi, diğer katılımcıların ise gözlem ve kayıt işleriyle başlaması kararlaştırıldı. Atölyenin amacı herkesi bir günde usta yapmak değil, bakım kararlarının nasıl verildiğini görünür kılmaktı. Selin kayıt masasını, Bora malzeme dolabını düzenlemeyi üstlendi. İlk buluşmada hiçbir elektrikli cihaz çalıştırılmadı. Katılımcılara, bir sorunu tanımak ile onu onarmaya yetkili olmak arasındaki fark açıklandı. Bu ayrım hazırlık listesinin ilk satırına yazıldı.\n\nSelin her eşya için bir kabul kartı hazırladı. Kartta eşyanın türü, sahibinin anlattığı sorun ve inceleme sonucu için ayrı alanlar vardı. Bir çantanın sahibi, fermuarın geçen haftadan beri takıldığını söyledi. Selin bunu doğrudan arızanın nedeni olarak kaydetmedi; sahibinin gözlemi olarak yazdı. İnceleme sırasında kumaşın fermuara sıkıştığı görüldü. Kartın ikinci alanına bu bulgu eklendi. Böylece anlatılan belirti ile kontrol edilerek bulunan neden birbirine karışmadı. Sahiplerin bütün geçmişini veya ev adresini kaydetmek gerekmiyordu. Geri dönüş için istenen iletişim bilgisi ayrı tutuldu ve yalnız görevlilerin erişimine bırakıldı. Katılımcılar hangi bilginin neden toplandığını okuyabiliyordu. Bir eşyanın fotoğrafı çekilecekse bunun için ayrıca izin soruluyordu.\n\nMalzeme dolabı hazırlanırken Bora bütün parçaları dış görünüşlerine göre aynı kutuya koymak istedi. Deneyimli bir gönüllü, birbirine benzeyen vidaların farklı ölçülerde olabileceğini gösterdi. Kutular ölçü ve kullanım bilgisiyle etiketlendi. Etiketi okunmayan bir parçaya tahminle ad verilmedi; kontrol bekleyen bölüme kondu. Sonra herkes dolaptan örnek bir parça bulmayı denedi. Bazı etiketler doğru olsa da rafın arkasında kaldığı için görülmüyordu. Yerleri değiştirildi ve yazılar büyütüldü. Düzenin başarısı yalnız kutuların düzgün görünmesiyle değil, aranan parçanın doğru bulunmasıyla değerlendirildi. İlk düzenleme fotoğrafı saklandı; sonraki değişikliğin nedeni kısa bir notla açıklandı. Böylece yeni gelen bir gönüllü de etiketlerin hangi ihtiyaçtan doğduğunu anlayabilecekti.\n\nİlk üç buluşmanın sonunda ekip onarılan eşya sayısını karşılaştırdı. Birinci buluşmada on, ikinci buluşmada yedi, üçüncüde on iki eşya tamamlanmıştı. Bora üçüncü buluşmanın kesinlikle en verimli gün olduğunu söyledi. Selin kabul kartlarına bakmayı önerdi. İkinci gün gelen eşyalardan birkaçının incelemesi daha uzun sürmüş, bazıları için parça beklenmişti. Üçüncü gün ise çok sayıda küçük kumaş onarımı yapılmıştı. Sayılar gerçekti, fakat işlerin güçlüğünü tek başına göstermiyordu. Ekip toplam sayının yanına işlem türlerini ve bekleyen işleri de ekledi. Karşılaştırmayı kaldırmadılar; hangi soruya cevap verdiğini daralttılar. Artık tablo, gönüllülerin becerisini sıralayan bir puan değil, farklı günlerde hangi işlerin yapıldığını anlatan bir kayıt olarak kullanılıyordu.\n\nBir katılımcı eski bir lambasının onarılmasını istediğinde güvenlik sınırı yeniden gündeme geldi. Uzman o gün atölyede değildi. Gönüllüler lambayı denemek yerine kabul kartını doldurup inceleme bekleyen bölüme kaldırdı. Lambanın o gün geri verilmemesi katılımcıyı üzdü, fakat neden açıkça anlatıldı. Beklemek, eşyanın önemsiz görüldüğü anlamına gelmiyordu. Uzman değerlendirmesi olmadan yapılan bir deneme başka riskler doğurabilirdi. Ekip bu olayı sonraki toplantıda konuştu ve atölye duyurusuna hizmet sınırlarını ekledi. Her eşya aynı gün onarılacakmış gibi bir söz verilmemeliydi. Tamir edilemeyen veya güvenle incelenemeyen eşya için de anlaşılır bir yönlendirme yapılması gerekiyordu. Bir görevin ertelenmesi, gerekçesi ve sonraki adımıyla birlikte kaydedildiğinde daha kolay anlaşılabiliyordu.\n\nAy sonunda malzeme alımı için sınırlı bir bütçe ayrıldı. Gönüllüler yalnız en çok kullanılan parçaları satın almayı önerdi. Ancak kabul kayıtlarında, eksik bir parça yüzünden sürekli bekleyen birkaç iş de bulunuyordu. Sık kullanım ile eksiklikten doğan bekleme aynı ölçüt değildi. Ekip iki listeyi birlikte inceledi. Önce temel sarf malzemeleri, ardından birden fazla işi tamamlayacak belirli parçalar seçildi. Her talep karşılanamadığı için alınmayanların gerekçesi de yazıldı. Kararı açıklamak, herkesin aynı tercihi yapacağı anlamına gelmiyordu; farklı tercihlerin hangi sınırlar içinde tartışıldığını gösteriyordu. Liste daha sonra değiştirilebilirdi. Bütçe artırılırsa veya eşya türleri değişirse önceki sıralama yeniden değerlendirilecekti. Böylece satın alma kararı tek bir kişinin alışkanlığına bağlı kalmadı.\n\nAtölyenin açık kaldığı saatleri değiştirmek isteyenler bir katılım çizelgesi hazırladı. Sabah gelenlerle akşam gelebilenlerin aynı kişiler olmadığı görüldü. Yalnız mevcut ziyaretçilere soru sorulursa hiç gelemeyenlerin ihtiyaçları görünmeyebilirdi. Selin bu sınırlılığı önerinin başına yazdı. Kültür merkezinin başka etkinliklerine katılanlara da uygun saatler soruldu, fakat yanıt vermeleri zorunlu tutulmadı. Çizelge belirli insanların gün içindeki hareketlerini takip etmek için kullanılmadı. Amaç, birkaç olası zaman aralığını karşılaştırmaktı. Ekip tek bir saati herkes için ideal ilan etmedi. İki farklı saat aralığını kısa süreli denemeyi ve katılımı yeniden değerlendirmeyi önerdi. Yeni saatler uygulanmadan önce görevli bulunabilirliği ve merkezin diğer etkinlikleri de kontrol edilecekti.\n\nAtölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
    "wordCount": 739,
    "source": "../../comprehension/v1/texts/repair-workshop.txt",
    "sourceSha256": "4f11e94e54fd624803f7d4759adf273eaef86a8fa04028446fa2339a4d27e479",
    "questions": [
      {
        "id": "2991d756-6bac-5b72-857c-2f766319f986",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Tamir atölyesinde güvenlik ve kayıtlarla ortak kararlar vermek",
        "optionB": "Her elektrikli cihazı herkesin hemen onarmasını sağlamak",
        "optionC": "Gönüllüleri yalnız tamamladıkları eşya sayısıyla sıralamak",
        "optionD": "Bütün eşyaları incelemeden yenileriyle değiştirmek",
        "correctAnswer": "A",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "53b614c9-dab3-5a66-8922-8c666ac9289c",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "En çok eşya tamamlanan gün bütün açılardan en başarılı gündür",
        "optionB": "Atölye kararları, işlem sayısıyla birlikte güvenlik ve gerekçelerle değerlendirilmelidir",
        "optionC": "Uzman bulunmasa da her cihazı çalıştırarak denemek gerekir",
        "optionD": "Sahibin anlattığı belirti kontrol edilmeden kesin neden sayılmalıdır",
        "correctAnswer": "B",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "f9e133d4-cb26-568b-83af-f8765e3bf38b",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Onarılan eşya sayısını ölçülmemiş çevresel tasarrufla eşitlemek",
        "optionB": "Her işi aynı gün bitirme sözü vermek",
        "optionC": "Tamir kararlarını güvenlik, kayıt ve kapsamıyla birlikte değerlendirmeyi göstermek",
        "optionD": "Kayıt tutmak yerine yalnız gönüllülerin belleğine güvenmek",
        "correctAnswer": "C",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "22dfeb19-e5df-535d-8af6-631e342c5445",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Her Eşya Aynı Gün Biter",
        "optionB": "Sadece Sayılar Önemlidir",
        "optionC": "İncelemeden Kesin Sonuç",
        "optionD": "Tamir atölyesinde gerekçeli kararlar",
        "correctAnswer": "D",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "e44949c0-b8d8-50ec-822e-102ea567282b",
    "exerciseId": "a4d35a8a-6f3c-57ba-a5a1-d8519105fb70",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "age": "adult",
    "level": 4,
    "title": "Merakı destekleyen kütüphane — Seviye 4",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.\n\nAy sonunda öneri kartları yeniden gözden geçirildi. Bazı istekler hemen yapılmış, bazıları bütçe bekliyordu. Pano, yalnız tamamlanan işleri değil, henüz çözülemeyen ihtiyaçları da gösteriyordu. Görevli, bir isteğin beklemesinin unutulduğu anlamına gelmediğini açıkladı.\n\nKütüphaneye yeni kitaplar geldiğinde herkesin sevdiği türlerden eşit sayıda kitap bulunmadı. Gönüllüler tek bir kişinin tercihine göre karar vermedi. Ödünç alma kayıtlarına, önerilere ve raflarda eksik kalan konulara birlikte baktılar. Kararın gerekçesini panoda paylaştılar.\n\nBazı kitaplar çok sık isteniyordu. Bu kitaplar için sıraya girenlere tahmini dönüş tarihi söylendi. Sıra beklemek istemeyenlere benzer konudaki başka kitaplar gösterildi. Bir seçenek sunmak, aranan kitabın hemen bulunamamasını daha anlaşılır hâle getirdi.\n\nBir okuma buluşmasında aynı öykü hakkında farklı yorumlar yapıldı. Duru kahramanın cesur olduğunu düşünürken arkadaşı aceleci olduğunu söyledi. İkisi de görüşünü öyküdeki bir olayla açıkladı. Farklı yorumların, dikkatle dinlendiğinde konuşmayı zenginleştirebildiğini gördüler.\n\nBuluşmayı yöneten kişi, herkesi aynı görüşte birleştirmeye çalışmadı. Önce yorumların hangi ayrıntıya dayandığını sordu. Sonra öykünün başka bir bölümünü birlikte okudular. Yeni ayrıntılar bazı düşünceleri değiştirdi, bazılarını güçlendirdi. Konuşma, yalnız sonuçtan değil gerekçelerden oluştu.\n\nDuru artık kütüphaneye yalnız kitap almak için gelmiyordu. Bir soruyu nasıl soracağını, bilgiyi nerede arayacağını ve bulduğunu nasıl düzenleyeceğini de deniyordu. Kütüphane onun için bitmiş yanıtların bulunduğu bir yerden çok, merakın geliştiği bir çalışma alanıydı.\n\nYeni bir ziyaretçi kapıdaki haritaya bakıp öykü rafını kolayca bulduğunda Duru sevindi. Küçük bir önerinin işe yaradığı görülmüştü. Yine de haritanın sonsuza kadar değişmeden kalması gerekmiyordu. Raflar ve ihtiyaçlar değiştikçe çizim de yeniden gözden geçirilebilirdi.\n\nKütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
    "wordCount": 731,
    "source": "../../free-reading/v1/texts/library-long.txt",
    "sourceSha256": "a856f71c5d3b8ff5966873de7e39ab894e018d3d7198980d5f09bb1dbf2fe774",
    "questions": [
      {
        "id": "d5bd8d67-8869-53a1-8168-372940502dd3",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Kütüphanede bilgiye ulaşmayı ve öğrenmeyi birlikte geliştirmek",
        "optionB": "Kitap kapaklarını yalnız renklerine göre sıralamak",
        "optionC": "Bütün okuyuculara aynı kitabı zorunlu seçtirmek",
        "optionD": "En çok sayfa okuyan kişiyi ödüllendirmek",
        "correctAnswer": "A",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "fe028883-0db8-5c85-8b95-9f5ea15ee7fe",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Kitap seçiminde bütün okuyucuların ihtiyacı aynıdır",
        "optionB": "Kütüphane, merak ve bilgiye erişimi ortak çalışmayla destekler",
        "optionC": "Bir kaynağa bakmak bütün soruları kesin olarak çözer",
        "optionD": "Kütüphanede yalnız ödünç alınan kitap sayısı önemlidir",
        "correctAnswer": "B",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "5f41150e-68f5-5e99-8430-f748e514b4f1",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Bir okuma hızının herkese uygun olduğunu kanıtlamak",
        "optionB": "Kitapları yalnız kalınlıklarına göre değerlendirmek",
        "optionC": "Kütüphanenin merakı ve bilgiye erişimi destekleyen rolünü anlatmak",
        "optionD": "Bütün ziyaretçileri aynı görüşte birleştirmek",
        "correctAnswer": "C",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "0ca3e40f-c137-527d-8521-601f85771c58",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Herkese Tek Kitap",
        "optionB": "Sayfa Sayısına Göre Yarış",
        "optionC": "Yalnız Kapaklara Bakmak",
        "optionD": "Merakı destekleyen kütüphane",
        "correctAnswer": "D",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "f4e4e434-6a58-5fa7-8cbe-a7ccef0e9e5a",
    "exerciseId": "8f6c9722-b784-54c2-bf22-98c32bb9716f",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "age": "adult",
    "level": 5,
    "title": "Sınırları açık bir ortak arşiv — Seviye 5",
    "content": "Kentteki bir kültür derneği, mahallenin geçmişini anlatan küçük bir dijital arşiv hazırlamak istedi. Amaç bütün kentin tarihini eksiksiz yazmak değil, gönüllülerin paylaşmak istediği sınırlı sayıdaki belgeyi açıklamalarıyla birlikte düzenlemekti. Eski dükkân fotoğrafları, etkinlik afişleri ve birkaç kişisel anlatı başlangıç malzemesi olarak seçildi. Projeyi yürüten Elif, ilk toplantıda arşivin neyi kapsamadığını da konuşmayı önerdi. Ellerindeki belgeler her dönemi ve her topluluğu eşit biçimde temsil etmiyordu. Bu durum sonradan küçük bir dipnotla geçiştirilmeyecekti. Arşivin giriş sayfasında kapsam açıkça belirtilecekti. Katılımcılar bir dosyanın ellerinde bulunmasını, onu herkesle paylaşma yetkisine sahip oldukları anlamında kabul etmedi. Kayıt ile yayın için ayrı kararlar verilmesi gerektiğini kararlaştırdılar.\n\nİlk getirilen fotoğraf eski bir fırının önünü gösteriyordu. Fotoğrafı getiren kişi görüntünün çocukluğunda çekildiğini hatırlıyor, fakat kesin bir yıl söyleyemiyordu. Elif dosyanın adına tahmini bir yıl yazmak yerine tarih bilinmiyor notunu ekledi. Fotoğraftaki tabelanın biçimi başka belgelerle karşılaştırılabilirdi, ancak bu henüz yapılmış bir doğrulama değildi. Kayıtta belgenin sahibi tarafından söylenenler ile ekip tarafından kontrol edilenler ayrı alanlara yazıldı. Böylece bir ziyaretçi bilgilerin aynı kesinlik düzeyinde olmadığını anlayabilecekti. Fotoğrafın güzel görünmesi, tarihi hakkında kesin bir cümle kurulmasını gerektirmiyordu. Daha sonra yeni bir belge bulunursa kayıt güncellenebilecek, değişikliğin dayanağı da açıklanacaktı. Belirsizliği korumak dosyayı kullanılamaz yapmadı; hangi sorunun henüz cevaplanmadığını görünür kıldı.\n\nBelgelerin taranması sırasında ekip aynı görüntünün farklı kopyalarını buldu. Bunlardan biri kenarları kesilmiş, diğeri daha düşük çözünürlüklüydü. İki dosyanın aynı fotoğraf olması, her bakımdan eşdeğer oldukları anlamına gelmiyordu. Kenardaki bir yazı kopyaların yalnız birinde okunabiliyordu. Ekip en ayrıntılı kopyayı sakladı, diğerinin nereden geldiğini de kayıtla ilişkilendirdi. Dosyalar gelişigüzel silinmedi. Ziyaretçiye gösterilecek görüntü ile çalışma için korunan asıl kopya ayrı tutuldu. Parlaklık ayarı yapılan bir görüntüye düzenleme notu eklendi. Görüntünün eksik kısmını hayal ederek tamamlamak veya görünmeyen bir sözcüğü kesinmiş gibi yazmak uygun bulunmadı. Amaç daha çekici bir sahne üretmek değil, elde bulunan belgenin anlaşılmasını sağlamaktı.\n\nArşivde arama yapılabilmesi için konu etiketleri hazırlandı. Bir afiş hem spor etkinliği hem mahalle buluşması başlığıyla ilgiliydi. İlk taslakta yalnız bir konu seçilmesi isteniyordu. Gönüllüler bunun bazı belgeleri dar bir başlık içinde saklayabileceğini fark etti. Birden fazla anlamlı etiket kullanılmasına izin verildi, fakat aynı şey için çok sayıda farklı ad üretmemek amacıyla ortak bir liste tutuldu. Listenin kapalı ve değişmez olması gerekmiyordu. Yeni bir ihtiyaç çıktığında ekleme önerilebilirdi. Önce birkaç kişi aradıkları belgeyi etiketlerle bulmayı denedi. Kullanılmayan veya karışıklık yaratan terimler yeniden gözden geçirildi. Etiket sayısının artması kendi başına başarı değildi; aranan belgenin doğru bağlamla bulunmasına yardımcı olması önemliydi.\n\nKişisel anlatıların eklenmesi proje için yeni sorular doğurdu. Aynı meydandaki bir etkinliği iki kişi farklı biçimde hatırlıyordu. Ekip hangisinin daha canlı anlattığına bakarak birini doğru ilan etmedi. Her anlatı, anlatanın hatırası olarak ve paylaşım izni kapsamında kaydedildi. Ayrıntılar doğrulanmadığında böyle olduğu belirtildi. Bir anlatının başka bir kişiden söz etmesi de otomatik yayın izni sayılmadı. Gerekli izin bulunmayan bölümler kamuya açık metne alınmadı. Arşiv yalnız bilgi toplamaya değil, hangi bilginin hangi bağlamda paylaşılabileceğini açıklamaya dayanıyordu. Farklı hatıralar her zaman çözülmesi gereken bir hata değildi. Bazen aynı olayın farklı kişiler için ne ifade ettiğini gösteriyor, okuyucuya yeni araştırma soruları sunuyordu.\n\nİlk deneme sayfasını kullananlar fotoğrafların altındaki açıklamalara baktı. Bazı ziyaretçiler kimin fotoğrafı olduğunu, bazıları ise görüntünün hangi sokağa ait olduğunu arıyordu. İlk tasarımda kaynak bilgisi uzun bir metnin sonunda kalmıştı. Elif bu bilgiyi başlığa yakın, kısa bir alana taşıdı. Ayrıntılı inceleme notu isteyenler için ayrı bir bağlantı korundu. Metni sadeleştirmek, belirsizlik ifadelerini çıkarmak anlamına gelmedi. Büyük yazı ve düzenli başlıklar, aynı açıklamayı daha kolay izlemeyi sağladı. Ekip yalnız bilgisayardan değil, küçük ekranlı cihazlardan da sayfayı denedi. Bir tasarımın hazırlayan kişiye anlaşılır gelmesi yeterli değildi; farklı okuyucuların ihtiyaçlarına göre sınanması gerekiyordu. Denemeden sonra değiştirilen alanlar ayrı bir listede belirtildi.\n\nProjenin tanıtımı için toplam belge sayısını vermek kolaydı. Ancak sayı, arşivin kapsamını tek başına anlatmıyordu. Aynı etkinlikten onlarca fotoğraf varken başka bir dönemden yalnız bir afiş bulunuyordu. Çok sayıda dosyanın olması bütün mahalle geçmişinin dengeli temsil edildiğini göstermiyordu. Ekip sayının yanına belge türleri ve dönemlere göre dağılımı da ekledi. Eksik kalan dönemler açıklandı. Yeni katkı çağrısı özellikle bu boşluklara odaklandı, fakat çağrının onları mutlaka dolduracağı sözü verilmedi. Mevcut arşivin değeri ile eksiksiz olma iddiası birbirinden ayrıldı. Ziyaretçilerin sayıyı ne anlama geldiğini bilerek kullanması amaçlandı. Bir ölçünün görünür olması, onun başka bütün nitelikleri temsil ettiği anlamına gelmezdi.\n\nBir ziyaretçi bir fotoğraftaki sokak adının yanlış yazıldığını bildirdi. Ekip öneriyi hemen kabul etmek veya reddetmek yerine dayanağını sordu. Ziyaretçinin gönderdiği başka bir afiş, aynı köşeyi eski bir adla gösteriyordu. Kaynaklar yeniden karşılaştırıldı ve hatanın bulunduğu alan düzeltildi. Eski kayıt tamamen yok edilmedi; düzeltme tarihçesinde önceki ad ve değişikliğin nedeni korundu. Böylece daha önce arşivi kullanan birinin farklı bilgiyle karşılaşması açıklanabilirdi. Her öneri doğru kabul edilse kayıtlar kolayca karışabilirdi. Hiçbir öneri dikkate alınmasa hatalar sürerdi. Ekip bu iki uç arasında kontrol edilebilir bir inceleme adımı belirledi. Başvurunun alındığı ve incelendiği durumlar da katkı sahibine anlaşılır biçimde bildirildi.\n\nArşivi başka bir gönüllü grubuna devretmek gündeme geldiğinde dosya yapısının önemi görüldü. Bazı klasör adlarını yalnız Elif anlıyordu. Kısaltmalar açıklanmadığı için yeni ekip aynı belgeyi iki yerde arayabilirdi. Klasörler düzenlendi ve dosya adlandırma kuralı kısa örneklerle anlatıldı. Devir sırasında paylaşım izni bulunmayan belgeler kamuya açık dosyalarla karıştırılmadı. Yeni ekip hangi kaydın inceleme, hangisinin yayın için hazır olduğunu öğrenmek zorundaydı. Birlikte örnek bir belge üzerinde çalışıldı. Sonuç dosyasını göstermek kadar ona ulaşırken verilen kararları anlatmak da önemliydi. Sorular çıktıkça yönergeye açıklama eklendi. Devir işi yalnız bir klasörü kopyalamak değil, kayıtların nasıl yorumlanacağını da paylaşmaktı.\n\nArşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
    "wordCount": 915,
    "source": "../../comprehension/v1/texts/community-archive.txt",
    "sourceSha256": "5e8947a4afc100c16cbee52de105ab360490c728c36747b9748bfbec9f92ebda",
    "questions": [
      {
        "id": "117cd84a-1a80-5157-8347-204ca29c92d3",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Kentin bütün tarihini eksiksiz ve kesin biçimde yazmak",
        "optionB": "Mahalle belgelerini kapsamı ve paylaşım yetkisi açık bir arşivde düzenlemek",
        "optionC": "Fotoğrafları yalnız estetik değerlerine göre sıralamak",
        "optionD": "Eldeki bütün dosyaları izin incelemeden yayımlamak",
        "correctAnswer": "B",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "6e94d427-63d1-5719-84ec-48ebe4fbf0d4",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Bir dosyanın elde bulunması onu yayımlama yetkisini otomatik verir",
        "optionB": "Belirsiz kayıtlar kesin bilgiyle tamamlanmış gibi sunulmalıdır",
        "optionC": "Arşiv kayıtları kaynakları, belirsizlikleri ve paylaşım sınırlarıyla sunulmalıdır",
        "optionD": "Arşiv bütün toplulukları eşit temsil etmek zorundadır ve bunu zaten başarır",
        "correctAnswer": "C",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "6fb5aa4f-4ed1-56f2-887e-5372e87b0226",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "En eski belgenin her zaman en doğru olduğunu kanıtlamak",
        "optionB": "Paylaşım yetkisini kayıt sayısına göre belirlemek",
        "optionC": "Bütün kişisel anlatıları aynı kesinlikte sunmak",
        "optionD": "Bir arşiv oluştururken kaynak, kapsam ve paylaşım sınırlarını açıklamanın önemini göstermek",
        "correctAnswer": "D",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "f4e28266-f93b-5171-84cc-66f2383d2dcd",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Sınırları açık bir ortak arşiv",
        "optionB": "Kentin Eksiksiz Tarihi",
        "optionC": "Her Dosya Herkese Açıktır",
        "optionD": "Belirsizlikleri Silmek",
        "correctAnswer": "A",
        "evidence": "Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "c7167211-c366-5753-8c15-97fabf23d4a6",
    "exerciseId": "8f6c9722-b784-54c2-bf22-98c32bb9716f",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "age": "adult",
    "level": 5,
    "title": "Tamir atölyesinde gerekçeli kararlar — Seviye 5",
    "content": "Mahalle kültür merkezinde açılan tamir atölyesi, kullanılmayan eşyaları hemen yenileriyle değiştirmek yerine önce sorunlarını anlamak için kurulmuştu. Gönüllüler ilk toplantıda kırık masa lambaları, fermuarı açılan çantalar ve gevşeyen sandalye ayakları hakkında konuştu. Ancak her eşyanın atölyede güvenle onarılabileceğini varsaymadılar. Elektrik bağlantısı bulunan cihazları yalnız yetkin bir uzmanın incelemesi, diğer katılımcıların ise gözlem ve kayıt işleriyle başlaması kararlaştırıldı. Atölyenin amacı herkesi bir günde usta yapmak değil, bakım kararlarının nasıl verildiğini görünür kılmaktı. Selin kayıt masasını, Bora malzeme dolabını düzenlemeyi üstlendi. İlk buluşmada hiçbir elektrikli cihaz çalıştırılmadı. Katılımcılara, bir sorunu tanımak ile onu onarmaya yetkili olmak arasındaki fark açıklandı. Bu ayrım hazırlık listesinin ilk satırına yazıldı.\n\nSelin her eşya için bir kabul kartı hazırladı. Kartta eşyanın türü, sahibinin anlattığı sorun ve inceleme sonucu için ayrı alanlar vardı. Bir çantanın sahibi, fermuarın geçen haftadan beri takıldığını söyledi. Selin bunu doğrudan arızanın nedeni olarak kaydetmedi; sahibinin gözlemi olarak yazdı. İnceleme sırasında kumaşın fermuara sıkıştığı görüldü. Kartın ikinci alanına bu bulgu eklendi. Böylece anlatılan belirti ile kontrol edilerek bulunan neden birbirine karışmadı. Sahiplerin bütün geçmişini veya ev adresini kaydetmek gerekmiyordu. Geri dönüş için istenen iletişim bilgisi ayrı tutuldu ve yalnız görevlilerin erişimine bırakıldı. Katılımcılar hangi bilginin neden toplandığını okuyabiliyordu. Bir eşyanın fotoğrafı çekilecekse bunun için ayrıca izin soruluyordu.\n\nMalzeme dolabı hazırlanırken Bora bütün parçaları dış görünüşlerine göre aynı kutuya koymak istedi. Deneyimli bir gönüllü, birbirine benzeyen vidaların farklı ölçülerde olabileceğini gösterdi. Kutular ölçü ve kullanım bilgisiyle etiketlendi. Etiketi okunmayan bir parçaya tahminle ad verilmedi; kontrol bekleyen bölüme kondu. Sonra herkes dolaptan örnek bir parça bulmayı denedi. Bazı etiketler doğru olsa da rafın arkasında kaldığı için görülmüyordu. Yerleri değiştirildi ve yazılar büyütüldü. Düzenin başarısı yalnız kutuların düzgün görünmesiyle değil, aranan parçanın doğru bulunmasıyla değerlendirildi. İlk düzenleme fotoğrafı saklandı; sonraki değişikliğin nedeni kısa bir notla açıklandı. Böylece yeni gelen bir gönüllü de etiketlerin hangi ihtiyaçtan doğduğunu anlayabilecekti.\n\nİlk üç buluşmanın sonunda ekip onarılan eşya sayısını karşılaştırdı. Birinci buluşmada on, ikinci buluşmada yedi, üçüncüde on iki eşya tamamlanmıştı. Bora üçüncü buluşmanın kesinlikle en verimli gün olduğunu söyledi. Selin kabul kartlarına bakmayı önerdi. İkinci gün gelen eşyalardan birkaçının incelemesi daha uzun sürmüş, bazıları için parça beklenmişti. Üçüncü gün ise çok sayıda küçük kumaş onarımı yapılmıştı. Sayılar gerçekti, fakat işlerin güçlüğünü tek başına göstermiyordu. Ekip toplam sayının yanına işlem türlerini ve bekleyen işleri de ekledi. Karşılaştırmayı kaldırmadılar; hangi soruya cevap verdiğini daralttılar. Artık tablo, gönüllülerin becerisini sıralayan bir puan değil, farklı günlerde hangi işlerin yapıldığını anlatan bir kayıt olarak kullanılıyordu.\n\nBir katılımcı eski bir lambasının onarılmasını istediğinde güvenlik sınırı yeniden gündeme geldi. Uzman o gün atölyede değildi. Gönüllüler lambayı denemek yerine kabul kartını doldurup inceleme bekleyen bölüme kaldırdı. Lambanın o gün geri verilmemesi katılımcıyı üzdü, fakat neden açıkça anlatıldı. Beklemek, eşyanın önemsiz görüldüğü anlamına gelmiyordu. Uzman değerlendirmesi olmadan yapılan bir deneme başka riskler doğurabilirdi. Ekip bu olayı sonraki toplantıda konuştu ve atölye duyurusuna hizmet sınırlarını ekledi. Her eşya aynı gün onarılacakmış gibi bir söz verilmemeliydi. Tamir edilemeyen veya güvenle incelenemeyen eşya için de anlaşılır bir yönlendirme yapılması gerekiyordu. Bir görevin ertelenmesi, gerekçesi ve sonraki adımıyla birlikte kaydedildiğinde daha kolay anlaşılabiliyordu.\n\nAy sonunda malzeme alımı için sınırlı bir bütçe ayrıldı. Gönüllüler yalnız en çok kullanılan parçaları satın almayı önerdi. Ancak kabul kayıtlarında, eksik bir parça yüzünden sürekli bekleyen birkaç iş de bulunuyordu. Sık kullanım ile eksiklikten doğan bekleme aynı ölçüt değildi. Ekip iki listeyi birlikte inceledi. Önce temel sarf malzemeleri, ardından birden fazla işi tamamlayacak belirli parçalar seçildi. Her talep karşılanamadığı için alınmayanların gerekçesi de yazıldı. Kararı açıklamak, herkesin aynı tercihi yapacağı anlamına gelmiyordu; farklı tercihlerin hangi sınırlar içinde tartışıldığını gösteriyordu. Liste daha sonra değiştirilebilirdi. Bütçe artırılırsa veya eşya türleri değişirse önceki sıralama yeniden değerlendirilecekti. Böylece satın alma kararı tek bir kişinin alışkanlığına bağlı kalmadı.\n\nAtölyenin açık kaldığı saatleri değiştirmek isteyenler bir katılım çizelgesi hazırladı. Sabah gelenlerle akşam gelebilenlerin aynı kişiler olmadığı görüldü. Yalnız mevcut ziyaretçilere soru sorulursa hiç gelemeyenlerin ihtiyaçları görünmeyebilirdi. Selin bu sınırlılığı önerinin başına yazdı. Kültür merkezinin başka etkinliklerine katılanlara da uygun saatler soruldu, fakat yanıt vermeleri zorunlu tutulmadı. Çizelge belirli insanların gün içindeki hareketlerini takip etmek için kullanılmadı. Amaç, birkaç olası zaman aralığını karşılaştırmaktı. Ekip tek bir saati herkes için ideal ilan etmedi. İki farklı saat aralığını kısa süreli denemeyi ve katılımı yeniden değerlendirmeyi önerdi. Yeni saatler uygulanmadan önce görevli bulunabilirliği ve merkezin diğer etkinlikleri de kontrol edilecekti.\n\nİşlem kayıtlarının sonraki ekip tarafından anlaşılması için küçük bir terim listesi oluşturuldu. Bazı kartlarda bitmiş bir onarım için tamamlandı, bazılarında ise kontrol edildi yazıyordu. Bu ifadeler eşdeğer değildi. Kontrol edilen bir eşya henüz onarılmamış olabilirdi. Terim listesinde kabul edildi, inceleme bekliyor, parça bekliyor ve tamamlandı durumları ayrı tanımlandı. Eski kartlar körlemesine yeniden etiketlenmedi. Belirsiz olanlar ilgili gönüllüye soruldu; yanıt alınamayanlarda belirsizlik korundu. Daha düzenli bir tablo elde etmek için gerçekte bilinmeyen bir durumu kesinleştirmek uygun bulunmadı. Yeni kayıtlar ortak tanımlarla yazılmaya başladı. Durum değiştiğinde tarih ve değişikliğin nedeni eklendi; eski notların üzerine yazılarak önceki süreç görünmez hâle getirilmedi.\n\nBir ay sonra atölye hakkında tanıtım yazısı hazırlanması istendi. Bir gönüllü, toplam eşya sayısını çevreye sağlanan kesin tasarruf miktarı olarak anlatmak istedi. Ancak eşyaların ağırlığı, kullanım ömrü ve sahiplerinin başka türlü ne yapacağı ölçülmemişti. Ekip doğrulanmamış bir çevresel sonuç açıklamak yerine, yapılan işlemleri ve öğrenilenleri anlatmaya karar verdi. Yazıda onarılan eşya sayısı kendi anlamıyla verildi. Bekleyen ve yönlendirilen eşyalar da belirtildi. Bu tercih atölyenin yararlı olmadığını söylemiyordu; eldeki kaydın desteklediği iddiayı daha geniş bir iddiadan ayırıyordu. Yazının sonunda yöntem ve kapsam kısaca açıklandı. Okuyan bir kişi sayının nasıl oluştuğunu anlayabilecek, onu atölyenin ölçmediği başka sonuçlarla karıştırmayacaktı.\n\nAtölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
    "wordCount": 933,
    "source": "../../comprehension/v1/texts/repair-workshop.txt",
    "sourceSha256": "4f11e94e54fd624803f7d4759adf273eaef86a8fa04028446fa2339a4d27e479",
    "questions": [
      {
        "id": "d265369c-588c-5182-8861-6f8263441f56",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Tamir atölyesinde güvenlik ve kayıtlarla ortak kararlar vermek",
        "optionB": "Her elektrikli cihazı herkesin hemen onarmasını sağlamak",
        "optionC": "Gönüllüleri yalnız tamamladıkları eşya sayısıyla sıralamak",
        "optionD": "Bütün eşyaları incelemeden yenileriyle değiştirmek",
        "correctAnswer": "A",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "7273773b-e216-58ab-88fa-3fcc767783dd",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "En çok eşya tamamlanan gün bütün açılardan en başarılı gündür",
        "optionB": "Atölye kararları, işlem sayısıyla birlikte güvenlik ve gerekçelerle değerlendirilmelidir",
        "optionC": "Uzman bulunmasa da her cihazı çalıştırarak denemek gerekir",
        "optionD": "Sahibin anlattığı belirti kontrol edilmeden kesin neden sayılmalıdır",
        "correctAnswer": "B",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "d682c9c1-a304-53bc-89c0-ec7a4ba5da93",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Onarılan eşya sayısını ölçülmemiş çevresel tasarrufla eşitlemek",
        "optionB": "Her işi aynı gün bitirme sözü vermek",
        "optionC": "Tamir kararlarını güvenlik, kayıt ve kapsamıyla birlikte değerlendirmeyi göstermek",
        "optionD": "Kayıt tutmak yerine yalnız gönüllülerin belleğine güvenmek",
        "correctAnswer": "C",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "c9522132-ec5b-5a93-836a-63667b2b0dd5",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Her Eşya Aynı Gün Biter",
        "optionB": "Sadece Sayılar Önemlidir",
        "optionC": "İncelemeden Kesin Sonuç",
        "optionD": "Tamir atölyesinde gerekçeli kararlar",
        "correctAnswer": "D",
        "evidence": "Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  },
  {
    "id": "6aeec178-aa9f-5576-8597-3a2a10ca1d8e",
    "exerciseId": "8f6c9722-b784-54c2-bf22-98c32bb9716f",
    "ageGroupId": "10000000-0000-0000-0000-000000000003",
    "age": "adult",
    "level": 5,
    "title": "Merakı destekleyen kütüphane — Seviye 5",
    "content": "Mahallenin küçük kütüphanesi, eski bir okul binasının girişindeydi. Pencereler bahçeye bakıyor, rafların arasında geniş bir geçit uzanıyordu. Duru, ilk ziyaretinde kitaplardan önce kapının yanındaki çizimi fark etti. Çizimde her raf farklı bir renkle gösterilmişti.\n\nGörevli, renklerin kitapların yerini bulmayı kolaylaştırdığını anlattı. Mavi raflarda öyküler, yeşil raflarda doğa kitapları vardı. Duru bir defter çıkardı ve aradığı başlığı yazdı. Başlığı unuttuğunda defterine bakmak, bütün rafları yeniden dolaşmaktan daha kolaydı.\n\nBir kitabı seçerken yalnız kapağa bakmadı. Arka kapaktaki kısa tanıtımı okudu, sonra birkaç sayfayı çevirdi. Bazı kitaplarda çizimler, bazılarında uzun bölümler vardı. Duru o gün, merak ettiği kuşları anlatan bir kitap aldı.\n\nKitabın ilk bölümünde parkta gördüğü serçeler anlatılıyordu. Duru, tanıdığı bir ayrıntıya rastlayınca okumayı sürdürdü. Bilmediği bir sözcüğü defterine yazdı. Eve dönerken parkta kısa bir süre durdu ve kitaptaki kuşla gerçek kuşu karşılaştırdı.\n\nErtesi gün defterini kütüphaneye getirdi. Görevliye sorusunu gösterdi ve yanıtı birlikte başka bir kitapta aradılar. Duru, bir sorunun farklı kaynaklarda farklı ayrıntılarla açıklanabileceğini gördü. Yeni bulduğu açıklamayı önceki notunun yanına ekledi.\n\nHafta sonunda bir arkadaşını da davet etti. Arkadaşı öykü okumayı seviyordu. Aynı rafta aynı kitabı seçmek zorunda olmadıklarını konuştular. Her biri kendi kitabından ilginç bir bölümü anlattı. Böylece tek ziyaret, iki ayrı keşfe dönüştü.\n\nKütüphane, ziyaretçilerin önerilerini küçük kartlarda topluyordu. Duru kartına, kitapların hangi rafta bulunduğunu gösteren daha büyük bir harita çizilmesini önerdi. Önerisini verirken nedenini de yazdı: İlk kez gelen biri renklerin anlamını hemen anlayamayabilirdi.\n\nGörevli, öneriyi diğer kartlarla birlikte panoya astı. Bir başka ziyaretçi büyük yazılı yön işaretleri istedi. Yaşlı bir okuyucu oturma yerlerinin ışığa yakın olmasını önerdi. Kartların hepsi aynı konuyu anlatmıyor, farklı ihtiyaçları görünür kılıyordu.\n\nBir sonraki buluşmada gönüllüler haritayı yeniledi. Renkleri korudular ama her renge bir sözcük ve basit bir şekil eklediler. Böylece yalnız renge bakarak ayırt edemeyen ziyaretçiler de rafları bulabildi. Duru, çiziminin başkalarının fikriyle geliştiğini gördü.\n\nHarita tamamlanınca hemen duvara asılmadı. Gönüllüler önce birkaç kişiden bir kitabın yerini bulmasını istedi. Yolunu şaşıran ziyaretçilere hangi işaretin karışık geldiğini sordular. Yanıtları dinledikten sonra okların yerini ve yazıların büyüklüğünü değiştirdiler.\n\nBu küçük deneme, güzel görünen bir çizimin her zaman kolay kullanılmadığını gösterdi. Haritanın amacı duvarı süslemek değil, ziyaretçinin aradığı yere ulaşmasını sağlamaktı. Gönüllüler bu yüzden kendi beğenilerinden çok, denemede karşılaşılan güçlükleri dikkate aldı.\n\nKütüphanenin kitap kayıtları da yenileniyordu. Bazı başlıklar defterde iki kez yazılmış, bazı kitapların yerleri değişmişti. Görevli, yanlış kayıtları sessizce silmek yerine önce raftaki kitaplarla karşılaştırdı. Bir kaydın başka baskıya ait olabileceğini de kontrol etti.\n\nGönüllüler işleri küçük parçalara böldü. Biri başlıkları okuyor, diğeri raf yerlerini yazıyordu. Her bölüm tamamlandığında kısa bir kontrol yapılıyordu. Bütün rafları tek seferde saymaya çalışmak yerine, yapılan işi açıkça işaretlemek karışıklığı azalttı.\n\nDuru, defterindeki notların da benzer biçimde düzenlenebileceğini düşündü. Sorularını ayrı bir sayfada, öğrendiği açıklamaları başka bir sayfada tuttu. Bir açıklamanın yanına hangi kitaptan geldiğini yazdı. Sonradan aynı konuyu ararken kaynağa daha kolay dönebiliyordu.\n\nAy sonunda öneri kartları yeniden gözden geçirildi. Bazı istekler hemen yapılmış, bazıları bütçe bekliyordu. Pano, yalnız tamamlanan işleri değil, henüz çözülemeyen ihtiyaçları da gösteriyordu. Görevli, bir isteğin beklemesinin unutulduğu anlamına gelmediğini açıkladı.\n\nKütüphaneye yeni kitaplar geldiğinde herkesin sevdiği türlerden eşit sayıda kitap bulunmadı. Gönüllüler tek bir kişinin tercihine göre karar vermedi. Ödünç alma kayıtlarına, önerilere ve raflarda eksik kalan konulara birlikte baktılar. Kararın gerekçesini panoda paylaştılar.\n\nBazı kitaplar çok sık isteniyordu. Bu kitaplar için sıraya girenlere tahmini dönüş tarihi söylendi. Sıra beklemek istemeyenlere benzer konudaki başka kitaplar gösterildi. Bir seçenek sunmak, aranan kitabın hemen bulunamamasını daha anlaşılır hâle getirdi.\n\nBir okuma buluşmasında aynı öykü hakkında farklı yorumlar yapıldı. Duru kahramanın cesur olduğunu düşünürken arkadaşı aceleci olduğunu söyledi. İkisi de görüşünü öyküdeki bir olayla açıkladı. Farklı yorumların, dikkatle dinlendiğinde konuşmayı zenginleştirebildiğini gördüler.\n\nBuluşmayı yöneten kişi, herkesi aynı görüşte birleştirmeye çalışmadı. Önce yorumların hangi ayrıntıya dayandığını sordu. Sonra öykünün başka bir bölümünü birlikte okudular. Yeni ayrıntılar bazı düşünceleri değiştirdi, bazılarını güçlendirdi. Konuşma, yalnız sonuçtan değil gerekçelerden oluştu.\n\nDuru artık kütüphaneye yalnız kitap almak için gelmiyordu. Bir soruyu nasıl soracağını, bilgiyi nerede arayacağını ve bulduğunu nasıl düzenleyeceğini de deniyordu. Kütüphane onun için bitmiş yanıtların bulunduğu bir yerden çok, merakın geliştiği bir çalışma alanıydı.\n\nYeni bir ziyaretçi kapıdaki haritaya bakıp öykü rafını kolayca bulduğunda Duru sevindi. Küçük bir önerinin işe yaradığı görülmüştü. Yine de haritanın sonsuza kadar değişmeden kalması gerekmiyordu. Raflar ve ihtiyaçlar değiştikçe çizim de yeniden gözden geçirilebilirdi.\n\nHarita yenilendikten sonra kütüphane ekibi ziyaretçilerin sorularını kısa notlarla toplamaya başladı. Bir rafın nerede olduğunu soranlarla belirli bir konu hakkında kaynak arayanların ihtiyaçları aynı değildi. Görevliler soruları yalnız saymak yerine türlerine göre ayırdı. Böylece kapıdaki yönlendirme çizimiyle danışma masasındaki açıklamaların farklı sorunları çözebileceğini gördüler. Tek bir değişiklik bütün ihtiyaçları karşılamak zorunda değildi.\n\nYeni bir okuma grubunda herkes aynı kitabı seçmedi. Katılımcılar önce ilgilendikleri konuları anlattı, ardından erişilebilir birkaç eser önerildi. Bir kitabın kalınlığı onun herkes için zor olduğu anlamına gelmiyordu. Konuya aşinalık, anlatım biçimi ve kullanılan sözcükler de etkiliydi. Duru bir seçimin uygunluğunu yalnız kapaktaki bilgiye bakarak değil, birkaç sayfayı dikkatle okuyarak değerlendirmeyi denedi.\n\nGruptakiler toplantıya geldiklerinde birbirlerini kaç sayfa okuduklarıyla karşılaştırmadı. Bir kişi üzerinde düşündüğü bir cümleyi, diğeri anlamadığı bir bölümü getirdi. Soruların paylaşılması, konuşmayı bir yarıştan uzaklaştırdı. Katılımcılar farklı hızlarda ilerleseler de metin hakkında söyleyecekleri vardı. Gerektiğinde aynı bölüme dönmek, okumayı baştan sona tek seferde tamamlamaktan daha yararlı bir çalışma olabiliyordu.\n\nKütüphane bir kaynak listesi hazırlarken kitap adlarının yanına kısa açıklamalar ekledi. Açıklamalar eserin bütün içeriğini özetlemeye çalışmıyordu. Hangi konuya giriş sunduğu ve hangi bölümünün daha ayrıntılı olduğu belirtiliyordu. Listede kişisel yorumla yayın bilgisi ayrı tutuldu. Böylece okuyucu bir önerinin gerekçesini görebiliyor, kendi ihtiyacı farklıysa başka bir kaynak seçebiliyordu.\n\nKütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
    "wordCount": 932,
    "source": "../../free-reading/v1/texts/library-long.txt",
    "sourceSha256": "a856f71c5d3b8ff5966873de7e39ab894e018d3d7198980d5f09bb1dbf2fe774",
    "questions": [
      {
        "id": "7a9efc44-bbf0-53e9-8599-7be15c02b384",
        "questionText": "Metnin genel konusu aşağıdakilerden hangisidir?",
        "type": 1,
        "bloomLevel": 2,
        "orderIndex": 0,
        "optionA": "Kitap kapaklarını yalnız renklerine göre sıralamak",
        "optionB": "Kütüphanede bilgiye ulaşmayı ve öğrenmeyi birlikte geliştirmek",
        "optionC": "Bütün okuyuculara aynı kitabı zorunlu seçtirmek",
        "optionD": "En çok sayfa okuyan kişiyi ödüllendirmek",
        "correctAnswer": "B",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "16063c60-b235-528e-862b-995e0ecddd82",
        "questionText": "Metnin ana düşüncesini en iyi hangi ifade özetler?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 1,
        "optionA": "Kitap seçiminde bütün okuyucuların ihtiyacı aynıdır",
        "optionB": "Bir kaynağa bakmak bütün soruları kesin olarak çözer",
        "optionC": "Kütüphane, merak ve bilgiye erişimi ortak çalışmayla destekler",
        "optionD": "Kütüphanede yalnız ödünç alınan kitap sayısı önemlidir",
        "correctAnswer": "C",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "ac431144-c69e-51ea-8ce2-3d5c2a226f6a",
        "questionText": "Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 2,
        "optionA": "Bir okuma hızının herkese uygun olduğunu kanıtlamak",
        "optionB": "Kitapları yalnız kalınlıklarına göre değerlendirmek",
        "optionC": "Bütün ziyaretçileri aynı görüşte birleştirmek",
        "optionD": "Kütüphanenin merakı ve bilgiye erişimi destekleyen rolünü anlatmak",
        "correctAnswer": "D",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      },
      {
        "id": "e3b8901b-0961-5ca1-8ccc-1f060e51c7a3",
        "questionText": "Metnin bütününe en uygun başlık hangisidir?",
        "type": 1,
        "bloomLevel": 4,
        "orderIndex": 3,
        "optionA": "Merakı destekleyen kütüphane",
        "optionB": "Herkese Tek Kitap",
        "optionC": "Sayfa Sayısına Göre Yarış",
        "optionD": "Yalnız Kapaklara Bakmak",
        "correctAnswer": "A",
        "evidence": "Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.",
        "explanation": "Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir."
      }
    ]
  }
]$content$::jsonb) item;
DO $$ BEGIN
  IF (SELECT count(*) FROM skimming_settings)<>12 OR (SELECT count(*) FROM skimming_content)<>36
    THEN RAISE EXCEPTION 'Skimming rollback payload incomplete'; END IF;
  IF EXISTS(SELECT 1 FROM skimming_settings s LEFT JOIN speed_reading.exercises e ON e.id=s.id
    WHERE e.id IS NULL OR e.target_age_group_id IS DISTINCT FROM s.age_id OR e.difficulty_level IS DISTINCT FROM s.level
      OR (e.configuration_json IS DISTINCT FROM s.config AND e.configuration_json IS DISTINCT FROM s.old_config))
    THEN RAISE EXCEPTION 'Skimming rollback catalog changed'; END IF;
  IF EXISTS(SELECT 1 FROM skimming_content c LEFT JOIN speed_reading.reading_texts t ON t.id=c.id
    WHERE t.id IS NULL OR t.title IS DISTINCT FROM c.title OR t.content IS DISTINCT FROM c.content
      OR t.word_count IS DISTINCT FROM c.word_count OR t.target_age_group_id IS DISTINCT FROM c.age_id
      OR t.exercise_id IS DISTINCT FROM c.exercise_id OR t.difficulty_level IS DISTINCT FROM c.level
      OR t.category IS DISTINCT FROM 'skimming' OR t.language IS DISTINCT FROM 'tr' OR t.is_deleted)
    THEN RAISE EXCEPTION 'Skimming rollback content changed'; END IF;
  IF EXISTS(SELECT 1 FROM skimming_content c CROSS JOIN LATERAL jsonb_array_elements(c.questions) q
    LEFT JOIN speed_reading.reading_questions r ON r.id=(q->>'id')::uuid
    WHERE r.id IS NULL OR r.reading_text_id IS DISTINCT FROM c.id OR r.question_text IS DISTINCT FROM q->>'questionText'
      OR r.correct_answer IS DISTINCT FROM q->>'correctAnswer' OR r.option_a IS DISTINCT FROM q->>'optionA'
      OR r.option_b IS DISTINCT FROM q->>'optionB' OR r.option_c IS DISTINCT FROM q->>'optionC'
      OR r.option_d IS DISTINCT FROM q->>'optionD' OR r.explanation IS DISTINCT FROM q->>'explanation'
      OR r.type IS DISTINCT FROM (q->>'type')::int OR r.bloom_level IS DISTINCT FROM (q->>'bloomLevel')::int
      OR r.order_index IS DISTINCT FROM (q->>'orderIndex')::int OR r.difficulty_level IS DISTINCT FROM c.level OR r.is_deleted)
    THEN RAISE EXCEPTION 'Skimming rollback questions changed'; END IF;
END $$;
UPDATE speed_reading.exercises e SET configuration_json=s.old_config,updated_at=now(),updated_by='skimming-v1-rollback',version=e.version+1
FROM skimming_settings s WHERE e.id=s.id AND e.configuration_json IS DISTINCT FROM s.old_config;
UPDATE speed_reading.reading_texts t SET is_active=false,updated_at=now(),updated_by='skimming-v1-rollback',version=t.version+1
FROM skimming_content c WHERE t.id=c.id AND t.is_active;
COMMIT;
