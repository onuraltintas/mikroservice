# Öğrenci günlük işleri ve öğretmen takip bağlamı

21 Ekim 2026. Geliştirme, yerel doğrulama ve canlı dağıtım tamamlandı. Aşağıdaki geliştirme testleri yerel kanıtlardır; gerçek hesap ve kayıt koruma sonuçları [canlı doğrulama raporunda](gunluk-isler-canli-20261021.md) bulunur.

## Öğrenci ana sayfası

Çalışma, son beş ödev ve kararları ayrı listelerde gösteren ana sayfa, **Günlük işlerim** bölümünü kullanır. Haftalık değerlendirme bağlantısı korunur; aynı kararlar ana sayfanın başka bölümünde tekrar listelenmez. Eski son-beş-ödev isteği ve bu kaldırılan bölüme özel kullanılmayan stiller kaldırıldı. Diğer plan/ödev/karar ekranları korunur.

| Filtre | Kapsam |
|---|---|
| Bugün | Bugünün henüz süresi geçmemiş, tamamlanmamış çalışmaları; teslim zamanı geçmemiş açık ödevleri; bugün son tarihli açık kararları |
| Yaklaşan | Yarından başlayarak sonraki yedi takvim günündeki açık işler |
| Geciken | Önceki günlerden kalan veya bugün bitiş saati geçmiş, henüz kapanmamış işler |

- Çalışmalar yalnız öğrencinin aktif planından gelir. Tamamlanan çalışmalar ve taslak/arşiv planın bekleyen işleri gösterilmez.
- Ödevlerde yalnız aktif, teslim edilmemiş Assigned/InProgress kayıtları gelir. Teslim edilmiş, değerlendirilmiş, tamamlanmış veya iptal edilmiş ödevler bekleyen listeye katılmaz.
- Kararlarda yalnız Planned/InProgress durumları gösterilir. Bağlı çalışma tamamlansa bile karar öğretmen tarafından ayrıca kapanmadıkça açık kalır.
- Çalışmalarda aktif planın saat dilimi, ödev ve kararlarda Türkiye saat dilimi kullanılır. Saat dilimi kartta görünür. Saati olmayan çalışma gün içinde bekler, ertesi gün gecikmiş olur. Saatli çalışma planlanan bitiş dakikasında, ödev son teslim anı geçildiğinde gecikir.
- Her kart ilgili çalışma ayrıntısına (`plan` + `task`), ödeve veya kararın haftasına (`week` + `decision`) gider.
- Üç filtrenin sayaçları sayfadaki satırlardan hesaplanmaz; sunucu tüm eşleşen kayıtları sayar. Liste 20 kayıtla sayfalanır ve tarihe göre sıralanır. İlgili işler kapanınca sayfa geçersiz kaldıysa son geçerli sayfa döner.
- Yenile düğmesi ve pencereye tekrar odaklanma verileri tazeler; ana sayfaya dönüşte bileşen yeniden okur. Bekleyen istek varken odak olayı ikinci istek başlatmaz. Filtre değişiminde önceki istek iptal edilir; hata/yetki kaybında eski satırlar ve sayaçlar temizlenir.
- Ortak tema değişkenleri, açık/koyu görünüm, mobil düzen ve merkezi toaster kullanılır. Yeni tarayıcı iletişim kutusu yoktur.

## Sunucu

Yeni salt okunur uç:

`GET /api/coaching/study-planning/daily-agenda?mode=today&pageNumber=1`

`mode`: `today`, `upcoming`, `overdue`. Öğrenci kimliği istemciden alınmaz; oturumdaki Student rolü ve aktif Coaching profili gerekir. Başka öğrencinin plan, ödev veya karar satırları sorguya dahil edilmez. Yanıt listeyi, üç toplamı, sayfa bilgisini ve okuma zamanını içerir.

Sayımlar ve liste aynı RepeatableRead işlemi içinde okunur; mevcut veritabanı yeniden deneme stratejisi kullanılır. Bütün yıllık plan tarayıcıya taşınmaz: sayaçlar SQL'de hesaplanır, her kaynaktan istenen sayfaya yetecek öndeki adaylar okunup birleştirilir. Ödev sıralamasında saniye hassasiyeti korunur.

**Yeni tablo, sütun, migration veya veri temizliği yoktur.** Uç kayıt oluşturmaz, tamamlamaz, taşımaz veya silmez. Yeni ekran mevcut tamamlama/teslim/karar akışlarına yönlendirir.

## Öğretmen takip merkezi

- Uygulanmış tarih, arama, tür ve sayfa bilgisi aynı öğretmen çalışma alanında korunur. Filtre formundaki henüz uygulanmamış metin de ayrıca korunur; sonuçların son uygulanmış filtrelere ait olduğu açıkça yazılır.
- Ayrıntıdan dönüşte veya başka öğretmen sekmesinden takip merkezine dönüşte sonuçlar ve sayaçlar yeniden sunucudan alınır. Önceki sonuç satırları önbellekten gösterilmez.
- Okuma hatasından sonra tekrar deneme aynı uygulanmış filtre ve sayfayı kullanır.
- Puanlama/teslim/karar kapanması nedeniyle son sayfa boşalırsa sunucu geçerli son sayfayı döndürür; sonraki yenileme bu sayfayı kullanır.
- Kaydedilmemiş plan, değerlendirme veya karar formu varken mevcut ayrılma korumaları sürer.
- Bağlam yalnız çalışma alanı belleğindedir; tarayıcı depolamasına öğrenci bilgisi yazılmaz. Yeni çalışma alanı/oturum önceki öğretmenin bağlamını devralmaz. Tam sayfa yenileme sonrası kalıcı filtre saklama bu paketin kapsamında değildir.

