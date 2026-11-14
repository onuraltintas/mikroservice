\set ON_ERROR_STOP on
-- Legal check of 14.11.2026 (claude/legal-check-2026-11-14.md), the findings the texts can settle; user decisions of the same day.
--   kvkk     Y8  draft wording becomes statement ("saklanır", "alınır"; no "artık"); the 10-year financial retention is said.
--            Y5  Google Analytics is named among the services used after cookie consent.
--   privacy  Y5  the same Google Analytics sentence; the 10-year financial retention.
--   cookies  Y6  vocab_progress (not in the code) is dropped; the Kurs Yönetimi refresh cookie, four localStorage keys and the
--                sessionStorage campaign key are listed.   Y7  the measured pages are named as they are.
--   speed-reading-terms  Y1  the purchase now shows the pre-information form and the distance-sales contract and takes the
--                buyer's acceptance (release together with the Hızlı Okuma sales flow, PR #152).
-- Everything else stays word for word. The retention sentence is true only with FinancialRetentionYears = 10 in both services.
-- Run on the identity database:  psql -v ON_ERROR_STOP=1 -v publish_day='<gün Ay yıl>' -f publish-legal-fixes-20261114.sql
-- Safe to run twice. A page that differs from the 14.11.2026 text (edited on the server) stops the script; nothing changes.
\if :{?publish_day}
\else
\set publish_day 'GG Ay YYYY'
\endif
BEGIN;
CREATE TEMP TABLE legal_fix(slug text PRIMARY KEY, title text NOT NULL, content text NOT NULL, previous_md5 text NOT NULL) ON COMMIT DROP;
INSERT INTO legal_fix VALUES
('privacy','Gizlilik politikası',replace($legal$__YAYIN_GÜNÜ__ — Gizlilik politikası

İşletmeci: ONAL YAZILIM OTOMASYON EĞİTİM DANIŞMANLIK SAN. TİC. LTD. ŞTİ. Vergi dairesi YOZGAT; vergi no 6421076848; MERSİS 0642107684800001. Adres: Karatepe Mah. Şehit Bayram Karataş Cad. Bulvar 1071 Sitesi, Merkez/Yozgat. İletişim: info@onalotomasyon.com, 0505 707 20 05.

Bu politika Eduİvme, Koçluk ve Hızlı Okuma hizmetlerinin öğrenci, veli, öğretmen ve kurum kullanıcıları içindir. Sağlık verisi veya hizmetle ilgisi olmayan üçüncü kişilerin özel bilgileri girilmemelidir.

Ad-soyad, e-posta, profil/rol, davet ve hesap bilgileri; kullanım, güvenlik, destek kayıtları ile kullanıcının ödev, hedef, plan, seans, deneme ve egzersiz/okuma sonuçları hizmeti sunmak, yetkileri yönetmek ve güvenliği sağlamak için işlenir. Ücretli abonelikte plan, havale/EFT bildirimi, onaylanan sözleşme metinleri ve fatura kayıtları aboneliği ve ödemeyi yürütmek için işlenir; kart bilgisi alınmaz. Kimlik doğrulama ortak Identity altyapısında, ürün profilleri ve sonuçları ilgili ürün veri alanlarında yönetilir. Öğretmen, kurum ya da veli bağlantısı kurduğunuzda ilgili yetkili kullanıcılar rol, atama ve bağlantı kapsamındaki bilgileri görebilir.

Google ile giriş ve etkin güvenlik formlarında Google reCAPTCHA kullanılır. Koçluk tanıtım, giriş, kayıt ve yasal metin sayfalarında, çerez bandında "Kabul et" seçilirse ziyaret ölçümü için Google Analytics kullanılır. İşletmeci beyanına göre Hostinger VPS ve aynı sunucudaki yedekler Litvanya'dadır. Bildirimler Hostinger SMTP üzerinden gönderilir; sistem göndericisi destek@eduivme.com olabilir, destek ve KVKK başvuru adresi info@onalotomasyon.com'dur. Bu hizmetler nedeniyle veriler yurt dışında işlenir.

Aboneliğin bitmesi hesabın silinmesi değildir. Açık hesapta kullanım geçmişi hizmet amacıyla kalabilir; süresiz saklama taahhüdü verilmez. Silme talepleri gereklilik ve yasal yükümlülükler bakımından ayrı değerlendirilir; mevcut teknik silme süreçlerinde özellikle mali kayıtlar nedeniyle sınırlamalar olabilir. Hesap silindiğinde abonelik, ödeme bildirimi, onaylanan sözleşme ve fatura kayıtları 10 yıl daha saklanır; süre dolunca abonelik ve ödeme kayıtlarındaki ad, e-posta ve notlar silinir, tutar, tarih ve ödeme referansı muhasebe kaydı olarak kalır. Otomatik, anında ve tüm yedekleri kapsayan silme garantisi verilmez. Talepler info@onalotomasyon.com üzerinden iletilebilir.

Bülten/pazarlama izni hesabın şartı değildir. KVKK hakları ve başvuru usulü ortak KVKK aydınlatmasında; çerez/depolama bilgileri Çerez Politikası'nda açıklanır. Yeni amaç ve esaslı değişiklikler ayrıca duyurulur.$legal$, '__YAYIN_GÜNÜ__', :'publish_day'),'e4d0345f669e7177164cadd2d0ce0609'),
('kvkk','KVKK aydınlatma metni',replace($legal$__YAYIN_GÜNÜ__ — Aydınlatma metni; açık rıza beyanı değildir

Veri sorumlusu ONAL YAZILIM OTOMASYON EĞİTİM DANIŞMANLIK SAN. TİC. LTD. ŞTİ.; vergi dairesi YOZGAT, vergi no 6421076848, MERSİS 0642107684800001. Adres: Karatepe Mah. Şehit Bayram Karataş Cad. Bulvar 1071 Sitesi, Merkez/Yozgat. İletişim info@onalotomasyon.com; telefon 0505 707 20 05.

Kapsam: Eduİvme, Koçluk ve Hızlı Okuma hizmetlerinin öğrenci, veli, öğretmen ve kurum kullanıcıları. Sağlık verisi gibi özel nitelikli kişisel veriler ve hizmetle ilgisi olmayan üçüncü kişilere ait bilgiler girilmemelidir.

Hesap ve iletişim, profil/rol, kurum, öğretmen ve veli bağlantıları, kullanıcının hizmet kullanımı ve sonuçları (hedef, ödev, seans, deneme, plan, egzersiz ve okuma sonuçları), güvenlik ve destek verileri işlenebilir. Ücretli abonelikte plan, havale/EFT bildirimi (ödeme referansı, gönderen adı ve yüklenirse dekont), onaylanan ön bilgilendirme formu ile mesafeli satış sözleşmesi, cayma talebi ve fatura kayıtları da işlenir. Kayıt, kullanıcı ve ödeme formları, Google giriş seçildiğinde kimlik doğrulaması, davetler, hizmet içi işlemler, destek yazışmaları ve teknik kayıtlar aracılığıyla elektronik ortamda toplanır.

Koçluk öğrencisinin ilk girişteki kurulum adımlarında şu bilgiler toplanır; her biri yalnız yazılan amaçla kullanılır. Doğum tarihi: öğrenci profilinde tutulur; bağlı öğretmen ve kurum kullanıcıları öğrencinin profilinde görebilir ve düzeltebilir. Doğum tarihi kayıt formunda değil, bu kurulumda istenir; başka bir hesaplamada kullanılmaz. Yaşanan şehir ve ilçe: ortak il/ilçe listesinden seçilir; öğrencinin kendi okulunu listeden seçebilmesi amacıyla kullanılır; okul listelerinde öğrencinin ilçesindeki okulların önce gösterilmesi için de kullanılır ve öğrenci profilinde tutulur. Eğitim durumu (ortaokul ya da lise öğrencisi, lise mezunu, üniversite öğrencisi ya da üniversite mezunu) ve öğrenciyse sınıfı; lise öğrencisiyse isteğe bağlı olarak listeden seçilen okulu: öğrenci profilinde tutulur ve bağlı öğretmenin öğrenci bilgilerinde görünür; eğitim durumu ve sınıf, çalışma ayarlarına da aktarılır (çalışma planı kurulumu bu bilgiyle eşleşir), hazırlanabileceği sınavları ve diploma notu sorusunun sorulup sorulmayacağını belirler; öğrenci olmayan bir eğitim durumu seçildiğinde kayıtlı sınıf, okul ve şube silinir. Eğitim durumu ya da sınıf, öğrencinin Profilim sayfasında ya da öğrencinin kurumu tarafından değiştirilirse çalışma ayarları da güncellenir; seçili sınav eğitim durumuna ya da sınıfa uymuyorsa öğrenciden sınavını yeniden seçmesi istenir. Öğrencinin eğitim durumu, sınıfı ya da sınavı değiştiğinde diploma notu artık sorulmuyorsa (YKS dışındaki sınavlar ya da 11. sınıftan önceki sınıflar; diploma notu YKS'de 11. ve 12. sınıf öğrencileri ile liseden mezun olanlar için sorulur) kayıtlı diploma notu silinir. Hazırlandığı sınav, alan ya da puan türü, yabancı dil ve hedef sınav oturumu: çalışma planındaki derslerin, sınava kalan gün sayısının ve puan tahmininin belirlenmesi amacıyla öğrencinin çalışma ayarlarında tutulur. YKS diploma notu ve notun tahmini olup olmadığı: diploma notundan hesaplanan OBP'nin YKS puan tahminine ve hedef karşılaştırmasına eklenmesi amacıyla öğrenci profilinde tutulur; bu tahmini öğrenci, bağlı veli ve öğretmen görür. Öğrenci bu adımı atlayabilir. Hedef okul, bölüm ya da hedef puan: ana sayfadaki hedef kartında ve Gelişimim bölümünde hedefe kalan farkın gösterilmesi amacıyla öğrencinin ana hedefinde tutulur; bağlı veli ve öğretmen de görebilir. Bu adım atlanabilir. Öğrenci sınavını ya da alanını değiştirirse kendi hedefinden yeni sınava uymayan okul, bölüm, puan ve net hedefleri kaldırılır; öğretmenin belirlediği hedef değiştirilmez, yalnız sınavla eşleşmediği gösterilir. Öğretmen ya da kurum davetinin kabulü: öğrencinin daveti gönderen öğretmen ya da kurumla bağlanması amacıyla işlenir; isteğe bağlıdır. Veli bağlantı kodu: yalnız öğrenci oluşturursa, velinin öğrencinin onayıyla çalışmayı görebilmesi amacıyla işlenir; isteğe bağlıdır ve bağlantı istendiğinde kaldırılabilir. Bildirim ve hatırlatma e-posta tercihleri: hangi e-postaların gönderileceğinin belirlenmesi amacıyla bildirim ayarlarında tutulur; varsayılanlar mevcut ayarlardır. Bu bilgiler hizmetin sunulması için sözleşmenin ifasıyla doğrudan ilgili olarak işlenir (KVKK m.5/2(c)). Öğrenci doğum tarihini, şehir ve ilçesini, eğitim durumunu, sınıfını, okulunu ve diploma notunu Profil ve ayarlar › Profilim sayfasından; sınavını, alanını ve oturumunu Profil ve ayarlar › Çalışma tercihlerim sayfasından; hedefini Gelişimim › Hedefim sayfasından; e-posta tercihlerini Profil ve ayarlar › Bildirimler sayfasından değiştirebilir. Hesap silme talebinde bu bilgiler de diğer koçluk verileriyle birlikte değerlendirilir.

Hesabı ve hizmeti sunmak, aboneliği ve ödemeyi yürütmek, öğretmen, kurum ve veli ilişkilerini işletmek için sözleşmenin kurulması/ifasıyla doğrudan ilgili gerekli işleme KVKK m.5/2(c); fatura ve mali kayıtlar gibi hukuki yükümlülük kayıtlarında m.5/2(ç), hakların korunması ve uyuşmazlık işlemlerinde m.5/2(e) uygulanabilir. Güvenlik amaçlı gerekli ve ölçülü kayıtlar temel hakları zedelemeyen meşru menfaat kapsamında m.5/2(f) ile değerlendirilir. Amaçla ilgisiz veri toplama veya bütün verileri sınırsız işleme yetkisi yoktur. Pazarlama ve bülten izni ayrıca ve isteğe bağlı olarak alınır.

Veriler gerekli kapsamda bağlandığınız öğretmen, kurum ve veli kullanıcıları, barındırma/e-posta/güvenlik/kimlik doğrulama tedarikçileri ve kanunen yetkili mercilerle paylaşılabilir. Hostinger VPS ve aynı sunucudaki yedekler işletmeci beyanıyla Litvanya'dadır; Hostinger SMTP ve Google giriş/reCAPTCHA kullanılır. Koçluk tanıtım, giriş, kayıt ve yasal metin sayfalarında, çerez bandında "Kabul et" seçilirse ziyaret ölçümü için Google Analytics kullanılır. Bu hizmetler nedeniyle veriler yurt dışında işlenir.

Veriler amaç ve yasal gereklilikle sınırlı olarak saklanır. Abonelik iptali hesap silme değildir. Hesap silindiğinde abonelik, ödeme bildirimi, onaylanan sözleşme ve fatura kayıtları 10 yıl daha saklanır; süre dolunca abonelik ve ödeme kayıtlarındaki ad, e-posta ve notlar silinir, tutar, tarih ve ödeme referansı muhasebe kaydı olarak kalır. Hesap silme talebi ve zorunlu mali/uyuşmazlık kayıtları ayrı değerlendirilir; otomatik sınırsız saklama veya bütün kayıtların anında silinmesi taahhüt edilmez.

KVKK m.11 kapsamında işleme olup olmadığını öğrenme, bilgi isteme, amacı ve amaca uygunluğu öğrenme, yurt içi/yurt dışı alıcıları öğrenme, düzeltme, şartları oluştuğunda silme/yok etme ve alıcılara bildirme, yalnız otomatik analizle aleyhe sonuçlara itiraz ve hukuka aykırı işleme nedeniyle zararın giderilmesini isteme hakları saklıdır. Şirket adresine yazılı veya Veri Sorumlusuna Başvuru Usul ve Esasları Hakkında Tebliğ'deki geçerli yöntemlerle başvuru yapılabilir. Önceden şirkete bildirilmiş ve sistemde kayıtlı e-posta adresinden info@onalotomasyon.com adresine başvuru yapılabilir. Gerekli kimlik teyidi ölçülü yürütülür. Başvuru en kısa sürede, en geç 30 gün içinde sonuçlandırılır; ücret yalnız mevzuatın izin verdiği durumlarda uygulanır.$legal$, '__YAYIN_GÜNÜ__', :'publish_day'),'bacb22e0368545c08d21d563fd0eda6b'),
('speed-reading-terms','Hızlı Okuma kullanım koşulları',replace($legal$__YAYIN_GÜNÜ__ — Hızlı Okuma kullanım koşulları

İşletmeci ONAL YAZILIM OTOMASYON EĞİTİM DANIŞMANLIK SAN. TİC. LTD. ŞTİ.; vergi dairesi YOZGAT, vergi no 6421076848, MERSİS 0642107684800001. Adres Karatepe Mah. Şehit Bayram Karataş Cad. Bulvar 1071 Sitesi, Merkez/Yozgat. İletişim info@onalotomasyon.com, 0505 707 20 05.

Hızlı Okuma öğrencilere, velilere, öğretmenlere ve kurumlara yöneliktir.

Platform okuma değerlendirmeleri, eğitim programları, egzersizler ve ilerleme raporları sunar. Sonuçlar metin, cihaz, ölçüm ve uygulama koşullarından etkilenir; klinik değerlendirme veya belirli okuma hızı/akademik başarı garantisi değildir. Koçluk hizmeti ayrıca yetkilendirilmedikçe bu erişime dahil değildir.

Ücretli abonelik banka havalesi/EFT ile satın alınır. Ödeme bildiriminden önce plan, toplam fiyat ve süre gösterilir; ön bilgilendirme formu ve mesafeli satış sözleşmesi sunulur ve alıcının onayı alınır. Abonelik ödemenin onaylanmasıyla başlar; kendiliğinden yenilenmez ve otomatik ödeme alınmaz. Cayma ve iade, mesafeli satış sözleşmesindeki koşullarla ve kanuni haklar saklı kalarak uygulanır.

Hesap paylaşımı, telif ihlali, yetkisiz öğrenci verisi erişimi ve sonuç manipülasyonu yasaktır. Kullanıcı içeriği yalnız hizmetin gerektirdiği kullanım kapsamında işlenir; sınırsız mülkiyet devri yoktur. Bakım, hata ve güvenlik nedeniyle kısıtlama olabilir; yasal haklar korunur. Hesap silme talepleri ortak KVKK/gizlilik usulüyle değerlendirilir.$legal$, '__YAYIN_GÜNÜ__', :'publish_day'),'e9d41a0cf6054d3ec4e19958ba15bcf1'),
('cookies','Çerez ve tarayıcı depolama politikası',replace($legal$__YAYIN_GÜNÜ__ — ONAL hizmetleri

İşletmeci ONAL YAZILIM OTOMASYON EĞİTİM DANIŞMANLIK SAN. TİC. LTD. ŞTİ.; iletişim info@onalotomasyon.com. Kapsam eduivme.com, onuraltintas.net ve masterhizliokuma.com hizmetleridir.

Oturum yenilemek için eduplatform_refresh çerezi, Kurs Yönetimi oturumunda eduplatform_course_management_refresh çerezi kullanılır. Kalıcı oturum seçimine göre süresi verilen oturum son kullanma zamanına bağlıdır; kalıcı olmayan oturumda tarayıcı oturumu esas alınabilir. Çıkışta temizleme kodu vardır. Çerez HttpOnly, üretimde Secure ve SameSite=Strict ayarlarıyla oluşturulur.

Tarayıcı localStorage alanında theme/sp-theme görünüm, lang dil, user-settings uygulama tercihleri; currentUser sınırlı kullanıcı görünüm bilgileri, rememberedEmail isteğe bağlı e-posta hatırlama; eduplatform_staff_active_product ve staff.coaching.institution personel portalında son seçilen ürün ve kurum; coaching.diplomaGrade ile başlayan kayıtlar puan tahmini için girilen diploma notu; coaching.celebration.dismissed kapatılan kutlama bildirimleri tutulabilir. localStorage kayıtlarının kendiliğinden sabit sona erme süresi yoktur; çıkış, ayar değişikliği veya tarayıcı verisi temizleme davranışı kayıt türüne göre farklıdır. Erişim tokeninin currentUser alanında saklandığı iddia edilmez.

Google giriş ve reCAPTCHA ilgili işlemlerde üçüncü taraf teknolojileri kullanır; g_state ve Google alan adlarına ait çerezler oluşabilir. Bunların bir bölümü kullanıcının önceden açık Google oturumundan gelebilir; süreler sağlayıcıya ve oturuma göre değişir ve tümü bu platformun oluşturduğu çerezler değildir. Koçluk tanıtım, giriş, kayıt ve yasal metin sayfalarında ziyaretler Google Analytics 4 ile ölçülebilir: analiz çerezleri (_ga ile başlayanlar) yalnız çerez bandında "Kabul et" seçildiğinde kullanılır, "Reddet" seçildiğinde yüklenmez; tercih tarayıcıda eduivme.cookie-consent.v1 kaydında tutulur ve onay geri alındığında bu çerezler silinir. Onay verildiğinde, gelinen bağlantıdaki kampanya etiketleri (utm) sekme kapanana kadar tarayıcının sessionStorage alanında eduivme.utm.v1 kaydında tutulur.

Bu belge bütün izleme faaliyetlerine genel onay değildir; zorunlu olmayan araçlar için tercih ayrıca sorulur. Çerez/depolamayı tarayıcı ayarlarından temizleyebilir veya sınırlayabilirsiniz; oturum ve bazı işlevler etkilenebilir. Google/Hostinger aktarımı için ortak gizlilik ve KVKK açıklamaları geçerlidir.$legal$, '__YAYIN_GÜNÜ__', :'publish_day'),'49a3eab0fa3735b9a3184ea368426081');

DO $$
DECLARE problems text;
BEGIN
 IF EXISTS (SELECT 1 FROM legal_fix WHERE content !~ '^[0-9]{1,2} (Ocak|Şubat|Mart|Nisan|Mayıs|Haziran|Temmuz|Ağustos|Eylül|Ekim|Kasım|Aralık) 20[0-9]{2} — ') THEN
   RAISE EXCEPTION 'Set publish_day to the publish day (psql -v publish_day=''16 Kasım 2026''). Nothing changed.';
 END IF;
 IF (SELECT count(*) FROM identity."users" WHERE "Email"='admin@eduivme.com') <> 1 THEN
   RAISE EXCEPTION 'Expected existing admin actor not found';
 END IF;
 -- Below the date line, each page must be the text published on 14.11.2026 or already the new one (a rerun).
 SELECT string_agg(c.slug, ', ' ORDER BY c.slug) INTO problems FROM legal_fix c
 WHERE NOT EXISTS (SELECT 1 FROM identity."PlatformLegalPages" p WHERE p."Slug"=c.slug AND p."ArchivedAt" IS NULL AND p."IsPublished"
     AND (md5(substr(p."Content", strpos(p."Content", E'\n')+1))=c.previous_md5
       OR substr(p."Content", strpos(p."Content", E'\n')+1)=substr(c.content, strpos(c.content, E'\n')+1)));
 IF problems IS NOT NULL THEN
   RAISE EXCEPTION 'These pages differ from the published texts (missing, archived, unpublished or edited on the server): %. Merge the edit by hand. Nothing changed.', problems;
 END IF;
END $$;

INSERT INTO identity."PlatformLegalPageRevisions" ("Id","PageId","Slug","Title","Content","IsPublished","IsArchived","Version","CreatedBy","CreatedAt")
SELECT gen_random_uuid(), p."Id",p."Slug",p."Title",p."Content",p."IsPublished",false,p."Version",coalesce(p."UpdatedBy",p."CreatedBy"),coalesce(p."UpdatedAt",p."CreatedAt")
FROM identity."PlatformLegalPages" p JOIN legal_fix c ON c.slug=p."Slug"
WHERE md5(substr(p."Content", strpos(p."Content", E'\n')+1))=c.previous_md5;

UPDATE identity."PlatformLegalPages" p SET "Title"=c.title, "Content"=c.content, "Version"=p."Version"+1,
 "UpdatedBy"=(SELECT "Id" FROM identity."users" WHERE "Email"='admin@eduivme.com'), "UpdatedAt"=now()
FROM legal_fix c
WHERE c.slug=p."Slug" AND md5(substr(p."Content", strpos(p."Content", E'\n')+1))=c.previous_md5;

SELECT "Slug","Version","IsPublished","Title",left("Content",30) AS first_line,length("Content") AS chars FROM identity."PlatformLegalPages"
WHERE "Slug" IN (SELECT slug FROM legal_fix) ORDER BY "Slug";
COMMIT;
