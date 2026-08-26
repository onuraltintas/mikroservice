# Age-group configuration migration

Yaş grubu konfigürasyonları SpeedReading API altında legacy
`AgeGroupConfigurations` tablosunun tüm öneri ve yaş aralığı alanlarıyla
kullanılıyor. Önceki minimal eşleme yalnızca ad alanlarını taşıdığı için
öneriler eksik kalabiliyordu; artık WPM, anlama, günlük süre ve zorluk seviyesi
alanları da korunuyor.

## Canonical endpoints

- `GET /api/speed-reading/age-group-configurations` (Admin)
- `GET /api/speed-reading/age-group-configurations/active`
- `GET /api/speed-reading/age-group-configurations/{id}`
- `GET /api/speed-reading/age-group-configurations/by-age/{age}`
- `GET /api/speed-reading/age-group-configurations/recommendations/{age}`
- `POST /api/speed-reading/age-group-configurations` (Admin)
- `PUT|DELETE /api/speed-reading/age-group-configurations/{id}` (Admin)

Eski `/api/v1/age-group-configurations` yolu Caddy üzerinden canonical yola
aktarılır. Yaş araması artık `MaxAge` null olduğunda da çalışır ve öneri
yanıtında istenen yaş bilgisi bulunur.
