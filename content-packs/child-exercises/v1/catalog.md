# Çocuk egzersiz kataloğu v1

5 seviye, 18 tür ve toplam 84 kayıt. Seviye dağılımı: 16 / 16 / 16 / 18 / 18. Temel 70 kayda dört türden 14 yeni egzersiz eklenmiştir.

Metin kimliği sabitlenmez. Yaş ve seviyeye uygun havuzdan iki okuma geçmişinde de kullanılmamış metin rastgele seçilir. Havuz tükenince en az kullanılan metinler arasından seçim yapılır. Başlatılıp bırakılan oturum da kullanım sayılır. Kelime etkinlikleri mevcut yaş/seviye kelime havuzunu kullanır.

Mevcut metin uzunlukları ve soruları korunur. Anlama ve değerlendirmeli serbest okumada mevcut sorular; göz gezdirmede mevcut açık bilgi soruları kullanılır. Hata analizinde oturuma özel eksik harfler oluşturulur; kayıtlı metin değiştirilmez.

## Seviye 1

| Egzersiz | Tür | Motor parametreleri |
|---|---|---|
| Küçük Hikâyeyi Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Bilgiyi Keşfet | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Tanıdık Kelimelerle Okuma | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":100,"chunkSize":1},"readingPurpose":"practice"}` |
| İkili Anlam Grupları | Chunking | `{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":100,"chunkSize":2},"readingPurpose":"practice"}` |
| Kelime Bahçesi | Vocabulary | `{"mode":"learning","quizType":"word_to_definition","timeLimitPerWord":0,"vocabulary":{"count":5,"difficultyLevel":1}}` |
| Kelimemi Seçiyorum | Vocabulary | `{"mode":"quiz","quizType":"word_to_definition","timeLimitPerWord":0,"vocabulary":{"count":5,"difficultyLevel":1}}` |
| Hikâyemi Seçiyorum | FreeReading | `{"content":{"source":"reading_text"},"mode":"free","readingPurpose":"evaluation"}` |
| Sözcüğü Bul | Scanning | `{"content":{"source":"random_text"},"targetCount":2,"timeLimit":90,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":3,"sequenceType":"numeric","rules":{"timeLimit":90}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":12,"speedMs":2500,"gridSize":3}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"circle"},"timing":{"durationSeconds":30,"speedMs":3000},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":1000,"durationSeconds":30},"content":{"type":"letter","points":3,"peripheralCount":1,"pointSize":44}}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":900,"durationSeconds":30},"content":{"type":"number","pattern":"horizontal","pointSize":44}}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":10,"displayDurationMs":1200,"startDegrees":8,"targetDegrees":16,"content":{"stimulusType":"letter"}}` |

## Seviye 2

| Egzersiz | Tür | Motor parametreleri |
|---|---|---|
| Olayları Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Neden Böyle Oldu? | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Akıcı Cümleler | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":130,"chunkSize":1},"readingPurpose":"practice"}` |
| Üçlü Kelime Grupları | Chunking | `{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":130,"chunkSize":3},"readingPurpose":"practice"}` |
| Bağlamdan Kelime | Vocabulary | `{"mode":"learning","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":6,"difficultyLevel":2}}` |
| Kelimeyi Hatırla | Vocabulary | `{"mode":"quiz","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":6,"difficultyLevel":2}}` |
| Metindeki İpuçları | Scanning | `{"content":{"source":"random_text"},"targetCount":3,"timeLimit":90,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Yeni Bir Hikâye | FreeReading | `{"content":{"source":"reading_text"},"mode":"free","readingPurpose":"evaluation"}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":3,"sequenceType":"numeric","rules":{"timeLimit":75}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":16,"speedMs":2300,"gridSize":3}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"horizontal"},"timing":{"durationSeconds":40,"speedMs":2800},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":900,"durationSeconds":35},"content":{"type":"letter","points":3,"peripheralCount":1,"pointSize":44}}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":800,"durationSeconds":35},"content":{"type":"number","pattern":"horizontal","pointSize":44}}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":12,"displayDurationMs":1100,"startDegrees":8,"targetDegrees":18,"content":{"stimulusType":"letter"}}` |

## Seviye 3

