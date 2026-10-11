# Regresyon Azaltma — v2

Yerel geliştirme ve geçiş paketi. GitHub'a gönderilmiş veya canlıda uygulanmış değildir.
Yalnız Hızlı Okuma veritabanını hedefler; Identity/Koçluk şeması değişmez. Yeni tablo veya migration yoktur.

## Onaylanan yapılandırma

| Yaş | Seviye 1 → 5 gösterim temposu (kelime/dakika) | Hedef gösterim süresi (saniye) |
| --- | --- | --- |
| Çocuk 9–12 | — / 140 / 160 / 180 / 200 | — / 60 / 70 / 80 / 90 |
| Genç 13–16 | 160 / 190 / 220 / 260 / 290 | 60 / 75 / 90 / 105 / 120 |
| Genç yetişkin 17–21 | 180 / 210 / 250 / 290 / 320 | 60 / 75 / 90 / 105 / 120 |
| Yetişkin 22+ | 180 / 220 / 270 / 310 / 340 | 60 / 75 / 90 / 105 / 120 |

- Çocuk seviye 1'de bu egzersiz yoktur; diğer 19 yaş/seviye kaydı mevcuttur.
- Aktif kelimenin gerisinde okunabilir kelime sayısı: **2 / 1 / 1 / 0 / 0**. Aktif kelime bu sayıya dahil değildir. Daha eski kelimeler maskelenir; aktif ve ilerideki kelimeler görünür kalır.
- Tek kelimelik vurgu, `trailing` maskeleme, sabit `targetWpm`; otomatik tempo değişimi yoktur. Eski, yalnız gecikme değeri kullanan yapılandırmalar çalışmaya devam eder.
- Süreler soru çözümünü içermez. Hedef kelime sayısı tempo × süre / 60, seçilecek tam metnin bandı yaklaşık ±%10'dur. Metin süre doldu diye kesilmez.
- Her yeni değerlendirme metninden **beş geçerli soru** alınır. Sunucu sahiplik ve cevap doğrulaması yapar; önizleme ölçülmüş başarı üretmez ve sonuç kaydetmez.
- Kayıtlı ve özel önizleme aynı metin uzunluğu/yaş/seviye/soru uygunluk kurallarını uygular. Özel ayarda geçmiş penceresi 0–2 ve tempo değiştirilebilir; katalog değiştirilmez.

## İçerik ve program bağlantısı

Salt okunur canlı envanteri `live-readiness-20261011.json`: 19 bandın 11'inde yeni süreye uygun, en az beş sorulu ortak metin bulunamadı.
Mevcut metinlere dokunmadan 57 ek seçki ve 285 soru hazırlandı; her yapılandırma için üç farklı metin vardır.

Kaynaklar proje içindeki özgün `grouping/v1/texts/library.txt`, `garden.txt`, `map.txt` öyküleridir. Seçkiler tam paragrafta biter; sözcük tekrarıyla doldurma veya yarım cümle kesme yoktur. Soruların metindeki dayanakları `questions.json` / `content.json` içindeki `evidence` alanında tutulur. Bu alan kullanıcıya cevap anahtarı olarak gönderilmez.

Aynı üç öykünün farklı uzunluktaki seçkileri ve aynı sorular seviyeler arasında tekrar kullanılmaktadır. Bunlar işlevsel başlangıç içerikleridir; bağımsız anlama gelişimi veya yaşa göre bilimsel norm kanıtı değildir. Daha geniş ve dengeli soru havuzu pedagojik içerik çalışması olarak ayrıca geliştirilebilir. Gösterim temposu ölçülmüş okuma hızı değildir; önceki kelimeye tıklama gerçek göz regresyonu ölçümü değildir.

19 eğitim programında 21. ve 26. günün birer okuma görevi bu egzersizle değiştirilir. Günlük 6–7 görev, toplam 182 görev korunur; dikkat/görsel algı görevleri çıkarılmaz. Çocuk seviye 1 ve seviye tespit programları değişmez. Mevcut öğrenci ataması, program takvimi, oturum snapshot'ı ve sonuçları yeniden yazılmaz. Yeni örüntü yeni program başlangıçlarına uygulanır.

## Doğrulama

- Backend birim testleri: 1099/1099.
- Frontend birim testleri: 801/801.
- Katalog/içerik/program doğrulamaları: 6/6.
- Gerçek PostgreSQL: 7/7; işlemsel geçiş, çakışmada geri alma, tekrar uygulama, 19 programın yeni takvim çözümü, beş soruluk oturumlar, sahiplik, tek sonuç kaydı ve ölçülmemiş WPM kontrol edildi.
- Masaüstü/mobil tarayıcı: 12/12; API/Identity fixture'larıyla gerçek Angular motoru; beş soru, özel ayar, maskeleme, duraklatma, yalnız sunucuca doğrulanmış cevap ve önizlemede yazmama kontrolleri. Canlı oturum testi değildir.
- API Release derlemesi: sıfır hata/uyarı. Frontend üretim derlemesi geçti; daha önce mevcut SCSS bütçe uyarısı devam ediyor (128,99 kB / 120 kB).

```text
node content-packs/regression/v2/generate.mjs
node --test tests/E2E/regression-catalog-v2.test.mjs
dotnet test tests/Integration/Identity.API.IntegrationTests --filter FullyQualifiedName~SpeedReadingRegression
```

Tarayıcı komutu `tests/E2E` dizininden:

```text
npx playwright test --config=speed-reading-regression.config.mjs
```

## Kontrollü yayın ve geri dönüş

1. Ayrı yayın onayı alın. Speed Reading veritabanını yedekleyip geri yükleme doğrulaması yapın; mevcut servis/frontend imajlarını ve hedef kayıtların sürümlerini saklayın.
2. Önce uyumlu API ve frontend'i yayımlayın. Bu paketten önce dört yaş grubunun v2 eğitim programları bulunmalıdır. Seviye 5 Gruplama düzeltmesi uygulanmışsa onun bilinen örüntüsü de kabul edilir; farklı bir yönetici değişikliği varsa durulur.
3. Geçiş boyunca yeni program başlatma/atama isteklerini bakım moduyla kapatıp sürmekte olan istekleri bitirin; bu garanti sağlanamıyorsa Speed Reading API'yi kontrollü durdurun. Tablo kilidi okumaları engellemez: aksi halde aynı anda başlayan bir atama eski örüntüyü snapshot olarak alabilir. Ardından `apply.sql` dosyasını yalnız Speed Reading DB'de `psql -v ON_ERROR_STOP=1` ile uygulayın. Altı bilinen kaydı günceller, 13 yeni kayıt, 57 metin ve 285 soru ekler, 19 program örüntüsünü değiştirir. Transaction/kilitler, kimlik çakışması ve başlangıç yapılandırması kontrolleri vardır. Aynı paket tekrar uygulanınca sürüm/audit alanları değişmez. Commit ve sağlık kontrollerinden sonra program başlatma akışını açın.
4. 19 aktif yapılandırmayı, üçer uygun metni, beş soruyu ve yeni programdaki iki görevi kontrol edin. Öğrenci/öğretmen/admin akışını gerçek oturumla doğrulayın.
5. Geri dönüş gerekirse alınmış kayıt bazlı yedekle eski altı yapılandırmayı ve 19 örüntüyü geri yükleyin; yeni egzersiz/metinleri gerekiyorsa pasifleştirin. Yayından sonra oluşmuş sonuçları veya bütün veritabanını koşulsuz silmeyin. Eski imajlara dönüşü şema/oturum uyumluluğuyla birlikte değerlendirin.
