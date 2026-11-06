\set ON_ERROR_STOP on
-- Paket C #5 (06.11.2026): the shared KVKK aydınlatma metni names the data the coaching student's first-login wizard collects
-- (birth date, city and district, grade or graduate status, diploma grade, exam and target, invite and parent link, e-mail
-- choices), why and where each is used. Same mechanism as publish-test-legal-20261001.sql: the current version is kept as a
-- revision and the page version goes up by one. It refuses, changing nothing, unless the live text is still the 1 Ekim 2026
-- text (an edit made on the server since then must be merged by hand). The user has it legally reviewed before release.
-- Only the text changes: registration acceptance, consents and account deletion are untouched.
BEGIN;
LOCK TABLE identity."PlatformLegalPages" IN SHARE ROW EXCLUSIVE MODE;
LOCK TABLE identity."PlatformLegalPageRevisions" IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE kvkk_texts(slug text PRIMARY KEY, title text NOT NULL, content text NOT NULL);
INSERT INTO kvkk_texts VALUES
('kvkk-previous','KVKK aydınlatma metni — yetişkin test dönemi',$legal$1 Ekim 2026 — Aydınlatma metni; açık rıza beyanı değildir

Veri sorumlusu ONAL YAZILIM OTOMASYON EĞİTİM DANIŞMANLIK SAN. TİC. LTD. ŞTİ.; vergi dairesi YOZGAT, vergi no 6421076848, MERSİS 0642107684800001. Adres: Karatepe Mah. Şehit Bayram Karataş Cad. Bulvar 1071 Sitesi, Merkez/Yozgat. İletişim info@onalotomasyon.com; telefon 0505 707 20 05.

Kapsam: 18 yaş ve üzeri kullanıcılarla, gerçek öğrencilere ait olmayan örnek verilerle, ödeme alınmadan Eduİvme/Koçluk/Hızlı Okuma testi. Gerçek çocuk, öğrenci veya özel nitelikli kişisel veri girilmemelidir.

Hesap ve iletişim, profil/rol, kurum-öğretmen bağlantıları, katılımcının test kullanım ve sonuçları, güvenlik ve destek verileri işlenebilir. Kayıt ve kullanıcı formları, Google giriş seçildiğinde kimlik doğrulaması, davetler, hizmet içi işlemler, destek yazışmaları ve teknik kayıtlar aracılığıyla elektronik ortamda toplanır.

Hesap/test hizmetini sunmak ve gerekli test ilişkilerini yürütmek için sözleşmenin kurulması/ifasıyla doğrudan ilgili gerekli işleme KVKK m.5/2(c); hukuki yükümlülük kayıtlarında m.5/2(ç), hakların korunması ve uyuşmazlık işlemlerinde m.5/2(e) uygulanabilir. Güvenlik amaçlı gerekli ve ölçülü kayıtlar temel hakları zedelemeyen meşru menfaat kapsamında m.5/2(f) ile değerlendirilir. Amaçla ilgisiz veri toplama veya bütün verileri sınırsız işleme yetkisi yoktur. Pazarlama ve bülten izni ayrıca, isteğe bağlı olarak alınmalıdır.

Veriler gerekli kapsamda yetkilendirdiğiniz test öğretmen/kurum kullanıcıları, barındırma/e-posta/güvenlik/kimlik doğrulama tedarikçileri ve kanunen yetkili mercilerle paylaşılabilir. Hostinger VPS ve aynı sunucudaki yedekler işletmeci beyanıyla Litvanya'dadır; Hostinger SMTP ve Google giriş/reCAPTCHA kullanılır. Yurt dışı aktarım belgeleri ve tüm alt işleyen/işleme ülkeleri henüz tamamlanmış değildir. Bu aydınlatma eksik aktarım mekanizmasının yerine geçmez.

Veriler amaç ve yasal gereklilikle sınırlı saklanmalıdır. Kategori bazlı kesin saklama/yedek imha takvimi henüz belirlenmemiştir. Abonelik iptali hesap silme değildir. Hesap silme talebi ve zorunlu mali/uyuşmazlık kayıtları ayrı değerlendirilir; otomatik sınırsız saklama veya bütün kayıtların anında silinmesi taahhüt edilmez.

KVKK m.11 kapsamında işleme olup olmadığını öğrenme, bilgi isteme, amacı ve amaca uygunluğu öğrenme, yurt içi/yurt dışı alıcıları öğrenme, düzeltme, şartları oluştuğunda silme/yok etme ve alıcılara bildirme, yalnız otomatik analizle aleyhe sonuçlara itiraz ve hukuka aykırı işleme nedeniyle zararın giderilmesini isteme hakları saklıdır. Şirket adresine yazılı veya Veri Sorumlusuna Başvuru Usul ve Esasları Hakkında Tebliğ'deki geçerli yöntemlerle başvuru yapılabilir. Önceden şirkete bildirilmiş ve sistemde kayıtlı e-posta adresinden info@onalotomasyon.com adresine başvuru yapılabilir. Gerekli kimlik teyidi ölçülü yürütülür. Başvuru en kısa sürede, en geç 30 gün içinde sonuçlandırılır; ücret yalnız mevzuatın izin verdiği durumlarda uygulanır.$legal$),
('kvkk','KVKK aydınlatma metni — yetişkin test dönemi',$legal$6 Kasım 2026 — Aydınlatma metni; açık rıza beyanı değildir

Veri sorumlusu ONAL YAZILIM OTOMASYON EĞİTİM DANIŞMANLIK SAN. TİC. LTD. ŞTİ.; vergi dairesi YOZGAT, vergi no 6421076848, MERSİS 0642107684800001. Adres: Karatepe Mah. Şehit Bayram Karataş Cad. Bulvar 1071 Sitesi, Merkez/Yozgat. İletişim info@onalotomasyon.com; telefon 0505 707 20 05.

Kapsam: 18 yaş ve üzeri kullanıcılarla, gerçek öğrencilere ait olmayan örnek verilerle, ödeme alınmadan Eduİvme/Koçluk/Hızlı Okuma testi. Gerçek çocuk, öğrenci veya özel nitelikli kişisel veri girilmemelidir.

Hesap ve iletişim, profil/rol, kurum-öğretmen bağlantıları, katılımcının test kullanım ve sonuçları, güvenlik ve destek verileri işlenebilir. Kayıt ve kullanıcı formları, Google giriş seçildiğinde kimlik doğrulaması, davetler, hizmet içi işlemler, destek yazışmaları ve teknik kayıtlar aracılığıyla elektronik ortamda toplanır.

Koçluk öğrencisinin ilk girişteki kurulum adımlarında şu bilgiler toplanır; her biri yalnız yazılan amaçla kullanılır. Doğum tarihi: öğrenci profilinde tutulur; bağlı öğretmen ve kurum kullanıcıları öğrencinin profilinde görebilir ve düzeltebilir. Doğum tarihi artık kayıt formunda değil, bu kurulumda istenir; başka bir hesaplamada kullanılmaz. Yaşanan şehir ve ilçe: ortak il/ilçe listesinden seçilir; öğrencinin kendi okulunu listeden seçebilmesi amacıyla kullanılır; okul listelerinde öğrencinin ilçesindeki okulların önce gösterilmesi için de kullanılır ve öğrenci profilinde tutulur. Sınıf ya da mezun olma durumu ve isteğe bağlı olarak listeden seçilen okul: öğrenci profilinde tutulur ve bağlı öğretmenin öğrenci bilgilerinde görünür; sınıf, çalışma ayarlarına da aktarılır (çalışma planı kurulumu bu bilgiyle eşleşir) ve diploma notu sorusunun sorulup sorulmayacağını belirler. Sınıf, öğrencinin Profilim sayfasında ya da öğrencinin kurumu tarafından değiştirilirse çalışma ayarları da güncellenir; seçili sınav sınıfa uymuyorsa öğrenciden sınavını yeniden seçmesi istenir. Öğrencinin sınıfı ya da sınavı değiştiğinde diploma notu artık sorulmuyorsa (YKS dışındaki sınavlar ya da 11. sınıftan önceki sınıflar) kayıtlı diploma notu silinir. Hazırlandığı sınav, alan ya da puan türü, yabancı dil ve hedef sınav oturumu: çalışma planındaki derslerin, sınava kalan gün sayısının ve puan tahmininin belirlenmesi amacıyla öğrencinin çalışma ayarlarında tutulur. YKS diploma notu ve notun tahmini olup olmadığı: diploma notundan hesaplanan OBP'nin YKS puan tahminine ve hedef karşılaştırmasına eklenmesi amacıyla öğrenci profilinde tutulur; bu tahmini öğrenci, bağlı veli ve öğretmen görür. Öğrenci bu adımı atlayabilir. Hedef okul, bölüm ya da hedef puan: ana sayfadaki hedef kartında ve Gelişimim bölümünde hedefe kalan farkın gösterilmesi amacıyla öğrencinin ana hedefinde tutulur; bağlı veli ve öğretmen de görebilir. Bu adım atlanabilir. Öğrenci sınavını ya da alanını değiştirirse kendi hedefinden yeni sınava uymayan okul, bölüm, puan ve net hedefleri kaldırılır; öğretmenin belirlediği hedef değiştirilmez, yalnız sınavla eşleşmediği gösterilir. Öğretmen ya da kurum davetinin kabulü: öğrencinin daveti gönderen öğretmen ya da kurumla bağlanması amacıyla işlenir; isteğe bağlıdır. Veli bağlantı kodu: yalnız öğrenci oluşturursa, velinin öğrencinin onayıyla çalışmayı görebilmesi amacıyla işlenir; isteğe bağlıdır ve bağlantı istendiğinde kaldırılabilir. Bildirim ve hatırlatma e-posta tercihleri: hangi e-postaların gönderileceğinin belirlenmesi amacıyla bildirim ayarlarında tutulur; varsayılanlar mevcut ayarlardır. Bu bilgiler hizmetin sunulması için sözleşmenin ifasıyla doğrudan ilgili olarak işlenir (KVKK m.5/2(c)). Öğrenci doğum tarihini, şehir ve ilçesini, sınıfını, okulunu ve diploma notunu Profil ve ayarlar › Profilim sayfasından; sınavını, alanını ve oturumunu Profil ve ayarlar › Çalışma tercihlerim sayfasından; hedefini Gelişimim › Hedefim sayfasından; e-posta tercihlerini Profil ve ayarlar › Bildirimler sayfasından değiştirebilir. Hesap silme talebinde bu bilgiler de diğer koçluk verileriyle birlikte değerlendirilir.

Hesap/test hizmetini sunmak ve gerekli test ilişkilerini yürütmek için sözleşmenin kurulması/ifasıyla doğrudan ilgili gerekli işleme KVKK m.5/2(c); hukuki yükümlülük kayıtlarında m.5/2(ç), hakların korunması ve uyuşmazlık işlemlerinde m.5/2(e) uygulanabilir. Güvenlik amaçlı gerekli ve ölçülü kayıtlar temel hakları zedelemeyen meşru menfaat kapsamında m.5/2(f) ile değerlendirilir. Amaçla ilgisiz veri toplama veya bütün verileri sınırsız işleme yetkisi yoktur. Pazarlama ve bülten izni ayrıca, isteğe bağlı olarak alınmalıdır.

Veriler gerekli kapsamda yetkilendirdiğiniz test öğretmen/kurum kullanıcıları, barındırma/e-posta/güvenlik/kimlik doğrulama tedarikçileri ve kanunen yetkili mercilerle paylaşılabilir. Hostinger VPS ve aynı sunucudaki yedekler işletmeci beyanıyla Litvanya'dadır; Hostinger SMTP ve Google giriş/reCAPTCHA kullanılır. Yurt dışı aktarım belgeleri ve tüm alt işleyen/işleme ülkeleri henüz tamamlanmış değildir. Bu aydınlatma eksik aktarım mekanizmasının yerine geçmez.

Veriler amaç ve yasal gereklilikle sınırlı saklanmalıdır. Kategori bazlı kesin saklama/yedek imha takvimi henüz belirlenmemiştir. Abonelik iptali hesap silme değildir. Hesap silme talebi ve zorunlu mali/uyuşmazlık kayıtları ayrı değerlendirilir; otomatik sınırsız saklama veya bütün kayıtların anında silinmesi taahhüt edilmez.

KVKK m.11 kapsamında işleme olup olmadığını öğrenme, bilgi isteme, amacı ve amaca uygunluğu öğrenme, yurt içi/yurt dışı alıcıları öğrenme, düzeltme, şartları oluştuğunda silme/yok etme ve alıcılara bildirme, yalnız otomatik analizle aleyhe sonuçlara itiraz ve hukuka aykırı işleme nedeniyle zararın giderilmesini isteme hakları saklıdır. Şirket adresine yazılı veya Veri Sorumlusuna Başvuru Usul ve Esasları Hakkında Tebliğ'deki geçerli yöntemlerle başvuru yapılabilir. Önceden şirkete bildirilmiş ve sistemde kayıtlı e-posta adresinden info@onalotomasyon.com adresine başvuru yapılabilir. Gerekli kimlik teyidi ölçülü yürütülür. Başvuru en kısa sürede, en geç 30 gün içinde sonuçlandırılır; ücret yalnız mevzuatın izin verdiği durumlarda uygulanır.$legal$);

DO $$ BEGIN
 IF (SELECT count(*) FROM identity."users" WHERE "Email"='admin@eduivme.com') <> 1 THEN
   RAISE EXCEPTION 'Expected existing admin actor not found';
 END IF;
 IF NOT EXISTS (SELECT 1 FROM identity."PlatformLegalPages" p WHERE p."Slug"='kvkk' AND p."ArchivedAt" IS NULL
     AND p."Content" IN (SELECT content FROM kvkk_texts)) THEN
   RAISE EXCEPTION 'kvkk text differs from the 1 Ekim 2026 version; merge the server edit by hand. Nothing changed.';
 END IF;
END $$;

INSERT INTO identity."PlatformLegalPageRevisions" ("Id","PageId","Slug","Title","Content","IsPublished","IsArchived","Version","CreatedBy","CreatedAt")
SELECT gen_random_uuid(), p."Id",p."Slug",p."Title",p."Content",p."IsPublished",false,p."Version",coalesce(p."UpdatedBy",p."CreatedBy"),coalesce(p."UpdatedAt",p."CreatedAt")
FROM identity."PlatformLegalPages" p JOIN kvkk_texts c ON c.slug=p."Slug"
WHERE p."Slug"='kvkk' AND (p."Content" IS DISTINCT FROM c.content OR p."Title" IS DISTINCT FROM c.title OR NOT p."IsPublished");

INSERT INTO identity."PlatformLegalPages" ("Id","Slug","Title","Content","IsPublished","Version","CreatedBy","CreatedAt")
SELECT gen_random_uuid(),c.slug,c.title,c.content,true,1,u."Id",now()
FROM kvkk_texts c CROSS JOIN identity."users" u WHERE c.slug='kvkk' AND u."Email"='admin@eduivme.com'
ON CONFLICT ("Slug") DO UPDATE SET "Title"=excluded."Title", "Content"=excluded."Content", "IsPublished"=true,
 "Version"=identity."PlatformLegalPages"."Version"+1,"UpdatedBy"=excluded."CreatedBy","UpdatedAt"=now()
WHERE identity."PlatformLegalPages"."Content" IS DISTINCT FROM excluded."Content" OR identity."PlatformLegalPages"."Title" IS DISTINCT FROM excluded."Title" OR NOT identity."PlatformLegalPages"."IsPublished";
SELECT "Slug","Version","IsPublished" FROM identity."PlatformLegalPages" WHERE "Slug"='kvkk';
COMMIT;