## Doğrulama

- **36 sunucu/PostgreSQL/HTTP testi geçti; başarısız veya atlanan yok.** Yeni okuma ucunun veri ayrımı, tüm kaynakların sayımı, sayfalama, yedi günlük sınır, tamamlanan/teslim edilen/kapanan kayıtların çıkması, saat ve saat dilimi kuralları dahil. Mevcut haftalık değerlendirme, bağlantılar, takip ve kayıt koruma regresyonları da bu gruptadır.
- Gerçek localhost Kestrel/MVC HTTP testi yeni rotada 200 yanıtını ve JSON sözleşmesini, anonim 401, öğretmen 403, hatalı mode/sayfa 400 yanıtlarını doğruladı. Kimlik doğrulama bu izole testte sentetik test şemasıdır; canlı giriş testi değildir.
- Geniş arayüz grubu: öğrenci **318**, öğretmen **285** başarılı. Son değişiklikler sonrası hedefli geliştirme grubu: öğrenci **129**, öğretmen **128** başarılı. Gruplar örtüşür; sayılar farklı senaryolar olarak toplanmamalıdır. Çalışma bağlantısı için mevcut yönlendirme testleri de sürekli regresyon komutuna eklendi.
- Yeni öğrenci bileşeni/servisi: V8 satır kapsamı **%100**, dal **%90**; ilgili öğretmen takip/çalışma alanı dosyaları: satır **%100**, dal **%91,58**. Yeni sunucu dosyasının ilgili sınıflarında satır kapsamı **%100** ölçüldü.
- Coaching API Release derlemesi sıfır uyarı/hata ile, iki portalın üretim derlemesi başarıyla geçti. Arayüzlerde mevcut paket/stil bütçe ve ilgisiz şablon uyarıları devam ediyor; derleme hatası yok.
- Yeni sunucu testinde okuma öncesi/sonrası plan, çalışma, kazanım, karar ve teslim satırlarının JSON özetleri eşit kaldı. Ayrıca kazanım bağlı eski tamamlanmış çalışma yüklenip yeni işler kapatıldıktan sonra eski çalışmanın ve kazanım bağlantısının değişmediği tekrar doğrulandı. Durum geçişleri yalnız ayrılmış, test sonunda silinen yerel test veritabanlarında yapıldı.

Yerel tarayıcı örneğinde gerçek bileşenlerle üç filtre, üç kaydın doğru bağlantıları, örnek tamamlamadan sonra sayaç 3→2 ve kartın çıkması; öğretmende arama/tür/tarih ve ikinci sayfanın sekmeler arasında korunması doğrulandı. Örnek puanlamadan sonra 26→25 sayaç ve 2→1 geçerli sayfa dönüşü görüldü. Tarayıcı örneği sentetik servis yanıtları kullanır; veritabanı/HTTP davranışı yukarıdaki ayrı testlerde doğrulanmıştır. Canlı uçtan uca doğrulama olarak sunulmaz.

Öğrenci yeni bölümü açık/koyu masaüstü ve açık/koyu 390×844 mobilde incelendi. Öğretmen bölümünde koyu mobil/açık masaüstü kontrolü yapıldı. Sayfa düzeyinde yatay taşma görülmedi; öğrenci mobil filtreleri tek sırada ve en az 44 piksel yükseklikte kaldı. Denetim sonunda yalnız yerel test sekmesi kapatıldı, mevcut canlı sekmeler değiştirilmedi.

## Kanıtlar

- `artifacts/daily-agenda-backend-red.log`, `daily-agenda-ui-red.log`, `daily-tracking-ui-red.log`: eksik uygulamayı gösteren ilk testler.
- `artifacts/daily-agenda-backend-final.log`: 36 başarılı test.
- `artifacts/daily-agenda-http-and-data.log`: yeni ucun gerçek HTTP ve PostgreSQL kontrolleri; coverage eki.
- `artifacts/daily-agenda-preservation-final.log`: eski tamamlanan çalışma/kazanım korunmasını da içeren son 4 yeni uç testi.
- `artifacts/daily-agenda-panels-regression-final.log`: 318/285 geniş arayüz grubu.
- `artifacts/daily-agenda-development-final.log`: 129/128 hedefli geliştirme grubu.
- `artifacts/daily-agenda-routing-regression.log`: doğru çalışma ayrıntısına odaklanma dahil 5 yönlendirme testi.
- `artifacts/daily-agenda-ui-coverage.log`, `artifacts/daily-tracking-ui-coverage.log`: arayüz kapsamı.
- `artifacts/daily-agenda-student-production-final.log`, `artifacts/daily-agenda-teacher-production.log`: üretim derlemeleri.
- `artifacts/daily-agenda-api-production.log`: Coaching API Release derlemesi.
- `artifacts/daily-agenda-ui-proof.json`: yerel arayüz ölçümleri ve bağlantılar.
- `artifacts/daily-agenda-student-light-desktop.png`, `artifacts/daily-agenda-student-dark-mobile.png`: yeni öğrenci bölümü.
- `artifacts/daily-agenda-teacher-light-desktop.png`, `artifacts/daily-agenda-teacher-dark-mobile.png`: öğretmen bölümü.

Coaching API ve öğrenci/öğretmen arayüzleri birlikte yayımlandı; veritabanı geçişi gerekmedi. Üretim oturumlarında durum geçişleri ve filtreye dönüş akışı doğrulandı; ayrıntılar canlı rapordadır.
