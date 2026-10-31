# Coaching kullanıcı portalı

`clients/admin-panel` içindeki `/coaching-portal` alanı öğrenci ve veli panelidir.
Öğretmen ve kurum yöneticileri ortak personel portalını
`https://eduivme.com/staff/?product=coaching` adresinde kullanır (adres
`PUBLIC_APP_BASE_URL` ayarından gelir). Eski `/coaching-portal/teacher/**` adresleri
bu portala yönlendirilir. Veliler bağlı çocuklarının salt okunur panelini
`/coaching-portal/parent` altında görür (roadmap Y7). Kimlik doğrulama ortak kalır;
yetkiyi Coaching ve Identity servisleri kendi kaynaklarında tekrar doğrular.

## Rol akışları

| Rol | Ekranlar | Yazma yetkisi |
| --- | --- | --- |
| Student | Ödev listesi/detayı, teslim, fotoğraf eki, hedefler, seanslar ve sınav sonuçları | Yalnızca kendi ödev teslimi/ekleri ve kendi hedef oluşturma-ilerleme güncellemesi |
| Parent | Çocuklarım, çocuğun özeti, deneme, ödev, seans, ücret ve abonelik ekranları; öğretmenle yazışma | Çocuk adına havale bildirimi ve yazışma; çocuğun kayıtlarını değiştiremez |
| Teacher | Yalnız personel portalında: ödevler, teslimler, puan/geri bildirim, seanslar, öğrenci listesi ve akademik takip | Yalnızca bağlı aktif öğrencilerine ödev/seans/sınav/hedef yönetimi; kendi seanslarında yoklama ve geri bildirim |

