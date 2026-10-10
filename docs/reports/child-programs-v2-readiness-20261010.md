# Çocuk programları v2 — yerel doğrulama

## Tamamlanan değişiklikler

- Günlük görev kimliği: program ilerlemesi + hafta/gün + sıra numarası. Aynı egzersiz tekrarları ayrı sonuçlar üretir.
- İlk tekrarın tamamlanması ikinciyi tamamlanmış göstermez; gün/program yalnız bütün görevler bitince tamamlanır.
- Aynı oturumun başka görevde kullanımı, belirsiz görev ve eski gün isteği reddedilir.
- Eski tekil görev istekleri ve önceki sürümün idempotency hash'leri korunur.
- Ön yüz URL'si görev sırası, program günü ve ilerleme kimliğini taşır; sayfa yenilemesi ve tekrar gönderme aynı bağlamı korur.
- Geçmiş/pratik görevler yenilemeden sonra da günlük ilerlemeye yazılmaz.
- nullable `slot_order` ve görev bazlı benzersiz index migration'ı hazır. Eski kayıtlar silinmez.
- Yeni beş çocuk programının her biri 28 gün/182 görevdir; sadece kullanılmamış eski beş şablon silinir.
- Seviye tespit programı, eşikler ve egzersiz kataloğu geçiş kapsamı dışındadır.

## Test kanıtları

| Kontrol | Sonuç |
|---|---|
| Hızlı Okuma birim testleri | 986 geçti, 0 başarısız, 0 atlanan |
| İlgili gerçek PostgreSQL entegrasyonu | 14 geçti, 0 başarısız, 0 atlanan |
| Headless Chrome bileşen/akış testleri | 8 geçti |
| 28 gün/182 görev/haftalık dağılım JSON testi | Geçti |
| Angular üretim derlemesi | Geçti; mevcut player SCSS boyutu uyarısı var |

PostgreSQL kapsamı: sıralı/eşzamanlı tekrarlar, yeniden gönderme, ikinci görevin bağımsızlığı,
aynı oturumu yeniden kullanma, yanlış gün, migration Up/Down, veri korumalı rollback engeli,
personel/öğrenci gün kilidi, anlama ölçümü, programın bir kere tamamlanması, yeni beş takvimin
oluşturulması, katalog/seviye tespit korunması, geçmiş varsa silmenin engellenmesi,
ID/açıklama çakışması ve yanlış plan sürümü/dağılımının engellenmesi, geçişin tekrar çalıştırılabilirliği.

Tarayıcı testleri gerçek üretim hesabıyla uçtan uca kullanıcı testi değildir.
Kod kapsamı için %100 iddiası yoktur.

## Canlı kontrol ve kalan yayın adımı

2026-10-10 salt okunur VPS kontrolünde eski beş çocuk eğitim programının her birinde 0 bağlı
ilerleme kaydı bulundu. Korunacak çocuk seviye tespit programı kimliği:
`1d65530e-13c2-4f09-a6cf-b717009fd959`.

Bu raporun hazırlandığı aşamada canlı DB ve servisler değiştirilmedi.
Canlı geçiş: yedek → görev sırası destekli backend/frontend → migration → v2 içerik scripti →
sağlık, katalog ve kullanıcı akışı kontrolleri. Koçluk/Identity DB geçişi yoktur.
Tekrarlı görev sonuçlarından sonra eski benzersiz index'e dönülmez; geçmişi silen rollback yapılmaz.
