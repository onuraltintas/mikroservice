# Güncel egzersizlerle dört yaş grubu — V3

20 normal program, 560 gün, 3.640 bağımsız görev. Kimlikler ve 28 gün/182 görev korunur. İlk 14 gün 6, son 14 gün 7 görev vardır. Sınav Simülasyonu hiçbir programa eklenmez; katalog ve eski sonuçları korunur.

- Görselleştirme iki kez; Metin Solma S1–2 bir, S3–5 iki kez.
- Her yaş/seviyede haftada en az bir Serbest Okuma; seviyeler program seviyesiyle eşleşir.
- Göz Gezdirme S3–5; RSVP çocuk S4–5, diğer yaşlar S3–5; Hata Analizi S4–5: ikişer çalışma.
- Regresyon ve İç Ses Azaltma mevcut uygun seviyelerde ikişer; temel görsel/dikkat görevleri korunur.
- Anlama haftalık bir değerlendirme, diğer görevlerde öğrenme modu seçer. Bu normal değerlendirme seviye tespiti değildir.
- Sabit program seviyesi korunur. Egzersiz içi uyarlama ayrı davranıştır.

`baseline-*.json` 12 Ekim salt okunur üretim envanteridir; kullanıcı verisi içermez. `programs.json` yeni ve önceki çizelgeleri taşır. Yeni kaynak `node content-packs/programs/v3/generate.mjs` ile üretilir. Bu generator yalnız kendi paket çıktılarını yazar; eski v2 kaynakları silmez.

Çocuk Metin Solma için üç özgün hikâyeden, seviyeye uygun uzunlukta 15 metin kaydı ve 60 bağlı soru hazırlanır. Aynı hikâyenin uzun seçkileri seviye arttıkça tekrar kullanılabilir; 15 bağımsız hikâye değildir. Gösterim 1,5–2 dakika + ilk bekleme; cevap/duraklama süresi hariç. `generate-short-content.mjs` üretir; metinler kesilerek mevcut sorular bozulmaz. Bu ürün pilot içeriğidir; pedagojik/akademik etkinlik garantisi verilmez.

## Doğrulama

`node --test content-packs/programs/v3/pack.test.mjs content-packs/programs/v3/short-content.test.mjs`

`TrainingProgramSelectionTests`, `TrainingDurationTests`, `TrainingProgramAuditTests` ve `TrainingProgramsV3PostgresTests` yeni seçim, süre, gerçek şema geçişi, atomic ret, idempotans, korunan tablolar ve eski atama güvenliğini denetler. Playwright curriculum testi gerçek Angular ekranını kontrollü API/Identity yanıtlarıyla masaüstü ve mobilde çalıştırır; canlı Identity testi değildir.

## Kontrollü yayın

1. Yalnız Hızlı Okuma DB'nin yedeğini al ve ayrı geçici DB'ye geri yükle.
2. Yeni API migration'ını kopyada çalıştır; 12 kısa ölçüm kaydı assessment-only işaretlenir. Kaynak drift varsa fail closed; fresh kurulum assessment seed'i flag'i yazar.
3. Kısa içerik, normal programlar ve hedefli eski-atama onarımını tek transaction'da uygula. Onarım özel release dosyasında pinli ilerleme kimliği/önceki JSON özeti kullanır; kullanıcı bilgisi Git'e girmez. Başlanmış oturum/görev/bozuk slot varsa işlem durur.
4. `verify.sql` ve API'nin salt okunur `--audit-training-programs` komutu 20/560/3640 seçimi, kesin yaş/seviye ve assessment ayrımını doğrular; oturum/atama oluşturmaz.
5. Hedef dışı tablolar/alanların içerik hash'lerini karşılaştır; ikinci uygulamada tüm tablolar değişmemeli. Geri dönüşü kopyada prova et.
6. Onaylı yayın sırasında yalnız Speed Reading API kısa bakım penceresinde durdurulur. Şema + içerik + programlar uygulanır; backend/frontend birlikte yayımlanır. Diğer servisler ve DB'ler değişmez.

Sadece yanlış ölçüm bağlantısı düzeltilen mevcut atama gün sırasını korur; yeni V3 sırası yeni atamalara uygulanır. Eski `ScheduleJson` toptan yeniden oluşturulmaz. Başlamamış test atamasına V3 sırasının da geçirilmesi istenirse ayrı kontrollü işlem gerekir.

Geri dönüş: eski uygulama imajlarını sakla. `rollback.sql` template'leri beklenen V3 kaynağından önceki çizelgeye döndürür; `rollback-short-content.sql` beş ayarı geri getirir ve eklenen metinleri arşivler, soruları/sonuçları silmez. Düzeltilen normal görev kimliklerini yeniden kısa ölçüm içeriklerine çevirmek gerekmez. Şema yeni sütunu eski uygulamaya zarar vermeden koruyabilir. Yeni kullanıcı kayıtları oluşmuşsa tüm DB'yi eski yedekle ezme.

Gösterilen günlük süre bilinen çalışma/gösterim süresidir; cevaplar/dinlenme hariç. Yanıt süresine bağlı egzersizlerde sahte beş dakika yerine belirsizlik gösterilir. Yaş ayarlarındaki eski önerilen günlük dakika değerleri bu pakette bilimsel norm gibi güncellenmez.
