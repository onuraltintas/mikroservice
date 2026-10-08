# Genç egzersiz kataloğu v1

13–16 yaş; 5 seviye; 15 tür; seviye başına 14, toplam 70 egzersiz.

Her seviyede 8 okuma/kelime etkinliği ve 6 dikkat oyunu. Metinler yaş ve tam seviyeye uygun okunmamış rastgele havuzdan seçilir; havuz tükenince en az kullanılanlardan rastgele seçilir. Metin ve sorular değiştirilmez. Kelime etkinlikleri kelime havuzunu kullanır.

Tempolar başlangıç ayarıdır. Otomatik tempo artırma, yüzde 80 ile seviye geçişi ve günlük program kuralları bu kayıtlarla eklenmez.

Sınav motoru soru başına 48 saniye sınırlar; 10 soruda en fazla 8 dakika cevaplama süresi. Metni okuma buna dahil değildir; toplam oturum süresi 8 dakika olarak sınırlandırılmaz.

Genişlik ayarları cihaz kalibrasyonu olmadan gerçek görsel açı olarak yorumlanmaz.

## Seviye 1

| Egzersiz | Tür | Motor parametreleri |
|---|---|---|
| Metni Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Metin Üzerinde Düşün | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Dengeli Tempoda Okuma | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":150,"chunkSize":1},"readingPurpose":"practice"}` |
| Anlam Gruplarıyla Okuma | Chunking | `{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":150,"chunkSize":2},"readingPurpose":"practice"}` |
| Kelime Öğrenme | Vocabulary | `{"mode":"learning","quizType":"word_to_definition","timeLimitPerWord":0,"vocabulary":{"count":6,"difficultyLevel":1}}` |
| Kelime Bilgisi Testi | Vocabulary | `{"mode":"quiz","quizType":"word_to_definition","timeLimitPerWord":0,"vocabulary":{"count":6,"difficultyLevel":1}}` |
| Bağımsız Metin Okuma | FreeReading | `{"content":{"source":"reading_text"},"mode":"free","readingPurpose":"evaluation"}` |
| Metinde Hedefi Bul | Scanning | `{"content":{"source":"random_text"},"targetCount":3,"timeLimit":120,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":4,"sequenceType":"numeric","rules":{"timeLimit":120}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":16,"speedMs":2300,"gridSize":3}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"horizontal"},"timing":{"durationSeconds":40,"speedMs":3000},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":900,"durationSeconds":35},"content":{"type":"letter","points":3,"peripheralCount":1,"pointSize":44}}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":800,"durationSeconds":35},"content":{"type":"number","pattern":"horizontal","pointSize":44}}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":12,"displayDurationMs":1100,"startDegrees":8,"targetDegrees":18,"content":{"stimulusType":"letter"}}` |

## Seviye 2

| Egzersiz | Tür | Motor parametreleri |
|---|---|---|
| Metni Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Metin Üzerinde Düşün | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Dengeli Tempoda Okuma | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":180,"chunkSize":1},"readingPurpose":"practice"}` |
| Anlam Gruplarıyla Okuma | Chunking | `{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":180,"chunkSize":3},"readingPurpose":"practice"}` |
| Kelime Öğrenme | Vocabulary | `{"mode":"learning","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":8,"difficultyLevel":2}}` |
| Kelime Bilgisi Testi | Vocabulary | `{"mode":"quiz","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":8,"difficultyLevel":2}}` |
| Metinde Hedefi Bul | Scanning | `{"content":{"source":"random_text"},"targetCount":4,"timeLimit":120,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Bağımsız Metin Okuma | FreeReading | `{"content":{"source":"reading_text"},"mode":"free","readingPurpose":"evaluation"}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":4,"sequenceType":"numeric","rules":{"timeLimit":100}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":20,"speedMs":2100,"gridSize":3}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"circle"},"timing":{"durationSeconds":45,"speedMs":2800},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":850,"durationSeconds":40},"content":{"type":"letter","points":4,"peripheralCount":1,"pointSize":44}}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":750,"durationSeconds":40},"content":{"type":"number","pattern":"vertical","pointSize":44}}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":14,"displayDurationMs":1000,"startDegrees":8,"targetDegrees":20,"content":{"stimulusType":"letter"}}` |

## Seviye 3

