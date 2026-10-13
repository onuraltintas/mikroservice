# Öğrenci koçluk paneli tasarım yenilemesi

Öğrenci paneli günlük çalışmaya odaklanan, mobilde rahat kullanılabilen bir çalışma alanı olarak yenilendi. Yayın yalnız `admin-panel` frontendini kapsar. Backend, veritabanı ve öğretmen/kurum frontend imajları bu tasarım kapsamında değiştirilmedi.

## Tasarım ve kullanım

- Ana sayfada ilk görünen bölüm günlük plan ve sıradaki çalışmadır. Öğrenci, görev kartından ilgili aktif planı tek tıklamayla açabilir.
- Günlük tamamlanma oranı yalnız o güne planlanan gerçek görevlerden hesaplanır. Süre yalnız girilmiş çalışma kayıtlarını toplar; boş gün, boş plan ve servis hatası birbirinden ayrılır.
- Ödevler, planlama, gelişim ve hesap bağlantıları üç menü grubunda toplandı. Mobilde dört sabit kısayol ve tüm bağlantıları içeren açılır menü bulunur.
- Teal/orman yeşili renkler, açık ve koyu tema, tutarlı kartlar, kaynak ve süre bilgileri, okunaklı formlar ve en az 44 px dokunma alanları kullanıldı.
- Mobil menü odak tuzağı, Escape ile kapanma, düğmeye odak dönüşü, içeriğe geçiş bağlantısı ve azaltılmış hareket tercihine uyum eklendi.
- Ortak plan/rapor/finans bileşenlerinin görsel kuralları öğrenci `.coaching-shell` alanıyla sınırlandı.
- Ödev listesinde Türkçe durum ve sayısal tarih/saat gösterimi uygulandı; gelişim özeti mobilde iki kolon kullanır. Güvenlik rozeti mobil alt menüyü örtmeden görünür tutulur.

Bu düzenlemelerin öğrencilerin giriş ve çalışma sıklığını artırması amaçlanır. Kullanım artışı henüz ölçülmedi; sahte puan, seri veya sıralama gösterilmez.

## Doğrulama

### Kaynak ve otomatik kontroller

- Ana değişiklik: `1213a786`; son mobil/sunum düzenlemeleri: `802d19ee`.
- Koçluk için 20 test dosyası, 139 test geçti. Son çalışma: `artifacts/coaching-student-design-unit-r2.log`.
- İzole production derlemesi başarılı: `artifacts/coaching-student-design-build-r2-final.log`. Baz kaynak, önceki doğrulanmış koçluk yayını `ac014834` olup yalnız öğrenci portalı değişiklikleri aktarıldı. Çalışma alanındaki yayımlanmamış kurs/auth değişiklikleri pakete alınmadı.
- Bazı SCSS dosyaları 4 kB uyarı bütçesini geçer; 8 kB hata sınırı altında kalır. Build başarısızlığı yoktur.
- TypeScript ve genel kod incelemeleri onaylandı. DateOnly gün kayması, menü odak dönüşü ve rollback imaj doğrulaması inceleme sırasında düzeltilip tekrar kontrol edildi.

### Yerel tarayıcı testleri

Gerçek Angular bileşenleri ayrı, geçici bir önizleme girişinde açıkça sentetik HTTP verileriyle çalıştırıldı. Bu kontrol gerçek kimlik doğrulama veya backend testi değildir. Önizleme girişi ve konfigürasyonu production derlemesinden kaldırıldı.

- Masaüstü ile 390 × 844 mobil görünümü, açık/koyu tema.
- Günlük kart → aktif plan → 35 dakika/16 soru kaydı → ana sayfada 2/3, %67 ve toplam 65 dakika güncellemesi.
- Boş plan ve servis hatasında ilerleme yüzdesi gösterilmemesi.
- Menüde bütün 11 hedefe erişim; Escape sonrası açma düğmesine odak dönüşü.
- Mobil ana sayfa ve planda belge genişliği 390 px; yatay taşma yok.

