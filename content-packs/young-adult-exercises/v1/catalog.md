# Genç yetişkin egzersiz kataloğu v1

17–21 yaş; 21 tür; toplam 95 egzersiz. Seviye dağılımı: 17 / 18 / 20 / 20 / 20.

Mevcut 75 metin ve soruları korunur. Metin kullanan egzersizler yaş ve tam seviyeye uygun okunmamış rastgele metni seçer; havuz tükenince en az kullanılanlardan seçilir. Kelime etkinlikleri kelime havuzunu kullanır. Visualization metin tabanlı 15 özgün sahne ve 45 soruyla çalışır; her seviyede üç sahne, sahne başına üç soru.

Başlangıç temposu 180, 210, 240, 270, 300 kelime/dk. İlk 7 çalışma günü sonrası mevcut kişisel destek mekanizması korunur. Yeni tekrar/transfer akışı yoktur. Regresyon v2 geçmiş kelimeleri maskeler. İç Ses Azaltma v2 metni görünür tutar ve bağımsız 60 BPM sayı sayma ritmi kullanır. Bunları sıfırlamak başarı şartı değildir. Sınav soru başına 60 saniye; metni okuma dahil değildir.

## Seviye 1

| Egzersiz | Tür | Parametreler |
|---|---|---|
| Anlam Gruplarıyla Okuma | Chunking | `{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":180,"chunkSize":2},"readingPurpose":"practice"}` |
| Metin Üzerinde Düşün | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Metni Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"horizontal"},"timing":{"durationSeconds":40,"speedMs":3000},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":900,"durationSeconds":35},"content":{"type":"letter","points":3,"peripheralCount":1,"pointSize":44}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":16,"speedMs":2300,"gridSize":3}` |
| Bağımsız Metin Okuma | FreeReading | `{"content":{"source":"reading_text"},"mode":"free","readingPurpose":"evaluation"}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":800,"durationSeconds":50},"content":{"type":"number","pattern":"horizontal","pointSize":44}}` |
| Metinde Hedefi Bul | Scanning | `{"content":{"source":"random_text"},"targetCount":3,"timeLimit":180,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":4,"sequenceType":"numeric","rules":{"timeLimit":120}}` |
| Dengeli Tempoda Okuma | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":180,"chunkSize":1},"readingPurpose":"practice"}` |
| Kısa Gösterimde Tanıma | Tachistoscope | `{"mode":"tachistoscope","content":{"type":"number","count":12},"timing":{"durationMs":800,"intervalMs":500},"adaptive":{"enabled":false,"minDurationMs":200,"maxDurationMs":1500}}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":12,"displayDurationMs":1100,"startDegrees":8,"targetDegrees":18,"content":{"stimulusType":"letter"}}` |
| Sahneyi Zihninde Canlandır | Visualization | `{"mode":"static"}` |
| Kelime Bilgisi Testi | Vocabulary | `{"mode":"quiz","quizType":"word_to_definition","timeLimitPerWord":0,"vocabulary":{"count":8,"difficultyLevel":1}}` |
| Kelime Öğrenme | Vocabulary | `{"mode":"learning","quizType":"word_to_definition","timeLimitPerWord":0,"vocabulary":{"count":8,"difficultyLevel":1}}` |

## Seviye 2

| Egzersiz | Tür | Parametreler |
|---|---|---|
| Anlam Gruplarıyla Okuma | Chunking | `{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":210,"chunkSize":3},"readingPurpose":"practice"}` |
| Metin Üzerinde Düşün | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Metni Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"circle"},"timing":{"durationSeconds":45,"speedMs":2800},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":850,"durationSeconds":40},"content":{"type":"letter","points":4,"peripheralCount":1,"pointSize":44}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":20,"speedMs":2100,"gridSize":3}` |
| Bağımsız Metin Okuma | FreeReading | `{"content":{"source":"reading_text"},"mode":"free","readingPurpose":"evaluation"}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":750,"durationSeconds":60},"content":{"type":"number","pattern":"vertical","pointSize":44}}` |
| Metinde Hedefi Bul | Scanning | `{"content":{"source":"random_text"},"targetCount":4,"timeLimit":180,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":4,"sequenceType":"numeric","rules":{"timeLimit":100}}` |
| Dengeli Tempoda Okuma | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":210,"chunkSize":1},"readingPurpose":"practice"}` |
| Kısa Gösterimde Tanıma | Tachistoscope | `{"mode":"tachistoscope","content":{"type":"number","count":14},"timing":{"durationMs":700,"intervalMs":500},"adaptive":{"enabled":false,"minDurationMs":200,"maxDurationMs":1500}}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":14,"displayDurationMs":1000,"startDegrees":8,"targetDegrees":20,"content":{"stimulusType":"letter"}}` |
| Sahneyi Zihninde Canlandır | Visualization | `{"mode":"static"}` |
| Kelime Bilgisi Testi | Vocabulary | `{"mode":"quiz","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":8,"difficultyLevel":2}}` |
| Kelime Öğrenme | Vocabulary | `{"mode":"learning","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":8,"difficultyLevel":2}}` |

