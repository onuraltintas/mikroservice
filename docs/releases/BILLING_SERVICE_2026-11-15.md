# Abonelik ve Ödeme servisi: yayın talimatı (hazırlık 15.11.2026)

Kapsam: canlıdaki `92624d7` ile `main` arasındaki her şey (#149–#177 ve yayın hazırlığı). Yeni `billing-service`, onu
kullanan Koçluk, Hızlı Okuma, Identity, bildirim servisi, gateway ve üç arayüz birlikte çıkar.

**Bu talimat kullanıcının ayrı onayı olmadan canlıda uygulanmaz.** Önce yedekten kurulmuş bir kopyada baştan sona
denenir (bölüm P). Her bölümün sonunda durulur, sonuç kullanıcıya yazılır, "devam et" beklenir.

Kurallar: `.env` içindeki hiçbir değer ekrana ya da rapora yazılmaz. Veritabanı denetimleri salt okunurdur ve yalnız
sayı döndürür. İmajlar sunucuda derlenmez; `pull-release-images.sh` ile çekilir.

## Bu yayınla ne değişir
| Alan | Önce | Sonra |
|---|---|---|
| Abonelik, sipariş, havale, koltuk, kupon, deneme, cayma, fatura | Koçluk ve Hızlı Okuma'nın kendi kodu ve tabloları | Tek servis: `billing-service`, kendi veritabanı (`billing` şeması) |
| Kimin erişimi var | Ürün kendi kayıtlarına bakar | Ürün billing'e sorar; billing'e ulaşılamazsa son 24 saatin yanıtı geçerli, hiç sorulmamış kullanıcı içeri alınır |
| Kullanıcı ekranı | Ürüne göre ayrı ödeme sayfaları | Her üründe aynı "Aboneliğim" sayfası |
| Yönetici | Koçluk ve Hızlı Okuma menülerinde ayrı sayfalar | `eduivme.com/dashboard` › "Abonelik ve Ödeme" |
| Veli | Çocuk adına ödeme formu | Yalnız çocuğun abonelik durumunu görür; abonelik öğrencinin hesabından alınır |
| Kurum lisansı (Identity) | Lisans türü, öğrenci/öğretmen sınırı, tarih | Okunmuyor ve değiştirilemiyor; sütunlar duruyor |
| Eski abonelik tabloları (Koçluk, Hızlı Okuma) | Kullanılıyor | Kullanılmıyor; **bu yayında silinmiyor** |
| Koçluk'ta plan süresi (karar 15.11.2026) | Plan her zaman sınava kadar | Sihirbazda seçim: sınava kadar (varsayılan), yalnız bu hafta ya da seçilen tarihe kadar. Kısa plan "Gelecek haftayı ekle" ile sürdürülür; bitmeden planı yapana hatırlatma gider. Yayım onayı neyin değişeceğini sayılarla söyler |
| Koçluk'ta planı kim yapar (karar 15.11.2026) | Öğretmene bağlı öğrenci de kendi planını yapar | Etkin öğretmeni olan öğrenci, öğretmeni izin verene kadar planını ve haftalık düzenini değiştiremez (varsayılan kapalı); öğretmeni olmayan öğrenci serbest. Öğretmen haftalık düzeni kaydedince ve ayarı değiştirince öğrenciye bildirim gider |

**Dikkat (plan ayarı):** yayından sonra etkin öğretmeni olan her öğrencide plan yapma kapalı başlar; öğretmen açana
kadar bu öğrenciler planlarını değiştiremez (kullanıcı kararı: mevcut bağlı öğrenciler test hesaplarıdır).

**Kimse erişim kaybetmez:** yeni serviste iki ürün de "abonelik isteme" ayarıyla açılır (hazır veri). Eski abonelik,
talep ve ödeme kayıtları yeni servise taşınmaz (kullanıcı kararı, 14.11.2026). "Abonelik iste" ayarını yönetici, planlar
ve banka hesabı girildikten sonra kendisi açar.

## Migration'lar
| Servis | Migration |
|---|---|
| billing-service | 7 migration, boş veritabanına: `InitialBillingCatalog` … `ManualPeriods` |
| speed-reading-service | 1: `20261114190000_SalesDocumentsAndWithdrawal` (91 → 92). Eklediği alanları artık kullanan kod yok; eski tablolarla birlikte sonra silinecek |
| coaching-service | 2 (103 → 105): `20261124090000_StudentCanPlanOwnStudy` (öğrenci profiline `CanPlanOwnStudy` sütunu, varsayılan kapalı) ve `20261125090000_PlanEndingReminder` (hatırlatma türlerine `PlanEnding` eklenir) |
| identity, notification | Yok |

## P. Prova (canlıdan önce, yedekten kurulmuş kopyada)
1. Son yedeklerden (identity, coaching, speed-reading) ayrı bir Compose projesinde kopya kurulur; canlı container'lara,
   canlı veritabanlarına ve canlı alan adlarına dokunulmaz. Kopyadan e-posta çıkmamalıdır (SMTP ayarı boş ya da sahte).
2. A–D bölümleri kopyada uygulanır.
3. Kopyada denenecekler (hepsi "TEST" işaretli verilerle):
   - Yönetici: satıcı bilgisi, banka hesabı, bir bireysel plan; Koçluk için "abonelik iste" açılır.
   - Öğrenci: Aboneliğim › plan seç › sözleşmeyi onayla › ödeme kodunu gör › "ödemeyi yaptım".
   - Yönetici: siparişi onayla; öğrencinin erişimi açılır. Onayı geri al; erişim kapanır.
   - `billing-service` durdurulur: az önce erişimi olan öğrenci çalışmaya devam eder. Yeniden başlatılır.
   - Öğretmen planı ve bir koltuk; kurum planı ve bir koltuk (personel portalı).
   - Hesap silme talebi: billing'in yanıtı beklenir ve akış tamamlanır.
   - Plan ayarı: öğretmene bağlı test öğrencisi "Planım"da uyarıyı görür, "Planı düzenle" sekmesi yoktur ve haftalık düzeni
     kaydedemez. Öğretmen personel portalında "Öğrenci kendi planını yapabilsin"i açar; öğrenci bildirim alır ve plan
     yapabilir. Öğretmen haftalık düzeni kaydeder; öğrenci bildirim alır.
   - Plan süresi: sihirbazın özet adımında "Yalnız bu hafta" seçilir; "yetişmiyor" uyarısı çıkmaz, plan yayımlanır. Planım'da
     "Gelecek haftayı ekle" görünür; iki seçenekle de (kalanlar önce / eklenmesin) denenir. Yeni plan bugünden bir hafta
     sonrasına uzanır; tamamlanan çalışmalar ve bu haftanın kalan bekleyen çalışmaları gününde ve saatinde kalır, yeni
     çalışmalar mevcut bitişten sonraki günlere eklenir. Aynısı öğretmenin öğrenci sayfasından denenir.
   - Hatırlatma: bitişi bugün ya da yarın olan haftalık planı yapan kişi 09:00'dan sonra bir kez "Çalışma planı bitiyor"
     bildirimi alır.
4. Sonuç ve süreler rapora yazılır. Kopya, kullanıcı onayından sonra kaldırılır.

## A. Hazırlık ve denetimler (salt okunur)
1. Çalışan sürümler `92624d7`; container sayısı ve sağlık durumu; disk doluluğu.
2. `main` üzerindeki yayın commit'inin CI'ı yeşil ve "Production images" iş akışı o commit için 9 imajı üretmiş.
3. Yedek: identity, coaching, speed-reading veritabanları (`docs/DB_BACKUP_RUNBOOK.md`); yedeklerin açıldığı doğrulanır.
4. Migration sayıları not edilir (beklenen: coaching 103, speed-reading 91, identity 38).
5. Eski tablolardaki kayıt sayıları not edilir (yalnız sayı): Koçluk ve Hızlı Okuma'da abonelik, havale talebi, ödeme.

## B. Billing veritabanı ve ayarlar
1. Mevcut PostgreSQL'de ayrı bir veritabanı ve yalnız o veritabanında yetkili bir rol oluşturulur (başka servislerin
   veritabanına erişimi olmaz). Parola sunucuda üretilir, hiçbir yere yazdırılmaz.
