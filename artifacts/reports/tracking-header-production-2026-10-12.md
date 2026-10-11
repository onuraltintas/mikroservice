# Göz Takibi başlık düzeltmesi — 12 Ekim 2026

## Kapsam

- Göz Takibi'nden ilgisiz Satır / Görünme göstergeleri kaldırıldı.
- Süre, nokta ilerlemesi, duraklat/devam et ve çıkış kontrolleri korunuyor.
- Sabitleme'nin Gösterim / Doğru / Yanlış göstergeleri korunuyor; Sakkad değişmedi.
- Yalnız Hızlı Okuma frontend yayımlandı. API, DB, ayarlar ve diğer servisler değişmedi.

## Doğrulama

- Önce Göz Takibi regresyon testi beklendiği gibi başarısız oldu (stats: 1, beklenen: 0).
- Düzeltmeden sonra 8/8 masaüstü/mobil Göz Takibi testi geçti.
- 2/2 masaüstü/mobil Sabitleme koruma testi geçti. Yeni testin sayı biçimi Türkçe ondalık virgülünü kabul edecek şekilde düzeltildi.
- 923/923 frontend birim testi geçti; production derlemesi başarılı.
- Önceden mevcut 128,99 kB SCSS / 120 kB bütçe uyarısı devam ediyor; bu yayının kapsamı dışında.
- Diff kontrolü temiz. Değişiklik yalnız şablonun mod koşulu ve buna bağlı testlerde; motor/zamanlama/kayıt akışına müdahale yok.

## Canlı yayın

- Kaynak commit: `b86659e4dd51be294ef063ccabd6bf1581d60706`, GitHub `codex/platform-hardening` dalına gönderildi.
- Yayın: `2026-10-11T22:47:09Z` (12 Ekim 2026 01:47:09 Türkiye).
- İmaj: `eduivme/speed-reading-frontend:tracking-header-20261012`.
- İmaj kimliği: `sha256:6cea4f92cb4ed0ac53b306faf91dfda2eb17299ea3cac46d2b0ba4074ee11006`.
- Paket SHA256: `5a3738dc8621fbd3d5d2e2a772a15f6bf8b9989569d3baafbd24c58cc91fe239`.
- Canlı `ngsw.json` SHA256: `052037b4e606a939925fe24602f50164525bae1b4ffdcd79a3088fbc9451d588`; yerel paketle aynı.
- Frontend healthy, yeniden başlama sayısı 0; mevcut API `/health/ready`: Healthy, yeniden başlama sayısı 0.
- HTTPS ana sayfa 200. Konteynır envanterinde yalnız frontend kimliği değişti.
- Gerçek admin oturumunda yeni `main-MSECQ4IR.js` doğrulandı. Kayıt oluşturmayan 15 saniyelik özel önizlemede stats sayısı 0; süre/ilerleme/duraklat/devam et çalıştı ve sonuç ekranına ulaşıldı. Tarayıcı error kaydı yok.
- Küçük arayüz yayını için kapsamlı bir API/veritabanı geçişi veya yeni 15 dakikalık API yük testi yapılmadı; yerel regresyon ve canlı arayüz/sağlık kontrolleri uygulandı.

## Geri dönüş

VPS yayın dizini: `/var/lib/eduivme/releases/tracking-header-20261012`.
Önceki frontend imajı ve eski statik dosyalar korundu. Gerekirse bu dizinde `bash deploy.sh rollback` yalnız frontend'i önceki imaja döndürür.
Canlı görüntü: `artifacts/tracking-header-20261012/live-header.jpg` (yerel doğrulama çıktısı).