## Seviye 3

| Egzersiz | Tür | Parametreler |
|---|---|---|
| Anlam Gruplarıyla Okuma | Chunking | `{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":240,"chunkSize":3},"readingPurpose":"practice"}` |
| Metin Üzerinde Düşün | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Metni Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"circle"},"timing":{"durationSeconds":45,"speedMs":2600},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":800,"durationSeconds":45},"content":{"type":"letter","points":4,"peripheralCount":1,"pointSize":44}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":24,"speedMs":2000,"gridSize":3}` |
| Bağımsız Metin Okuma | FreeReading | `{"content":{"source":"reading_text"},"mode":"free","readingPurpose":"evaluation"}` |
| Sıralı Kelime Gösterimi | RSVP | `{"mode":"rsvp","content":{"source":"reading_text"},"timing":{"durationMs":250,"intervalMs":0},"readingPurpose":"evaluation"}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":700,"durationSeconds":70},"content":{"type":"number","pattern":"horizontal","pointSize":44}}` |
| Metinde Hedefi Bul | Scanning | `{"content":{"source":"random_text"},"targetCount":5,"timeLimit":180,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":5,"sequenceType":"numeric","rules":{"timeLimit":150}}` |
| Metne Genel Bakış | Skimming | `{"content":{"source":"reading_text"},"mode":"skimming","rules":{"timeLimit":180},"timing":{"minReadingTimeMs":3000},"readingPurpose":"evaluation"}` |
| Dengeli Tempoda Okuma | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":240,"chunkSize":1},"readingPurpose":"practice"}` |
| Kısa Gösterimde Tanıma | Tachistoscope | `{"mode":"tachistoscope","content":{"type":"number","count":16},"timing":{"durationMs":600,"intervalMs":500},"adaptive":{"enabled":false,"minDurationMs":200,"maxDurationMs":1500}}` |
| Kaybolan Metinle Okuma | TextFading | `{"content":{"source":"reading_text"},"fading":{"speedWpm":240,"lagMs":2000},"timing":{"timeLimitSec":0},"readingPurpose":"evaluation"}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":16,"displayDurationMs":950,"startDegrees":8,"targetDegrees":22,"content":{"stimulusType":"letter"}}` |
| Sahneyi Zihninde Canlandır | Visualization | `{"mode":"static"}` |
| Kelime Bilgisi Testi | Vocabulary | `{"mode":"quiz","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":10,"difficultyLevel":3}}` |

## Seviye 4

