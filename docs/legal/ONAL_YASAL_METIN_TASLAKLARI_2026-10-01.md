# ONAL — Merkezi yasal metin taslakları

Hazırlama tarihi: 1 Ekim 2026. Durum: inceleme taslağı, yayımlanmamıştır.

## 1 Ekim teknik doğrulama ve kesinleşen işletme kuralları

Bu bölüm aşağıdaki ilk taslakta belirsiz bırakılan bilgileri günceller; çelişen eski ifadeler son yayın metnine aktarılmamalıdır.

- İşletmecinin beyanına göre VPS Litvanya'dadır; yedekler de aynı sunucudadır. Aynı sunucudaki yedek ayrı felaket kurtarma lokasyonu değildir. SMTP ve Google hizmetlerinin işleme ülkeleri bu beyanla belirlenmiş sayılmaz.
- Notification servisinin canlı ayarları: `Email__Host=smtp.hostinger.com`, port 465, gönderici `destek@eduivme.com`, gönderici adı Eduİvme. Parola ve anahtar okunup yayımlanmamıştır. Destek/KVKK adresi işletme tercihiyle `info@onalotomasyon.com` olarak kalır. Hostinger SMTP tedarikçisi doğrulanmıştır; e-posta işleme/alt sağlayıcı ülkesi henüz doğrulanmamıştır.
- Koçluk aboneliği yalnız platform araçlarını kapsar; birebir öğretmen görüşmesi veya danışmanlık satışı değildir.
- Ücretsiz deneme yoktur; abonelik otomatik yenilenmez. İşletmenin etkinleştirme taahhüdü: ödemenin işletmeye ulaşmasını takiben ödeme kontrolü ve erişimin etkinleştirilmesi en geç 3 iş günü içinde tamamlanır. Ödeme kontrolünü ayrıca belirsiz bir süreye bırakıp bu 3 iş gününün dışında tutan ifade kullanılmaz.
- Abonelik sona ermesi hesabı silmez; açık hesapta eğitim geçmişi hizmet amacıyla korunur. Hesap silme/KVKK talebinde yasal saklama zorunluluğu olmayan veriler silme/yok etme veya uygun anonimleştirme sürecine alınır. Zorunlu mali/uyuşmazlık kayıtları ayrı değerlendirilir. Bu kural bütün verilerin süresiz saklanacağı anlamına gelmez. Kategori bazlı süre ve yedek imha takvimi koddan kesinleştirilemedi.
- Minimum yaş 13 ve küçük kullanıcı için veli üzerinden ücretli sözleşme işletme politikasıdır. Bunların mevcut uygulamada uçtan uca zorlandığı doğrulanmış değildir; sözleşme metni kod kontrolünün yerine geçmez.
- Adres işletmecinin bildirdiği biçimiyle kullanılacaktır; bina/kapı numarası uydurulmayacaktır.
- Yurt dışı aktarım belgeleri işletmeci beyanıyla mevcut değildir. Aktarımın gerekli sözleşme/bildirim süreçleri tamamlanmış gibi gösterilmeyecektir. Yayın bu eksikliği gidermez.

### Kodda doğrulanan çerez ve tarayıcı depolama bilgileri

| Ad/teknoloji | Amaç | Süre ve silme bilgisi |
| --- | --- | --- |
| `eduplatform_refresh` çerezi | Oturum yenileme | Gerçek oturum yapılandırmasında verilen son kullanma zamanı; çıkışta temizleme kodu var, sabit gün sayısı bu incelemede doğrulanmadı |
| `theme`, `sp-theme` | Arayüz görünüm tercihi | localStorage; değiştirilene veya tarayıcı verisi temizlenene kadar, otomatik süre yok |
| `lang` | Dil tercihi | localStorage; değiştirilene veya tarayıcı verisi temizlenene kadar |
| `currentUser` | Hızlı Okuma'da sınırlı kullanıcı görünüm bilgisi | localStorage; çıkışta temizleme var; erişim tokeni bu anahtarda saklanmıyor |
| `rememberedEmail` | Kullanıcının e-posta hatırlama tercihi | localStorage; seçim kapatılınca kaldırılıyor |
| `vocab_progress_<kullanıcı>` | Kelime egzersizi yerel ilerlemesi | localStorage; tarayıcı verisi temizlenene kadar kalabilir |
| Google giriş/reCAPTCHA teknolojileri | İsteğe bağlı Google giriş ve etkin formlarda kötüye kullanım önleme | Sağlayıcının çerez adları/süreleri canlı tarayıcı envanteriyle ayrıca doğrulanmalı |

