# Root olmayan container'lar — 26.10.2026

Coaching ve Speed Reading API'leri ile Speed Reading frontend ve staff portal artık root kullanıcıyla çalışmıyor. Identity, Notification ve Gateway zaten UID 1654 ile çalışıyordu; bu değişiklikle bütün uygulama container'ları yetkisiz kullanıcıyla çalışıyor.

| Servis | Önce | Sonra |
| --- | --- | --- |
| coaching-service, coaching-migrations | root | UID 1654 (`USER $APP_UID`) |
| speed-reading-service, speed-reading-migrations | root | UID 1654 (`USER $APP_UID`) |
| speed-reading-frontend | nginx (root, port 80) | `nginxinc/nginx-unprivileged` (UID 101, port 8080) |
| staff-portal | nginx (root, port 80) | `nginxinc/nginx-unprivileged` (UID 101, port 8080) |

Uygulama kodu değişmedi. Korunan ürün baseline'ı yalnız Dockerfile ve nginx.conf değişiklikleri için yenilendi.

## Yeni tek seferlik servisler

- `coaching-volume-permissions`: `coaching_data_protection`, `coaching_attachments`, `coaching_cms_media` → `chown -R 1654:1654`
- `speed-reading-volume-permissions`: `speed_reading_data_protection`, `speed_reading_media` → `chown -R 1654:1654`

İkisi de root olarak, ağsız çalışır ve API'ler başlamadan önce tamamlanır (`service_completed_successfully`). İlk çalıştırmadan sonra zararsızdır.

## Yayın adımları

1. Değişen imajları derle: coaching-service, speed-reading-service, speed-reading-frontend, staff-portal. Diğer imajlar yeniden etiketlenir.
2. `runtime-secrets-preflight` artık Coaching ve Speed Reading için de çalışır. DataProtection PFX'i UID 1654 tarafından okunabilir olmalı; Identity yayınından beri `0640 root:1654`.
3. Sırasıyla değiştir:
   1. `coaching-volume-permissions`, sonra `coaching-service`
   2. `speed-reading-volume-permissions`, sonra `speed-reading-service`
   3. `speed-reading-frontend` ve `staff-portal`
   4. Edge'ler: Caddy artık `speed-reading-frontend:8080` ve `staff-portal:8080` adreslerine gidiyor. Frontend'ler değiştikten hemen sonra edge'leri yeniden yükle. Aradaki birkaç saniyede bu iki site 502 dönebilir.
4. Doğrula:
   - `docker exec <container> id`: API'lerde `uid=1654`, frontend'lerde `uid=101` görünmeli.
   - Ödev eki yükleme ve indirme ile CMS medya yükleme bir kez denenmeli.
   - Hızlı Okuma ana sayfası ve `/staff/` 200 dönmeli.

## Geri dönüş

Önceki imaj etiketlerine dönülür. Volume'ların sahibi 1654 olarak kalır; eski root imajlar bu dosyaları okuyup yazabilir. Eski frontend imajları port 80 kullandığı için edge Caddyfile'ları da önceki commit'teki hâline döndürülür.
