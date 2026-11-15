\set ON_ERROR_STOP on
-- Billing service ("Abonelik ve Ödeme"), stage 10: what the texts must say about the new payment flow. User decision of 15.11.2026:
-- written here first, the lawyer checks and corrects.
--   kvkk     the bank statement uploaded to match transfers is named: date, amount, description and sender name of a movement;
--            a payer who is not the subscriber (a parent, say) is named as a data subject whose name comes from the bank, not
--            from the person; legal basis m.5/2(c), (ç) and, for a payer who is not the subscriber, (f); the sender name and
--            description of a movement matched to an order are cleared with the payment records after the 10 years.
--   privacy  the same statement sentence, short.
--            a movement tied to no order loses its sender name and description one year after the movement (user decision
--            15.11.2026; RetentionRules.UnmatchedBankLineYears).
-- Everything else stays word for word.
-- Run AFTER publish-legal-fixes-20261114.sql, on the identity database:
--   psql -v ON_ERROR_STOP=1 -v publish_day='<gün Ay yıl>' -f publish-billing-legal-20261115.sql
-- Safe to run twice. A page that differs from the 14.11.2026 text (edited on the server) stops the script; nothing changes.
\if :{?publish_day}
\else
\set publish_day 'GG Ay YYYY'
\endif
BEGIN;
CREATE TEMP TABLE legal_billing(slug text PRIMARY KEY, title text NOT NULL, content text NOT NULL, previous_md5 text NOT NULL) ON COMMIT DROP;
INSERT INTO legal_billing VALUES
('privacy','Gizlilik politikası',replace($legal$__YAYIN_GÜNÜ__ — Gizlilik politikası

İşletmeci: ONAL YAZILIM OTOMASYON EĞİTİM DANIŞMANLIK SAN. TİC. LTD. ŞTİ. Vergi dairesi YOZGAT; vergi no 6421076848; MERSİS 0642107684800001. Adres: Karatepe Mah. Şehit Bayram Karataş Cad. Bulvar 1071 Sitesi, Merkez/Yozgat. İletişim: info@onalotomasyon.com, 0505 707 20 05.

Bu politika Eduİvme, Koçluk ve Hızlı Okuma hizmetlerinin öğrenci, veli, öğretmen ve kurum kullanıcıları içindir. Sağlık verisi veya hizmetle ilgisi olmayan üçüncü kişilerin özel bilgileri girilmemelidir.

Ad-soyad, e-posta, profil/rol, davet ve hesap bilgileri; kullanım, güvenlik, destek kayıtları ile kullanıcının ödev, hedef, plan, seans, deneme ve egzersiz/okuma sonuçları hizmeti sunmak, yetkileri yönetmek ve güvenliği sağlamak için işlenir. Ücretli abonelikte plan, havale/EFT bildirimi, onaylanan sözleşme metinleri ve fatura kayıtları aboneliği ve ödemeyi yürütmek için işlenir; kart bilgisi alınmaz. Havale/EFT ödemelerini siparişle eşleştirmek için şirketin banka hesap dökümündeki hareketin tarihi, tutarı, açıklaması ve gönderenin adı da işlenir; ödemeyi abone dışında biri yaptıysa onun adı yalnız bu amaçla işlenir. Kimlik doğrulama ortak Identity altyapısında, ürün profilleri ve sonuçları ilgili ürün veri alanlarında yönetilir. Öğretmen, kurum ya da veli bağlantısı kurduğunuzda ilgili yetkili kullanıcılar rol, atama ve bağlantı kapsamındaki bilgileri görebilir.

Google ile giriş ve etkin güvenlik formlarında Google reCAPTCHA kullanılır. Koçluk tanıtım, giriş, kayıt ve yasal metin sayfalarında, çerez bandında "Kabul et" seçilirse ziyaret ölçümü için Google Analytics kullanılır. İşletmeci beyanına göre Hostinger VPS ve aynı sunucudaki yedekler Litvanya'dadır. Bildirimler Hostinger SMTP üzerinden gönderilir; sistem göndericisi destek@eduivme.com olabilir, destek ve KVKK başvuru adresi info@onalotomasyon.com'dur. Bu hizmetler nedeniyle veriler yurt dışında işlenir.

Aboneliğin bitmesi hesabın silinmesi değildir. Açık hesapta kullanım geçmişi hizmet amacıyla kalabilir; süresiz saklama taahhüdü verilmez. Silme talepleri gereklilik ve yasal yükümlülükler bakımından ayrı değerlendirilir; mevcut teknik silme süreçlerinde özellikle mali kayıtlar nedeniyle sınırlamalar olabilir. Hesap silindiğinde abonelik, ödeme bildirimi, onaylanan sözleşme ve fatura kayıtları 10 yıl daha saklanır; süre dolunca abonelik ve ödeme kayıtlarındaki ad, e-posta ve notlar silinir, tutar, tarih ve ödeme referansı muhasebe kaydı olarak kalır. Otomatik, anında ve tüm yedekleri kapsayan silme garantisi verilmez. Talepler info@onalotomasyon.com üzerinden iletilebilir.

Bülten/pazarlama izni hesabın şartı değildir. KVKK hakları ve başvuru usulü ortak KVKK aydınlatmasında; çerez/depolama bilgileri Çerez Politikası'nda açıklanır. Yeni amaç ve esaslı değişiklikler ayrıca duyurulur.$legal$, '__YAYIN_GÜNÜ__', :'publish_day'),'c901892fd5ea264d835f1797b5e96cf4'),
('kvkk','KVKK aydınlatma metni',replace($legal$__YAYIN_GÜNÜ__ — Aydınlatma metni; açık rıza beyanı değildir

Veri sorumlusu ONAL YAZILIM OTOMASYON EĞİTİM DANIŞMANLIK SAN. TİC. LTD. ŞTİ.; vergi dairesi YOZGAT, vergi no 6421076848, MERSİS 0642107684800001. Adres: Karatepe Mah. Şehit Bayram Karataş Cad. Bulvar 1071 Sitesi, Merkez/Yozgat. İletişim info@onalotomasyon.com; telefon 0505 707 20 05.

Kapsam: Eduİvme, Koçluk ve Hızlı Okuma hizmetlerinin öğrenci, veli, öğretmen ve kurum kullanıcıları. Sağlık verisi gibi özel nitelikli kişisel veriler ve hizmetle ilgisi olmayan üçüncü kişilere ait bilgiler girilmemelidir.

Hesap ve iletişim, profil/rol, kurum, öğretmen ve veli bağlantıları, kullanıcının hizmet kullanımı ve sonuçları (hedef, ödev, seans, deneme, plan, egzersiz ve okuma sonuçları), güvenlik ve destek verileri işlenebilir. Ücretli abonelikte plan, havale/EFT bildirimi (ödeme referansı, gönderen adı ve yüklenirse dekont), onaylanan ön bilgilendirme formu ile mesafeli satış sözleşmesi, cayma talebi ve fatura kayıtları da işlenir. Havale/EFT ödemelerinin siparişle eşleştirilmesi için şirketin banka hesap dökümü sisteme yüklenir; dökümdeki hareketin tarihi, tutarı, açıklaması ve gönderenin adı işlenir. Ödemeyi abone dışında biri (örneğin velisi) yaptıysa onun adı da yalnız bu amaçla işlenir; bu bilgi kişinin kendisinden değil, bankanın şirkete verdiği hesap dökümünden elde edilir. Kayıt, kullanıcı ve ödeme formları, Google giriş seçildiğinde kimlik doğrulaması, davetler, hizmet içi işlemler, destek yazışmaları ve teknik kayıtlar aracılığıyla elektronik ortamda toplanır.

Koçluk öğrencisinin ilk girişteki kurulum adımlarında şu bilgiler toplanır; her biri yalnız yazılan amaçla kullanılır. Doğum tarihi: öğrenci profilinde tutulur; bağlı öğretmen ve kurum kullanıcıları öğrencinin profilinde görebilir ve düzeltebilir. Doğum tarihi kayıt formunda değil, bu kurulumda istenir; başka bir hesaplamada kullanılmaz. Yaşanan şehir ve ilçe: ortak il/ilçe listesinden seçilir; öğrencinin kendi okulunu listeden seçebilmesi amacıyla kullanılır; okul listelerinde öğrencinin ilçesindeki okulların önce gösterilmesi için de kullanılır ve öğrenci profilinde tutulur. Eğitim durumu (ortaokul ya da lise öğrencisi, lise mezunu, üniversite öğrencisi ya da üniversite mezunu) ve öğrenciyse sınıfı; lise öğrencisiyse isteğe bağlı olarak listeden seçilen okulu: öğrenci profilinde tutulur ve bağlı öğretmenin öğrenci bilgilerinde görünür; eğitim durumu ve sınıf, çalışma ayarlarına da aktarılır (çalışma planı kurulumu bu bilgiyle eşleşir), hazırlanabileceği sınavları ve diploma notu sorusunun sorulup sorulmayacağını belirler; öğrenci olmayan bir eğitim durumu seçildiğinde kayıtlı sınıf, okul ve şube silinir. Eğitim durumu ya da sınıf, öğrencinin Profilim sayfasında ya da öğrencinin kurumu tarafından değiştirilirse çalışma ayarları da güncellenir; seçili sınav eğitim durumuna ya da sınıfa uymuyorsa öğrenciden sınavını yeniden seçmesi istenir. Öğrencinin eğitim durumu, sınıfı ya da sınavı değiştiğinde diploma notu artık sorulmuyorsa (YKS dışındaki sınavlar ya da 11. sınıftan önceki sınıflar; diploma notu YKS'de 11. ve 12. sınıf öğrencileri ile liseden mezun olanlar için sorulur) kayıtlı diploma notu silinir. Hazırlandığı sınav, alan ya da puan türü, yabancı dil ve hedef sınav oturumu: çalışma planındaki derslerin, sınava kalan gün sayısının ve puan tahmininin belirlenmesi amacıyla öğrencinin çalışma ayarlarında tutulur. YKS diploma notu ve notun tahmini olup olmadığı: diploma notundan hesaplanan OBP'nin YKS puan tahminine ve hedef karşılaştırmasına eklenmesi amacıyla öğrenci profilinde tutulur; bu tahmini öğrenci, bağlı veli ve öğretmen görür. Öğrenci bu adımı atlayabilir. Hedef okul, bölüm ya da hedef puan: ana sayfadaki hedef kartında ve Gelişimim bölümünde hedefe kalan farkın gösterilmesi amacıyla öğrencinin ana hedefinde tutulur; bağlı veli ve öğretmen de görebilir. Bu adım atlanabilir. Öğrenci sınavını ya da alanını değiştirirse kendi hedefinden yeni sınava uymayan okul, bölüm, puan ve net hedefleri kaldırılır; öğretmenin belirlediği hedef değiştirilmez, yalnız sınavla eşleşmediği gösterilir. Öğretmen ya da kurum davetinin kabulü: öğrencinin daveti gönderen öğretmen ya da kurumla bağlanması amacıyla işlenir; isteğe bağlıdır. Veli bağlantı kodu: yalnız öğrenci oluşturursa, velinin öğrencinin onayıyla çalışmayı görebilmesi amacıyla işlenir; isteğe bağlıdır ve bağlantı istendiğinde kaldırılabilir. Bildirim ve hatırlatma e-posta tercihleri: hangi e-postaların gönderileceğinin belirlenmesi amacıyla bildirim ayarlarında tutulur; varsayılanlar mevcut ayarlardır. Bu bilgiler hizmetin sunulması için sözleşmenin ifasıyla doğrudan ilgili olarak işlenir (KVKK m.5/2(c)). Öğrenci doğum tarihini, şehir ve ilçesini, eğitim durumunu, sınıfını, okulunu ve diploma notunu Profil ve ayarlar › Profilim sayfasından; sınavını, alanını ve oturumunu Profil ve ayarlar › Çalışma tercihlerim sayfasından; hedefini Gelişimim › Hedefim sayfasından; e-posta tercihlerini Profil ve ayarlar › Bildirimler sayfasından değiştirebilir. Hesap silme talebinde bu bilgiler de diğer koçluk verileriyle birlikte değerlendirilir.

Hesabı ve hizmeti sunmak, aboneliği ve ödemeyi yürütmek, öğretmen, kurum ve veli ilişkilerini işletmek için sözleşmenin kurulması/ifasıyla doğrudan ilgili gerekli işleme KVKK m.5/2(c); fatura ve mali kayıtlar gibi hukuki yükümlülük kayıtlarında m.5/2(ç), hakların korunması ve uyuşmazlık işlemlerinde m.5/2(e) uygulanabilir. Banka dökümündeki gönderen adı, ödemenin doğru siparişle eşleştirilmesi için m.5/2(c), mali kayıt yükümlülüğü için m.5/2(ç) kapsamında; ödemeyi abone dışında biri yaptıysa ödemenin doğru kişiye işlenmesindeki meşru menfaat kapsamında m.5/2(f) ile işlenir. Güvenlik amaçlı gerekli ve ölçülü kayıtlar temel hakları zedelemeyen meşru menfaat kapsamında m.5/2(f) ile değerlendirilir. Amaçla ilgisiz veri toplama veya bütün verileri sınırsız işleme yetkisi yoktur. Pazarlama ve bülten izni ayrıca ve isteğe bağlı olarak alınır.

Veriler gerekli kapsamda bağlandığınız öğretmen, kurum ve veli kullanıcıları, barındırma/e-posta/güvenlik/kimlik doğrulama tedarikçileri ve kanunen yetkili mercilerle paylaşılabilir. Hostinger VPS ve aynı sunucudaki yedekler işletmeci beyanıyla Litvanya'dadır; Hostinger SMTP ve Google giriş/reCAPTCHA kullanılır. Koçluk tanıtım, giriş, kayıt ve yasal metin sayfalarında, çerez bandında "Kabul et" seçilirse ziyaret ölçümü için Google Analytics kullanılır. Bu hizmetler nedeniyle veriler yurt dışında işlenir.

Veriler amaç ve yasal gereklilikle sınırlı olarak saklanır. Abonelik iptali hesap silme değildir. Hesap silindiğinde abonelik, ödeme bildirimi, onaylanan sözleşme ve fatura kayıtları 10 yıl daha saklanır; süre dolunca abonelik ve ödeme kayıtlarındaki ad, e-posta ve notlar silinir, tutar, tarih ve ödeme referansı muhasebe kaydı olarak kalır; siparişle eşleşen banka hareketindeki gönderen adı ve açıklama da aynı sürenin sonunda silinir. Hiçbir siparişle eşleşmeyen banka hareketlerindeki gönderen adı ve açıklama, hareket tarihinden 1 yıl sonra silinir. Hesap silme talebi ve zorunlu mali/uyuşmazlık kayıtları ayrı değerlendirilir; otomatik sınırsız saklama veya bütün kayıtların anında silinmesi taahhüt edilmez.

KVKK m.11 kapsamında işleme olup olmadığını öğrenme, bilgi isteme, amacı ve amaca uygunluğu öğrenme, yurt içi/yurt dışı alıcıları öğrenme, düzeltme, şartları oluştuğunda silme/yok etme ve alıcılara bildirme, yalnız otomatik analizle aleyhe sonuçlara itiraz ve hukuka aykırı işleme nedeniyle zararın giderilmesini isteme hakları saklıdır. Şirket adresine yazılı veya Veri Sorumlusuna Başvuru Usul ve Esasları Hakkında Tebliğ'deki geçerli yöntemlerle başvuru yapılabilir. Önceden şirkete bildirilmiş ve sistemde kayıtlı e-posta adresinden info@onalotomasyon.com adresine başvuru yapılabilir. Gerekli kimlik teyidi ölçülü yürütülür. Başvuru en kısa sürede, en geç 30 gün içinde sonuçlandırılır; ücret yalnız mevzuatın izin verdiği durumlarda uygulanır.$legal$, '__YAYIN_GÜNÜ__', :'publish_day'),'58104e0a9b47f720abbfe2fb80199684');

DO $$
DECLARE problems text;
BEGIN
 IF EXISTS (SELECT 1 FROM legal_billing WHERE content !~ '^[0-9]{1,2} (Ocak|Şubat|Mart|Nisan|Mayıs|Haziran|Temmuz|Ağustos|Eylül|Ekim|Kasım|Aralık) 20[0-9]{2} — ') THEN
   RAISE EXCEPTION 'Set publish_day to the publish day (psql -v publish_day=''16 Kasım 2026''). Nothing changed.';
 END IF;
 IF (SELECT count(*) FROM identity."users" WHERE "Email"='admin@eduivme.com') <> 1 THEN
   RAISE EXCEPTION 'Expected existing admin actor not found';
 END IF;
 -- Below the date line, each page must be the text of publish-legal-fixes-20261114.sql or already the new one (a rerun).
 SELECT string_agg(c.slug, ', ' ORDER BY c.slug) INTO problems FROM legal_billing c
 WHERE NOT EXISTS (SELECT 1 FROM identity."PlatformLegalPages" p WHERE p."Slug"=c.slug AND p."ArchivedAt" IS NULL AND p."IsPublished"
     AND (md5(substr(p."Content", strpos(p."Content", E'\n')+1))=c.previous_md5
       OR substr(p."Content", strpos(p."Content", E'\n')+1)=substr(c.content, strpos(c.content, E'\n')+1)));
 IF problems IS NOT NULL THEN
   RAISE EXCEPTION 'These pages differ from the published texts (missing, archived, unpublished or edited on the server): %. Merge the edit by hand. Nothing changed.', problems;
 END IF;
END $$;

INSERT INTO identity."PlatformLegalPageRevisions" ("Id","PageId","Slug","Title","Content","IsPublished","IsArchived","Version","CreatedBy","CreatedAt")
SELECT gen_random_uuid(), p."Id",p."Slug",p."Title",p."Content",p."IsPublished",false,p."Version",coalesce(p."UpdatedBy",p."CreatedBy"),coalesce(p."UpdatedAt",p."CreatedAt")
FROM identity."PlatformLegalPages" p JOIN legal_billing c ON c.slug=p."Slug"
WHERE md5(substr(p."Content", strpos(p."Content", E'\n')+1))=c.previous_md5;

UPDATE identity."PlatformLegalPages" p SET "Title"=c.title, "Content"=c.content, "Version"=p."Version"+1,
 "UpdatedBy"=(SELECT "Id" FROM identity."users" WHERE "Email"='admin@eduivme.com'), "UpdatedAt"=now()
FROM legal_billing c
WHERE c.slug=p."Slug" AND md5(substr(p."Content", strpos(p."Content", E'\n')+1))=c.previous_md5;

SELECT "Slug","Version","IsPublished","Title",left("Content",30) AS first_line,length("Content") AS chars FROM identity."PlatformLegalPages"
WHERE "Slug" IN (SELECT slug FROM legal_billing) ORDER BY "Slug";
COMMIT;
