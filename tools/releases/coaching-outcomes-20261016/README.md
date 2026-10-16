# coaching-outcomes-20261016

16 Ekim 2026 için hazırlanmış, sabit kaynak ve önceki canlı sürüm denetimleri olan tek seferlik yayın araçlarıdır. Genel dağıtım aracı olarak kullanılmamalıdır. Tamamlanan yayın ve kanıtlar: [canlı raporu](../../../docs/coaching/coaching-outcomes-live-release-20261016.md).

`prepare.mjs` önceki canlı kaynaklarına yalnız ilgili commit farklarını taşır. `package.mjs` üretim çıktılarından checksummed paket oluşturur; `unpack.sh` sabit paket hash'ini doğrular. Paket önceki canlı imajları temel alır ve Identity/Shared değişikliklerini içermez.

`remote.mjs` açıkça belirtilen `.sh` dosyasını sabit sunucuya SSH ile çalıştırır; komut çıktısına ortam veya bağlantı dizeleri basılmamalıdır. `deploy.sh` sırasıyla `prepare`, `build`, `drill`, `promote`, `validate` adımlarını uygular; önceki imaj kimliklerini, kilitleri, yedekleri, geri yükleme provasını ve iş verisi parmak izlerini denetler. Başarılı yayın adımları tekrar uygulanmaz. Normal son kontrol:

```powershell
node tools/releases/coaching-outcomes-20261016/remote.mjs tools/releases/coaching-outcomes-20261016/deploy.sh validate
node tools/releases/coaching-outcomes-20261016/remote.mjs tools/releases/coaching-outcomes-20261016/read-results.sh
```

`Verification` ayrı komut satırı yayın operatörüdür; web uygulamasına veya herkese açık API'ye eklenmez. Yalnız sabit prova/canlı veritabanı adlarını ve açık kullanıcı yayın onayı işaretini kabul eder. Dağıtım betiği mevcut aktif SystemAdmin hesabını Identity'den doğrular. Aktarım, eşleştirme onayı ve süre yayını servis davranışlarını kullanır; kullanıcı onayını dayanak alanlarına yazar. Öğretmen ölçümü yapılmış gibi davranmaz. Kullanılmamış, bu pakete ait bootstrap taslaklarını yayınlanmış karşılıkları sonrasında kaldırır. Öğrenci önizlemesini ve eski plan/görevlerin korunmasını denetler. Sabit tarih/tek aktif öğrenci beklentisi bu yayın provasına özeldir. Proje derleme başvuruları `artifacts/coaching-outcomes-live-20261016-r2/publish/coaching` çıktısını gerektirir.

`monitor.sh` beş dakikayı aşan 11 örnek alır. `verify-preserved.sh` yalnız belgelenmiş yeni tarayıcı test planını dışarıda bırakarak eski verileri karşılaştırır ve kişisel 30 dakika seçiminin katalogdaki 135 dakikayı değiştirmediğini doğrular. Bu betik farklı bir test planı için aynen kullanılamaz.

Sunucudaki yedekler ve runtime ortam dosyaları özel kalır; bunları depoya veya yerel kanıt klasörüne indirmeyin. Geri dönüş önceki üç uygulama imajıyla yapılır; veritabanı otomatik geri yüklenmez.
