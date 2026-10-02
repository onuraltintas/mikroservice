# Koçluk admin katalog yönetimi — yerel ilerleme

Bu kayıt yeni yerel çalışmayı anlatır; önceki listeleme/kullanım/kalıcı silme yayını ile karıştırılmamalıdır. Yeni değişiklikler GitHub'a gönderilmedi ve canlıya alınmadı.

## Tamamlanan geliştirmeler

- Beş katalog türünde oluşturma, düzenleme ve ayrı aktif/pasif işlemleri. Yeni kayıtlar pasif oluşturulur.
- Global yönetici kapsamı, ContentManage izni ve mevcut ayara bağlı Coaching MFA korunur.
- Kaynak kimlikleri değişmez; tarihsel ders/ünite/konu bağlantıları yeniden yazılmaz.
- Güncel kayıt parmak izi eski ekranla yazmayı engeller. İşlem gerekçesi ve denetim kaydı katalog değişikliğiyle aynı transaction'da kaydedilir.
- Aktif alt kayıt varken üst kayıt pasifleştirilemez; pasif üst kayda bağlı kayıt yayınlanamaz.
- Okul konumları Identity konum API'si üzerinden doğrulanır. Kaynak şehir/ilçe adları korunur.
- Oluşturma formunda aramalı ve sayfalı ders/ünite/üst konu seçimi vardır. Üst konu filtresi sayfalama öncesi sunucuda uygulanır. Değişen üst seçimlerin eski sonuçları temizlenir.
- Liste ekranında doğrulanmış şehir/ilçe filtreleri eklendi; il değişince ilçe ve bekleyen eski sorgu temizlenir.
- Dinamik form açılışı ekran okuyucuya duyurulur.

## Doğrulamalar

- Geniş seçili backend regresyonu: 240 geçti, 0 başarısız, 0 atlanan.
- Ardından belge/aktiflik yanıtı dahil controller seçimi: 8 geçti. Bu sayı geniş seçimle örtüşür; toplanmaz.
- Son geniş seçili admin arayüz regresyonu: 78 geçti.
- Son şehir/ilçe filtreleri dahil admin production derlemesi geçti. Önceden var olan Hızlı Okuma katalog CSS bütçesi uyarısı sürüyor; bu kapsamda değiştirilmedi.
- Gerçek disposable PostgreSQL üzerinde geçici yazma hatası ve commit cevabı kaybı dahil işlemler doğrulandı.
- C# yönetim servisi ve son konum liste filtresi arayüz incelemelerinde engelleyici bulgu kalmadı; TypeScript kontrolü geçti.
- Bunlar tam tarayıcı E2E veya ürün genelinde yüzde yüz kapsama iddiası değildir.

## Kalan işler

1. Liste ekranında ders/ünite ilişkisi filtreleri, son arayüz incelemesi ve üretim derlemesi tamamlandı; aşağıdaki son kontrol notuna bakın.
2. Gerçek API ile yönetim ekranı tarayıcı E2E kontrolleri.
3. Kontrollü aktarım önizlemesi, fark raporu, gerekçeli onay ve ayrı yayın. Kalıcı silinen kaynak kimliği eski dosyadan yeniden oluşturulmamalı.
4. Öğrenci detayında yeni müsaitlik, çalışma planı, görev ve revizyon geçmişi incelemesi.
5. Öğrenci detayında yeni hedef, sınav ve hedefe yaklaşma raporları.
6. Öğrenciye ilişkin yetkili gerekçeli düzeltme/arşivleme ve işlem geçmişi.
7. Tam kapsam regresyonu, yayın raporu ve ayrı canlı onayı.

Plan bütünü henüz tamamlanmadı. Bu yerel adım için yeni veritabanı migration'ı gerekmedi.

## Son yerel kontrol — hiyerarşi filtreleri ve aktarım güvenliği

- Ders/ünite listesi arama ve 25 kayıtlık sayfalama ile filtrelere bağlandı. Üst ders değişince ünite seçimi, seçenekleri ve bekleyen eski sorgu temizlenir. Aramada seçili seçenek korunur.
- Arayüz regresyonu: 79 geçti. Production derlemesi geçti; mevcut Speed Reading CSS bütçe uyarısı değişmedi. Salt okunur TypeScript/Angular incelemesinde ek bulgu yok.
- Eski kaynak dosyası kalıcı silinen kaydı yeniden oluşturamaz. Aynı tablo kilitleri altında kaynak/tür/kimlik denetimi yapılır; tüm aktarım reddedilir. Konu kimlik önekleri korunur.
- `PreviewAsync` aynı içerik/ilişki/kalıcı silme kontrollerini çalıştırıp eklenecek kayıt sayısını hesaplar; SaveChanges veya Commit çağırmaz. İçeriği değişmiş mevcut kaydı otomatik güncellemez.
- Gerçek disposable PostgreSQL üzerinde önizleme/aktarımı/silme ve operator regresyonu: 15 geçti, 0 başarısız, 0 atlanan. C# incelemesinde engelleyici bulgu yok.
- Henüz admin aktarım API/formu, ayrıntılı fark tablosu ve gerekçeli onay/yayın arayüzü yok. Önizleme temeli bunların tamamlandığı anlamına gelmez.
- Öğrenci müsaitlik/plan/revizyon/hedef/yeni rapor admin incelemeleri ve tam tarayıcı E2E hâlâ kalan işlerdir.
- Bu yeni işler yerelde commitlendi; GitHub'a veya canlıya aktarılmadı.
