# Regresyon sonuç cevapları · 15.10.2026

## Neden ve düzeltme

Regresyon motoru seçilen harfi `userAnswer` alanında saklar. Ortak soru sonuç şablonu yalnız `selectedAnswer` okuyup eksik gördüğü her cevapta yanlış yazılmış “Cüre Doldu” metnini gösteriyordu. Puan ve doğru/yanlış doğrulaması etkilenmiyordu.

Üretim değişikliği tek HTML satırıdır: `selectedAnswer || userAnswer || 'Yanıt verilmedi'`. Diğer motorların alan önceliği korunur. Cevap olmayan durumlarda süre dolduğu iddia edilmez. Motor, API, veri yapısı, puanlama ve değerlendirme gizlilik koşulları değiştirilmedi.

## Kanıtlar

- `9fc429be`: Önce gerçek önceki üretim derlemesinde A doğru ve B yanlış seçimlerinin ikisi de sonuçta “Cüre Doldu” göstererek iki E2E reproducer başarısız oldu. İlk localhost ağ bağlamı denemesinin bağlantı hatası RED kanıtı sayılmadı; düzeltme öncesi iki test asıl hatayla yeniden çalıştırıldı.
- `a760f9fc7c8ecb072718749ebd76d8a67a187f27`: 16 ilgili Angular birim testi ve 14 masaüstü/mobil Regresyon tarayıcı testi başarılı. Gerçek Angular şablonunda iki cevap alanı, alan önceliği, boş/eksik yanıt ve değerlendirme gizliliği doğrulandı. Gerçek motorla doğru ve yanlış cevaplar kayıttan sonra görünür; yanlışta doğru cevap ayrıca gösterilir. Personel önizlemesi sonuç yazmaz.
- Üretim derlemesi başarılı. Önceden var olan 135,19 kB oynatıcı SCSS bütçesi uyarısı değişmedi; stil kaynağı değiştirilmedi. Tüm depo için ölçülmemiş kapsam yüzdesi iddia edilmez.
- Bağımsız kaynak/test ve operasyon incelemelerinde engelleyici bulgu yok.

## Yayın

15.10.2026 **22:51:15 Türkiye saati** (`19:51:15Z`) yalnız öğrenci arayüzü yayımlandı. Kaynak revizyonu `a760f9fc7c8ecb072718749ebd76d8a67a187f27`.

- Yeni öğrenci imajı: `sha256:b9dae675951ad42bcacb668e355483f4687e65a00ed92d156ad7e9e2ddafffce`.
- Korunan önceki öğrenci imajı: `sha256:455114e9466fa46a24b932634e41d95cbc9cb3f91e03ffef829b424fc1b94f38`.
- Değişmeyen API: `sha256:a1750e44674b9aba3753c8e1b89124537ebed2b47f0f8288853985df4900c134`.
- Arşiv SHA-256: `2e724e0625f88260ad6dd954327249ad9f52d8ad9a9b57785782ee2e037e74d7`.

İzole önceki öğrenci kaynağına yalnız bir HTML satırı uygulandı. Sunucuda 141 aday dosyanın hashleri test edilen bundle ile eşleşti. Çift dağıtım kilidi, tam önceki imaj sınırı, frozen compose ve ortam, tek servis `--no-deps`, diğer konteyner kimliklerinin değişmemesi ve sağlık kontrolü başarısızlığında doğrulanan geri dönüş korunur. Eski hashli JavaScript dosyaları tutulur. API, migration, abonelikler, hesap/kurum bağları ve CAPTCHA ayarları değişmedi.

Operasyon kaynakları [yayın dizininde](../../tools/releases/regression-answer-label-20261015/README.md). Test ve canlı kanıtları `artifacts/regression-answer-label-20261015/` içindedir.

## Canlı sonuç doğrulaması

Önceden açık istemci ilk denemede service worker üzerinden eski `main-6XCFKJWM.js` sürümünde kaldı ve eski etiketi gösterdi. Bu deneme düzeltmenin canlı kanıtı sayılmadı. Yeni sekmenin güncel `main-AH6Q4JGO.js` yüklediği DOM'daki script adreslerinden doğrulandı; service worker veya CAPTCHA kapatılmadı.

Yeni sürümde test öğrencisi gerçek 196 kelimelik Regresyon çalışmasını normal hızında tamamladı. Bir yanlış ve dört doğru yanıt seçildi: **B, B, C, D, A**. Sonuç kaydedildikten sonra beş seçimin tamamı bu harflerle göründü. İlk yanlış B cevabında ayrıca “Doğru: A” gösterildi; 4/5 doğru, 80 puan ve %80 anlama korundu. “Cüre Doldu” görünmedi; öğrenci konsolunda hata yok. `live-result.txt` ve `live-result.jpg` kanıttır.

Genel `/student/assignments` adresinin sunucudan gelen HTML'si yeni payload ile SHA-256 düzeyinde eşleşti: `d81105c849eac6c5fb5c0fb6587207246b1ca1f41b62868f53ad82a086b0d16f`. `ngsw.json` genel adreste `no-cache` ve `no-store,must-revalidate` başlıklarıyla sunuluyor. Eski istemci sürümü ayrımı tarayıcıdaki gerçek script adresleriyle yapıldı.

Test tamamlanmış ödevin pratik tekrarıyla yapıldı. Kurumda asıl ödevin önceki 100 puanı ve tamamlanma kaydı korundu (`live-assignment-preserved.txt`). Öğrenme verileri yalnız test öğrencisine aittir. Önceden açık eski sekmelerin yeni sürümü alması için sayfayı yenilemek/yeni sekme açmak gerekir.

## Yayın sonrası izleme

`19:51:15Z–20:06:30Z` aralığında **15 dakika 15 saniye** izlendi. Son ölçüm: 40 API isteği, **0 sunucu 5xx**, ortalama 110,94 ms, en yüksek 2457,77 ms. Genel öğrenci sayfası HTTP 200 / 75 ms. API ve öğrenci servisi çalışıyor, yeniden başlama sayıları 0. Yalnız öğrenci imajı değişti; diğer servis konteyner kimlikleri ve CAPTCHA açık / 0,5 eşiği korundu. Son kanıt `artifacts/regression-answer-label-20261015/live-health-final.log`.
