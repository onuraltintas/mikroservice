# Gruplama planı — yerel tamamlanma raporu

Tarih: 11 Ekim 2026. Başlangıç: `8209a99a`, dal: `codex/platform-hardening`. **GitHub'a gönderim ve canlı yayın yapılmadı.** Canlıda yalnız salt okunur şablon/ayar envanteri incelendi.

## Tamamlanan kapsam

- Dört yaş grubunda beş seviye: 20 ayar; mevcut 16 ayarın temposu/grup büyüklüğü korunup metin bandı tanımlandı, dört yeni seviye 5 eklendi.
- Cümle/paragraf sınırlarını koruyan sabit gruplar; kısa grup süresi gerçek kelime sayısıyla orantılı. Anlamsal gruplama iddiası yok.
- Yeni kayıtlı oturumlar grup sınırlarını/tempo/boyutu sunucuda snapshot olarak saklar. Eski sürümsüz oturumlar eski sabit grupları ve kesirli tempoyu korur; güncel katalogla yeniden oluşturulmaz.
- Gerçek metin seçimi: dil, yaş, seviye, egzersiz sahipliği, aktiflik ve kelime bandı. İçerik yoksa yedek metin gösterilmez; gerçek kelime sayısı da doğrulanır. Özel önizleme bandı atlayamaz, anlama sorusu üretmez.
- Her yaş/seviye için üç farklı seçki; toplam 60 egzersize bağlı metin kaydı. Üç özgün kaynak anlatıdan tam paragraf seçkileri üretilmiştir; bunlar 60 bağımsız eser veya psikometrik derecelendirme değildir.
- Dört mevcut seviye 5 program şablonunda yalnız Gruplama görevleri 4'ten 5'e yükseltilir: her birinde 16 görev. 28 gün/182 görev/günde 6–7 görev korunur. Eski öğrenci `ScheduleJson` kayıtları ve sonuçları silinmez/değiştirilmez.
- Sonuçlar: kelime sayısı, kanonik grup sayısı, tamamlanan gösterim, aktif süre ve tam sayı gösterim temposu. Anlama/doğruluk/ölçülmüş WPM `null`, XP 0; geçerli kayıtlı çalışma günlük görevi tamamlayabilir. Önizleme kayıt/ilerleme oluşturmaz.
- Uzun özel kelime grupları masaüstü ve mobilde taşmadan sarılır. Girişte gösterim temposu ile ölçülmüş okuma hızı ayrılır.
- .NET/JavaScript cümle sınırı, NEL/BOM ve sayı sonu kuralları için ortak örneklerle parite kontrol edildi.

## Doğrulama kanıtı

| Kontrol | Sonuç |
| --- | --- |
| Speed Reading C# birim testlerinin tamamı | 1063 geçti, 0 başarısız/atlanan |
| Hızlı Okuma frontend birim testlerinin tamamı | 756 geçti |
| Gerçek PostgreSQL Gruplama entegrasyonu | 4 geçti |
| PostgreSQL Guided Practice, Anlama, Tarama regresyonu | 11 geçti |
| Gruplama masaüstü/mobil tarayıcı akışları | 10 geçti |
| Ortak Guided Practice masaüstü/mobil akışları | 6 geçti |
| TypeScript `tsc --noEmit` | Geçti |
| Üretim frontend derlemesi | Geçti; mevcut büyük oynatıcı SCSS bütçe uyarısı sürüyor |
| Paket tekrar üretimi | 20/60/4; takip edilen çıktılarda fark yok |
| `git diff --check` | Temiz |

PostgreSQL testleri gerçek, geçici Docker veritabanıyla çalıştı: paket iki kez uygulanınca mükerrer kayıt/audit sürüm değişimi oluşmuyor; kaynak ayar değişmişse transaction bütünüyle reddediliyor. Her yaş/seviyede yeni oturum başlatıldı; dört şablondan üretilen 182 görevin 16'sı gerçek seviye 5 Gruplama egzersizine bağlandı. Öğrenci schedule snapshot'ları değişmeden kaldı.

Tarayıcı testlerinde gerçek Angular arayüzü/oynatıcı, kontrollü Identity/API yanıtlarıyla kullanıldı. Admin/öğretmen önizlemesi, öğrencinin yetki sınırı, kayıtlı tamamlanma, eski zaman aşımı, renk/sınır/paragraf, büyük özel grup ve mobil taşma kontrol edildi. **Bu, gerçek canlı Identity oturumuyla yayın sonrası kabul testi değildir.** İlk mobil testte taşma yeniden üretildi ve düzeltmeden sonra geçti. Aynı anda çalıştırılan iki E2E koşusunun yerel sunucu çakışması başarısız koşu doğurdu; nihai koşular tek sunucuyla sıralı çalıştırıldı ve geçti.

Frontend kapsamı: `grouping-text.ts` satır %93,75 / dal %94,11; `word-highlight.engine.ts` satır %94,24 / dal %87,06. Tüm uygulama satır kapsamı %62,60'tır; uygulama genelinde %100 veya %80 kapsama iddiası yoktur. C# kapsam toplama denemesi datacollector bağlantı zaman aşımıyla sonuçlandı; kapsam oranı doğrulanamadı. Aynı C# test hedefi kapsam toplayıcı olmadan tamamen geçti. Bu görevde kapsam toplayıcı bağımlılıkları değiştirilmedi. ESLint komutu projede tanımlı değildir; lint başarısı iddia edilmez.

Bağımsız C# ve frontend/SQL incelemelerinde kalan engelleyici bulgu yok. Çalışmalar RED/GREEN yerel commitleriyle korunmuştur; ilgisiz geçici/artifact dosyaları commit kapsamına alınmadı.

## Yayından önce/yayın sonrası kalanlar

1. GitHub/canlı için ayrı onay. Bu rapor yayın yapıldığını göstermez.
2. Speed Reading DB yedeği ve geri yükleme kontrolü, hedef satırların geri dönüş kopyası; API/frontend ve yalnız `content-packs/grouping/v1/apply.sql` kontrollü yayın.
3. Gerçek Identity oturumu ile canlı öğrenci tamamlanma ve admin/öğretmen önizleme kontrolü; yeni seviye 5 program kayıtlarının doğrulanması.
4. Eğitimciyle pilot tempo ve yaşa uygun içerik değerlendirmesi. Ayarlar onaylanan ürün planına uygundur; bilimsel yaş normları olarak doğrulanmış değildir.

Tam ayar matrisi ve yayın/geri dönüş talimatları: [İçerik paketi](../../content-packs/grouping/v1/README.md).