| Egzersiz | Tür | Motor parametreleri |
|---|---|---|
| Metni Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Metin Üzerinde Düşün | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Dengeli Tempoda Okuma | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":210,"chunkSize":1},"readingPurpose":"practice"}` |
| Anlam Gruplarıyla Okuma | Chunking | `{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":210,"chunkSize":3},"readingPurpose":"practice"}` |
| Kelime Bilgisi Testi | Vocabulary | `{"mode":"quiz","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":10,"difficultyLevel":3}}` |
| Metinde Hedefi Bul | Scanning | `{"content":{"source":"random_text"},"targetCount":5,"timeLimit":150,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Metne Genel Bakış | Skimming | `{"content":{"source":"reading_text"},"mode":"skimming","rules":{"timeLimit":150},"timing":{"minReadingTimeMs":3000},"readingPurpose":"evaluation"}` |
| Bağımsız Metin Okuma | FreeReading | `{"content":{"source":"reading_text"},"mode":"free","readingPurpose":"evaluation"}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":5,"sequenceType":"numeric","rules":{"timeLimit":150}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":24,"speedMs":2000,"gridSize":3}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"circle"},"timing":{"durationSeconds":45,"speedMs":2600},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":800,"durationSeconds":45},"content":{"type":"letter","points":4,"peripheralCount":1,"pointSize":44}}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":700,"durationSeconds":45},"content":{"type":"number","pattern":"horizontal","pointSize":44}}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":16,"displayDurationMs":950,"startDegrees":8,"targetDegrees":22,"content":{"stimulusType":"letter"}}` |

## Seviye 4

| Egzersiz | Tür | Motor parametreleri |
|---|---|---|
| Metni Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Metin Üzerinde Düşün | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Dengeli Tempoda Okuma | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":240,"chunkSize":1},"readingPurpose":"practice"}` |
| Anlam Gruplarıyla Okuma | Chunking | `{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":240,"chunkSize":4},"readingPurpose":"practice"}` |
| Kelime Bilgisi Testi | Vocabulary | `{"mode":"quiz","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":10,"difficultyLevel":4}}` |
| Metinde Hedefi Bul | Scanning | `{"content":{"source":"random_text"},"targetCount":6,"timeLimit":180,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Metne Genel Bakış | Skimming | `{"content":{"source":"reading_text"},"mode":"skimming","rules":{"timeLimit":180},"timing":{"minReadingTimeMs":3000},"readingPurpose":"evaluation"}` |
| Eksik Harfi Bul | ErrorAnalysis | `{"content":{"source":"reading_text"},"errorCount":5,"timeLimit":300}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":5,"sequenceType":"numeric","rules":{"timeLimit":120}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":28,"speedMs":1800,"gridSize":3}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"infinity8"},"timing":{"durationSeconds":60,"speedMs":2500},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":750,"durationSeconds":45},"content":{"type":"letter","points":5,"peripheralCount":1,"pointSize":44}}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":650,"durationSeconds":45},"content":{"type":"number","pattern":"vertical","pointSize":44}}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":18,"displayDurationMs":900,"startDegrees":8,"targetDegrees":24,"content":{"stimulusType":"letter"}}` |

## Seviye 5

| Egzersiz | Tür | Motor parametreleri |
|---|---|---|
| Metni Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Metin Üzerinde Düşün | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Dengeli Tempoda Okuma | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":270,"chunkSize":1},"readingPurpose":"practice"}` |
| Kelime Bilgisi Testi | Vocabulary | `{"mode":"quiz","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":12,"difficultyLevel":5}}` |
| Metinde Hedefi Bul | Scanning | `{"content":{"source":"random_text"},"targetCount":6,"timeLimit":210,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Metne Genel Bakış | Skimming | `{"content":{"source":"reading_text"},"mode":"skimming","rules":{"timeLimit":210},"timing":{"minReadingTimeMs":3000},"readingPurpose":"evaluation"}` |
| Eksik Harfi Bul | ErrorAnalysis | `{"content":{"source":"reading_text"},"errorCount":6,"timeLimit":360}` |
| Süreli Metin ve Sorular | ExamSimulation | `{"content":{"source":"reading_text"},"readingPurpose":"evaluation","timing":{"questionTimeSeconds":48}}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":5,"sequenceType":"numeric","rules":{"timeLimit":100}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":30,"speedMs":1600,"gridSize":3}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"infinity8"},"timing":{"durationSeconds":60,"speedMs":2300},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":700,"durationSeconds":45},"content":{"type":"letter","points":5,"peripheralCount":1,"pointSize":44}}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":600,"durationSeconds":45},"content":{"type":"number","pattern":"random","pointSize":44}}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":20,"displayDurationMs":850,"startDegrees":8,"targetDegrees":26,"content":{"stimulusType":"letter"}}` |
