# Review, series access and student reading migration

Tekrar kuyruğu, seri erişimi ve öğrenci okuma akışları SpeedReading API altında
legacy tabloları kullanır. Tekrar kuyruğu için mevcut `ExerciseReviewItems`
tablosu aynı SM-2 alanlarıyla eşlenir; öğrenci okuma akışı mevcut
`ReadingTexts`, `ReadingQuestions` ve `ReadingSessions` tablolarını kullanır.

## Canonical endpoints

- `/api/speed-reading/review/...`
- `/api/speed-reading/series-access/...`
- `/api/speed-reading/student-reading/...`

Eski `/api/v1/review`, `/api/v1/series-access`, `/api/v1/student-reading` ve
`/api/v1/studentreading` yolları Caddy üzerinden canonical yollara aktarılır.
Öğrenci okuma `available` endpoint'i hem eski `specificLevel` hem de istemcinin
kullandığı `minLevel/maxLevel` parametrelerini destekler. Tarih filtresinde de
`dateFrom/dateTo` ve `startDate/endDate` adları birlikte desteklenir.

Seri açma mevcut aktif programı pasifleştirir ve yeni ilerleme kaydı oluşturur.
Tekrar günlük ilerleme endpoint'i kullanıcı sahipliğini doğrular.