2. `.env` dosyasına `BILLING_CONNECTION_STRING` eklenir (yerel geliştirme dosyasındaki biçimle; sunucu adı `postgres`).
   İsteğe bağlı: `BILLING_ALLOWED_HOSTS`, `BILLING_OTLP_ENDPOINT`.
3. `docker-compose.billing.production.yml` ve yeni `infrastructure/systemd/eduivme-production.service` sunucuya alınır;
   `systemctl daemon-reload`. Birim dosyası artık dört Compose dosyasıyla çalışır.
4. `docker compose … config --quiet` hatasız geçer (dört dosyayla).

## C. İmajlar ve geçiş
1. `infrastructure/scripts/pull-release-images.sh <7 karakterlik commit>` → "PASS: all 9 images".
2. `.env` içinde `RELEASE_TAG` yeni commit olur.
3. Sıra (her adımdan sonra sağlık ve log denetimi):
   1. `identity-service`
   2. `notification-service` (yeni `billing-notice` kuyruğunu dinler)
   3. `billing-migrations` (bir kez çalışır, 7 migration), ardından `billing-service`
   4. `coaching-migrations` (2 migration), ardından `coaching-service` (yeni hatırlatma türünü bildirim servisi 2. adımda tanımış olmalı)
   5. `speed-reading-migrations`, ardından `speed-reading-service`
   6. `api-gateway`
   7. `admin-panel`, `staff-portal`, `speed-reading-frontend`
