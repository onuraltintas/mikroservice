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

## İlgili kişi akışları

1. **Export:** `GET /api/data-privacy/export` yalnız Student JWT'siyle çalışır.
   Identity servisi aktif öğrenci kapsamını tekrar doğrular. JSON çıktısı
   `schemaVersion` içerir; koçun özel notlarını ve storage anahtarlarını içermez.
2. **Düzeltme:** veri sahibi mevcut alan bazlı güncelleme akışlarını kullanır;
   değişiklikler yetki ve concurrency kontrollerinden geçer.
3. **Silme/anonimleştirme talebi:** Identity talebin kimliğini doğrular, yasal
   saklama/hold kararını kaydeder ve her bounded context'e idempotent iş emri
   yollar. Coaching veriyi tek başına, senkron bir HTTP isteğinde topluca silmez.
4. **Kapanış:** servis sonuçları korelasyon kimliğiyle toplanır; başarısız işler
   tekrar denenir; tamamlanma kanıtı kullanıcıya bildirilir.

## Güvenlik kuralları

- Export sadece veri öznesi öğrenciye açıktır; veli adına export ayrı temsil
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
  anında yeniden kontrol eder. Nesne depolama silme kanıtının ayrı E2E testi eksik.
- Kurum bazlı, hukukça onaylı retention konfigürasyonu ve legal-hold kararlarının
  operasyonel onay/prova süreci.
- Backup'larda crypto-erasure/expiry ve restore sonrası yeniden-imha prosedürü.
- Export, kısmi başarısızlık, servis kesintisi/retry ve tenant izolasyonu E2E testleri.

Disposable Docker ortamında hesap silme akışı
`node --test tests/E2E/privacy-erasure.docker.test.mjs` ile tekrar doğrulanır.
Koşu yalnız `E2E_DISPOSABLE_ENV=true` ve Development/Staging yapılandırmasında
başlar; çalışan Identity container'ının ortamını da kontrol eder. Test, rastgele
oluşturulan bir kullanıcı için üç servis verisini, Identity anonimleştirmesini
ve aynı yürütme olayının tekrar tesliminde receipt tekilliğini doğrular. Bu
testin yerelde geçmesi staging/production restore veya pentest kanıtı değildir.