Tema/dil/egzersiz ayarları tarayıcı depolaması kullanır; bunlar sunucu çereziyle aynı şey değildir. İncelenen kaynaklarda Google Analytics/Tag Manager reklam izleme entegrasyonu tespit edilmedi; bu sınırlı kod taraması bütün canlı ağ isteklerinin denetimi değildir. Tam tercih yönetimi ve üçüncü taraf süre envanteri tamamlanmış sayılmaz.
Bu dosya hukuk görüşü veya mevzuata tam uyum belgesi değildir. Köşeli parantezli eksikler tamamlanmadan yayımlanmaz. Başlıklardaki slug değerleri merkezi Yasal Sayfalar ekranında kullanılabilir. Mevcut yayımlanmış metinler bu dosyayla değiştirilmemiştir.

## Ortak şirket bilgileri

İşletmeci/veri sorumlusu: ONAL YAZILIM OTOMASYON EĞİTİM DANIŞMANLIK SAN. TİC. LTD. ŞTİ. (ONAL).
Vergi dairesi: YOZGAT. Vergi numarası: 6421076848. MERSİS: 0642107684800001.
Adres: Karatepe Mah. Şehit Bayram Karataş Cad. Bulvar 1071 Sitesi, Merkez/Yozgat.
Destek ve veri koruma iletişimi: info@onalotomasyon.com. Telefon: 0505 707 20 05.
Hizmetler: Eduİvme (eduivme.com), Koçluk (onuraltintas.net), Hızlı Okuma (masterhizliokuma.com).
[YAYIN ÖNCESİ: Tebligata elverişli tam adres için bina/bağımsız bölüm numarasını doğrulayın.]

## 1. Gizlilik politikası — `privacy`

### Kapsam ve hesaplar

Bu politika ONAL tarafından işletilen Eduİvme, Koçluk ve Hızlı Okuma hizmetlerinde kişisel bilgilerin kullanımını açıklar. Kimlik doğrulama ve ortak hesap işlemleri merkezi Identity altyapısında yürütülür. Koçluk ve Hızlı Okuma profilleri, ürün üyelikleri ve eğitim sonuçları ilgili ürünün veri alanında yönetilir. Ortak hesap sahibi olmak tek başına bütün ürünlere veya başka öğrencilerin bilgilerine erişim hakkı vermez.

### İşlenen bilgiler ve amaçlar

Hesap açma, giriş ve destek işlemleri kapsamında ad-soyad, e-posta, hesap/profil bilgileri, rol ve kurum ilişkileri; güvenliğin sağlanması için oturum ve teknik işlem kayıtları işlenebilir. Satın alma kapsamında plan, ödeme talebi ve ödeme referansı ile gerekli fatura bilgileri işlenir. Koçlukta hedef, ödev, seans, sınav ve gelişim kayıtları; Hızlı Okuma'da değerlendirme, egzersiz, okuma hızı, anlama ve program ilerlemesi kayıtları hizmetin sunulması için kullanılır. Tam alan listesi ve saklama süreleri KVKK aydınlatmasıyla birlikte değerlendirilir.

### Erişim ve paylaşım

Öğretmen ve kurum yetkilileri yalnız yetkilendirildikleri öğrenci ve kurum ilişkisi kapsamında verilere erişebilir. Erişim kapsamı ürün, rol ve atamaya göre belirlenir; kişisel veriler herkesin görebileceği şekilde yayımlanmaz. Kurumun öğrenci verileri bakımından ayrı veri sorumlusu veya veri işleyen olduğu durumlar kurum sözleşmesinde açıklanmalıdır. Kullanıcı içeriğinin öğretmen veya kurumla paylaşılması, hizmetin gerektirdiği kapsamla sınırlıdır.

### Dış hizmetler

Google ile giriş seçildiğinde Google kimlik doğrulaması; ilgili formlarda etkin olduğunda Google reCAPTCHA güvenlik hizmeti kullanılır. Barındırma için Hostinger VPS kullanıldığı işletme tarafından bildirilmiştir. Bu hizmetlerin ülkeleri, alt sağlayıcıları ve aktarım mekanizmaları henüz doğrulanmamıştır. ONAL bu taslakta verilerin yalnız Türkiye'de tutulduğunu taahhüt etmez.
[YAYIN ÖNCESİ: VPS veri merkezi, SMTP/e-posta sağlayıcısı, yedek lokasyonu, varsa diğer dış hizmetler ve KVKK m.9 aktarım mekanizmalarını doğrulayın.]

