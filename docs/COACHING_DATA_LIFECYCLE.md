# Koçluk verisi yaşam döngüsü

Bu belge koçluk bounded context'indeki kişisel verilerin sahipliğini, erişim
sınırını ve imha kararını tanımlar. Süreler ürün varsayımıdır; kurumun KVKK
envanteri, sözleşmeleri ve hukuk değerlendirmesi onaylamadan production'da
otomatik imha etkinleştirilmez.

## Veri sınıfları

| Veri | Sahip servis | Erişim | Önerilen aktif saklama | Süre sonunda |
| --- | --- | --- | --- | --- |
| Seans planı ve katılım | Coaching | Öğrenci, yetkili veli, koç; kurum yalnız kapsam dahilinde | ilişki + 2 yıl | kimlik bağını anonimleştir; zorunlu kayıt yoksa sil |
| Koç özel notu | Coaching | yalnız koç | seans kapanışı + 1 yıl | sil |
| Paylaşılan koç notu | Coaching | seçilen görünürlük sınıfı | ilişki + 2 yıl | anonimleştir veya sil |
| Öğrenci yansıtması/notu | Coaching | öğrenci ve yetkili koç | ilişki + 2 yıl | sil veya anonimleştir |
| Ödev teslimi ve ekleri | Coaching + object storage | öğrenci ve yetkili eğitim rolleri | akademik dönem + 2 yıl | DB kaydı ve nesne birlikte silinir |
| Sınav sonucu | Coaching | öğrenci, yetkili veli/koç/kurum | mevzuat/kurum politikası | mümkünse anonimleştir; yasal saklama varsa erişimi kısıtla |
| Hedef ve ilerleme | Coaching | öğrenci ve yetkili koç/veli | ilişki + 2 yıl | anonimleştir veya sil |
| Anlaşma/onam kanıtı | Coaching | ilgili kişi; denetimde yetkili roller | hukuken gerekli süre | içerik değişmez, erişim kısıtlı tutulur |
| Admin audit | Coaching | MFA'lı operasyon yetkisi | güvenlik politikası | append-only arşiv/imha süreci |
| Abonelik, koltuk, deneme, banka transferi (velinin gönderdikleri dahil), ödeme kaydı | Coaching | kullanıcı (export), global yönetici | silme talebinden itibaren 5 yıl, değişmeden (31.10.2026 kararı; avukat kontrolü bekliyor) | ad, e-posta, ödeyen adı, notlar, cayma gerekçesi/notu ve sözleşme metinlerindeki alıcı adı/e-postası silinir; kullanıcı kimliği (veli gönderdiyse velinin kimliği de) rastgele bir kimlikle değişir; tutar, tarih, plan ve banka referansı kalır |

## İlgili kişi akışları

1. **Export:** `GET /api/data-privacy/export` Student, Teacher veya Parent JWT'siyle
   çalışır ve her zaman yalnız token sahibinin verisini döndürür. Öğrencide
   Identity servisi aktif öğrenci kapsamını tekrar doğrular. Veli yalnız kendi
   oluşturduğu verileri alır (kendi yazışmaları, çocuğu için gönderdiği ödeme
   bildirimleri); çocuğun verisi çocuğun dışa aktarımındadır. JSON çıktısı
   `schemaVersion` (şu an `1.11`) içerir; abonelik, ücretsiz deneme, ödenen dönemler,
   koltuk, banka transferi (liste fiyatı, indirim, sözleşme sürümü ve onayı, cayma),
   ödeme ve fatura kayıtları, deneme bölüm cevapları ve konu analizi, net hedefleri,
   ana hedef ve teslim dosyalarının bilgileri de dahildir; koçun özel notlarını,
   yönetici notlarını ve storage anahtarlarını içermez.
2. **Düzeltme:** veri sahibi mevcut alan bazlı güncelleme akışlarını kullanır;
   değişiklikler yetki ve concurrency kontrollerinden geçer.
3. **Silme/anonimleştirme talebi:** Identity talebin kimliğini doğrular, yasal
   saklama/hold kararını kaydeder ve her bounded context'e idempotent iş emri
   yollar. Coaching veriyi tek başına, senkron bir HTTP isteğinde topluca silmez.
4. **Kapanış:** servis sonuçları korelasyon kimliğiyle toplanır; başarısız işler
   tekrar denenir; tamamlanma kanıtı kullanıcıya bildirilir.
