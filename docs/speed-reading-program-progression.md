# Hızlı Okuma program günlerini erken açma

Genel ayar **Admin → Hızlı Okuma → Program ilerleme** ekranındadır. Başlangıç değeri kapalıdır. Açıldığında günlük görevlerini tamamlayan öğrenciler sonraki program gününü açabilir.

Belirli bir öğrenci için **Kullanıcı İşlemleri → Kullanıcılar → Düzenle → Hızlı Okuma → Program ilerleme izni** alanını kullanın:

| Seçenek | Davranış |
| --- | --- |
| Genel ayarı kullan | Genel ayardaki değişiklikleri takip eder. |
| Beklemeden ilerleyebilir | Genel ayar kapalı olsa da gün tamamlandığında ilerleyebilir. |
| Ertesi günü beklesin | Genel ayar açık olsa da takvime göre ilerler. |

**İlerleme iznini kaydet** yalnız bu tercihi kaydeder. Profil, roller, servis erişimi, abonelikler ve kurum bağlantıları ayrı kalır. Yönetim uçları admin rolü ve `Permissions.SpeedReading.SettingsManage` yetkisi ister; mevcut MFA politikası uygulanır.

Öğrenci günlük egzersiz listesindeki bütün görevleri tamamladığında, izinliyse **Sonraki güne geç** düğmesi görünür. Aynı egzersiz birden fazla görev olarak atanmışsa her görev ayrı tamamlanmalıdır. Düğme yalnız bir sonraki günü açar; yeni görevler tamamlanmış sayılmaz. Programın son gününden sonra yeni gün açılmaz.

Geçiş, mevcut programa kazanılmış ek gün sayısı ekler. Programın başlangıç tarihi, tamamlanan gün/görev sayıları ve sonuç kayıtları değiştirilmez. İzin sonradan kapatıldığında önceden açılmış günler kapanmaz; sonraki takvim günleri bu ilerlemenin üzerine eklenir. Takvim sınırı Europe/Istanbul saat dilimidir. Personel eğitim programlarının mevcut ilerleme davranışı korunur.

## API

- `GET/PUT /api/speed-reading/program-progression/settings`: genel boolean tercih.
- `GET/PUT /api/speed-reading/program-progression/users/{userId}`: `inherit`, `allow`, `deny` öğrenci tercihi.
- `GET /api/speed-reading/daily-progress/day-advance`: oturumdaki öğrencinin geçiş durumu.
- `POST /api/speed-reading/daily-progress/advance-day`: `programProgressId` ve `completedProgramDay` ile tek gün açma.

Sunucu aktif program sahipliğini, izni, tamamlanan bütün görevleri ve sonraki günün varlığını yeniden kontrol eder. Aynı programa ait aynı gün isteğinin tekrarı ikinci gün açmaz; eski program/gün bağlamı reddedilir. Abonelik denetimi aynı günlük ilerleme uçlarında uygulanmaya devam eder.

## Veritabanı ve yayın

`20261015202217_AddProgramDayAdvance`, mevcut programlara varsayılanı sıfır olan `EarlyAdvanceDays` alanını ve global/kullanıcı tercih tablosunu ekler. Global satır anahtarı `Guid.Empty` değeridir. Kullanıcı silme süreci yalnız o kullanıcının tercihini temizler; genel ayarı ve diğer kullanıcıları korur.

Önce yedek ve migration, ardından API, öğrenci ve admin paketleri yayınlanır. Uygulama geri dönüşünde eklenen şema korunur; eski uygulama imajları bu additive şemayla uyumludur.

PostgreSQL ve HTTP yetki testleri için Docker açıkken `dotnet test tests/Integration/SpeedReading.ProgramProgression.IntegrationTests/SpeedReading.ProgramProgression.IntegrationTests.csproj` çalıştırın. Tarayıcı testleri `tests/E2E/speed-reading-day-advance.config.mjs` yapılandırmasıyla, öğrenci üretim paketinin localhost:4352 üzerinde servis edildiği ortamda çalışır.
