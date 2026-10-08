# Genç egzersiz kataloğu v1

13–16 yaş; 21 tür; toplam 90 egzersiz. Seviye dağılımı: 16, 16, 18, 20, 20. Önceki 70 egzersizin kimlik ve parametreleri korunur.

Metin kullanan egzersizler yaş ve tam seviyeye uygun okunmamış rastgele metni seçer; havuz tükenince en az kullanılanlardan seçilir. Mevcut metin ve sorular değiştirilmez. Visualization için okul bağlamında 15 metin tabanlı sahne ve 45 soru; seviye başına üç sahne, sahne başına üç soru.

RSVP ve TextFading 3–5, RegressionReduction ve SubvocalizationReduction 4–5; Tachistoscope ve Visualization 1–5. Geri dönüş çalışmasında maskeleme yok; iç ses çalışmasında metronom kapalıdır. Bunları sıfırlamak başarı şartı değildir. Mevcut ilk 7 çalışma günü sonrası destek akışı korunur; yeni tekrar/transfer akışı eklenmez.

Sınav soru başına 48 saniye; 10 soruda en fazla 8 dakika cevaplama, metni okuma dahil değildir. Görsel genişlik ayarları cihaz kalibrasyonu olmadan gerçek görsel açı sayılmaz.

## Seviye 1

| Egzersiz | Tür | Parametreler |
|---|---|---|
| Anlam Gruplarıyla Okuma | Chunking | `{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":150,"chunkSize":2},"readingPurpose":"practice"}` |
| Metin Üzerinde Düşün | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Metni Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"horizontal"},"timing":{"durationSeconds":40,"speedMs":3000},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":900,"durationSeconds":35},"content":{"type":"letter","points":3,"peripheralCount":1,"pointSize":44}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":16,"speedMs":2300,"gridSize":3}` |
| Bağımsız Metin Okuma | FreeReading | `{"content":{"source":"reading_text"},"mode":"free","readingPurpose":"evaluation"}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":800,"durationSeconds":35},"content":{"type":"number","pattern":"horizontal","pointSize":44}}` |
| Metinde Hedefi Bul | Scanning | `{"content":{"source":"random_text"},"targetCount":3,"timeLimit":120,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":4,"sequenceType":"numeric","rules":{"timeLimit":120}}` |
| Dengeli Tempoda Okuma | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":150,"chunkSize":1},"readingPurpose":"practice"}` |
| Kısa Gösterimde Tanıma | Tachistoscope | `{"mode":"tachistoscope","content":{"type":"number","count":12},"timing":{"durationMs":1000,"intervalMs":500},"adaptive":{"enabled":false,"minDurationMs":200,"maxDurationMs":1500}}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":12,"displayDurationMs":1100,"startDegrees":8,"targetDegrees":18,"content":{"stimulusType":"letter"}}` |
| Sahneyi Zihninde Canlandır | Visualization | `{"mode":"static"}` |
| Kelime Bilgisi Testi | Vocabulary | `{"mode":"quiz","quizType":"word_to_definition","timeLimitPerWord":0,"vocabulary":{"count":6,"difficultyLevel":1}}` |
| Kelime Öğrenme | Vocabulary | `{"mode":"learning","quizType":"word_to_definition","timeLimitPerWord":0,"vocabulary":{"count":6,"difficultyLevel":1}}` |

## Seviye 2

| Egzersiz | Tür | Parametreler |
|---|---|---|
| Anlam Gruplarıyla Okuma | Chunking | `{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":180,"chunkSize":3},"readingPurpose":"practice"}` |
| Metin Üzerinde Düşün | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Metni Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"circle"},"timing":{"durationSeconds":45,"speedMs":2800},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":850,"durationSeconds":40},"content":{"type":"letter","points":4,"peripheralCount":1,"pointSize":44}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":20,"speedMs":2100,"gridSize":3}` |
| Bağımsız Metin Okuma | FreeReading | `{"content":{"source":"reading_text"},"mode":"free","readingPurpose":"evaluation"}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":750,"durationSeconds":40},"content":{"type":"number","pattern":"vertical","pointSize":44}}` |
| Metinde Hedefi Bul | Scanning | `{"content":{"source":"random_text"},"targetCount":4,"timeLimit":120,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":4,"sequenceType":"numeric","rules":{"timeLimit":100}}` |
| Dengeli Tempoda Okuma | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":180,"chunkSize":1},"readingPurpose":"practice"}` |
| Kısa Gösterimde Tanıma | Tachistoscope | `{"mode":"tachistoscope","content":{"type":"number","count":14},"timing":{"durationMs":900,"intervalMs":500},"adaptive":{"enabled":false,"minDurationMs":200,"maxDurationMs":1500}}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":14,"displayDurationMs":1000,"startDegrees":8,"targetDegrees":20,"content":{"stimulusType":"letter"}}` |
| Sahneyi Zihninde Canlandır | Visualization | `{"mode":"static"}` |
| Kelime Bilgisi Testi | Vocabulary | `{"mode":"quiz","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":8,"difficultyLevel":2}}` |
| Kelime Öğrenme | Vocabulary | `{"mode":"learning","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":8,"difficultyLevel":2}}` |

