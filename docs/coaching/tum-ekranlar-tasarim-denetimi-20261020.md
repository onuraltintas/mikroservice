# Öğrenci ve öğretmen ekranları tasarım denetimi

Tarih: 20.10.2026. Canlı sürüm: `coaching-screen-audit-20261020-r2`.

Öğrenci ve öğretmen çalışma alanlarındaki kart, form, tablo, takvim ve butonlar ortak tasarım diliyle denetlendi. Bulunan kalıcı uyumsuzluklar ortak stil dosyasında düzeltildi ve iki portalın canlı sürümüne alındı. Veritabanı şeması ve iş akışları değiştirilmedi.

## Yapılan düzeltmeler

- Ortak hedef arama, hedef ayrıntıları ve çalışma raporlarının kartları, alan kenarlıkları, yüzeyleri ve butonları tema değişkenlerine bağlandı. Öğretmende açık renk kalan hedef butonları düzeltildi.
- Eski slate, yeşil, turuncu ve kırmızı yardımcı sınıfların metin ve durum renkleri ortak tema renkleriyle eşleştirildi. İki temada da aynı anlamı taşıyan başarı, uyarı ve hata renkleri kullanılıyor.
- Haftalık takvimin küçük ve soluk açıklaması okunur hâle getirildi. Yıllık takvimin koyu temada açık yüzey üzerinde kalan “Saat” başlığı düzeltildi.
- Takvim boş durumları ve taşıma alanları ortak yüzey renklerine bağlandı. Geniş tablolar kendi alanlarında kaydırılıyor; sayfanın tamamını genişletmiyor.
- Öğretmen ödev ekranındaki uzun başlık eylemleri mobilde satıra geçiyor. “Geciken ve teslim edilmeyenler” ile “Yeni ödev” butonlarının sayfa dışına taşması giderildi.
- Öğretmen karşılama alanının küçük üst başlığındaki düşük kontrast düzeltildi.
- Sınıfı olmayan ve Angular sekme değişiminden sonra `class=""` kalan yerel butonların ortak kenarlık, yüzey ve yazı stili korunuyor.

Üretim kodundaki değişiklik yalnızca `clients/admin-panel/src/app/features/coaching-portal/layout/coaching-workspace-depth.scss` dosyasıdır. Bu dosya iki portal tarafından ortak kullanılır. Mevcut merkezi toaster ve uygulama içi onay bileşenleri korunmuştur; tarayıcı iletişim kutusu eklenmemiştir.

## Canlı kapsam

| Alan | Ekran/bölüm | Açık masaüstü | Koyu masaüstü | Açık mobil | Koyu mobil |
| --- | ---: | --- | --- | --- | --- |
| Öğrenci | 17 | Kontrol edildi | Kontrol edildi | Kontrol edildi | Kontrol edildi |
| Öğretmen | 10 | Kontrol edildi | Kontrol edildi | Kontrol edildi | Kontrol edildi |

Toplam 108 ana ekran/görünüm kontrolü, ayrıca 30 farklı iç ekran veya son durum gözlemi kaydedildi. Masaüstü 1440 × 1000, mobil 390 × 844 CSS piksel boyutlarında denetlendi. Ana kontrollerin hiçbirinde sayfa düzeyinde yatay taşma veya alan dışına çıkan görünür form kontrolü tespit edilmedi.

Öğrenci kapsamı: ana sayfa, profil, ilk kurulum, çalışma saatleri, otomatik plan, plan sihirbazı, haftalık takvim, yıllık plan önizlemesi, kayıtlı plan takvimi, ödev listesi, teslim/değerlendirme ayrıntısı, seanslar, ücretler, abonelik, bildirimler, sınav sonuçları ve gelişim ekranı.

Öğretmen kapsamı: öğrenciler/raporlar, takip merkezi, çalışma takibi, koçluk profili/davetler, tahsilat, ödevler, seanslar, hedefler, sınavlar ve öğretmen planı.

İç ekran kontrollerinde hedef ekleme formu, LGS katalog filtreleri ve arama sonuçları, gelişim alt sekmeleri, öğrenci haftalık değerlendirmesi, öğretmenin öğrenci profili/düzenleme formu, çalışma raporu ve haftalık karar görünümü incelendi. Öğretmenin yeni ödev, seans ve sınav formları boş açılıp kaydedilmeden kapatıldı. Dolu yıllık planın gün/hafta/ay görünümleri de kontrol edildi.

Bu bir görsel ve salt okunur regresyon denetimidir. Yeni plan yayımlama, ödev değerlendirmesini değiştirme, hedef seçimini kaydetme, ödeme veya profil güncelleme işlemi yapılmadı. Öğrencinin kurulumunu ilerletmek için mevcut eğitim bilgileri değiştirilmedi; yalnız mevcut hesapla erişilebilen kurulum durumu canlıda incelendi.

## Testler

