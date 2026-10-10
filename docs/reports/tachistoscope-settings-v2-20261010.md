# Takistoskop yaş/seviye ve otomatik uyarlama — 10 Ekim 2026

## Durum

Yerel geliştirme ve doğrulama tamamlandı. GitHub'a gönderilmedi, canlıya alınmadı;
canlı veritabanı değiştirilmedi. Kod ve katalog geçişi birlikte yayınlanmalıdır.
Mevcut test hesabının eğitim kayıtlarını silmek gerekmedi; hiçbir kayıt silinmedi.

Onaylanan 20 kombinasyon ve uygulama sırası:
[ayar matrisi](../../content-packs/tachistoscope-settings/v2/README.md).

## Tamamlananlar

- Yaş grubuna göre basamak sınırı: çocuk 3–5, genç 3–6, genç yetişkin 4–6, yetişkin 4–7.
- Başlangıç basamak, tur ve süre ayarları ayrı ayrı, 4 yaş × 5 seviyede tanımlandı.
- Beş cevaplık bağımsız bloklar: 4–5 doğru süreyi %10 azaltır, 3 doğru sabit tutar,
  0–2 doğru %10 artırır. Basamak değişimi yalnız süre sınırında, bir basamak olarak yapılır.
- Önizleme ve kayıtlı oturum motorları aynı kurala uyar; özel ayarlarda başlangıç
  basamağı/süresi yaş sınırlarını aşamaz ve otomatik uyarlama açık kalır.
- Eski süre alanı yazım varyantlarının kullanıcının özel seçimini ezmesi düzeltildi.
- Seviye tespitinin uyarlaması kapalı tutuldu. Dört değerlendirme kataloğu değiştirilmez.
- Devam edilen oturumun blok geçmişi korunur; sonuçta her turun uygulanan süresi
  ve doğru/yanlış sayıları saklanır. Takistoskop sonucu okuma hızı olarak sunulmaz.
- Yeni sınır alanları bulunmayan eski oturumlar uyumlu şekilde okunur.
- SQL yalnız 20 eğitim yapılandırmasını değiştirir. Eksik/farklı kaynak, çift kayıt,
  yaş/seviye uyuşmazlığı, yönetici değişikliği veya fazladan alias durumunda tüm işlem durur.

## Doğrulama

| Kontrol | Sonuç |
|---|---|
| .NET: Takistoskop ve owned-domain regresyonları | 236 geçti |
| ChromeHeadless: Takistoskop, özel ayarlar ve RSVP | 73 geçti |
| Node: 20 kombinasyonun onaylanan matrise uygunluğu | 1 geçti |
| Geçici gerçek PostgreSQL: atomiklik, tekrar çalıştırma, kapsam ve hata durumları | 10 geçti |
| Masaüstü + mobil tarayıcı: özel ayar, uyarlama, tamamlanma, önizlemenin kaydedilmemesi ve görsel sabitlik | 4 geçti |
| Frontend production derlemesi | Başarılı |
| C#/TypeScript bağımsız kod incelemesi | Bu 20 kayıtlık kapsam için engelleyici bulgu yok |

Tarayıcı testleri yerel uygulamada kontrollü API/Identity yanıtlarıyla yapıldı;
gerçek canlı kullanıcı oturumu veya canlı ödeme/eğitim kaydı oluşturulmadı.
PostgreSQL testleri atılabilir test konteynırında çalıştı; test konteynırı ve kendi
volümü kaldırıldı. Bu test gerçek production şemasının tam restore tatbikatı değildir.

Chrome kapsam raporunda bu işte değişen yürütülebilir TypeScript satırları:
`text-stream.engine.ts` 23/23, `custom-preview-settings.ts` 12/12.
Bu oran tüm projenin kod kapsamı değildir; genel frontend kapsamı bu hedefli
çalıştırmada %16,93 satırdır. TDD sürecinde hata testleri önce çalıştırıldı ve
RED/GREEN yerel commit kontrol noktaları oluşturuldu.

Derlemede mevcut `exercise-player.component.scss` boyutu için uyarı var:
128,98 kB; uyarı sınırı 120 kB. Derlemeyi engellemiyor ve bu işte stil dosyası değiştirilmedi.

## Kapsam dışı teknik not

İncelemede, açık maksimum uzunluk tanımlanmayan eski özel sayı/harf havuzlarında
havuz öğesinin uzunluğunun 12'yi aşabilmesi not edildi. Bu paketin tüm kayıtları
rastgele sayı üretir ve açık maksimumları en fazla 7'dir; söz konusu özel havuz
durumu bu değişiklik tarafından kullanılmaz. Özel içerik yönetimi genişletilirse
bu eski havuz sözleşmesi ayrıca netleştirilmelidir.

## Yayın öncesi

API/frontend imajları, Hızlı Okuma veritabanı yedeği ve canlıdaki 20 kaydın tam
kaynak eşleşmesi doğrulanmalı; sonra SQL uygulanmalı ve kapsam dışı kayıtların
değişmediği kontrol edilmelidir. Yasal metin, Identity, Koçluk ve diğer servislerin
veritabanlarına bu paketin işlem yapması gerekmez.
