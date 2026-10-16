# Ölçümsüz raporlar yayını · 16.10.2026

Kapsam ve doğrulama kanıtları [yayın raporunda](../../../docs/reports/speed-reading-no-measurement-reports-20261016.md). Çalışan tabanlardan izole derleme kullanılır; çalışma ağacındaki diğer geliştirmeler pakete girmez. Uygulama kaynak revizyonu `4006f41c3676991fa93a4bc6f0ab501642bac02e`.

Backend önceki `exercise-reports-20261016` tabanından yalnız beş C# dosyası değiştirilerek derlendi. API imajına yalnız Application ve Infrastructure DLL/PDB dosyaları eklenir. API ve Domain derlemeleri değiştirilmez; DTO yapıcıları korunur, ölçüm bayrakları ek özelliklerdir.

Personel aynı canlı tabandan yalnız üç TS/HTML dosyasıyla derlendi; komut `ng build staff-portal --configuration production --base-href /staff/`. Öğrenci canlı tabanı önceki izole kaynak ve kaydedilmiş yedi sonraki değişiklikten yeniden oluşturuldu. Değişiklik uygulanmadan yapılan kontrol derlemesi canlıdaki `main-PACRIYZM.js` dosyasını aynen üretti. Bu yayın öğrenci tarafında yalnız yedi rapor dosyasını değiştirir; yeni ana paket `main-ZSTD3B7J.js`.

`Verification.csproj` adayın gerçek DLL'leriyle önceki 21 tür sonuç matrisi ve beş ölçüm senaryosunu, toplam altı senaryo olarak çalıştırır. Yazma yalnız `no_measurement_reports_drill_20261016` kopyasında mümkündür; üretimde yalnız `--production-read-only` kabul edilir. Bekleyen migration/model değişikliği reddedilir. Prova 86 tablonun mevcut 8.609 satırını korudu.

Sıra `prepare → build → drill → preview → promote → validate`. Bu tamamlanmış yayın için `promote` tekrar çalıştırılmaz. Payload manifesti LF ve POSIX yolları kullanır. Öğrenci/personel önizlemeleri base-href, HTML ve ana JS için kesin HTTP 200 ister. API/öğrenci/personel imajlarının üçünün de geri dönüşü ve sağlık kontrolü hazırlanmıştır.

Promote iki yayın kilidini alır, API yazıcısını durdurur, PostgreSQL yedeği alır ve aday oluşturulmadan önce/sonra tüm tablo hash çokluklarını karşılaştırır. Sonra yalnız üç hedef konteyneri sırayla başlatıp doğrular. Migration veya veri backfill'i yoktur. Ortam dosyaları, yedekler ve dondurulmuş compose yalnız erişimi kısıtlı sunucu dizininde tutulur.

`observe.sh` salt okunur sağlık, imajlar, diğer konteyner kimlikleri, dış sayfa HTTP durumları ve API hata/gecikmesini kontrol eder. `monitor.sh` yayın işaretinden itibaren en az 15 dakika gözlem yapar ve başarılı sonunda `monitoring-complete.txt` üretir. Operasyon kayıtları `/var/lib/eduivme/releases/no-measurement-reports-20261016` altındadır.
