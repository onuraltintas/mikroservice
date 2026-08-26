# Notification migration

Bildirim, duyuru, e-posta şablonu ve kampanya uçları SpeedReading API altında
mevcut legacy tabloları kullanacak şekilde taşındı. Eski kolonlar korunurken
frontend’in kullandığı eksik duyuru ve e-posta alanları additive migration ile
eklendi.

## Canonical endpoints

- `GET|POST /api/speed-reading/notifications`
- `GET /api/speed-reading/notifications/unread-count`
- `GET|PUT /api/speed-reading/notifications/preferences`
- `POST /api/speed-reading/notifications/subscribe`
- `PUT /api/speed-reading/notifications/{id}/mark-read`
- `PUT /api/speed-reading/notifications/mark-all-read`
- `DELETE /api/speed-reading/notifications/{id}`
- `GET /api/speed-reading/notifications/all`
- `POST /api/speed-reading/notifications/bulk`
- `GET|POST /api/speed-reading/announcements`
- `GET|PUT|DELETE /api/speed-reading/announcements/{id}`
- `POST /api/speed-reading/announcements/{id}/view|click|dismiss`
- `GET|POST /api/speed-reading/email-templates`
- `GET|PUT|DELETE /api/speed-reading/email-templates/{id}`
- `POST /api/speed-reading/email-templates/{id}/preview`
- `GET|POST /api/speed-reading/email-campaigns`
- `GET|PUT|DELETE /api/speed-reading/email-campaigns/{id}`
- `POST /api/speed-reading/email-campaigns/{id}/send`
- `GET /api/speed-reading/email-campaigns/{id}/stats`

Eski `/api/v1/...` ve ilgili versiyonsuz notification/e-posta yolları Caddy
üzerinden canonical endpoint’lere yönlendirilir. Bildirim tercihleri 16 türün
eksik kayıtlarını varsayılanlarla tamamlar; duyuru hedef kitlesi ve kullanıcı
etkileşimleri artık sorguya dahil edilir.

`014_notification_compatibility.sql` yalnızca eksik tabloları/kolonları ve
indeksleri ekler. Gerçek SMTP/Web Push kuyruğu olmadığı için kampanya gönderimi
legacy durum geçişini korur; alıcı gönderim istatistikleri mevcut veri yoksa
üretilmez. Toplu bildirimde e-posta seçilirse yalnızca in-app kayıtları yaratılır
ve sonuçta e-posta kanalının yapılandırılmadığı açıkça bildirilir.
