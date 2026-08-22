# Coaching kullanıcı portalı

`clients/admin-panel` içindeki `/coaching-portal` alanı, yönetim panelinden
ayrı bir lazy feature boundary olarak öğrenci, öğretmen ve veli çalışma
alanlarını sunar. Kimlik doğrulama ortak kalır; yetkiyi Coaching ve Identity
servisleri kendi kaynaklarında tekrar doğrular.

## Rol akışları

| Rol | Ekranlar | Yazma yetkisi |
| --- | --- | --- |
| Student | Ödev listesi/detayı, teslim, fotoğraf eki, hedefler, seanslar ve sınav sonuçları | Yalnızca kendi ödev teslimi/ekleri ve kendi hedef oluşturma-ilerleme güncellemesi |
| Teacher | Kendi ödevleri, assignment detayı, öğrenci teslimleri, puan/geri bildirim, seanslar, öğrenci listesi ve akademik takip | Yalnızca bağlı aktif öğrencilerine ödev/seans/sınav/hedef yönetimi; kendi seanslarında yoklama ve geri bildirim |
| Parent | Bağlı aktif çocuk seçimi, çocuğun ödev/hedef/sınav/seans özeti | Koçluk verilerine yazma yok |

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

## Seans ve öğrenci hedefi akışları

Öğrenci kendi kapsamındaki seansları `GET /api/sessions/student/{studentId}`
ile, öğretmen kendi seanslarını `GET /api/sessions/teacher/{teacherId}` ile
görüntüler. Grup seanslarında öğrenci cevabı yalnızca kendi attendance satırını
içerir; başka öğrencilerin kimlikleri veya katılım bilgileri response'a
eklenmez. Yetkili kullanıcıya yalnız HTTP(S) olarak doğrulanmış `meetingLink`
alanı da döner; öğrenci ve öğretmen bu bağlantıyı portalın seans kartından
açabilir. Öğrenci seans sonrası `PUT /api/sessions/{sessionId}/student-note`
ile yalnız kendi attendance kaydına en fazla 2.000 karakterlik yansıma notu
ekleyebilir ve okuyabilir; veli/öğretmen bu özel notu göremez ve bu endpoint ile yazamaz. Veli, aktif çocuğu
seçtikten sonra aynı öğrenci okuma endpoint'ini
kullanır ve Coaching → Identity ilişki kontrolü tekrar çalışır.

Öğretmen response'unda yalnızca Identity tarafından yetkilendirilen ve boş
olmayan öğrenci yansımaları `studentReflections` alanında görünür; öğretmen bu
alanı değiştiremez. Portal ayrıca `GET /api/calendar/teacher.ics` veya
`GET /api/calendar/student.ics` ile standart iCalendar dışa aktarımı sunar.
Feed 366 günle sınırlıdır, seans kimlikleri ve toplantı bağlantısı dışındaki
öğrenci verilerini içermez ve erişim JWT rolüyle tekrar doğrulanır.

Öğrenci özeti `GET /api/reports/student/{studentId}/progress` endpoint'inden
tek bir aggregate response olarak ödev, sınav, hedef ve seans metriklerini alır;
frontend sayfalama limitlerine güvenerek eksik ortalama hesaplamaz. Öğrenci `POST /api/goals` ile
`TeacherId = null` göndererek kendi hedefini
oluşturabilir. İstek `Idempotency-Key` header'ı taşır; istemci aynı ağ
zaman aşımı için aynı anahtarı, yeni hedef için yeni anahtarı kullanır.
İlerleme yalnız öğrencinin kendi hedefi için `PUT
/api/goals/{goalId}/progress` ile 0–100 arasında güncellenebilir. Öğretmen ve
veli bu kullanıcı portalında başkasının hedef ilerlemesini değiştiremez;
öğretmen/admin yönetimi ayrı, MFA ve `Permissions.Coaching.Manage` korumalı
admin API'sinde kalır.

## Öğretmen akademik ve seans yönetimi

Öğretmen portalı `/coaching-portal/teacher/academic` ekranında yalnızca kendi
öğretmen kimliğiyle sınav ve hedef listelerini sayfalı olarak okur. Sınav/hedef
oluşturma istekleri `Idempotency-Key` taşır; düzenleme endpoint'leri kaynak
sahipliğini sunucu tarafında doğrular. Liste DTO'ları açıklama, süre, sınıf,
hedef sınavı ve ders alanlarını da döndürür; böylece düzenleme sırasında
gönderilmeyen opsiyonel alanlar yanlışlıkla silinmez.

Seans listeleri 25'lik sayfalarla yüklenir ve toplam kayıt sayısı API'nin
`totalCount` alanından gösterilir. Gelecekteki planlanmış seans iptal
edildiğinde Coaching outbox üzerinden `SessionCancelledEvent` yayınlanır;
Notification servisi öğrencileri bilgilendirir. Tamamlanmış, iptal edilmiş veya
başlamış seanslar domain kuralıyla yeniden planlanamaz/iptal edilemez ve API
400 döndürür. Düzenleme formu öğretmen notunu mevcut kaynaktan doldurur.

Ödev düzenleme ekranı aktif olmayan öğrenci ilişkilerini sessizce kaldırmaz;
pasif atama varsa öğretmene açık uyarı gösterir ve güvenli ilişki doğrulaması
tamamlanmadan güncellemeyi engeller.

## SSR ve güvenlik

Kimlik gerektiren `/coaching-portal/**` rotaları Angular SSR'da client render
edilir. Böylece assignment ID, puan, geri bildirim veya çocuk ilerlemesi
prerender edilmiş HTML'a girmez. API hata cevapları kullanıcıya sınırlı mesaj
gösterir; detaylı hata ve korelasyon bilgisi sunucu loglarındadır.