### Güvenlik, saklama ve çocuklar

Yetki sınırlama ve kimlik doğrulama gibi önlemler kullanılır; hiçbir sistem için mutlak güvenlik garantisi verilemez. Bilgiler gerekli amaç ve uygulanabilir yasal süreyle sınırlı saklanır. Silme talepleri yasal saklama ve hakların korunması yükümlülükleriyle birlikte değerlendirilir. [Kategori bazlı saklama, yedekten silinme ve imha takvimi tamamlanacaktır.]
Hizmet için işletmenin belirlediği minimum yaş 13'tür; bu sayı hukuki ehliyet veya bütün veri işlemleri için otomatik geçerli rıza yaşı olarak kabul edilmez. 13–17 yaş kullanıcılarının korunması, yasal temsilci bilgilendirmesi ve gerekli izinler ayrı değerlendirilir. Bültene katılmak veya pazarlama izni vermek hesabın şartı değildir.

### İletişim ve değişiklik

Gizlilik soruları info@onalotomasyon.com adresine iletilebilir. Resmî KVKK başvuruları aşağıdaki aydınlatmada açıklanan usulle yapılır. Önemli değişiklikler yeni sürümle duyurulur; gereken işlemler için ayrıca bilgilendirme/onay alınır.

## 2. KVKK aydınlatma metni — `kvkk`

### Veri sorumlusu

6698 sayılı Kanun kapsamında bu metinde açıklanan ONAL faaliyetlerinin veri sorumlusu, ortak şirket bilgileri bölümünde unvan ve iletişim bilgileri yer alan ONAL'dır. Kurumların kendi eğitim faaliyetleri için belirlediği veri işleme amaçları bakımından sorumluluk dağılımı kurum sözleşmelerinde ayrıca belirlenir. Bu metin açık rıza beyanı değildir.

### Toplama yöntemleri

Veriler kayıt/profil ve satın alma formları, Google giriş işlemi seçildiğinde kimlik doğrulama, öğrenci-öğretmen/kurum davetleri, eğitim içi kullanıcı işlemleri, destek yazışmaları ve teknik kayıtlar aracılığıyla elektronik ortamda; ilgili fatura ve başvuru işlemlerinde gerekli olduğunda diğer kanallardan toplanır.

### Veri, amaç ve hukuki sebep eşleştirmesi

| Faaliyet/veri grubu | Amaç | Önerilen dayanak — hukuk incelemesine tabi |
| --- | --- | --- |
| Hesap, iletişim ve hizmet profili | Talep edilen hizmeti sunma ve hesap işlemleri | KVKK m.5/2(c), sözleşmeyle doğrudan ilgili gerekli işleme; küçük kullanıcı bakımından sözleşme/temsilci yapısı ayrıca doğrulanır |
| Ödev, hedef, seans, sınav; okuma, egzersiz ve program sonuçları | Kullanıcının aldığı eğitim hizmetinin yürütülmesi ve yetkili raporlar | KVKK m.5/2(c); kurum adına işleme varsa kurumun dayanağı ve ONAL'ın rolü ayrıca belirlenir |
| Fatura, satın alma ve zorunlu muhasebe kayıtları | Mali ve hukuki yükümlülükleri yerine getirme | Uygulanabilir yükümlülükle sınırlı m.5/2(a) ve (ç) |
| Güvenlik, oturum ve kötüye kullanım kayıtları | Hizmeti ve hesapları koruma | Ölçülülük ve menfaat dengelemesi doğrulandığında m.5/2(f); yükümlülük varsa m.5/2(ç) |
| Destek/şikâyet ve uyuşmazlık kayıtları | Talepleri çözme, hakların korunması | İşleme göre m.5/2(c) veya (e) |
| İsteğe bağlı bülten e-postası ve onay kaydı | İlgili ürün bülteni ve izin yönetimi | Gerekli faaliyet için m.5/1 açık rıza; ticari ileti kuralları ayrıca uygulanır |

Bu tablo tüm amaçlara sınırsız dayanak oluşturmaz. Sağlık, biyometrik veya başka özel nitelikli kişisel veri kullanıcı notlarına eklenmemelidir; böyle bir veri işlenecekse ayrı gereklilik ve m.6 değerlendirmesi yapılmadan bu taslak yeterli sayılmaz.

