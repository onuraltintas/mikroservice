# Koçluk teknik temel: doğrulanmış durum (22 Eylül 2026)

Bu belge yalnız mevcut çalışma ağacında çalıştırılmış kontrolleri ve henüz
kanıtlanmamış kapıları ayırır. “%100” veya production onayı anlamına gelmez.

## Doğrulanmış

- `dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj
  --configuration Release`: 614 başarılı, 0 başarısız. Gateway'in çalışan
  process gerektiren 2 testi bu koşuda atlandı; ayrı smoke kapısı CI'de var.
- Disposable Docker veri gizliliği E2E: Identity, Coaching, Notification ve
  Speed Reading ayrı veritabanlarındaki hesap silme, tekrar teslim tekilliği
  ve aktif legal hold altında silmeme; 5 test başarılı. Hesap silme
  senaryosunda artık Koçluk ödev eki PostgreSQL kaydı ve gerçek MinIO nesnesi
  de siliniyor; aynı kovadaki ilgisiz nesne korunuyor. Güncel yerel dağıtık
  gizlilik dosyasının iki testi yeniden geçti. Bu, gerçek VPS/staging nesne
  depolama doğrulaması değildir.
- Aynı disposable yığında MinIO, silme olayı yayımlanırken durduruldu.
  Identity talebi `Processing` kaldı; Koçluk eki korunurken execution kaydı
  oluşmadı. Koçluk logunda `R-RETRY` ve `minio:9000` bağlantı
  hatası gözlendi. MinIO başlayınca talep `Completed` oldu, nesne ve DB
  kaydı silindi, ilgisiz nesne korundu. Tam gizlilik dosyası 2/2 geçti.
  Bu kısa yerel kesinti provası, staging toparlanma süresi veya uzun süreli
  kesintiden sonra otomatik alarm kanıtı değildir.
- Koçluk silme servisi hata enjeksiyon testinde nesne depolama silmesi
  başarısız olunca öğrenci verileri ve değerlendirme korundu, tamamlanma
  kaydı oluşmadı; aynı talep depolama düzeldikten sonra başarılı oldu.
  İlgili servis testleri 4/4, tam entegrasyon grubu 614 başarılı/2 atlanan.
  Bu, gerçek MinIO kesintisinde broker yeniden teslim ölçümü değildir.
- İzole MinIO container'lı entegrasyon testi: dosya gerçekten yüklendi ve
  geri okundu; Coaching silme servisi DB ek kaydını ve gerçek nesneyi kaldırdı.
  Güncel çalışma ağacında ilgili 4 test tekrar geçti. Bu, staging'deki tam
  dağıtık zincirin yerine geçmez.
- MinIO imajının mevcut Docker Hub adresi çekilemedi; aynı sabit sürümün resmi
  Quay adresi indirildi. Compose adresi düzeltildi ve config kontrolü geçti.
- CI, .NET 10 SDK ile `net10.0` projelerini derleyecek şekilde eşitlendi.
  Dağıtık gizlilik E2E işi container build için zorunlu bağımlılık yapıldı.
- Disposable Koçluk backup/restore testi, mevcut veritabanından yalnız şemayı
  alıp sentetik bir hedefi yedekledi ve yeni veritabanına geri yükledi. Geçici
  veritabanları temizlendi; CI gizlilik işine aynı prova eklendi.
- Yönetim paneli testleri: 181/181 başarılı; production browser/SSR build'i
  tamamlandı. Build'de Hızlı Okuma katalog stil bütçesi uyarısı var; Koçluk
  doğrulaması adına bu ayrı platformun değişikliklerine dokunulmadı.
- Coaching.API Release `--warnaserror` derlemesi 0 uyarı/0 hata; EF
  `has-pending-model-changes` sonucu değişiklik yok; `dotnet list package
  --vulnerable --include-transitive` taramasında Koçluk API'si için raporlanan
  NuGet güvenlik açığı yok. Bu, dış pentest veya container imaj taraması değildir.
- Koçluk `GET /api/data-privacy/export` uç noktasının ağ geçidinde eksik rotası
  test-önce yöntemiyle tespit edilip yalnız GET'e izin veren rota eklendi.
  Gateway rota sözleşme testi ve Release `--warnaserror` derlemesi geçti; rota
  sözleşmesi CI gizlilik işine eklendi. Çalışan ağ geçidinde HTTP testi henüz yok.
- Koçluk export repository'si için izole EF veritabanında iki öğrencinin
  hedefleriyle veri ayrımı regresyon testi geçti (ilgili test sınıfı 4/4).
  Bu, gerçek PostgreSQL veya uçtan uca HTTP yetkilendirme kanıtı değildir.
- Veri dışa aktarma isteğinin, koçluk sözleşmesi yokken genel işlem kapısında
  engellendiği RED testle doğrulandı. Yalnız bu istek kapıdan muaf tutuldu;
  öğrencilik ve Identity erişim kontrolleri korundu. İlgili 11 test geçti.