Öğrenci ve veli rolleri `/coaching-portal` alanına erişir; veli yalnız veli
ekranlarını görür. Öğrenci portalında öğretmen ekranı ya da öğretmen işlemi
yoktur (D3'te kalan kod kaldırıldı). Öğretmen/kurum kullanıcıları ortak personel
portalına, yönetim rolleri `/dashboard`a yönlendirilir. Eski öğretmen ekranları
uygulama route'larında sunulmaz; eski
adresler uyumluluk için yeni portala aktarılır. Asıl veri yetkisi servislerdeki
JWT + ürün/tenant/ilişki kontrollerindedir.

## Teslim ve ekler

Öğrenci eki iki aşamalı yüklenir:

1. `POST /api/assignments/{assignmentId}/students/{studentId}/attachments`
   ile dosya adı, MIME, boyut ve SHA-256 metadata'sı kaydedilir.
2. Dönen kısa ömürlü path'e `PUT` ile ham dosya gönderilir ve
   `X-Content-SHA256` header'ı tekrar verilir.

Kabul edilen türler: JPEG, PNG, WebP, PDF, Word (.doc, .docx) ve Markdown (.md).
Dosya başına en çok 10 MiB; öğrenci başına ödev başına en çok 10 ek ve toplam 50 MiB.
Öğretmenin ödeve eklediği yönerge dosyaları ödev başına en çok 10 adettir
(`AssignmentAttachmentPolicy`). "Fotoğraf çek" kamerayı açar ve yalnız görsel kabul
eder. Coaching
servisi dosyayı ClamAV taramasından geçirmeden temiz kabul etmez. Frontend
kontrolleri kullanıcı deneyimi içindir; güvenlik doğrulaması her zaman API'de
tekrarlanır.

## Veli ilişkileri ve API kapsamı

Veli, öğrencinin verdiği 7 günlük tek kullanımlık kodla bağlanır; öğrenci onaylar
ve iki taraf da bağı kaldırabilir (`/api/users/me/parent-links`). Pazar akşamı
veliye haftalık özet e-postası gider. `GET /api/users/me/children` sunucu tarafında yalnız
`Parent` rolüyle kullanılabilir; Identity servisi aktif veli profilini, aktif
öğrenci kullanıcısını, aktif öğrenci profilini ve varsa aktif kurumu birlikte
kontrol eder. Koçluk okuma API'leri de ürün ve ilişki kapsamını sunucuda tekrar
doğrular.

## Seans ve öğrenci hedefi akışları

Öğrenci kendi kapsamındaki seansları `GET /api/sessions/student/{studentId}`
ile, öğretmen kendi seanslarını `GET /api/sessions/teacher/{teacherId}` ile
görüntüler. Grup seanslarında öğrenci cevabı yalnızca kendi attendance satırını
içerir; başka öğrencilerin kimlikleri veya katılım bilgileri response'a
eklenmez. Yetkili kullanıcıya yalnız HTTP(S) olarak doğrulanmış `meetingLink`
alanı da döner; öğrenci ve öğretmen bu bağlantıyı portalın seans kartından
açabilir. Öğrenci seans sonrası `PUT /api/sessions/{sessionId}/student-note`
ile yalnız kendi attendance kaydına en fazla 2.000 karakterlik yansıma notu
ekleyebilir ve okuyabilir; veli/öğretmen bu özel notu göremez ve bu endpoint ile
yazamaz.

Öğretmen response'unda yalnızca Identity tarafından yetkilendirilen ve boş
olmayan öğrenci yansımaları `studentReflections` alanında görünür; öğretmen bu
alanı değiştiremez. Öğrenci portalı `GET /api/calendar/student.ics`, personel
portalı `GET /api/calendar/teacher.ics` ile standart iCalendar dışa aktarımı sunar;
ikisi de kişisel abonelik adresi de verir (`GET /api/calendar/feed/{token}.ics`, O6).
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
veli rolleri bu endpoint ile başkasının hedef ilerlemesini değiştiremez.
öğretmen/admin yönetimi ayrı, MFA ve `Permissions.Coaching.Manage` korumalı
admin API'sinde kalır.

## Öğretmen akademik ve seans yönetimi

Yeni personel portalındaki akademik takip ekranı yalnızca kendi
öğretmen kimliğiyle sınav ve hedef listelerini sayfalı olarak okur. Sınav/hedef
oluşturma istekleri `Idempotency-Key` taşır; düzenleme endpoint'leri kaynak
sahipliğini sunucu tarafında doğrular. Liste DTO'ları açıklama, süre, sınıf,
hedef sınavı ve ders alanlarını da döndürür; böylece düzenleme sırasında
gönderilmeyen opsiyonel alanlar yanlışlıkla silinmez.

Sınav kartındaki `Sonuçları yönet` akışı `GET /api/exams/{examId}/teacher-detail`
ile yalnızca sınav sahibinin öğrenci sonuçlarını açar; `pageNumber` ve `pageSize`
parametreleri sonuçları 25'lik varsayılan sayfalarla sınırlar. Öğretmen sonuç eklerken
`POST /api/exams/{examId}/results` ve benzersiz `Idempotency-Key`, düzeltirken
`PUT /api/exams/{examId}/results/{resultId}` kullanılır. Puan, cevap dağılımı,
sıralama, ders puanları ve öğretmen notu hem istemcide hem Coaching domain
kurallarında doğrulanır; öğrenci hedefi Identity ilişki kontrolünden geçmeden
sonuç eklenemez.

Öğrenci ilerleme ekranı son altı sınavı kronolojik trend olarak, puan/sıralama,
doğru-yanlış-boş ve ders kırılımıyla gösterir ve sonuçları 25'lik sayfalarla
yükler. Veli çocuğun son denemelerini kendi panelinde görür.

Seans listeleri 25'lik sayfalarla yüklenir ve toplam kayıt sayısı API'nin
`totalCount` alanından gösterilir. Gelecekteki planlanmış seans iptal
edildiğinde Coaching outbox üzerinden `SessionCancelledEvent` yayınlanır;
Notification servisi öğrencileri bilgilendirir. Tamamlanmış, iptal edilmiş veya
başlamış seanslar domain kuralıyla yeniden planlanamaz/iptal edilemez ve API
400 döndürür. Düzenleme formu öğretmen notunu mevcut kaynaktan doldurur.

Ödev düzenleme ekranı aktif olmayan öğrenci ilişkilerini sessizce kaldırmaz;
pasif atama varsa öğretmene açık uyarı gösterir ve güvenli ilişki doğrulaması
tamamlanmadan güncellemeyi engeller.

### Paylaşım ayarı, bağın bitmesi ve geçmiş (K2, 31.10.2026)

- **"İlerlemeyi öğretmenlerle paylaş" kapalıysa** öğretmenden yalnız çalışma planı, raporlar, geçmiş ve ilerleme
  gizlenir. Öğretmenin verdiği ödevler, teslimler, ekler, seanslar ve yoklama bağ sürdükçe görünür; öğretmen
  teslimi notlandırır ve yoklama alır (`AuthorizeStudentWorkReadAsync`).
- **Katılımcı düzenleme:** istemci yalnız görebildiği öğrencileri gönderir; grup seansında arayanın göremediği
  öğrenciler sunucuda korunur, sessizce silinmez.
- **Bağ bitince** (kurum bağı sonlandırma, öğrencinin kurumdan ayrılması, bağımsız koçla bağın bitmesi, halefsiz
  öğretmen çıkarma) o öğretmenin öğrenciyle gelecekteki seanslarından öğrenci çıkarılır (kimse kalmazsa seans
  iptal edilir) ve teslim edilmemiş açık ödevleri geri çekilir (kimse kalmazsa ödev iptal edilir). Teslim
  edilmiş, notlanmış ya da dosya yüklenmiş çalışma ve geçmiş seanslar kayıt olarak kalır. Öğrenciye, öğretmene
  ve kurum yöneticisine neyin kapandığı bildirilir (`CoachingTeacherLinkEndedEvent`, `InstitutionEnded`).
- **Geçmiş öğrenciyi izler** (kullanıcı kararı 28.10 ve 31.10.2026: "hepsi görünsün"): yeni kurum ve öğretmenler
  öğrencinin önceki kurumlarındaki ve bağımsız koçuyla yaptığı ödev, deneme, seans ve hedef geçmişini görür.
  Öğrenci bunu davet kabulünden önce ve "Kişisel verilerim" sayfasında okur.
- **Mesaj incelemesi:** öğretmen–öğrenci çifti başka bir kuruma geçerse yazışma onlarla gider; yeni kurum yalnız
  o kurumdaki bağın başladığı andan sonraki mesajları ve dosyaları inceleyebilir.

## SSR ve güvenlik

Kimlik gerektiren `/coaching-portal/**` rotaları Angular SSR'da client render
edilir. Böylece assignment ID, puan, geri bildirim veya çocuk ilerlemesi
prerender edilmiş HTML'a girmez. API hata cevapları kullanıcıya sınırlı mesaj
gösterir; detaylı hata ve korelasyon bilgisi sunucu loglarındadır.