### Alıcılar ve aktarım

Veriler gerekli kapsamda yetkili öğretmen/kurum kullanıcıları, barındırma ve e-posta/güvenlik/kimlik doğrulama tedarikçileri, mali müşavirlik hizmeti, ödeme/fatura sürecinde gerçekten kullanılan alıcılar ve kanunen yetkili kamu mercileriyle paylaşılabilir. Google giriş ve CAPTCHA ile yurt dışı hizmetler söz konusu olabilir. [Gerçek alıcı envanteri, tedarikçi sözleşmeleri, ülkeler ve KVKK m.9 aktarım dayanağı tamamlanacaktır; bu paragraf aktarım izni değildir.]

### Haklar ve başvuru

Kanun m.11 kapsamında kişisel verilerinizin işlenip işlenmediğini öğrenme, işlenmişse bilgi isteme, amacı ve amaca uygun kullanımı öğrenme, yurt içi/yurt dışı alıcıları bilme, eksik/yanlış verilerin düzeltilmesini isteme, şartları oluştuğunda silme/yok etme isteme ve bunların alıcılara bildirilmesini talep etme, yalnız otomatik analizle aleyhe sonuç doğmasına itiraz etme ve hukuka aykırı işleme nedeniyle zararın giderilmesini isteme haklarınız vardır.
Başvurular şirkete yazılı olarak adres üzerinden veya Veri Sorumlusuna Başvuru Usul ve Esasları Hakkında Tebliğ'deki geçerli yöntemlerle iletilebilir. Önceden şirkete bildirilmiş ve sistemde kayıtlı e-posta adresinden info@onalotomasyon.com adresine başvuru yapılabilir; diğer elektronik yöntemlerin geçerliliği ve kimlik teyidi ilgili usule göre değerlendirilir. Gereksiz kimlik belgesi istenmez. Başvurular en kısa sürede ve en geç 30 gün içinde sonuçlandırılır; ücret ancak mevzuatın izin verdiği durumlarda gündeme gelir.

## 3. Koçluk kullanım koşulları — `coaching-terms`

Koçluk hizmetinin işletmecisi ortak şirket bilgileri bölümündeki ONAL'dır. Hizmet; satın alınan plan ve yetkiye göre eğitim hedefleri, ödevler, seans kayıtları, gelişim/sınav bilgileri, öğretmen-öğrenci ilişkileri ve kurum raporları için dijital araçlar sunar. Bir platform aboneliği, plan açıkça belirtmedikçe birebir danışmanlık veya canlı öğretmen görüşmesi satın alındığı anlamına gelmez. Hızlı Okuma erişimi ayrıca tanımlanmadıkça dahil değildir.

Minimum kullanıcı yaşı 13'tür. 13–17 yaş öğrenci hesapları için gerekli veli/yasal temsilci bilgilendirme ve izin süreci tamamlanmalıdır; ücretli sözleşme veli/yasal temsilci üzerinden kurulmalıdır. Öğrencinin kendi hesabını açması veli adına borç doğurmaz. [Bu akışın uygulamada mevcut olduğu doğrulanmadan yayına çıkılmaz.]

Kullanıcı doğru bilgi verir, hesabını paylaşmaz, başkalarının verilerine izinsiz erişmez. Öğretmen ve kurumlar öğrenci verilerini amaç dışı kullanamaz, izinsiz üçüncü kişilerle paylaşamaz. Kurum/öğretmen ataması bütün geçmişe sınırsız erişim izni sayılmaz; erişim kapsamı ve geçmiş veri paylaşımı ilgili aydınlatma ve yetkilendirme süreciyle uyumlu olmalıdır.

Eğitim araçları belirli sınav puanı, başarı, yerleştirme veya gelişim garantisi vermez; sağlık hizmeti, teşhis veya psikoterapi yerine geçmez. Bu açıklama ONAL'ın kanuni hizmet sorumluluklarını ortadan kaldırmaz.

Ücretsiz deneme sunulmaz. Abonelik ödeme onayından sonra, işlem öncesinde belirtilen süre ve kapsamla başlar; otomatik yenilenmez. Süre sonunda devam için yeni satın alma gerekir. Fiyat, vergi dahil toplam bedel, kapsam ve süre satın alma ekranında gösterilir. Satın alma ve iade bakımından aşağıdaki ortak kurallar geçerlidir.

