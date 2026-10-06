# Hata Analizi — yerel düzeltme ve doğrulama

Tarih: 6 Ekim 2026. Kapsam: Hızlı Okuma `error_analysis` motoru, öğrenci oynatıcısı, sahipli oturum doğrulaması ve sonuç kaydı. GitHub/üretim yayını yapılmadı.

## Tamamlanan aşamalar

1. İçerik ve yaşam döngüsü: boş/bozuk kelimeler, yinelenen ve metinde olmayan hedefler, hatalı kelime–indeks eşleşmeleri reddediliyor. Eski timer temizleniyor; tamamlanan çalışma tekrar başlamıyor. Duraklamada seçim, ipucu ve bitirme engelleniyor.
2. Sunucu doğrulaması: `error_analysis_start/select/hint/finish` aksiyonları sahiplik ve aktif oturum kontrolünden geçiyor. Öğrenci doğru/yanlış kararını veya puanı göndererek sonucu değiştiremiyor. Aynı aksiyon tekrar gönderildiğinde iki kez sayılmıyor.
3. Cevap gizliliği: öğrenci başlangıç/katalog yanıtından `errors`, doğru kelime ve özgün metin çıkarılıyor. Kök/iç içe yapılandırmalar, eski motor adı, büyük-küçük harf, tire ve boşluk varyantları test edildi. Düzeltmeler tamamlanma sonrasında inceleme için açılıyor.
4. Süre: motor aktif geçen gerçek süreyi izliyor; duraklama dışlanıyor. Sunucu zaman sınırını bağımsız uyguluyor, geç seçim kabul etmiyor; sonuçta bitirmeden sonra geçen süre eklenmiyor. Varsayılan 180 saniye, yapılandırılmış sınır en fazla 3600 saniye.
5. Arayüz: kelime sırası yerine gerçek `word.index` kullanılıyor. Kelimeler klavyeyle kullanılabilen düğmeler; bekleyen işlem sırasında tekrar seçim engelleniyor. Sunucu pause/resume bağlantısı eklendi. Sonsuz ölçeklenen ipucu animasyonu kaldırıldı. API kesintisinde kullanıcı aynı aksiyon kimliğiyle yeniden gönderebiliyor.
6. Sonuç: bulunan/kaçırılan hatalar, yanlış seçim ve kullanılan ipucu ayrı gösteriliyor; ipucuyla çalışma açıkça belirtiliyor. Hatalı/doğru kelime düzeltmeleri sonuç ekranında incelenebiliyor. Tekrarlı tamamlama aynı puan/XP döndürüyor, tek sonuç kaydı oluşturuyor. Okuma hızı, anlama ve KDP üretilmiyor.

## İçerik ve değerlendirme sınırları

Kelime ve hata hedefleri seçilen, seviyeye ait katalog egzersizinin veritabanındaki yapılandırmasından oturuma alınır. Oturum kendi içerik anlık görüntüsünü tutar; sonradan katalog değişmesi açık oturumun hedeflerini değiştirmez. Rastgele yazım hatası uydurulmaz, başka seviyeden içerik otomatik alınmaz. Bu çalışma yeni bir kelime/metin kataloğu veya içerik seed işlemi eklemez. Geçersiz/eski içerik sessizce ikame edilmez, kullanıcı dostu hata ile reddedilir.

Mevcut alıştırma puanlama kuralı korunmuştur: bulunan hedef oranından yanlış seçim başına 5 puan (en çok 30) düşülür. Doğruluk, bulunan hedef sayısının toplam hedef + yanlış seçim sayısına oranıdır; kaçırılan hedefler böylece doğruluğa yansır. İpucu sayısı ayrıca kaydedilir; gizli bir puan cezası yoktur. Bu bir hata bulma alıştırmasıdır, standartlaştırılmış d-prime/klinik ölçüm veya okuma hızı testi değildir.

## Son doğrulamalar

| Kontrol | Sonuç |
| --- | --- |
| Tüm Hızlı Okuma frontend birim testleri | 633/633 geçti |
| Hata Analizi motor testleri (yukarıdaki toplamın içinde) | 14/14 geçti |
| Tüm SpeedReading.Application unit testleri | 910/910 geçti |
| Hata Analizi sunucu testleri (yukarıdaki toplamın içinde) | 10/10 geçti |
| Gerçek PostgreSQL kalıcılık testi | 1/1 geçti; geçici Docker konteynırı, canlı veriye dokunulmadı |
| Masaüstü ve mobil tarayıcı akışı | 4/4 geçti |
| Üretim Angular derlemesi | Geçti; mevcut oyuncu SCSS bütçe uyarısı 128.48 kB / 120 kB |
| TS ve C# ayrı kod incelemeleri | Bildirilen engeller kapatıldı; kalan blocker yok |

Motor kapsamı: statements %96.18, branches %87.8, functions %98.14, lines %99.54. Bunlar Hata Analizi dosyasına aittir; tüm projenin kapsamı değildir.

Tarayıcı testleri gerçek Angular arayüzünü API fixture yanıtlarıyla çalıştırdı; gerçek canlı Identity oturumu ve üretim ağı doğrulanmadı. PostgreSQL testi gerçek EF/Npgsql oturum kayıt akışını ayrıca doğruladı. Yayın ve üretim duman testi bu isteğin kapsamı dışında bırakıldı. Yeni veritabanı migration'ı yok; mevcut oturum JSON alanları kullanılıyor.

## Yeniden çalıştırma

- `clients/speed-reading`: `node node_modules/@angular/cli/bin/ng.js test --watch=false --browsers=ChromeHeadless --code-coverage`
- Depo kökü: `dotnet test tests/Unit/SpeedReading.Application.UnitTests --no-restore --verbosity quiet`
- Depo kökü, Docker açık: `dotnet test tests/Integration/Identity.API.IntegrationTests --filter FullyQualifiedName~SpeedReadingErrorAnalysisPostgresTests --no-restore --verbosity quiet`
- `tests/E2E`: `node node_modules/@playwright/test/cli.js test --config speed-reading-error-analysis.config.mjs`
- `clients/speed-reading`: `node node_modules/@angular/cli/bin/ng.js build --configuration production`

Başlangıçta mevcut olan iki TextFade çalışma ağacı değişikliği ve diğer görevlerden kalan dosyalar bu işin commitlerine alınmadı.