4. Denetimler:
   - `billing-service` ağ içinden `/health/ready` 200.
   - Gateway üzerinden `GET /api/billing/v1/catalog/coaching` 200 (plan listesi boş olabilir).
   - Gateway üzerinden `/internal/billing/...` 404 (iç uç dışarıya açık değil).
   - Üç sitenin ana ve giriş sayfaları 200; yeniden başlama sayısı 0; loglarda hata yok.
   - Bir test öğrencisi Koçluk'a ve Hızlı Okuma'ya girebiliyor (abonelik istenmiyor).

## D. Yasal metinler (identity veritabanı, sırayla)
Üçü de md5 korumalıdır; sunucudaki metin beklenenden farklıysa durur ve hiçbir şeyi değiştirmez.
1. `docs/legal/publish-newsletter-consent-20261114.sql`
2. `docs/legal/publish-legal-fixes-20261114.sql`
3. `docs/legal/publish-billing-legal-20261115.sql` (2'den sonra)

Her biri `-v publish_day='<gün Ay yıl>'` ile çalıştırılır; çıktıdaki sürüm numaraları rapora yazılır.

## E. Yayından sonra (yönetici panelinde, kullanıcıyla birlikte)
1. "Abonelik ve Ödeme" › Ayarlar: satıcı bilgileri, banka hesabı (IBAN), uyarı e-posta adresi.
2. Planlar: fiyat, süre, kim alır, koltuk sayısı, verdiği haklar.
3. Hâlen elle erişim verilmiş kullanıcı varsa "Abonelikler › Elle tanımla" ile girilir (gerekçe yazılır).
4. Hazır olunca ürün ayarından "abonelik iste" açılır. Bu andan sonra aboneliği olmayan öğrenci ürüne giremez.
5. Canlı test: öğrenci, öğretmen, kurum yöneticisi ve veli oturumlarıyla satın alma ve koltuk akışı.

## Bu yayında yapılmayanlar (ayrı karar ve ayrı onay)
- Koçluk ve Hızlı Okuma'daki eski abonelik tablolarının, içlerindeki test kayıtlarının ve Identity'deki kurum lisans
  sütunlarının silinmesi. Silen migration henüz yazılmadı; yedekten sonra ve kullanıcıya ayrıca sorularak yapılır.
- Bildirim servisindeki eski `coaching-subscription-notice` tüketicisi bir sürüm daha durur.
- Kurs Yönetimi canlıda kurulu değil; billing bağlantısı yalnız kod düzeyinde hazır.

## Geri dönüş
- Yalnız billing sorunluysa: `billing-service` durdurulur. Ürünler son 24 saatin yanıtıyla çalışır; "abonelik iste"
  kapalıyken zaten herkes girer. Satın alma durur.
- Bütün yayın geri alınacaksa: `RELEASE_TAG=92624d7`, eski birim dosyası (üç Compose dosyası), servisler ters sırayla
  eski imajlara döner. Hızlı Okuma'nın yeni migration'ı yalnız ekleme yapar; eski kod onunla çalışır, geri alınması
  gerekmez. Billing veritabanı yerinde bırakılır. Yasal metinler bir önceki revizyona yönetici panelinden döndürülür.
- Veritabanı yedeğine dönmek yalnız veri bozulmasında ve kullanıcı onayıyla.

## Denenmemiş olanlar
- Yeni servis ile arayüzler şimdiye kadar birlikte çalıştırılmadı; ilk kez provada çalışacak.
- Halkbank döküm okuyucusu gerçek dosyayla kodda çalıştırılmadı; düzeni kullanıcının verdiği örnekten öğrenildi.
- Bu talimatın B bölümündeki rol ve veritabanı adımları için hazır betik yok; provada yazılıp buraya eklenmelidir.