| Egzersiz | Tür | Parametreler |
|---|---|---|
| Anlam Gruplarıyla Okuma | Chunking | `{"content":{"source":"reading_text"},"mode":"chunking","pacer":{"speedWpm":270,"chunkSize":4},"readingPurpose":"practice"}` |
| Metin Üzerinde Düşün | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Metni Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Eksik Harfi Bul | ErrorAnalysis | `{"content":{"source":"reading_text"},"errorCount":5,"timeLimit":300}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"infinity8"},"timing":{"durationSeconds":60,"speedMs":2500},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":750,"durationSeconds":45},"content":{"type":"letter","points":5,"peripheralCount":1,"pointSize":44}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":28,"speedMs":1800,"gridSize":3}` |
| Sıralı Kelime Gösterimi | RSVP | `{"mode":"rsvp","content":{"source":"reading_text"},"timing":{"durationMs":222,"intervalMs":0},"readingPurpose":"evaluation"}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":650,"durationSeconds":80},"content":{"type":"number","pattern":"vertical","pointSize":44}}` |
| Metinde Hedefi Bul | Scanning | `{"content":{"source":"random_text"},"targetCount":6,"timeLimit":180,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":5,"sequenceType":"numeric","rules":{"timeLimit":120}}` |
| Metne Genel Bakış | Skimming | `{"content":{"source":"reading_text"},"mode":"skimming","rules":{"timeLimit":150},"timing":{"minReadingTimeMs":3000},"readingPurpose":"evaluation"}` |
| Dengeli Tempoda Okuma | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":270,"chunkSize":1},"readingPurpose":"practice"}` |
| Kısa Gösterimde Tanıma | Tachistoscope | `{"mode":"tachistoscope","content":{"type":"number","count":18},"timing":{"durationMs":550,"intervalMs":500},"adaptive":{"enabled":false,"minDurationMs":200,"maxDurationMs":1500}}` |
| Kaybolan Metinle Okuma | TextFading | `{"content":{"source":"reading_text"},"fading":{"speedWpm":270,"lagMs":2000},"timing":{"timeLimitSec":0},"readingPurpose":"evaluation"}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":18,"displayDurationMs":900,"startDegrees":8,"targetDegrees":24,"content":{"stimulusType":"letter"}}` |
| Sahneyi Zihninde Canlandır | Visualization | `{"mode":"static"}` |
| Kelime Bilgisi Testi | Vocabulary | `{"mode":"quiz","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":12,"difficultyLevel":4}}` |

## Seviye 5

| Egzersiz | Tür | Parametreler |
|---|---|---|
| Metin Üzerinde Düşün | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Metni Anla | Comprehension | `{"content":{"source":"reading_text"},"mode":"comprehension","readingPurpose":"evaluation"}` |
| Eksik Harfi Bul | ErrorAnalysis | `{"content":{"source":"reading_text"},"errorCount":6,"timeLimit":300}` |
| Süreli Metin ve Sorular | ExamSimulation | `{"content":{"source":"reading_text"},"readingPurpose":"evaluation","timing":{"questionTimeSeconds":60}}` |
| Hareketi Takip Et | EyeTracking | `{"mode":"tracking","path":{"type":"infinity8"},"timing":{"durationSeconds":60,"speedMs":2300},"content":{"type":"dot","pointSize":36}}` |
| Noktaya Odaklan | Fixation | `{"mode":"fixation","timing":{"holdMs":700,"durationSeconds":45},"content":{"type":"letter","points":5,"peripheralCount":1,"pointSize":44}}` |
| Konum Belleği | Focus | `{"mode":"position","nLevel":1,"totalSteps":30,"speedMs":1600,"gridSize":3}` |
| Sıralı Kelime Gösterimi | RSVP | `{"mode":"rsvp","content":{"source":"reading_text"},"timing":{"durationMs":200,"intervalMs":0},"readingPurpose":"evaluation"}` |
| Hedefler Arası Geçiş | Saccade | `{"mode":"saccade","timing":{"holdMs":600,"durationSeconds":90},"content":{"type":"number","pattern":"random","pointSize":44}}` |
| Metinde Hedefi Bul | Scanning | `{"content":{"source":"random_text"},"targetCount":6,"timeLimit":180,"targets":{"mode":"find_all","caseSensitive":false}}` |
| Sayı Avcısı | SchulteTable | `{"gridSize":5,"sequenceType":"numeric","rules":{"timeLimit":100}}` |
| Metne Genel Bakış | Skimming | `{"content":{"source":"reading_text"},"mode":"skimming","rules":{"timeLimit":150},"timing":{"minReadingTimeMs":3000},"readingPurpose":"evaluation"}` |
| Dengeli Tempoda Okuma | SpeedReading | `{"content":{"source":"reading_text"},"mode":"guided","pacer":{"speedWpm":300,"chunkSize":1},"readingPurpose":"practice"}` |
| Kısa Gösterimde Tanıma | Tachistoscope | `{"mode":"tachistoscope","content":{"type":"number","count":20},"timing":{"durationMs":500,"intervalMs":500},"adaptive":{"enabled":false,"minDurationMs":200,"maxDurationMs":1500}}` |
| Kaybolan Metinle Okuma | TextFading | `{"content":{"source":"reading_text"},"fading":{"speedWpm":300,"lagMs":2000},"timing":{"timeLimitSec":0},"readingPurpose":"evaluation"}` |
| Çevredeki Harfler | VisualExpansion | `{"mode":"horizontal","rounds":20,"displayDurationMs":850,"startDegrees":8,"targetDegrees":26,"content":{"stimulusType":"letter"}}` |
| Sahneyi Zihninde Canlandır | Visualization | `{"mode":"static"}` |
| Kelime Bilgisi Testi | Vocabulary | `{"mode":"quiz","quizType":"mixed","timeLimitPerWord":0,"vocabulary":{"count":12,"difficultyLevel":5}}` |