## Seviye 3

| Egzersiz | Tür | Parametreler |
|---|---|---|
| Anlam Gruplarıyla Okuma | Chunking | `{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":210,"chunkSize":3},"readingPurpose":"practice"}` |
| Metin Üzerinde Düşün | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Metni Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"circle"},"timing":{"durationSeconds":45,"speedMs":2600},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":800,"durationSeconds":45},"content":{"type":"letter","points":4,"peripheralCount":1,"pointSize":44}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":24,"speedMs":2000,"gridSize":3}` |
| Bağımsız Metin Okuma | FreeReading | `{"content":{"source":"reading_text"},"mode":"free","readingPurpose":"evaluation"}` |
| Sıralı Kelime Gösterimi | RSVP | `{"mode":"rsvp","content":{"source":"reading_text"},"timing":{"durationMs":286,"intervalMs":0},"readingPurpose":"evaluation"}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":700,"durationSeconds":45},"content":{"type":"number","pattern":"horizontal","pointSize":44}}` |
| Metinde Hedefi Bul | Scanning | `{"content":{"source":"random_text"},"targetCount":5,"timeLimit":150,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":5,"sequenceType":"numeric","rules":{"timeLimit":150}}` |
| Metne Genel Bakış | Skimming | `{"content":{"source":"reading_text"},"mode":"skimming","rules":{"timeLimit":150},"timing":{"minReadingTimeMs":3000},"readingPurpose":"evaluation"}` |
| Dengeli Tempoda Okuma | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":210,"chunkSize":1},"readingPurpose":"practice"}` |
| Kısa Gösterimde Tanıma | Tachistoscope | `{"mode":"tachistoscope","content":{"type":"number","count":16},"timing":{"durationMs":800,"intervalMs":500},"adaptive":{"enabled":false,"minDurationMs":200,"maxDurationMs":1500}}` |
| Kaybolan Metinle Okuma | TextFading | `{"content":{"source":"reading_text"},"fading":{"speedWpm":210,"lagMs":2500},"timing":{"timeLimitSec":0},"readingPurpose":"evaluation"}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":16,"displayDurationMs":950,"startDegrees":8,"targetDegrees":22,"content":{"stimulusType":"letter"}}` |
| Sahneyi Zihninde Canlandır | Visualization | `{"mode":"static"}` |
| Kelime Bilgisi Testi | Vocabulary | `{"mode":"quiz","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":10,"difficultyLevel":3}}` |

## Seviye 4