| Egzersiz | Tür | Motor parametreleri |
|---|---|---|
| Paragrafı Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| İpuçlarından Sonuca | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Akıcı Paragraflar | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":160,"chunkSize":1},"readingPurpose":"practice"}` |
| Dörtlü Kelime Grupları | Chunking | `{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":160,"chunkSize":4},"readingPurpose":"practice"}` |
| Kelime Bağlantıları | Vocabulary | `{"mode":"learning","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":8,"difficultyLevel":3}}` |
| Ana Bilgiyi Bul | Scanning | `{"content":{"source":"random_text"},"targetCount":3,"timeLimit":120,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Metne İlk Bakış | Skimming | `{"content":{"source":"reading_text"},"mode":"skimming","rules":{"timeLimit":120},"timing":{"minReadingTimeMs":3000},"readingPurpose":"evaluation"}` |
| Kendi Hızımda Okuyorum | FreeReading | `{"content":{"source":"reading_text"},"mode":"free","readingPurpose":"evaluation"}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":4,"sequenceType":"numeric","rules":{"timeLimit":120}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":20,"speedMs":2100,"gridSize":3}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"circle"},"timing":{"durationSeconds":45,"speedMs":2500},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":850,"durationSeconds":40},"content":{"type":"letter","points":4,"peripheralCount":1,"pointSize":44}}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":750,"durationSeconds":40},"content":{"type":"number","pattern":"vertical","pointSize":44}}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":14,"displayDurationMs":1000,"startDegrees":8,"targetDegrees":20,"content":{"stimulusType":"letter"}}` |

## Seviye 4

| Egzersiz | Tür | Motor parametreleri |
|---|---|---|
| Cevabın Kanıtı | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Ana Fikir ve Ayrıntı | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Dengeli Okuma | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":200,"chunkSize":1},"readingPurpose":"practice"}` |
| Geniş Kelime Grupları | Chunking | `{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":200,"chunkSize":4},"readingPurpose":"practice"}` |
| Kelime Anlamlarını Ayır | Vocabulary | `{"mode":"quiz","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":8,"difficultyLevel":4}}` |
| Bilgi Dedektifi | Scanning | `{"content":{"source":"random_text"},"targetCount":4,"timeLimit":150,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Başlık ve Özet | Skimming | `{"content":{"source":"reading_text"},"mode":"skimming","rules":{"timeLimit":150},"timing":{"minReadingTimeMs":3000},"readingPurpose":"evaluation"}` |
| Metni Kontrol Et | ErrorAnalysis | `{"content":{"source":"reading_text"},"errorCount":4,"timeLimit":240}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":4,"sequenceType":"numeric","rules":{"timeLimit":100}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":24,"speedMs":2000,"gridSize":3}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"infinity8"},"timing":{"durationSeconds":45,"speedMs":3000},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":800,"durationSeconds":45},"content":{"type":"letter","points":4,"peripheralCount":1,"pointSize":44}}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":700,"durationSeconds":45},"content":{"type":"number","pattern":"horizontal","pointSize":44}}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":16,"displayDurationMs":950,"startDegrees":8,"targetDegrees":22,"content":{"stimulusType":"letter"}}` |

## Seviye 5

| Egzersiz | Tür | Motor parametreleri |
|---|---|---|
| Metnin Derin Anlamı | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Bilgileri Birleştir | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Akıcılığı Koru | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":230,"chunkSize":1},"readingPurpose":"practice"}` |
| Kelime Ustası | Vocabulary | `{"mode":"quiz","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":10,"difficultyLevel":5}}` |
| Seçici Bilgi Arama | Scanning | `{"content":{"source":"random_text"},"targetCount":5,"timeLimit":180,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Metnin Haritası | Skimming | `{"content":{"source":"reading_text"},"mode":"skimming","rules":{"timeLimit":180},"timing":{"minReadingTimeMs":3000},"readingPurpose":"evaluation"}` |
| Dikkatli Editör | ErrorAnalysis | `{"content":{"source":"reading_text"},"errorCount":5,"timeLimit":240}` |
| Bağımsız Okuyucu | FreeReading | `{"content":{"source":"reading_text"},"mode":"free","readingPurpose":"evaluation"}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":5,"sequenceType":"numeric","rules":{"timeLimit":150}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":24,"speedMs":1800,"gridSize":3}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"infinity8"},"timing":{"durationSeconds":60,"speedMs":2800},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":750,"durationSeconds":45},"content":{"type":"letter","points":5,"peripheralCount":1,"pointSize":44}}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":650,"durationSeconds":45},"content":{"type":"number","pattern":"random","pointSize":44}}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":18,"displayDurationMs":900,"startDegrees":8,"targetDegrees":24,"content":{"stimulusType":"letter"}}` |