## Regresyon v2

| Egzersiz | Tür | Motor parametreleri |
|---|---|---|
| Regresyon Azaltma — Seviye 1 | RegressionReduction | `{"targetWpm":180,"chunkSize":1,"maskingType":"trailing","visibleHistoryWords":2,"readingPurpose":"evaluation","content":{"source":"reading_text","questionCount":5,"minWordCount":162,"maxWordCount":199}}` |
| Regresyon Azaltma — Seviye 2 | RegressionReduction | `{"targetWpm":210,"chunkSize":1,"maskingType":"trailing","visibleHistoryWords":1,"readingPurpose":"evaluation","content":{"source":"reading_text","questionCount":5,"minWordCount":236,"maxWordCount":289}}` |
| Regresyon Azaltma — Seviye 3 | RegressionReduction | `{"targetWpm":250,"chunkSize":1,"maskingType":"trailing","visibleHistoryWords":1,"readingPurpose":"evaluation","content":{"source":"reading_text","questionCount":5,"minWordCount":337,"maxWordCount":413}}` |
| Regresyon Azaltma — Seviye 4 | RegressionReduction | `{"targetWpm":290,"chunkSize":1,"maskingType":"trailing","visibleHistoryWords":0,"readingPurpose":"evaluation","content":{"source":"reading_text","questionCount":5,"minWordCount":456,"maxWordCount":559}}` |
| Regresyon Azaltma — Seviye 5 | RegressionReduction | `{"targetWpm":320,"chunkSize":1,"maskingType":"trailing","visibleHistoryWords":0,"readingPurpose":"evaluation","content":{"source":"reading_text","questionCount":5,"minWordCount":576,"maxWordCount":704}}` |

## İç Ses Azaltma v2

Her okuma oturumunda hafif sesli 1–2–3–4 sayma ve 60 BPM görsel ritim. Anlama bozulabilir; iç ses azalması ölçülmez.

| Seviye | Motor parametreleri |
|---|---|
| 2 | `{"targetWpm":210,"msPerWord":286,"chunkSize":2,"displayMode":"highlight","metronomeEnabled":true,"visualMetronome":true,"metronomeBpm":60,"readingPurpose":"evaluation","content":{"source":"reading_text","questionCount":5,"minWordCount":378,"maxWordCount":463}}` |
| 3 | `{"targetWpm":240,"msPerWord":250,"chunkSize":2,"displayMode":"highlight","metronomeEnabled":true,"visualMetronome":true,"metronomeBpm":60,"readingPurpose":"evaluation","content":{"source":"reading_text","questionCount":5,"minWordCount":432,"maxWordCount":528}}` |
| 4 | `{"targetWpm":270,"msPerWord":222,"chunkSize":2,"displayMode":"highlight","metronomeEnabled":true,"visualMetronome":true,"metronomeBpm":60,"readingPurpose":"evaluation","content":{"source":"reading_text","questionCount":5,"minWordCount":486,"maxWordCount":594}}` |
| 5 | `{"targetWpm":300,"msPerWord":200,"chunkSize":3,"displayMode":"highlight","metronomeEnabled":true,"visualMetronome":true,"metronomeBpm":60,"readingPurpose":"evaluation","content":{"source":"reading_text","questionCount":5,"minWordCount":540,"maxWordCount":660}}` |
