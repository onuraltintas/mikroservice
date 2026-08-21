# Coaching kullanıcı portalı

`clients/admin-panel` içindeki `/coaching-portal` alanı, yönetim panelinden
ayrı bir lazy feature boundary olarak öğrenci, öğretmen ve veli çalışma
alanlarını sunar. Kimlik doğrulama ortak kalır; yetkiyi Coaching ve Identity
servisleri kendi kaynaklarında tekrar doğrular.

## Rol akışları

| Rol | Ekranlar | Yazma yetkisi |
| --- | --- | --- |
| Student | Ödev listesi/detayı, teslim, fotoğraf eki, hedefler ve sınav sonuçları | Yalnızca kendi ödev teslimi ve kendi ekleri |
| Teacher | Kendi ödevleri, assignment detayı, öğrenci teslimleri ve puan/geri bildirim | Yalnızca sahibi olduğu assignment öğrencilerini değerlendirme |
| Parent | Bağlı aktif çocuk seçimi, çocuğun ödev/hedef/sınav özeti | Koçluk verilerine yazma yok |

Giriş sonrası `Student`, `Teacher` ve `Parent` rolleri `/coaching-portal`a,
yönetim rolleri `/dashboard`a yönlendirilir. `/coaching-portal` route'u
yalnızca bu üç rol için gezinme alanı açar; asıl veri yetkisi servislerdeki
JWT + tenant/ilişki kontrollerindedir.

## Teslim ve fotoğraf eki

Öğrenci fotoğrafı iki aşamalı yüklenir:

1. `POST /api/assignments/{assignmentId}/students/{studentId}/attachments`
   ile dosya adı, MIME, boyut ve SHA-256 metadata'sı kaydedilir.
2. Dönen kısa ömürlü path'e `PUT` ile ham fotoğraf gönderilir ve
   `X-Content-SHA256` header'ı tekrar verilir.

Yalnız JPEG, PNG ve WebP kabul edilir; maksimum boyut 10 MiB'dir. Coaching
servisi dosyayı ClamAV taramasından geçirmeden temiz kabul etmez. Frontend
kontrolleri kullanıcı deneyimi içindir; güvenlik doğrulaması her zaman API'de
tekrarlanır.

## Veli çocuk kapsamı

`GET /api/users/me/children` yalnız `Parent` rolüyle kullanılabilir. Identity
servisi aktif veli profilini, aktif öğrenci kullanıcısını, aktif öğrenci
profilini ve varsa aktif kurumu birlikte kontrol eder; pasif veya başka veliye
bağlı çocuklar response'a eklenmez. Parent daha sonra çocuğun `userId`'siyle
Coaching okuma endpoint'lerini çağırır; Coaching → Identity iç erişim kontrolü
aynı ilişkiyi tekrar doğrular.

## SSR ve güvenlik

Kimlik gerektiren `/coaching-portal/**` rotaları Angular SSR'da client render
edilir. Böylece assignment ID, puan, geri bildirim veya çocuk ilerlemesi
prerender edilmiş HTML'a girmez. API hata cevapları kullanıcıya sınırlı mesaj
gösterir; detaylı hata ve korelasyon bilgisi sunucu loglarındadır.
