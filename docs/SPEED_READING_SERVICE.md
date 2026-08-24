# Speed Reading Service

Speed Reading, Coaching'e bağlı olmayan ayrı bir bounded context olarak çalışır.
`masterhizliokuma.com` gibi bağımsız bir uygulama veya Eduİvme platformunun
Gateway'i arkasında aynı servis kullanılabilir.

## Veri sahipliği ve geçiş güvenliği

- Mevcut hızlı okuma veritabanı korunur; ilk entegrasyon aşamasında servis
  migration çalıştırmaz ve şemayı değiştirmez.
- `ConnectionStrings:SpeedReading` veya `SPEED_READING_CONNECTION_STRING`
  zorunludur. Bağlantı bilgisi yoksa servis güvenli şekilde başlamaz.
- Yeni servis veri sahibi olarak devreye alındığında eski uygulamanın yazma
  yolları kapatılmalı, önce salt-okunur doğrulama ve geri dönüş planı
  tamamlanmalıdır.

## Çalışma modları

`SpeedReading:Mode` iki değerden biridir:

- `Standalone`: Coaching entegrasyonu kapalıdır; hızlı okuma uygulaması tek
  başına çalışır.
- `Platform`: Eduİvme Gateway ve yetki sözleşmeleri üzerinden platforma
  bağlanır. Coaching, Notification veya Subscription entegrasyonları ayrıca
  açıkça etkinleştirilir.

Varsayılan mod `Standalone`'dır. Standalone modda Coaching entegrasyonu
etkinleştirilirse servis açılışta durur; böylece bağımsız uygulama yanlışlıkla
platform bağımlılığına dönüşmez.

## Gateway ve Compose

Gateway dışarıya `/api/speed-reading` rotasını sunar ve iç ağda
`speed-reading-service:8080` hedefine yönlendirir. Geliştirme ortamında servis
`localhost:5004` üzerinde çalıştırılabilir.

Base Compose'da servis `speed-reading` profiliyle isteğe bağlıdır:

```powershell
docker compose --profile speed-reading up -d speed-reading-service
```

Staging ve production overlay'leri profili kaldırır; bu ortamlar için
`SPEED_READING_CONNECTION_STRING` deployment secret olarak verilmelidir.
Hızlı okuma veritabanı platform PostgreSQL container'ına taşınmadığı sürece
servis `postgres` migration/depends-on zincirine eklenmez.

## Yetki sınırı

Identity permission seed'i aşağıdaki bağımsız anahtarları sağlar:

- `Permissions.SpeedReading.View`
- `Permissions.SpeedReading.ContentManage`
- `Permissions.SpeedReading.ProgramManage`
- `Permissions.SpeedReading.ProgressView`
- `Permissions.SpeedReading.ReportView`
- `Permissions.SpeedReading.GamificationManage`
- `Permissions.SpeedReading.SettingsManage`

Kurum rolleri varsayılan olarak yalnızca görünürlük, ilerleme ve rapor okuma
yetkilerini alır; içerik/program/ayar değişiklikleri SystemAdmin veya açıkça
atanmış yetki gerektirir.
