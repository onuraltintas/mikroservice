# Koçluk profilleri, eğitim bağlamı ve ilk kurulum — canlı dağıtım

Tarih: 16 Ekim 2026. Tamamlanma: **19:39:55 TSİ / 16:39:55 UTC**.
Uygulama revizyonu: `8757ca75ebcee217d76f6d1630ac3bd6f445dd92`.
Canlı adres: [öğrenci kurulumu](https://eduivme.com/coaching-portal/setup).

## Yayın kapsamı

Identity, Coaching API, öğrenci/yönetici istemcisi ve ortak profil bileşenini kullanan
personel istemcisi yayımlandı. Koçluk öğrenci/öğretmen profilleri, kurum üyelikleri,
öğretmen–öğrenci atamaları ve davetler Coaching'e ait. İlişkiler ortak hesap `UserId`'sini
kullanıyor. Ortak hesap, parola, rol, ürün erişimi ve oturum Identity'de kaldı.

Eğitim bağlamı, sürümlü ders kataloğu ve altı adımlı öğrenci kurulumu canlıda. Genel
zorunlu yönlendirme yok; eksik kurulumla mevcut plana erişim korunuyor. Mevcut eğitim
bilgisi yeterliyse öğrenciye yeniden profil doldurtulmuyor. Okul ve doğum tarihi zorunlu
değil. Çalışma amacı, müsaitlik, dersler ve başlangıç beyanları ayrı adımlarda kaydediliyor.

Hızlı Okuma, bildirim servisi, gateway ve diğer hedef dışı konteynerler değiştirilmedi;
konteyner kimlikleri önceki envanterle karşılaştırıldı. Çalışma ağacındaki ilgisiz Kurs
Yönetimi ve diğer geliştirmeler pakete alınmadı. Canlı PDB'lerdeki 768 kaynak belge
eşleştirilerek mevcut canlı davranışlar üzerine yalnız bu özellik taşındı.

## Veritabanı ve korunma

Üretimde Identity ve Coaching için geçişten hemen önce özel sunucu dizinine PostgreSQL
custom-format yedek alındı; SHA256 doğrulamaları geçti. Yedekler ve eski imajlar sunucuda
korunuyor. Hesap/parola içeren yedekler yerel artifact veya kaynak kontrolüne alınmadı.

Uygulanan üç Coaching migration:

1. `20261016133831_OwnedCoachingProfiles`
2. `20261016134530_OwnedCoachingInvitations`
3. `20261016144923_StudentLearningSetup`

Canlı migration geçmişi 38'den 41'e çıktı. Canlıda eskiden bulunan ek tarihsel migration
kaydı da korundu; geçmiş yeniden yazılmadı. Eski Identity profil/atama satırları bu yayında
silinmedi. Yeni servisler Coaching'deki sahip olunan veriyi kullanır.

Önceki geçiş tasarımına uygun olarak eski Identity bekleyen davetleri yeni davet
tablosuna aktarılmadı; arşivde kaldı. Gerekli test davetleri yeni panelden yeniden
oluşturulur. Bu yayında davet mesajı gönderilmedi.

| Kontrol | Geçiş öncesi | Geçiş ve öğrenci testi sonrası |
|---|---:|---:|
| Ortak hesap | 8 | 8 |
| Çalışma planı | 3 | 3 |
| Çalışma kaydı | 4 | 4 |
| Tamamlanmış kayıt | 4 | 4 |
| Kaydedilmiş toplam dakika | 86 | 86 |
| Coaching öğrenci profili | yok | 1 |
| Coaching öğretmen profili | yok | 1 |
| Coaching öğretmen–öğrenci ataması | yok | 1 |
| Profil UserId'sine çözülemeyen atama | — | 0 |

Geçiş sırasında mevcut iş tablolarının satır sayıları ve sıralanmış satır içerik özetleri
eşit çıktı. Audit/outbox/inbox ve yeni tablolar karşılaştırmadan ayrı tutuldu. İki açık
istisna: Identity açılış seed'inin yenilediği `Configurations.UpdatedAt` ve derslere
eklenen yeni `CurriculumVersionId`. Yapılandırmanın değerleri/metaverisi ayrıca aynı
bulundu. Hesap, rol, mevcut ilişki ve çalışma alanlarında toplu bir istisna uygulanmadı.

Canlı tarayıcı testi bittikten sonra plan ve çalışma tablolarının **tüm satırları** ilk
üretim yedeği öncesindeki özetlerle yeniden karşılaştırıldı: birebir aynı. Yeni taslak
kaydedilmedi, plan yayımlanmadı ve tamamlanmış çalışma yeniden yazılmadı.

## Katalog hazırlığı

Canlıdaki mevcut `approved-catalog` kaynağının 169 aktif dersi, yeni içerik oluşturmadan
`coaching-approved-catalog-2026-10` sürümüne bağlandı ve yayımlandı. Görünen ad:
**Koçluk onaylı ders kataloğu**, etiket **2026.10**, eğitim yılı **2026–2027**.

Bu, uygulamanın mevcut onaylı katalog sürümüdür; güncel resmî MEB müfredat doğrulaması
iddiası değildir. 5.220 konu korundu. Bu konuların tahmini dakika alanlarının tamamı
eskiden olduğu gibi boş; süre uydurulmadı. Öğrenci önizlemede açık dakika giriyor.

## Klon veritabanı provası

Canlıya dokunmadan iki üretim DB'si ayrı adlara restore edildi. Ayrı RabbitMQ, Redis ve
rastgele JWT anahtarı kullanıldı; prova servislerine dış port açılmadı. Migrations,
profil aktarımının önizlemesi, uygulaması ve tekrar uygulaması çalıştırıldı. Eski iş
verileri karşılaştırması geçti. Gerçek HTTP/API ve PostgreSQL ile şu sekiz senaryo geçti:

- Profilin ortak hesap UserId'siyle okunması.
- Kurulum tamamlanmadan eski planlara erişim.
- Kurulumu kaydetme ve yeni istekle kaldığı yerden devam.
- Müsaitlik kaydı.
- Bağlama uygun ders ve konu beyanı kaydı.
- 30 dakikalık gerçek plan önizlemesi.
- Üç eski plan ve dört tamamlanmış kaydın değişmemesi.
- Taşınan atamanın öğretmenin öğrenci listesinde UserId ile bulunması.

Yeni iki arayüz imajı ayrıca ağsız, port yayımlanmayan geçici konteynerlerde başlatıldı;
HTTP açılış kontrolleri geçti. Prova konteynerleri ve iki klon DB başarı sonunda kaldırıldı.

Prova sırasında geçici RabbitMQ dosya izni ve yardımcı doğrulama aracındaki sürüm/SQL/
yanıt biçimi hataları düzeltildi. Son başarılı prova paketi r6'dır; uygulama payload'ı
bu operasyon düzeltmeleri boyunca değişmedi. Bu başarısız denemeler üretim DB'lerinde
çalıştırılmadı. Ayrıntılı hata günlükleri sır içerebileceğinden rapora taşınmadı.
İmajların derlendiği r2 ile başarılı r6 paketinin 317 uygulama payload dosyasının hash'leri
ayrıca karşılaştırıldı; tamamı aynı.

## Canlı tarayıcı doğrulaması

Kullanıcının normal giriş yaptığı test öğrenci hesabı kullanıldı. CAPTCHA, parola,
MFA veya rol ataması değiştirilmedi. Test, yeni hesaba kayıt olmayı değil, mevcut
hesabın yeni kurulum özelliğini ilk kez kullanmasını doğrular.

| Adım | Canlı sonuç |
|---|---|
| Ana sayfayı yeni sürümle yenileme | Yeni kurulum kartı ve mevcut tamamlanmış günlük plan birlikte göründü. |
| Taşınan eğitim profili | Öğrenci ve 8. sınıf doğru geldi; okul/doğum tarihi zorunlu sorulmadı. |
| Kurulum eksikken plan erişimi | Üç plan listelendi; aktif ve arşiv plan açıldı. |
| Çalışma amacı | 2026 eğitim yılı, LGS, 2027 sınav yılı kaydedildi. |
| Yarıda bırakma/devam | Ana sayfaya dönüldü, tam sayfa yenilendi, “Kuruluma devam et” müsaitlik adımını açtı. LGS ve 2027 kaydı korundu. |
| Müsaitlik | Önceden aralık boştu. Test hesabına Pazartesi 17:00–18:00 kaydedildi. |
| Ders kataloğu | Yalnız altı LGS dersi listelendi; konu/süre eksikliği bilgileri göründü. |
| Ders tercihi | Matematik, öncelik 1 kaydedildi. |
| Konu beyanı | Çarpanlar ve Katlar → “Tekrar etmek istiyorum”; başlangıç gözden geçirme kaydedildi. |
| Süre doğrulaması | Süre boşken önizleme engellendi ve açıklama gösterildi. |
| Gerçek önizleme | 19.10.2026 başlangıç, 7 gün, 30 dakika: Pazartesiye 30 dakika yerleştirildi; kalan kapasite 30 dakika. |
| Aktif planı koruma | “Henüz kaydedilmedi. Aktif planın değişmedi.” mesajı görüldü; yeni taslak/yayın yapılmadı. |
| Yeni sekmede devam | Kaydedilen kurulum doğrudan hazır/önizleme adımında açıldı. |
| Eski kayıtlar | Aktif planda 12 ve 17 dakika, arşiv planda 40 dakika/24 soru görüldü; plan/çalışma DB özetleri aynı kaldı. |

Test hesabındaki yeni kalıcı veriler: bir eğitim bağlamı, bir ders tercihi, bir konu
beyanı ve bir haftalık müsaitlik aralığıdır. Bunlar test tercihidir; gerçek öğrenci
ölçümü sayılmaz. Kaydedilmeyen plan önizlemesi yeniden yüklemede saklanmaz; kurulum
tercihleri saklanır.

Dar ekran akışı ve yeni sekmede geniş ekran okuma doğrulandı. İlk sekmede önizlemeden
ayrılırken tarayıcı kontrol aracı yanıt vermedi; aynı oturumda yeni sekme açılarak
kurulumun kalıcılığı ve plan erişimi tamamlandı. Bu durum uygulama hatası olarak
kanıtlanmadı; kesintisiz tarayıcı otomasyon koşusu iddiası yoktur.

Yerel ekran kanıtları `artifacts/coaching-learning-live-20261016/` altında:
`live-home-new.jpg`, `live-owned-profile.jpg`, `live-purpose-resumed.jpg`,
`live-preview-result.jpg`, `live-old-plan-after-setup.jpg`,
`live-setup-restored-desktop.jpg`. Bu dizin kaynak kontrolü dışındadır.

## İmajlar ve geri dönüş

| Servis | Yeni SHA256 imaj kimliği |
|---|---|
| Identity | `b9db67ad64fa9d665ae646304ed40de1f7675c5070171231d48d78b79dc7df37` |
| Coaching | `a03bd6a5d248cf1bf906f16f16ef01b8bf7477fc6a9198162f1a34906e886cf2` |
| Öğrenci/yönetici | `e9feb4fc096236c0bb8b2b024cd1c78b85d0bc3cea28e05b2b17d6618fc9a491` |
| Personel | `3c2266ae0af34601533d10df2b1bd45d808cb699de2df674b8b6b52dcb306e0c` |

Önceki dört imaj, özel rollback compose dosyası ve DB yedekleri
`/var/lib/eduivme/releases/coaching-learning-20261016` altında korunuyor. Uygulama
geri dönüşü bu dört imajla yapılabilir; yeni tablolar silinmez ve otomatik Down/restore
çalıştırılmaz. Geri dönüş tetikleyicileri: readiness kaybı, yeniden başlatma döngüsü,
kurulum/plan erişimi hatası veya eski çalışma özetlerinin değişmesi.

## Test dayanağı ve sınırlar

Önceki uygulama doğrulaması: 760 backend, 59 öğrenci/yönetici ve 22 personel testi;
API fixture kullanan 4 masaüstü/mobil tarayıcı senaryosu geçti. Gerçek PostgreSQL
geçiş/koruma testi dahil. Bu yayında bunlara üretim kopyası üzerindeki gerçek servis
provası, izole üretim build'leri ve yukarıdaki canlı kullanıcı akışı eklendi.

Bu canlı turda yeni hesap kaydı, gerçek öğretmen hesabıyla tarayıcı girişi, bütün puan
türleri ve yeni plan yayımlayıp çalışma tamamlama işlemi yapılmadı. Öğretmen ilişkisi
gerçek klon API testinde; yeni profil/rol/kapsam koşulları uygulama testlerinde doğrulandı.
Eski tamamlanmış kayıtların korunması hem canlı UI hem DB karşılaştırmasıyla kontrol edildi.

Katalog süreleri için editör çalışması ve hedef oluşturma/konu öneri geliştirmesi sonraki
paketlerdir. Eski PKCS paket ve Angular bütçe uyarıları bu yayınla değiştirilmedi.

## Dağıtım sonrası gözlem

En az 15 dakika boyunca 30 saniye aralıkla 31 örnek alındı. Dört konteynerin 124 durum
kontrolü başarılı; yeniden başlatma sayıları sıfır. Giriş, kurulum ve personel genel
sayfalarında normal yönlendirmeler izlenerek yapılan 93 HTTP kontrolünün tamamı 200
döndü; en uzun ölçülen yanıt yaklaşık 0,155 saniye. Son readiness ve imaj kontrolleri
de geçti. Bu sentetik gözlem gerçek trafik hata oranı analizi yerine geçmez.
