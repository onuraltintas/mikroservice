# Hedef ekranları: canlı dağıtım ve doğrulama

Tarih: 19 Ekim 2026. Kapsam: öğrenci ve atanmış öğretmen için resmî 2026 LGS okul kataloğunu arama, filtreleme ve mevcut akademik hedefe okul bağlama.

## Dağıtım

- API ve personel portalı: `coaching-targets-20261019`.
- Öğrenci portalı: `coaching-targets-20261019-r3` (kategori sözleşmesi, Türkçe etiketler ve hedef sayacı düzeltmeleri dahil).
- Kaynaklar önceki canlı sürümün yalıtılmış kopyalarından hazırlandı. Çalışma alanındaki ilgisiz ödev eki ve diğer ürün değişiklikleri pakete alınmadı.
- Yeni veritabanı geçişi çalıştırılmadı. `TargetAdmissions2026` geçişi ve onaylanmış LGS verileri önceki işlemde zaten uygulanmıştı.
- Coaching, Identity ve Notification yedekleri sunucudaki her sürüm dizininde tutuldu; üretim veritabanı yerel makineye indirilmedi. Dağıtım hatalarında önceki imajlara dönüş tanımlı.

## Canlı hesaplarda doğrulananlar

| Akış | Sonuç |
| --- | --- |
| Öğretmen, atanmış öğrenciyi seçerek ayrı test hedefi oluşturur | Başarılı |
| Öğretmen: SAMSUN + Bafra + Fen Lisesi + okul adı | Bafra Fen Lisesi, 2026 kontenjanı 120 |
| Öğretmen okul hedefini kaydeder, sunucudan ve sayfa yenilemesinden sonra yeniden açar | Bafra Fen Lisesi bağlantısı korunur |
| Öğrenci kendi test hedefini oluşturur, KAYSERİ + Kayseri Fen Lisesi ile arar | Doğrulanmış tek katalog kaydı, kontenjan 120 |
| Öğrenci okul hedefini bağlar ve sayfayı yeniler | Kayseri Fen Lisesi bağlantısı korunur |
| Öğrenci öğretmenin oluşturduğu hedefi açar | Bafra Fen Lisesi görünür; okul bağlantısı öğretmen yetkisinde salt okunurdur |
| Öğrenci: il + ilçe + kurum türü + eğitim dili + alan + öğretim biçimi | Bafra Fen Lisesi tek sonuç olarak döner |
| Öğretmen: İZMİR + MENEMEN + Özsoy | 2026 ana yerleştirme 441.2318 / %4.08 ve 1. nakil 442.6725 / %3.88 ayrı satırlar |
| En yüksek yüzdelik 4 filtresi | Yalnız %3.88 olan nakil sonucu kalır |
| Katalog kaydında puan yayımlanmamıştır | Puan uydurulmaz, yayımlanmadığı belirtilir |
| Kaynakta kontenjan yoktur | `Bilinmiyor` gösterilir, 0 yazılmaz |
| 2027 yılıyla sorgu | 2026 kayıtları karıştırılmaz; sonuç yok |
| 2026 tüm kayıtlar, ikinci sayfa | 274 doğrulanmış kayıt, ikinci sayfada 20 kayıt |
| Kaynak niteliği/eşleşmesi belirsiz satırlar | Aramaya açılmaz |

Kurum türü kaynakta boş olan bir kaydı `Fen Lisesi` türü filtresi dışarıda bırakır. Okul adından tür tahmin edilmez. Filtreyi `Tümü` yapmak bu kayıtların bulunmasını sağlar.

## Bulunan ve giderilen hatalar

1. Öğrenci hedef oluşturma isteği kategori numarası gönderdiği için API 400 dönüyordu. Öğretmen portalındakiyle aynı kategori adları gönderiliyor. Altı kategori regresyon testinde doğrulandı.
2. API'den dönen kategori adı öğrenci ekranında İngilizce kalıyordu. Güncel adlar ve eski sayısal kategoriler Türkçe görüntülenir.
3. Yeni hedef kaydedilince üstteki toplam sayısı yenilenmiyordu. Başarılı kayıttan sonra özet sunucudan yenilenir.

## Testler ve koruma

- Gerçek yerel PostgreSQL üzerinde 135 backend entegrasyon testi geçti.
- Yalıtılmış son sürüm ağacında 49 Angular testi geçti: katalog arama, hedef bağlama, öğrenci ilerleme ve portal servisleri.
- Kategori isteği için RED→GREEN; kategori etiketi ve özet yenilemesi için iki başarısız testten GREEN aşamasına geçildi.
- API, öğrenci ve personel üretim derlemeleri başarılı. Önceden mevcut CSS boyut uyarıları dışında derleme hatası yok.
- Her dağıtımın hemen öncesi ve sonrasında tüm korunan koçluk tablolarının sayım ve içerik özetleri aynı.
- Tarayıcı testlerinin ardından, eski 14 akademik hedef ve eski tekrar kayıt önleme kayıtları ayrı karşılaştırmada tamamen aynı kaldı. Diğer tüm korunan koçluk tabloları da aynı kaldı.
- İki açıkça etiketlenmiş yeni test hedefi ve bunlara ait iki idempotency kaydı eklendi. Mevcut planlara, çalışma kayıtlarına ve kazanım bağlantılarına yazılmadı.

## Veri kapsamı

Bu dağıtım Türkiye geneli LGS verisinin tamamlandığı anlamına gelmez. Aranabilir katalog 274 doğrulanmış **kayıt** içerir; bunlar tekil okul sayısı değildir. 147 belirsiz, 9 bekleyen ve 2 çelişkili satır arama dışında kalır. Kaynakta eksik olan alanlar korunur; eski veya aşaması belirsiz puanlarla tamamlanmaz.

## Kanıt dosyaları

- `artifacts/coaching-targets-20261019/source-provenance.json`
- `artifacts/coaching-targets-20261019/final-preservation.log`
- `artifacts/target-catalog-20261019/deploy-backend-tests.log`
- `artifacts/coaching-targets-20261019-r3/final-ui-tests.log`
- `artifacts/coaching-targets-20261019/student-saved-targets.png`

Sunucudaki yedekler ve dağıtım özetleri `/var/lib/eduivme/releases/coaching-targets-20261019*` dizinlerindedir; bu dosyalar erişimi sınırlı tutulur.