### Canlı tarayıcı testleri

Normal girişle `testogrenci@eduivme.com` hesabı kullanıldı; parola bu rapora kaydedilmez.

- Yeni ana sayfa, aktif plan bağlantısı ve önceden tamamlanan 40 dakika/24 soru kaydı doğrulandı.
- 14 Ekim günü planlanmış görev bulunmadığı için ana sayfa yüzde yerine boş gün durumunu gösterdi.
- Ödevler, çalışma saatleri, otomatik plan, gelişim, sınav sonuçları, seanslar, bildirimler, ücretler ve abonelik ekranları açıldı.
- Çalışma raporu gerçek kayıtlardan 1/1 tamamlanan iş, 45 planlanan/40 kaydedilen dakika ve 30 planlanan/24 kaydedilen soru gösterdi.
- Mobil menü ve odak dönüşü canlıda doğrulandı; abonelik erişimi 20.11.2026 tarihine kadar açık göründü.
- Bu tasarım testinde canlı çalışma, ücret veya ödeme kaydı değiştirilmedi.

## Dağıtım ve geri dönüş

Yayın yardımcıları: `tools/releases/coaching-student-design-20261013/`. Önceki imaj kimliği, gerçek ortam ve Compose yapılandırması sunucudaki erişimi kısıtlı release dizininde tutulur. Aday imaj port yayımlamadan ayrı konteynerde test edilir. Promotion yalnız `admin-panel` için `--no-deps` kullanır. Geri dönüşte eski imaj kimliği ve SSR sağlığı doğrulanmadan başarı bildirilmez.

İlk yayın: `/var/lib/eduivme/releases/coaching-student-design-20261013`, 13.10.2026 21:04:57 UTC. Son sunum paketi: `/var/lib/eduivme/releases/coaching-student-design-20261013-r2`.

Canlı sonuç ve son sağlık kontrolleri aşağıdaki yayın doğrulaması bölümünde kaydedilmiştir.

## Yayın doğrulaması

Son paket 13.10.2026 **21:12:06 UTC** (14.10.2026 00:12:06 Türkiye) tarihinde yayımlandı. Çalışan imaj: `sha256:42c0a7299ba76303b8e368480d172da17c3a19e6edaae9d8dcb8b6a592be5f09`; çalışma kullanıcısı `node`.

21:14:03 UTC son kontrolünde frontend çalışıyor, restart sayısı **0**, yayın sonrası frontend loglarında `ERROR|Exception|Unhandled` eşleşmesi **0**. Identity, Coaching ve Notification hazır olma kontrolleri **Healthy**. Öğrenci ve staff halka açık adresleri **HTTP 200**. Release helper diğer üretim konteynerlerinin kimliklerinin değişmediğini doğruladı.

Son paketle ödev kartı `Çalışmanı bekliyor` ve `14.10.2026 23:27` gösterdi. Mobil gelişim özeti iki kolonla görüntülendi. Çıkış/yeniden giriş ve sayfa yenileme başarılı; girişten kalan güvenlik rozeti mobilde `bottom: 86px` ile görünürken **Gelişim** kısayolu tıklanarak açıldı. Canlı tarayıcıda hata seviyesi konsol kaydı bulunmadı.

Yerel kanıtlar:

- `artifacts/coaching-student-design-20261013/live-health-final.log`
- `artifacts/coaching-student-design-20261013/live-page-checks.json`
- `artifacts/coaching-student-design-20261013/live-desktop-viewport.png`
- `artifacts/coaching-student-design-20261013/live-mobile-home.png`
- `artifacts/coaching-student-design-20261013/live-mobile-progress.png`

Canlı giriş: <https://eduivme.com/auth/login>. Öğrenci çalışma alanı: <https://eduivme.com/coaching-portal>.
