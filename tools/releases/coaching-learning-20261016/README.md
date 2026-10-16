# Koçluk profil ve eğitim bağlamı yayını — 16 Ekim 2026

Canlı uygulama revizyonu: `8757ca75ebcee217d76f6d1630ac3bd6f445dd92`.
Tamamlanma: `2026-10-16T16:39:55Z`. Son paket: `release-r6.tar.gz`, SHA256
`078df8e6cb27700a425fca7417a3b51dcefc48666123c35a8fe3ac6f6504ad9a`.

## Kaynak ve paket

Bu yayın doğrudan kirli çalışma dizinini derlemez. `prepare-source.mjs`, canlı PDB kaynak
özetlerini `artifacts/coaching-learning-live-20261016/live-sources.json` girdisiyle
eşleştirir. 768 canlı belge hesaba katılmıştır. Değişmeyen dosyalar canlı sürümle aynı
kalır; özellik farkı `61955eba..8757ca75` üzerinden uygulanır. Canlıda daha yeni olan
CreateUser davranışı korunarak ilgili özellik farkı üzerine uygulanmıştır.

`prepare-ui.mjs`, canlı öğrenci/yönetici ve personel istemcilerinin saklanmış kaynak
kopyalarını temel alır; yalnız bu özelliğe ait dosyaları ekler. Canlıda bulunmayan Kurs
Yönetimi auth controller'ı izole kaynak ağacından çıkarılır. Ana çalışma ağacına ve başka
yayınların kaynak kopyalarına müdahale edilmez.

İzole kaynaklardan Linux için Identity/Coaching publish, iki Angular production build
ve Verification publish alınır. `package.mjs r6`, `artifacts/coaching-learning-live-20261016/`
altındaki bu çıktıları birleştirir. Var olan paket dizinini üzerine yazmaz. Secret veya
`.env` paketlenmez. Assembly listesi Dockerfile'larda sınırlıdır; diğer çalışan servis
bağımlılıkları canlı temel imajlardan korunur. Derleme/prova kaynak girdileri ve çıktıları
yerel artifact dizinindedir; bu betikler kaynak girdileri olmadan tek başına build yapmaz.

## Sunucu aşamaları

Betik yalnız `/var/lib/eduivme/releases/coaching-learning-20261016` dizininde çalışır.
İlgili iki dağıtım kilidini alır ve önceki konteyner kimliklerini doğrular. Dört hedef:
Identity, Coaching, öğrenci/yönetici istemcisi ve personel istemcisi.

1. `bash deploy.sh prepare`: canlı imaj/ortam envanterini sunucuda özel dosyalara alır;
   mevcut JWT, CAPTCHA ve diğer çalışma ayarlarını korur. Çıktılar sır içerebilir; indirilmez.
2. `bash deploy.sh build`: yerel canlı temel imajlara sınırlı dosyaları kopyalar; ağdan
   paket indirmez. Paket r2 ile üretilen dört uygulama imajı r6'da da aynı uygulama
   payload'ını kullanır; sonraki paketler yalnız prova/operasyon aracını düzeltmiştir.
3. `bash deploy.sh drill`: Identity ve Coaching'i ayrı veritabanlarına yedekten kurar;
   ayrı RabbitMQ/Redis ve rastgele anahtarla, port yayımlamadan migration/import/API
   senaryolarını çalıştırır. Prova konteynerleri ve veritabanları başarıda kaldırılır.
   `drill-verify`, veri bütünlüğünü yeniden doğrulayarak yalnız son API aşamasını sürdürür;
   önceki başarısız API koşusu iş verisi değiştirmişse tam prova yeniden kurulmalıdır.
4. `bash deploy.sh promote`: dört yazıcı/arayüz hedefini durdurur; güncel iki DB yedeği
   ve satır özetlerini alır; üç additive migration, profil aktarımı ve mevcut onaylı
   kataloğun sürüme bağlanmasını uygular. Veri karşılaştırması ve sağlık kontrolleri
   geçmeden tamamlanma işareti yazmaz.
5. `bash deploy.sh validate`: yeni imajlar, readiness, yeniden başlatma sayıları ve
   hedef dışındaki konteynerlerin değişmediğini doğrular.
6. `bash monitor.sh`: 30 saniye aralıkla 31 örnek alır; dört konteynerin durumunu ve
   üç genel sayfanın HTTP kodu/süresini kaydeder. Normal yönlendirmeler izlenir.

SSH ile `bash -s` kullanılıyorsa tüm gönderilen betik tek blok olarak parse edilmeli ve
alt komutların sonraki betik satırlarını standart girdiden tüketmesi engellenmelidir.
Bu yayında yerel SSH çalıştırıcısı `{ ... } </dev/null` kullandı. SQL stdin'ı yalnız açık
heredoc ile; restore stdin'ı yalnız ilgili yedek dosyasıyla verilir.

## Koruma ve geri dönüş

- Mevcut Identity ve Coaching iş tablolarının satır sayısı ve sıralanmış JSON satır
  özetleri karşılaştırılır. Audit, outbox/inbox ve yeni tablolar ayrı tutulur.
- Yalnız `identity.Configurations.UpdatedAt` açılış seed'i nedeniyle; yalnız derslerin
  yeni `CurriculumVersionId` alanı planlanan katalog bağı nedeniyle karşılaştırmadan
  ayrıdır. Yapılandırma değerleri ve tüm eski iş alanları karşılaştırılmaya devam eder.
- Başarısız promote, `rollback.compose.json` ile önceki dört uygulama imajını geri açar.
  Veri içeren tablolara `Down` veya otomatik DB restore uygulamaz.
- Daha sonra uygulama geri dönüşü gerektiğinde aynı özel rollback compose dosyasıyla
  yalnız dört hedef servis başlatılır ve readiness doğrulanır. Identity'deki eski profil
  satırları bu yayında silinmediği için önceki uygulama için de korunmuştur.
- Yedekler, önceki imajlar ve sunucuya özel compose/env dosyaları korunur; sır içeren
  dosyaları kaynak kontrolüne veya rapora koymayın.

Ayrıntılı canlı doğrulama: [yayın raporu](../../../docs/coaching/coaching-learning-live-release-20261016.md).