5. **Engellenen yürütme:** Coaching yürütme anında etkin legal hold veya eksik/
   olumsuz değerlendirme görürse hiçbir şey silmez ve
   `PersonalDataErasureExecutionBlockedV1` yayınlar; Identity talebi okunur bir
   gerekçeyle `Failed` yapar (yönetim panelinde görünür).
6. **Mali saklama:** silmede abonelik, koltuk, transfer ve ödeme kayıtları
   değişmeden kalır (kurumdaki koltuk serbest bırakılır) ve
   `CoachingErasureExecutions` satırına saklama bitişi yazılır. Günlük çalışan
   işçi süresi dolan kayıtları anonimleştirir; etkin legal hold anonimleştirmeyi
   erteler. Değerlendirmede bu kayıtlar `RetainedFinancialRecords` olarak raporlanır.
7. **Dosyalar:** silinen ödev eklerinin dosyaları, silme işlemi commit edildikten
   sonra outbox üzerinden teslim edilen `AssignmentAttachmentDeletionRequested`
   mesajıyla kaldırılır; geri alınan bir silme dosya kaybettirmez.

## Güvenlik kuralları

- Export sadece veri öznesine (öğrenci veya öğretmen) açıktır; veli adına export ayrı temsil
  yetkisi ve çocuk yüksek yararı değerlendirmesi olmadan açılmaz.
- Export yanıtı cache'lenmez, log gövdesine yazılmaz ve kalıcı indirme URL'si
  üretmez.
- Silme, audit/onam gibi hukuken tutulması gereken kaydı sessizce yok edemez.
- Object storage imhası ile veritabanı imhası aynı iş emrinin denetlenebilir
  adımlarıdır.
- Legal hold, süre aşımını durdurur; hold kaldırılmadan worker fiziksel imha
  yapamaz.

## Kalan uygulama kapıları

- Identity'de `DataSubjectRequest` durum makinesi, kullanıcının kendi taleplerini
  izlemesi, silme talebi için MFA kapısı ve SystemAdmin'in ayrı `Privacy.View` /
  `Privacy.Manage` izinleriyle kimlik doğrulama ve gerekçeli karar API'leri
  tamamlandı. Yönetim panelinde değerlendirme ve yürütme kontrolleri vardır.
- Versionlanmış assessment request/result olayları ve Coaching dry-run tüketicisi
  tamamlandı. Identity, kapsamın gerektirdiği servis sonuçlarını toplar ve Account
  silmesinde ancak Coaching, Notification ve Speed Reading tamamlanınca hesabı
  anonimleştirip talebi `Completed` yapar.
- Coaching'de idempotent dry-run envanteri ve legal-hold bloklaması tamamlandı;
  onaylı yürütme öğrenciye bağlı kayıtları ve ekleri siler, legal hold'u yürütme
  anında yeniden kontrol eder. Yerel depolama entegrasyon testi,
  yürütme servisinin gerçek nesneyi ve DB ek kaydını kaldırdığını doğrular;
  staging'deki tam broker + PostgreSQL + nesne depolama zinciri ayrıca kanıtlanmalıdır.
- Kurum bazlı, hukukça onaylı retention konfigürasyonu ve legal-hold kararlarının
  operasyonel onay/prova süreci.
- Backup'larda crypto-erasure/expiry ve restore sonrası yeniden-imha prosedürü.
- Export, kısmi başarısızlık, servis kesintisi/retry ve tenant izolasyonu E2E testleri.

Disposable Docker ortamında hesap silme akışı
`node --test tests/E2E/privacy-erasure.docker.test.mjs` ile tekrar doğrulanır.
Koşu yalnız `E2E_DISPOSABLE_ENV=true` ve Development/Staging yapılandırmasında
başlar; çalışan Identity container'ının ortamını da kontrol eder. Test, rastgele
oluşturulan bir kullanıcı için üç servis verisini, Identity anonimleştirmesini
ve aynı yürütme olayının tekrar tesliminde receipt tekilliğini doğrular. Ayrı
bir senaryo, aktif legal hold altında Coaching ve Identity assessment sonucunun
olumsuz olduğunu, koçluk verisiyle hesabın korunduğunu doğrular. Bu
testin yerelde geçmesi staging/production restore veya pentest kanıtı değildir.
CI'deki `privacy-erasure-e2e` işi aynı testi kendine ait, disposable Compose
projesinde çalıştırır; başarısızlıkta servis loglarını saklar ve test verisini
içeren Docker volume'lerini iş sonunda temizler. CI işinin yeşil sonucu ayrıca
GitHub Actions üzerinde doğrulanmalıdır.
