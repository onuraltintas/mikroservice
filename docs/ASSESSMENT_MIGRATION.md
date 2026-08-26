# Assessment migration

Seviye tespit akışı SpeedReading API altında mevcut `Exercises`,
`StudentExerciseResults`, `Users`, `AgeGroupConfigurations` ve
`ExerciseProgramTemplates` tablolarını kullanır. Yeni değerlendirme tablosu
oluşturulmaz; admin şablonları mevcut program şablonlarının `WeeklyPatternJson`
alanında saklanır.

## Canonical endpoints

- `GET /api/speed-reading/assessment/exercises`
- `GET /api/speed-reading/assessment/status`
- `POST /api/speed-reading/assessment/calculate`
- `POST /api/speed-reading/assessment/skip`
- `GET /api/speed-reading/admin/assessment-templates`
- `GET /api/speed-reading/admin/assessment-templates/age-group/{ageGroupId}`
- `POST /api/speed-reading/admin/assessment-templates`
- `PUT|DELETE /api/speed-reading/admin/assessment-templates/{id}`

Eski `/api/v1/assessment` ve admin template yolları Caddy üzerinden canonical
yollara aktarılır. Hesaplama son üç sonucu kullanır; egzersiz bazlı skorlar
istekteki egzersiz tipine göre eşlenir. `skip` işlemi kullanıcının yaş grubu
önerilerini korur.

Gerçek ödeme sağlayıcısı bu kapsamda değildir ve mevcut ödeme endpoint'leri
sağlayıcı yapılandırılmadığında 503 döndürmeye devam eder.