## Eklenen dört tür (9–12 yaş)

Toplam 84 egzersiz, 18 tür. Seviye dağılımı: 16 / 16 / 16 / 18 / 18. Önceki 70 kayıt korunur.

| Tür | Seviye | Parametreler |
|---|---|---|
| Tachistoscope | 1 | `{"mode": "tachistoscope", "content": {"type": "number", "count": 8}, "timing": {"durationMs": 1500, "intervalMs": 750}, "adaptive": {"enabled": false, "minDurationMs": 200, "maxDurationMs": 2000}}` |
| Visualization | 1 | `{"mode": "static"}` |
| Tachistoscope | 2 | `{"mode": "tachistoscope", "content": {"type": "number", "count": 10}, "timing": {"durationMs": 1400, "intervalMs": 750}, "adaptive": {"enabled": false, "minDurationMs": 200, "maxDurationMs": 2000}}` |
| Visualization | 2 | `{"mode": "static"}` |
| Tachistoscope | 3 | `{"mode": "tachistoscope", "content": {"type": "number", "count": 12}, "timing": {"durationMs": 1300, "intervalMs": 750}, "adaptive": {"enabled": false, "minDurationMs": 200, "maxDurationMs": 2000}}` |
| Visualization | 3 | `{"mode": "static"}` |
| RSVP | 4 | `{"mode": "rsvp", "content": {"source": "reading_text"}, "timing": {"durationMs": 500, "intervalMs": 0}, "readingPurpose": "evaluation"}` |
| Tachistoscope | 4 | `{"mode": "tachistoscope", "content": {"type": "number", "count": 14}, "timing": {"durationMs": 1200, "intervalMs": 750}, "adaptive": {"enabled": false, "minDurationMs": 200, "maxDurationMs": 2000}}` |
| TextFading | 4 | `{"content": {"source": "reading_text"}, "fading": {"speedWpm": 120, "lagMs": 5000}, "timing": {"timeLimitSec": 0}, "readingPurpose": "evaluation"}` |
| Visualization | 4 | `{"mode": "static"}` |
| RSVP | 5 | `{"mode": "rsvp", "content": {"source": "reading_text"}, "timing": {"durationMs": 429, "intervalMs": 0}, "readingPurpose": "evaluation"}` |
| Tachistoscope | 5 | `{"mode": "tachistoscope", "content": {"type": "number", "count": 16}, "timing": {"durationMs": 1100, "intervalMs": 750}, "adaptive": {"enabled": false, "minDurationMs": 200, "maxDurationMs": 2000}}` |
| TextFading | 5 | `{"content": {"source": "reading_text"}, "fading": {"speedWpm": 140, "lagMs": 5000}, "timing": {"timeLimitSec": 0}, "readingPurpose": "evaluation"}` |
| Visualization | 5 | `{"mode": "static"}` |

Visualization: her seviyede üç metin sahnesi, her sahnede ayrıntı/konum/sıra için üç soru; toplam 15 sahne ve 45 soru. Gösterim süreleri 40/45/50/55/60 saniye. Tachistoscope sayı uzunluğu motor tarafından seviye+2 (3–7 basamak) olarak belirlenir. RSVP ve TextFading yalnızca 4–5. seviyelerde katalogda bulunur; isteğe bağlılık ayrı bir otomatik program filtresi değildir. Mevcut yaş/seviye ve kullanım geçmişi üzerinden metin seçimi kullanılır; kısa metin sınırı tanımlanmamıştır. TextFading duraklatılabilir; kaybolan kelimeleri aynı oturumda geri getiren bir katalog parametresi bulunmaz.