- Ayrı `privacy-e2e-local-export` Docker projesinde Gateway → Coaching →
  Identity → PostgreSQL HTTP testi geçti: oturumsuz 401, yanlış rol 403,
  Identity'de olmayan öğrenci 403, kayıtlı öğrenci 200; yanıt `no-store` ve
  yalnız kendi hedefini içerdi. Aynı test, öğrencinin kendi oturum notu ve ona
  açık koç notunun görünmesini; koça özel notun, katılım öğretmen notunun ve
  başka öğrencinin oturumunun çıkmamasını da doğruladı. Ödev ve sınav
  sonuçları için öğrencinin kendi kayıtları, notu, geri bildirimi ve puanı
  görünürken başka öğrencinin kayıtları ile özel sınav öğretmen notu çıkmadı.
  Anlaşma yokken dışa aktarım çalıştı; anlaşma kanıtı eklendiğinde yalnız
  ilgili öğrencinin onayı çıktı. Böylece DTO'daki hedef, oturum, ödev, sınav
  ve anlaşma grupları sentetik kayıtlarla HTTP üzerinden doğrulandı.
  İki aktif öğrenci ayrı Identity kurumlarına bağlandığında her biri kendi
  kayıtlarını aldı; karşı kurum öğrencisinin kayıtları ve anlaşma onayı
  görünmedi. Bu, öğrenci export sınırının kanıtıdır; koç/yönetici tenant
  yetkilerinin tamamını kapsamaz.
  Identity container'ı yalnız bu izole projede durdurulduğunda export 5xx ile
  veri döndürmeden kapandı; yeniden başlatma sonrası aynı istek 200 oldu.
  PostgreSQL durdurulduğunda da export 5xx ile kapandı; yeniden başlatma
  sonrası veritabanı bağlantısı ve HTTP yanıtı toparlandı.
  RabbitMQ durdurulduğunda salt-okunur export 200 ile doğru öğrenci verisini
  döndürmeye devam etti. Bu, tüketici/olay toparlanmasının kanıtı değildir.
  Kesinti provası diğer gizlilik testleriyle çakışmaması için CI'da ayrı adımda.
  Geçici container ve hacimler testten sonra kaldırıldı; canlı VPS kullanılmadı.
- Ayrı `privacy-e2e-local-broker` Docker projesinde Koçluk tüketicisi kapalıyken
  kalıcı değerlendirme olayı RabbitMQ kuyruğuna yazıldı. Broker yeniden
  başlatılınca mesaj kuyrukta kaldı; Koçluk açılınca hedef sayısı doğru
  değerlendirildi, sonuç Identity gizlilik talebine ulaştı ve hedef silinmedi.
  Test geçti; CI gizlilik işine diğer kesinti testinden sonraki ayrı adımda
  eklendi. Bu, kısa süreli kesinti için teslim kanıtıdır; uzun süreli yük,
  tekrar deneme alarmı veya gerçek staging toparlanma süresi kanıtı değildir.
- Ayrı `privacy-e2e-local-attachment` Docker projesinde Gateway → Koçluk →
  Identity → PostgreSQL → MinIO üzerinden sentetik öğrenci dosyası oluşturma,
  yükleme ve indirme geçti. Tarama öncesi okuma 409, başka öğrencinin okuması
  403; taranıp temiz işaretlenen dosyanın baytları sahibine doğru döndü.
  Öğrenci Identity'de pasifleştirildikten sonra eski JWT ile bekleyen dosyayı
  yükleme, yeni dosya kaydı oluşturma ve mevcut dosyayı indirme girişimlerinin
  üçü de 403 döndü; bekleyen kayıt `PendingUpload` kaldı. Aktif kullanıcı akışı
  aynı testte başarılı oldu. Böylece dosya yazma yolları yalnız JWT'ye değil,
  güncel Identity durumuna da kapatıldı.
  CI gizlilik işine eklendi. Testte Development tarayıcı adaptörü kullanıldı;
  ClamAV'li production/staging taraması ve dağıtık silme zinciri bu HTTP
  testinin kapsamı değildir. MinIO yalnız disposable test ortamındaydı.
- MinIO etkin tam `privacy-e2e-local-full` Docker yığınında CI gizlilik
  adımlarının yerel provası yapıldı: ilk adım 7/7, export/kesinti 1/1,
  broker toparlanması 1/1, attachment HTTP 1/1 geçti. İlk deneme, eski
  silme testinin RabbitMQ yönetim portunu sabit `15672` alıp yanlış brokerı
  sorguladığını açığa çıkardı. Test artık seçilen disposable Compose
  projesinin RabbitMQ container/port eşleşmesini doğruluyor ve `.env.example`
  kullanıyor. Yanlış port verildiğinde test veri oluşturmadan bu kapıda
  beklenen şekilde reddedildi. Bu prova mevcut yerel imajlarla `--no-build`
  çalıştı; GitHub Actions'ın gerçek `--build` koşusunun yerine geçmez.
