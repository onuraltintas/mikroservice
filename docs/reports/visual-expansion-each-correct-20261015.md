# Görsel genişleme: her doğru cevapta bir adım

15 Ekim 2026 Türkiye saati 20:35:10'da (`2026-10-15T17:35:10Z`) canlıya alındı. Kullanıcının son isteği doğrultusunda aynı gün yayımlanan iki-doğru kuralının yerini alır. Ürün kaynağı: `badab8fdfb90aec51f0e88338f7dbcf545b0810d`. Yayın etiketi: `visual-expansion-each-correct-20261015`.

## Güncel davranış

- Normal öğrenci çalışmasında ve önizlemede her tam doğru cevap göreli uzaklığı bir birim artırır. Hedef uzaklık aşılmaz.
- Yanlış veya kısmen doğru cevapta uzaklık ve gösterim süresi aynı kalır.
- Doğru/yanlış sayımı ilk cevaptan başlar; önceki sayaç ve değerlendirme etiketi düzeltmesi korunur.
- Hedefe ulaşıldığında mevcut üç ardışık doğruyla süre azaltma kuralı korunur. Seviye tespitinin eski ve bloklu kuralları değişmedi.

Göreli uzaklık fiziksel açı ölçümü değildir. Yatay yerleşimde bir birim yaklaşık 0,75 yüzde puandır; arayüz bir ondalığa yuvarlar. Katalog veya kayıtlı değerlendirme ayarı değiştirilmedi, migration uygulanmadı.

## Test kanıtları

| Kontrol | Sonuç |
|---|---:|
| Backend birim testleri | 1294/1294 |
| Gerçek PostgreSQL görsel genişleme testleri | 8/8 |
| Gerçek canlı tabanından izole öğrenci testleri | 997/997 |
| İzole üretim arayüzünde masaüstü/mobil senaryoları | 24/24 |
| Canlıdan sunulan arayüzde fixture API ile masaüstü/mobil önizleme | 24/24 |
| Aday imajla üretim DB'sinin ayrı kopyasındaki kontroller | 146/146 |

Değişiklik öncesi testler yeni kuralın eski iki-doğru davranışında başarısız olduğunu gösterdi: backend 5/28, frontend 3/8. İlk geniş birim koşusunda kalan tek eski sonuç beklentisi yakalandı: iki turda artık 10 → 11 sunulduğu için en büyük sunulan uzaklık 11 olmalıydı. Beklenti düzeltildi; son koşu 1294/1294 geçti. İlk başarısız kayıtlar saklandı.

PostgreSQL testleri her cevapta sayımı, sunulan/sonraki uzaklığı, yanlışta beklemeyi, sonuç kaydını, duraklatma/devamı ve idempotency'yi kapsar. Birim testleri hedefte süre kuralını ve değerlendirme ayrımını da kapsar. Tarayıcı testleri gerçek arayüz ve motorla dört yaş grubunun 1/5 seviyelerini, yatay/dikey/radyal sunumu ve yanlışta beklemeyi kontrol eder; Identity/API yanıtları fixture'dır, gerçek hesaba yazılmaz. Kapsama oranı veya bütün egzersizlerin canlıda sınandığı iddia edilmez.

İzole frontend üretim derlemesi ve backend/verifier Release derlemesi başarılı. Önceden mevcut egzersiz SCSS bütçe uyarısı sürer; bu değişiklikte stil dosyası değiştirilmedi.

## Gerçek canlı öğrenci kontrolü

Yeni Chrome sekmesinde mevcut `testogrenci@eduivme.com` oturumuyla Egzersizler → Görsel Genişleme → Yetişkin → Seviye 1 açıldı. Görünen harfler arayüzden okunarak cevaplandı; uygulama saati ve gizli cevap durumu değiştirilmedi.

| Tamamlanan cevap | Doğru | Yanlış | Sonraki turdaki uzaklık |
|---|---:|---:|---:|
| Başlangıç | 0 | 0 | %6 |
| İlk cevap doğru | 1 | 0 | %6,8 |
| İkinci cevap yanlış | 1 | 1 | %6,8 |
| Üçüncü cevap doğru | 2 | 1 | %7,5 |