| Egzersiz | Tür | Parametreler |
|---|---|---|
| Anlam Gruplarıyla Okuma | Chunking | `{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":240,"chunkSize":4},"readingPurpose":"practice"}` |
| Metin Üzerinde Düşün | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Metni Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Eksik Harfi Bul | ErrorAnalysis | `{"content":{"source":"reading_text"},"errorCount":5,"timeLimit":300}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"infinity8"},"timing":{"durationSeconds":60,"speedMs":2500},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":750,"durationSeconds":45},"content":{"type":"letter","points":5,"peripheralCount":1,"pointSize":44}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":28,"speedMs":1800,"gridSize":3}` |
| Sıralı Kelime Gösterimi | RSVP | `{"mode":"rsvp","content":{"source":"reading_text"},"timing":{"durationMs":250,"intervalMs":0},"readingPurpose":"evaluation"}` |
| Okuma Takibi ve Geri Dönüş Farkındalığı | RegressionReduction | `{"content":{"source":"reading_text"},"targetWpm":240,"wordDelayMs":250,"maskingType":"none","readingPurpose":"evaluation"}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":650,"durationSeconds":45},"content":{"type":"number","pattern":"vertical","pointSize":44}}` |
| Metinde Hedefi Bul | Scanning | `{"content":{"source":"random_text"},"targetCount":6,"timeLimit":180,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":5,"sequenceType":"numeric","rules":{"timeLimit":120}}` |
| Metne Genel Bakış | Skimming | `{"content":{"source":"reading_text"},"mode":"skimming","rules":{"timeLimit":180},"timing":{"minReadingTimeMs":3000},"readingPurpose":"evaluation"}` |
| Dengeli Tempoda Okuma | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":240,"chunkSize":1},"readingPurpose":"practice"}` |
| İç Ses ve Okuma Ritmi | SubvocalizationReduction | `{"content":{"source":"reading_text"},"targetWpm":240,"msPerWord":250,"chunkSize":1,"displayMode":"highlight","metronomeEnabled":false,"visualMetronome":false,"metronomeBpm":60,"readingPurpose":"evaluation"}` |
| Kısa Gösterimde Tanıma | Tachistoscope | `{"mode":"tachistoscope","content":{"type":"number","count":18},"timing":{"durationMs":700,"intervalMs":500},"adaptive":{"enabled":false,"minDurationMs":200,"maxDurationMs":1500}}` |
| Kaybolan Metinle Okuma | TextFading | `{"content":{"source":"reading_text"},"fading":{"speedWpm":240,"lagMs":2500},"timing":{"timeLimitSec":0},"readingPurpose":"evaluation"}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":18,"displayDurationMs":900,"startDegrees":8,"targetDegrees":24,"content":{"stimulusType":"letter"}}` |
| Sahneyi Zihninde Canlandır | Visualization | `{"mode":"static"}` |
| Kelime Bilgisi Testi | Vocabulary | `{"mode":"quiz","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":10,"difficultyLevel":4}}` |

## Seviye 5

| Egzersiz | Tür | Parametreler |
|---|---|---|
| Metin Üzerinde Düşün | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Metni Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Eksik Harfi Bul | ErrorAnalysis | `{"content":{"source":"reading_text"},"errorCount":6,"timeLimit":360}` |
| Süreli Metin ve Sorular | ExamSimulation | `{"content":{"source":"reading_text"},"readingPurpose":"evaluation","timing":{"questionTimeSeconds":48}}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"infinity8"},"timing":{"durationSeconds":60,"speedMs":2300},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":700,"durationSeconds":45},"content":{"type":"letter","points":5,"peripheralCount":1,"pointSize":44}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":30,"speedMs":1600,"gridSize":3}` |
| Sıralı Kelime Gösterimi | RSVP | `{"mode":"rsvp","content":{"source":"reading_text"},"timing":{"durationMs":222,"intervalMs":0},"readingPurpose":"evaluation"}` |
| Okuma Takibi ve Geri Dönüş Farkındalığı | RegressionReduction | `{"content":{"source":"reading_text"},"targetWpm":270,"wordDelayMs":222,"maskingType":"none","readingPurpose":"evaluation"}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":600,"durationSeconds":45},"content":{"type":"number","pattern":"random","pointSize":44}}` |
| Metinde Hedefi Bul | Scanning | `{"content":{"source":"random_text"},"targetCount":6,"timeLimit":210,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":5,"sequenceType":"numeric","rules":{"timeLimit":100}}` |
| Metne Genel Bakış | Skimming | `{"content":{"source":"reading_text"},"mode":"skimming","rules":{"timeLimit":210},"timing":{"minReadingTimeMs":3000},"readingPurpose":"evaluation"}` |
| Dengeli Tempoda Okuma | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":270,"chunkSize":1},"readingPurpose":"practice"}` |
| İç Ses ve Okuma Ritmi | SubvocalizationReduction | `{"content":{"source":"reading_text"},"targetWpm":270,"msPerWord":222,"chunkSize":1,"displayMode":"highlight","metronomeEnabled":false,"visualMetronome":false,"metronomeBpm":60,"readingPurpose":"evaluation"}` |
| Kısa Gösterimde Tanıma | Tachistoscope | `{"mode":"tachistoscope","content":{"type":"number","count":20},"timing":{"durationMs":600,"intervalMs":500},"adaptive":{"enabled":false,"minDurationMs":200,"maxDurationMs":1500}}` |
| Kaybolan Metinle Okuma | TextFading | `{"content":{"source":"reading_text"},"fading":{"speedWpm":270,"lagMs":2500},"timing":{"timeLimitSec":0},"readingPurpose":"evaluation"}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":20,"displayDurationMs":850,"startDegrees":8,"targetDegrees":26,"content":{"stimulusType":"letter"}}` |
| Sahneyi Zihninde Canlandır | Visualization | `{"mode":"static"}` |
| Kelime Bilgisi Testi | Vocabulary | `{"mode":"quiz","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":12,"difficultyLevel":5}}` |
