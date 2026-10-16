# Egzersiz raporları yayını · 16.10.2026

Çalışan API ve personel portalı üzerinden izole derleme; kapsam ve kanıtlar [yayın raporunda](../../../docs/reports/speed-reading-exercise-reports-20261016.md).

İzole backend `.tmp/exercise-reports-20261016/backend`, çalışan `vocabulary-scoring-20261016` kaynak tabanından türetildi. Yalnız üç rapor Infrastructure dosyası değişti. Personel kaynakları önceki `speed-reading-assignment-fixes-20261015/staff-release` tabanından türetildi; dört TS/HTML dosyası değişti. Çalışma ağacının diğer geliştirmeleri yayına alınmaz. Staff derlemesi `ng build staff-portal --configuration production --base-href /staff/` kullanır.

`Verification.csproj` gerçek PostgreSQL senaryosunu bağlar. Aday uygulamanın gerçek DLL'leriyle çalıştırılır. `REPORTS_CONNECTION` yalnız sabit `exercise_reports_drill_20261016` kopyasında dört senaryo çalıştırır; üretimde yalnız `--production-read-only` kabul edilir. Bekleyen migration/model değişikliği reddedilir. Prova sonrası tüm mevcut satırların hash çoklukları korunmalıdır.

Operasyon sırası `prepare → build → drill → preview → promote → validate`. Bu yayın tamamlandıktan sonra `promote` yeniden çalıştırılmaz. Yedekler, dondurulmuş ortam ve compose dosyaları erişimi kısıtlı `/var/lib/eduivme/releases/exercise-reports-20261016` dizininde kalır; depoya alınmaz. Payload hash manifesti LF ve POSIX yolları kullanır.

Promote iki yayın kilidini alır, yazıcıyı durdurur, yedek alır, 86 tablonun tüm hash'lerini aday oluşturulmadan önce/sonra karşılaştırır ve ardından API/Staff'ı başlatır. API ve Staff eski imajlarına geri dönüş hazırlanmıştır. Staff sağlık kontrolü HTML base-href ve ana JS varlığını sınar. `observe.sh`, yalnız salt okunur sağlık, imaj kimliği, diğer konteyner kimlikleri, dış sayfalar ve HTTP hata/gecikme kontrolüdür. Şema veya veri migration'ı yoktur.
