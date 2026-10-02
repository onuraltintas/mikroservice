# Koçluk admin katalog yayını — 2026-10-02

Ürün kaynak arşivi: `cb50a969` (son arayüz kodu `8b88f21b`).
Yayın betiğinin son sürümü: `ad492c87`.
GitHub dalı: `codex/platform-hardening`.

## Kapsam

- Katalog listeleme/arama/filtreleme, kullanım incelemesi ve yalnız kullanılmayan
  kayıtların gerekçeli kalıcı silinmesi yayımlandı.
- Yalnız Koçluk API ve admin-panel imajları değişti: `catalog-20261002`.
- Identity, Notification, Hızlı Okuma, staff-portal ve gateway imajları değişmedi.
- Staging oluşturulmadı. Katalog verisi yeniden içe aktarılmadı; kayıt silinmedi.
- Bu yayın, tüm admin katalog planının tamamlanması anlamına gelmez.

## Doğrulama

- Backend seçili regresyon: 215 geçti, 0 başarısız, 0 atlanan.
- Admin seçili regresyon: 64 geçti. Son erişilebilirlik düzeltmesi incelendi;
  TypeScript kontrolü geçti. İki Docker üretim imajı VPS'te başarıyla derlendi.
- Koçluk custom-format yedeği alındı (yaklaşık 2,2 MB), geçici veritabanına
  hatasız geri yüklendi; iki migration bu kopyada başarıyla çalıştı.
- Canlı ve prova migration sayısı 31'den 33'e çıktı. Beklenen migration kimlikleri
  ayrı ayrı doğrulandı; sınav JSON korumasının üç trigger'ı canlıda mevcut.
- Mevcut sınav kaydı sayısı 0; eski JSON referans kontrolünde sorun bulunmadı.
- Ders sayısı 169, okul sayısı 3010 olarak korundu.
- Koçluk `/health/ready`: HTTP 200 Healthy. Admin container healthy.
- İlk canlı kontrollerde iki değişen container'ın restart sayısı 0.
- Admin, Koçluk ve Hızlı Okuma ana adresleri yönlendirmeler sonrası HTTP 200.
- Yeni admin katalog sayfası HTTP 200; oturumsuz katalog API'si HTTP 401.
- Gerçek yönetici oturumuyla listeleme/silme akışı bu turda doğrulanmış sayılmaz.
  Canlıda test amacıyla gerçek katalog kaydı silinmedi. Tam tarayıcı E2E ve
  15 dakikalık kesintisiz izleme tamamlandı olarak raporlanmaz.

## Yedek ve geri dönüş

VPS yayın dizini: `/var/lib/eduivme/releases/coaching-admin-catalog-20261002`.
Yedek: `coaching.backup` (yalnız root okuyabilir). Geri dönüş override'ı önceki
container'ların sabit image ID'lerini ve `pull_policy: never` içerir.
Geri dönüş yalnız iki uygulama imajını değiştirir; migration Down otomatik çalışmaz.
Yedeğe dönüş yeni kayıt kaybına neden olabileceğinden ayrı veri değerlendirmesi ister.
Geçici prova veritabanı silindi; yedek, yayın kaynakları ve eski imajlar korunuyor.

## Kalan kapsam

Oluşturma/düzenleme/pasifleştirme, ilişki ve konum seçicileri, kontrollü içe aktarma,
öğrenci plan/hedef/rapor incelemesi ve tam uçtan uca doğrulama henüz tamamlanmadı.
İçe aktarma geliştirmesi kalıcı silinen kaynak kimliklerini denetim kaydından
dikkate almalı; eski katalog dosyası silinen kaydı yeniden oluşturmamalı.
Bu koruma tamamlanana kadar eski katalog içe aktarma aracı yeniden çalıştırılmamalı.