Telifli içerikler izinsiz çoğaltılamaz, satılamaz veya topluca dışarı aktarılamaz. Kullanıcı yüklediği içeriğe ilişkin yetkiye sahip olmalı; ONAL'a yalnız hizmetin yürütülmesi için gerekli saklama/gösterme yetkisini verir, mülkiyetini devretmez. Yasadışı kullanım ve güvenlik riski halinde ölçülü erişim tedbirleri alınabilir; yasal haklar ve varsa ödenmiş hizmete ilişkin haklar saklıdır. Destek/itiraz: info@onalotomasyon.com.

## 4. Hızlı Okuma kullanım koşulları — `speed-reading-terms`

İşletmeci ONAL'dır. Hizmet satın alınan plan kapsamında okuma değerlendirmeleri, eğitim programları, egzersizler ve ilerleme raporları sunar. Koçluk erişimi veya birebir eğitim plan açıklamasında açıkça belirtilmedikçe dahil değildir.
Minimum yaş 13'tür. 13–17 yaş için Koçluk koşullarındaki temsilci ve ücretli satın alma ilkeleri geçerlidir. Ücretsiz deneme ve otomatik yenileme yoktur. Planın bedeli, süresi, kapsamı ve etkinleşme koşulu işlemden önce açıklanır.
Okuma hızı ve anlama sonuçları kullanılan metin, cihaz, ölçüm yöntemi ve kullanıcı uygulamasından etkilenebilir; klinik ölçüm veya belirli akademik başarı garantisi değildir. Kullanıcı başkasının hesabını kullanamaz, egzersiz sonuçlarını manipüle edemez; kurum/öğretmenler yalnız yetkili öğrenci kapsamındaki raporları kullanır. İçerik telifleri ve kullanıcı içeriği bakımından Koçluk koşullarındaki sınırlı kullanım ilkeleri uygulanır.
Mevcut ücretli hizmeti esaslı azaltan değişikliklerde tüketicinin kanuni hakları korunur. Uyuşmazlıklarda tüketici hakem heyeti/tüketici mahkemesine başvuru hakları saklıdır; yalnız Yozgat mahkemelerini zorunlu kılan tüketici hükmü kullanılmaz.

## 5. Çerez ve benzeri teknolojiler — `cookies`

ONAL web hizmetleri hesap oturumu, tercihleri hatırlama, güvenlik ve uygulama çalışması amacıyla çerez, tarayıcı depolaması ve benzeri teknolojiler kullanabilir. Google giriş veya CAPTCHA etkinse ilgili üçüncü taraf teknolojiler de devreye girer. Tarayıcı depolaması silindiğinde yeniden giriş gerekebilir; zorunlu işlevler engellendiğinde hizmet kısmen çalışmayabilir.
Bu metin genel izin değildir. Zorunlu olmayan analiz/reklam teknolojileri kullanılacaksa gerçek envanter ve uygun tercih mekanizması devreye alınmalıdır; kullanılan üçüncü taraf teknolojiler otomatik olarak zorunlu kabul edilmez.
[YAYIN ÖNCESİ: Her alan adı için gerçek çerez/depolama adı, sağlayıcı, amaç, süre, tür ve izin gerekliliği tablosu oluşturulmalıdır. Henüz doğrulanmamış analytics/reklam aracı bu metinde varmış gibi belirtilmemiştir.]
Google'ın işlemleri ve yurt dışı aktarımlar bakımından ortak gizlilik/KVKK açıklamalarıyla birlikte değerlendirme yapılır. Tercihleri yönetme yolu: [uygulamadaki gerçek tercih ekranı/bağlantısı].

## 6. Koçluk bülteni onayı — `coaching-newsletter-consent`

Koçluk bültenine isteğe bağlı katılarak e-posta adresimin ONAL tarafından Koçluk içerikleri, hizmet duyuruları ve kampanyaları için kullanılmasına ve bu kapsamda e-posta gönderilmesine izin veriyorum. Bülten onayı, hesap açma veya hizmet satın alma şartı değildir; Hızlı Okuma bültenine otomatik katılım sağlamaz. İznimi e-postadaki abonelikten çıkma bağlantısıyla veya info@onalotomasyon.com üzerinden geri alabilirim. Ret bildirimi ve ileti gönderimini sonlandırma uygulanabilir mevzuat sürelerine göre yürütülür. Aydınlatma ortak KVKK metninde sunulur; ticari elektronik ileti izni ile kişisel veri işleme onayının ayrıştırılması gerektiği ölçüde ayrı seçimler kullanılmalıdır.
[YAYIN ÖNCESİ: E-posta sağlayıcısı, onay doğrulama akışı, İYS yükümlülükleri ve çocuklara yönelik pazarlama/temsilci süreci doğrulanmalıdır.]