- Ayrı `privacy-e2e-local-admin` Docker projesinde iki kurumun yönetici,
  koç ve öğrencileriyle gerçek Gateway → Koçluk → Identity → PostgreSQL HTTP
  testi geçti. Yönetici ödev/oturum/sınav/hedef listeleri, ayrıntıları ve
  özet sayaçları yalnız kendi kurumunu içerdi; diğer kurumun doğrudan ID
  erişimleri 404 döndü. Koçun kendi atamasının listesi/ayrıntısı 200,
  karşı koçun listesi ve karşı kurum ayrıntısı 403 oldu. Identity kurum
  yöneticiliği pasifleştirildiğinde mevcut JWT ile yeni istek 403 döndü.
  CI gizlilik işine ayrı adım eklendi; yerel prova `--no-build` imajlarıyla
  yapıldı ve gerçek staging kanıtı değildir.
- Aynı iki kurumlu disposable HTTP testinde A kurumunun koçu B kurumunun
  ödevini güncelleme, iptal etme ve silme denemelerinde 403 aldı; B ödevinin
  başlığı ve durumu PostgreSQL'de değişmedi. Aynı koç kendi ödevini bu üç
  yolla sırasıyla güncelledi, iptal etti ve sildi; her aşama HTTP yanıtı ve
  PostgreSQL durumu ile doğrulandı. Bu, üç öğretmen yazma yoluna ilişkin
  yerel kanıttır; tüm Koçluk yazma yollarını kapsamaz.
- Production/staging Compose'a MinIO bucket'ını ve root hesabından ayrı,
  yalnız Koçluk bucket'ı için yetkili uygulama hesabını hazırlayan idempotent
  `minio-provision` işi eklendi. Production'da MinIO verisi, zorunlu
  `ATTACHMENT_MINIO_DATA_HOST_PATH` ile VPS'in şifreli filesystem'ine bağlanır;
  uygulama provisioning tamamlanmadan başlamaz. Sözleşme testi ve birleşik
  production/staging Compose config doğrulaması geçti. Bu, canlı VPS deploy'u,
  şifreli disk doğrulaması veya harici yedek/restore kanıtı değildir.

## Kapsam ölçümü

`--collect:"XPlat Code Coverage"` ile 613 başarılı, 2 atlanan testin olduğu
yerel koşudan alınan Cobertura raporu: toplam instrumented assembly satır
kapsamı %11,40; Coaching.API %3,23, Coaching.Application %51,85,
Coaching.Domain %73,02, Coaching.Infrastructure %8,87. CI artık bu dört
paket için sırasıyla %3/%50/%70/%8 gerileme tabanını denetler. Bu eşikler
hedef değil, yalnız mevcut düzeyin düşmesini engelleyen alt sınırdır.
Bu oranlar **yalnız bu entegrasyon test projesinin**
kapsamıdır; ayrı unit/UI/E2E koşularıyla birleştirilmiş ürün kapsamı değildir.
Özellikle `DataPrivacyController` %0, MinIO adaptörü %64,86 görünürken
Coaching erasure execution servisi bu raporda %100'dür. Yüksek test sayısı,
genel %80 kapsam hedefini kanıtlamaz.

## Kapanması gereken kapılar

1. Koçluk export için daha karmaşık ilişki/veri durumları ve ek dosya
   içeriğinin dışa aktarım kapsamı; koç/yönetici
   yazma işlemleri ve diğer okuma yollarında kurum sınırları, diğer olay
   tüketicileri ve gerçek operasyonel toparlanma ölçümleri.
2. Kullanıcının kendi VPS'inde çalışacak, güvenlik bakımından desteklenen S3
   uyumlu depoda ve staging'deki broker + PostgreSQL + nesne depolama zincirinde
   gerçek yükle–oku–sil; erişim, TLS, bucket yetkileri, kimlik bilgisi
   rotasyonu, mevcut nesnelerin taşınması ve VPS dışı yedek onayı. Garage bir
   adaydır; canlı MinIO değişimi henüz onaylanıp yapılmadı.
3. CI işinin GitHub Actions'ta yeşil çalıştığına dair kayıt. Yerel YAML ve
   Compose doğrulaması bu kanıtın yerine geçmez.
4. Staging migration-only/canary, gerçek veri kapsamlı yedek–geri yükleme ve
   yeniden-imha provası; hata oranı, gecikme ve alarm alıcılarının gözlenen
   sonucu. Yerel şema/sentetik kayıt provası bunun yerine geçmez.
5. Dış güvenlik testi ve bulguların kapatılması. Eski MinIO OSS sürümünün
   production güvenlik onayı verilmedi; depolamanın VPS'te kalması seçildi.

Bu kapılar kapanmadan teknik temel “%100” veya production-ready olarak
işaretlenmez.