- `node tools/test-coaching-student-design.mjs`: öğrenci için 310 test/46 dosya, öğretmen için 281 test/34 dosya geçti; toplam 591 test.
- İki uygulamanın yerel üretim derlemesi başarılı.
- Canlı kaynak kopyası üzerine yalnız ortak SCSS eklenerek hazırlanan iki izole üretim derlemesi başarılı. Yereldeki başka geliştirmeler bu sürüme alınmadı.
- `node tools/check-coaching-student-design.mjs`: açık/koyu tema metinleri, durum renkleri, ders renkleri ve hafif renkli yüzeyler için en az 4,5:1 kontrast kontrolleri geçti; öğrenci bileşenlerinde tarayıcı dialog kullanımı kontrolü geçti.
- Değişen dosyalar için `git diff --check` geçti.
- Canlı CSS ve JavaScript dosyaları HTTP 200 ve doğru içerik türleriyle sunuluyor. İki portal çalışıyor, yeniden başlama sayıları sıfır.

Derlemelerde önceden bulunan paket ve bileşen stil bütçesi uyarıları devam ediyor; derleme hatası yok.

Ham canlı ölçümler `artifacts/coaching-screen-audit-20261020-r2/live-matrix.json` dosyasında saklanır. Öğretmen sekmesine tıklamanın hemen ardından alınan bazı ölçümler 180 ms arka plan geçişinin ara rengini yakalayabiliyor. Bu ham uyarılar silinmedi; geçiş sonrası ölçümler ve ekran görüntüleri kalıcı bir renk uyumsuzluğu göstermedi. Takvim/tablo/sekme içindeki kasıtlı yatay kaydırma, gizli dosya seçici ve yerel kaydırma çubukları buton boyutu ihlali olarak sayılmadı.

Hızlı tam sayfa yenilemeli ilk taramada öğrenci oturumu geçici olarak giriş ekranına döndü. Oturum daha sonra mevcut çerezle yeniden açıldı. Kaynak kodda kimlik doğrulama için 30 istek/dakika sınırı bulunuyor; bu gözlem o sınırla uyumlu olmakla birlikte bu denetimde HTTP 429 kaydı alınmadı. Kalan tarama normal uygulama menüsü geçişleriyle tamamlandı. Güvenlik sınırları değiştirilmedi.

## Kayıtların korunması ve dağıtım

Dağıtım öncesi ve sonrası satır özetleri karşılaştırıldı. Kapanış kontrolünde:

- Coaching veritabanındaki bütün eski satırlar, planlar, kazanım bağlantıları ve çalışma kayıtları aynı kaldı.
- Notification veritabanındaki bütün eski satırlar aynı kaldı.
- Identity veritabanında, kullanıcının test sırasında yeniden giriş yaptığı tek öğrenci hesabı dışında bütün eski satırlar aynı kaldı. İstisna yalnız `testogrenci@eduivme.com` hesabına daraltıldı; kullanıcı sayısının değişmediği ve başka hesabın değişmediği ayrıca doğrulandı. Bu hesabın bütün satırı yeniden giriş nedeniyle farklı olduğundan, onun için bayt düzeyinde değişmezlik iddia edilmez.

İlk katı kapanış karşılaştırmasının bu giriş satırında başarısız olduğu log korundu. Son denetim bu tek yetkili giriş istisnasını açıkça raporlayarak geçti; bütün `users` tablosu denetim dışı bırakılmadı.

İki çalışma zamanı yapılandırması korundu. Canlıya alınan kaynak karşılaştırması ortak SCSS dışında değişiklik olmadığını doğruladı. Veritabanı geçişi, kayıt temizliği veya arka uç dağıtımı yapılmadı.

Paket SHA-256: `f230df1c4c3389fb3d8d3cc74c03b04c8fc17535168f4404db1dfc45658ad4f6`.

Öğrenci CSS: `styles-W2AMBVSO.css`. Öğretmen CSS: `styles-6NOIWE22.css`.

## Kanıt dosyaları

- `artifacts/coaching-screen-audit-20261020/regression.log`
- `artifacts/coaching-screen-audit-20261020/build.log`
- `artifacts/coaching-screen-audit-20261020-r2/live-matrix.json`
- `artifacts/coaching-screen-audit-20261020-r2/verification.log` — yeniden giriş satırını yakalayan ilk katı kapanış kontrolü.
- `artifacts/coaching-screen-audit-20261020-r2/final-audit.log` — dar kapsamlı giriş istisnasıyla başarılı kapanış kontrolü.
- `artifacts/coaching-screen-audit-20261020-r2/student-light-desktop.png`
- `artifacts/coaching-screen-audit-20261020-r2/teacher-light-desktop.png`
- `artifacts/coaching-screen-audit-20261020-r2/teacher-dark-mobile.png`
- İlk canlı turdaki diğer tema/ekran görüntüleri: `artifacts/coaching-screen-audit-20261020/`.

Denetim sonunda iki portal açık temaya döndürüldü ve geçici cihaz/ekran boyutu emülasyonları kaldırıldı. Kullanıcının açık sekmeleri korundu.