## 7. Hızlı Okuma bülteni onayı — `speed-reading-newsletter-consent`

Hızlı Okuma bültenine isteğe bağlı katılarak e-posta adresimin ONAL tarafından Hızlı Okuma içerikleri, hizmet duyuruları ve kampanyaları için kullanılmasına ve bu kapsamda e-posta gönderilmesine izin veriyorum. Bu izin kayıt veya satın alma şartı değildir; Koçluk bültenine otomatik katılım sağlamaz. İzin e-postadaki iptal bağlantısıyla veya info@onalotomasyon.com üzerinden geri alınabilir. Ortak KVKK aydınlatması ve önceki bülten bölümündeki ayrı izin, ret bildirimi ve yayın öncesi doğrulama koşulları bu bülten için de uygulanır.

## 8. Abonelik, iptal ve iade politikası — `subscription-refund-policy`

Koçluk ve Hızlı Okuma abonelikleri süreli ve otomatik yenilemesizdir. Ücretsiz deneme sunulmaz. Ödeme teyidi sonrasında plan ekranında belirtilen hizmet etkinleştirilir. Ödeme talebi göndermek tek başına aboneliği etkinleştirmez; başarısız/teyit edilmemiş işlem için erişim başlamayabilir. İade gerektiren bir ödeme alınmışsa hizmetin açılmamış olması bedelin şirkette tutulmasına sınırsız hak vermez.

Kullanıcının hizmeti kullanmaktan vazgeçmesi, mevzuattan doğan hakları hariç, kalan süre için isteğe bağlı iade hakkı doğurmaz. Yürürlükteki mevzuattan kaynaklanan cayma, ayıplı hizmet, hiç veya gereği gibi ifa edilmeme ve bedel iadesi hakları saklıdır. Bu zorunlu durumlar dışında isteğe bağlı ücret iadesi sunulmaz.

Tüketiciye genel olarak tanınan cayma hakkı ve istisnalar, gerçek hizmete göre işlem öncesinde açıklanır. Elektronik ortamda anında ifa edilen hizmet/dijital içerik istisnası her süreli aboneliğe veya gelecekte yapılacak koçluk seansına otomatik uygulanmaz. Gerekli hallerde hizmete erken başlanmasına ilişkin ayrı, açık tüketici talebi/onayı alınır; tek başına kutucuk bütün kanuni hakları kaldırmaz.

İptal/iade başvurusu info@onalotomasyon.com adresine sipariş/ödeme referansıyla yapılabilir. Tüketicinin başka yasal bildirim yöntemleri engellenmez. Yasal iadeler ilgili sürede ve mevzuata uygun yöntemle gerçekleştirilir. İleride politika değişikliği geçmiş işlemlere geriye dönük hak kaybı yaratmaz. Kurumsal ticari sözleşmeler ayrı hazırlanır; bu tüketici politikası bütün kurum alımlarını otomatik tüketici sözleşmesi yapmaz.

## 9. Ön bilgilendirme şablonu — `pre-contract-information`

Bu belge her satın alma için dinamik doldurulmalıdır; boş alanlarla sözleşme kurulamaz.
Satıcı/sağlayıcı ONAL; adres, vergi, MERSİS ve iletişim bilgileri ortak şirket bölümündedir.
Alıcı/sözleşme tarafı: [yetişkin alıcı veya 13–17 yaş kullanıcının veli/yasal temsilcisi; gerekli fatura bilgileri]. Öğrenci: [gerekiyorsa hizmetten yararlanan kişi].
Ürün/plan: [Koçluk veya Hızlı Okuma, plan adı]. Esas özellikler ve dahil olmayanlar: [işlem anındaki kapsam]. Süre: [başlangıç kuralı ve bitiş]. Vergiler dahil bedel: [TL]. Ek ücret: [varsa açık tutar, yoksa yok]. Ödeme yöntemi ve teyit koşulu: [işleme özel]. Etkinleşme: [ödemeden sonra gerçek taahhüt edilen süre]. Teknik kullanım gereksinimleri: [cihaz/tarayıcı/internet ve gerekiyorsa uyumluluk].
Otomatik yenileme ve ücretsiz deneme bulunmaz. Cayma hakkı, kullanılma usulü ve uygulanıyorsa hizmete özel istisna: [hukuken doğrulanmış işlem bilgisi]. İptal/iade politikası ayrı olarak sunulur. Şikâyet: info@onalotomasyon.com / 0505 707 20 05. Tüketicinin uyuşmazlık tutarı ve koşullara göre tüketici hakem heyeti veya tüketici mahkemesine başvuru hakkı korunur.
Siparişin ödeme yükümlülüğü doğurduğu açıkça gösterilir; bu bilgilendirmenin sunulduğu ve alıcıya kalıcı veri saklayıcısıyla iletildiği doğrulanmalıdır.

