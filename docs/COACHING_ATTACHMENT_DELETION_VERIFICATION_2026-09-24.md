# Koçluk eki silme doğrulaması — 24 Eylül 2026

## Kapsam ve sonuç

Koçluk servisi `eduivme/coaching-service:outbox-4995b753` imajına geçirildi.
Ödev silme komutu, ek dosyalar için silme olaylarını MassTransit veritabanı
outbox'ına ödev silinmesiyle aynı `SaveChanges` işleminde kaydeder. Veritabanı
başarısız olursa dosyaya dokunulmaz. Başarılı kayıt sonrasında dosya hemen
silinmeye çalışılır; bu deneme başarısız olursa outbox tüketicisi idempotent
silme işlemini yeniden dener. Kalıcı depo arızasında MassTransit hata kuyruğu
operasyonel müdahale gerektirir.

## Kanıt

- `CoachingAssignmentAttachmentDeletionTests`: 3/3 başarılı. Veritabanı
  başarısızlığı dosyayı koruyor; anlık depo hatasında outbox olayı tutuluyor;
  birden çok ek silinirken ilgisiz dosya korunuyor.
- `CoachingAttachmentDeletionConsumerTests`: 1/1 başarılı. Mesaj tüketicisi
  istenen depo anahtarını siliyor. Odaklı coverage ölçümünde tüketici satır
  kapsamı %100; silme işleyicisi async akışının satır kapsamı %96,55 ve dal
  kapsamı %83,33.
- İlgili koçluk ekleri test filtresi: 24/24 başarılı. Yerel NuGet güvenlik
  verisi alınamadığı için `NU1900` uyarıları vardı; bu, açık taraması kanıtı
  değildir.
- Tüm koçluk odaklı entegrasyon testleri taramasında 202 test geçti. Ayrı
  `CoachingStudentReadRepositoryTests` sınıfındaki 11 test, yerel Windows
  ortamında Docker named pipe erişimi bulunmadığı için PostgreSQL
  Testcontainers fixture'ını başlatamadı; bu sınıf hariç 202/202 geçti.
- Ayrı PostgreSQL, RabbitMQ ve volume kullanan disposable Docker projesinde
  HTTP yükleme, tarama, sahiplik kontrolü, indirme ve silme geçti. Silme
  olayının tüketiciye ulaştığı `InboxState` ile doğrulandı. Proje ve tüm
  sentetik volume'ları kaldırıldı.
- Canlı ortamda üç geçici sentetik hesap, gerçek Identity giriş API'siyle
  oturum açtı. Koçluk ekinin ClamAV taraması `Clean`, sahibi tarafından
  indirilen içerik birebir aynı, ikinci öğrencinin erişimi `403`, öğretmenin
  ödev silmesi `204` oldu. Veritabanı kaydı ve fiziksel dosya kayboldu;
  outbox tüketimi doğrulandı. Test hesabı, kurum, ödev ve dosyası temizlendi;
  son ek kaydı sayısı sıfırdı.
- Canlı Compose koçluk imajını `outbox-4995b753` olarak sabitler. Önceki
  `delete-5509a82d` imajı ve Compose geri dönüş kopyası korunur.

## İşletim notu

Outbox ve kısa süreli tekrar denemeler geçici hatalara dayanıklılık sağlar;
uzun süreli dosya deposu arızası otomatik olarak sonsuza dek denenmez. Bu
durumda `AssignmentAttachmentDeletionRequested_error` kuyruğu ve koçluk
logları incelenmeli, depo düzeltildikten sonra mesajlar kontrollü yeniden
oynatılmalıdır. Genel alarm kanalı ve otomatik VPS yedekleme ayrı operasyon
işleridir. GitHub Actions'ın güncel yeşil koşusu bu provada doğrulanmadı.