Canlıda ilk doğruyla artış, yanlışta aynı konum ve sonraki doğruyla tekrar artış doğrulandı. Ekran kanıtını kararlı sonraki turda almak için ikinci bir normal denemede ardışık iki doğru da test edildi: 0/0 %6 → 1/0 %6,8 → 2/0 %7,5. İki deneme normal çıkışla kapatıldı; tam egzersiz sonuç kaydı oluşturulduğu iddia edilmez. Bu sekmenin hata/uyarı konsol kaydı boştu. CAPTCHA açık, eşik 0,5 olarak korundu.

Ekranlar: `artifacts/visual-expansion-each-correct-20261015/live-first-correct.jpg`, `live-wrong-holds.jpg`, `live-each-correct.jpg`. Test logları/TRX ve klon kontrol listesi aynı artifact dizininde.

İlk denemede cevap geçişinde alınan iki kare bir önceki sunum uzaklığını gösterdi; bu kareler `transition-first-correct.jpg` ve `transition-after-third-answer.jpg` adıyla saklandı. Nihai doğru ekranları ikinci denemede sonraki turun cevap alanı açıldıktan sonra alındı. Canlıdan sunulan JS ile geçen ayrı 24 önizleme senaryosu fixture API kullanır; gerçek öğrenci denemeleriyle karıştırılmaz.

## Yayın kapsamı

Önceki gerçek canlı paket `visual-expansion-two-correct-20261015` izole edilerek yalnız `VisualExpansionRoundRules.cs` ve `visual-expansion.engine.ts` üretim dosyaları değiştirildi. API'ye yalnız Application DLL/PDB kopyalandı. Gerçek canlı Domain/Infrastructure ve diğer bağımlılıklar korundu. Personel, admin, Identity ve çalışma alanındaki ilgisiz geliştirmeler yayımlanmadı.

- API imajı: `sha256:f41b9cf5f0193d8299473c9c8aa101e28fa4a748a0c73da3725c1fbc03f04c54`
- Öğrenci arayüzü imajı: `sha256:326463acccdfd7c548119fa9c4a74598fe4dc5af21afe71ceeb157de1d6628b5`
- Aktarım arşivi SHA256: `768f652e34ef9650b638708d6a2ce771803f81b7a9d8450115ec52bc427dc848`

Üretim yedeği alındı. Adayın gerçek DLL'leri yalnız `visual_expansion_each_correct_drill_20261015` adlı ayrı PostgreSQL kopyasında 146 kontrolle doğrulandı. Eğitim/eski eğitim uzaklıkları doğru → yanlış → doğru → doğru → doğru → doğru dizisinde 8, 9, 9, 10, 11, 12 olarak sunuldu. Eski ve bloklu değerlendirmeler ile mola/devam da geçti. Kopya kaldırıldı. Migration ve katalog değişikliği yoktur.

İki yayın kilidi, önceki imaj kimlikleri, frozen compose/env, prova onayının aday imaj ve DLL hash'ine bağlanması, hazır olma kontrolü ve önceki iki tam imaja geri dönüş korunur. Diğer konteyner kimlikleri değişmedi. Eski hashli arayüz dosyaları tutulur; eski açık PWA sekmeleri yeni sürümü almak için kapatılıp yeniden açılabilir.

Operasyon kaynakları: `tools/releases/visual-expansion-each-correct-20261015/`. Sunucudaki özgün paket/yedek dizini: `/var/lib/eduivme/releases/visual-expansion-each-correct-20261015`. Gizli bağlantılar, üretim ortamı ve DB yedekleri depoya alınmaz.

Yayın sonrası 15 dakikalık sağlık izlemesi `2026-10-15T17:50:25Z` tarihinde tamamlandı: HTTP 200, iki konteynerde restart 0 ve yayın sonrası 57 istekte sunucu 5xx sayısı 0. Gözlenen ortalama süre 56,29 ms, en yüksek süre 914,15 ms; düşük trafik örneği yük testi değildir. Son kayıt: `artifacts/visual-expansion-each-correct-20261015/production-health-final.log`.