## 10. Mesafeli hizmet sözleşmesi şablonu — `distance-service-agreement`

Taraflar: ONAL ile işlemde adı belirtilen yetişkin alıcı/veli-yasal temsilci. Kurumsal satın alma için ayrı sözleşme değerlendirilir.
Konu: Ön bilgilendirme ve sipariş özetinde tanımlanan dijital eğitim hizmetinin belirtilen süre/kapsamda sunulmasıdır. Plan, bedel, vergiler, ödeme, etkinleşme ve teknik şartlar siparişe özgü kayıtla bu sözleşmeye bağlanır.
ONAL hizmeti açıklanan niteliklere uygun sunar; alıcı doğru satın alma/fatura bilgisi verir. Hizmetten yararlanan küçük kullanıcıya ilişkin gerekli temsilci işlemleri ayrıca tamamlanır. Kullanım koşulları hizmet kullanımını; bu sözleşme ücretli işlemi düzenler. Kanuni tüketici haklarına aykırı koşul uygulanmaz.
Süreli abonelik otomatik yenilenmez. Cayma/iptal/iade, işlem öncesi sunulan bilgilendirme ve zorunlu mevzuata göre değerlendirilir. İsteğe bağlı iade yoktur; zorunlu iadeler saklıdır. Hizmetin başlaması bütün hakların kendiliğinden kaybı anlamına gelmez.
Sözleşme ve sipariş bilgileri alıcının saklayabileceği biçimde sunulur. Uyuşmazlıklarda kanuni başvuru mercileri ve görev/yetki kuralları korunur. Destek ve bildirimler ortak şirket bilgileri üzerinden yapılır. [İşlem anındaki sürüm, kabul zamanı, alıcı, sipariş ve gönderim kaydı sisteme bağlanmalıdır.]

## Yayın öncesi tamamlanacaklar

- VPS ülkesini Hostinger paneli veya sağlayıcı teyidiyle belirlemek; IP coğrafyasını kesin delil saymamak.
- SMTP, yedek/alt işleyen, veri aktarım ve kategori bazlı saklama envanterini tamamlamak.
- Tam adresi ve şirket temsil yetkisini doğrulamak.
- Minimum yaş/veli satın alma kontrolleri, gerçek otomatik yenilemesiz planlar ve denemesiz erişimi uygulamada doğrulamak.
- Google yeni kullanıcı akışında aydınlatma ve kullanım koşullarını satın alma onayıyla karıştırmadan sunmak.
- Bülten rızası/ticari ileti izinleri, İYS ve çerez tercihlerinin gerçek davranışını doğrulamak.
- Ön bilgilendirme ve sözleşmeyi gerçek sipariş bilgileriyle üretmek; statik sayfayı tek başına satış sözleşmesi saymamak.
- Hukuk incelemesi ve işletme onayı sonrasında merkezi Identity Yasal Sayfalar'a sürümlü taslak aktarımı/yayın.

## Resmî inceleme kaynakları

- KVKK aydınlatmanın kapsamı: https://www.kvkk.gov.tr/Icerik/2033/Aydinlatma-Yukumlulugu-
- Aydınlatma ve açık rızanın ayrılması: https://www.kvkk.gov.tr/Icerik/5420/2018-90
- Mesafeli sözleşme bilgilendirmesi: https://tuketici.ticaret.gov.tr/yayinlar/tuketici-bilgi-rehberi/mesafeli-sozlesmeler-hakkinda-bilgilendirme

Bu kaynaklar taslak hazırlığı içindir; metinler hizmete özel hukuk ve veri envanteri incelemesinin yerini tutmaz.
