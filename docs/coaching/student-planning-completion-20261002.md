# Koçluk öğrenci planlama, hedef ve rapor — tamamlanma raporu

Tarih: 2026-10-02. Bu belge yerel geliştirme/doğrulama anını kaydeder.
Sonraki canlı yayın ve doğrulama durumu: [canlı yayın raporu](student-planning-live-release-20261002.md).

## Tamamlanan kapsam

- Öğrenciye ait müsaitlik, saat dilimi ve sürüm kontrollü güncelleme.
- Manuel taslak/düzenleme/yayım, görev tamamlama, gerçek süre, tarih değiştirme ve geçmiş.
- Müsaitliğe göre deterministik otomatik önizleme/taslak; konu filtreleri ve kapasite açıklaması.
  Sabit işler ve tamamlanmış geçmiş korunur. Önizleme, taslak ve yayım ayrı adımlardır.
- Mevcut AcademicGoal üzerinde okul/üniversite bağlantısı, puan/tür/ölçek yapılandırması.
  Öğrenci sahipliği, öğretmen hedefinin salt okunurluğu ve beklenen sürüm korunur.
- Mevcut Exam/ExamResult üzerinde öğrenci beyanı ve ders/konu doğru/yanlış/boş girişi.
- Dönemlik plan, süre, konu, sınav ve ders soru dağılımı; güncel hedef ve otomatik puan raporu.
- Ana sayfa özeti, EF outbox, öğrenci dışa aktarma ve hesap silme kapsamı.
  Yeni puan ölçeği/sınav türü/ders hedefi dışa aktarmaya dahildir.
- Altı onaylı katalog dosyası için salt okunur ön kontrol, atomik/tekrarlanabilir pasif aktarım
  ve ayrı kontrollü yayın/yayından kaldırma aracı. İçerik uyuşmazlığı reddedilir;
  yayın aynı kilit/transaction içinde gerçek içeriği doğrular. Geçmiş silinmez.
- Notification'da silinen alıcı işareti ve eşzamanlılık koruması; bekleyen olay yeniden
  bildirim oluşturmaz. Kapsam kullanıcı tarafından yerel geliştirme için onaylanmıştır.

## Otomatik “hedefine yaklaşma” hesabı

`Puan hedefine erişim = min(100, son uyumlu sonuç / hedef puanı × 100)`.
Bir ondalığa yuvarlanır; `kalan puan = max(0, hedef − sonuç)`.
Örnek: 500 ölçeğinde 400 hedef, 320 sonuç → %80 erişim, 80 puan açık.

- Aynı sınav türü ve **aynı maksimum puan ölçeği** karşılaştırılır.
- Seçilen dönemdeki son sonuç kullanılır; öğrenci beyanı ve öğretmen kaydı ayrıdır.
- Ders hedefi toplam sınav puanıyla karşılaştırılmaz. Eksik yapılandırma/sonuç yokluğunda
  neden gösterilir; uydurma %0 üretilmez. Eski hedeflere ölçek tahmin edilmez.
- Öğrenme artışı, resmî sınav puanı veya yerleşme olasılığı değildir. Manuel ilerleme ve
  tamamlanma işareti otomatik değiştirilmez. Hedefler güncel durumdur, tarihsel görüntü değildir.

## Son doğrulama

| Kontrol | Sonuç |
|---|---|
| Planlama/hedef/rapor/katalog/dışa aktarma backend regresyonu | 147 geçti, 0 başarısız, 0 atlanan |
| Bildirim silme/teslim ve plan outbox/gizlilik regresyonları | 13 + 8 geçti, 0 başarısız |
| Koçluk öğrenci paneli ve oturum arayüz testleri | 99 geçti, 19 dosya |
| Tarayıcı E2E | 3 senaryo iki kez, 6/6 geçti |
| E2E ortam korumaları | 5 Node testi geçti |
| Angular production derlemesi / TypeScript | Başarılı / 0 tip hatası |
| Katalog aracı derlemesi | Başarılı, 0 uyarı, 0 hata |
| Yeni GoalScoreCalculator kapsamı | %100 satır ve %100 dal |

Kapsam oranı yalnız yeni hesaplayıcı içindir; tüm ürün kapsamı değildir. Seçimler arasında
ortak testler vardır; bu sayılar toplam ürün test sayısı olarak toplanmamalıdır.
Ayrı frontend lint scripti yoktur; lint çalıştırılmış sayılmaz. Angular'da önceden mevcut
Hızlı Okuma katalog stil bütçesi uyarısı devam eder.

Gerçek PostgreSQL'de puan ölçeği migration'ı, constraint ve geri alma koruması doğrulandı.
Yapılandırılmış hedef varsa Down alanı sessizce silmez; doğrulanmış yedek veya açık veri kararı gerekir.
Gerçek kataloglar yalnız geçici yerel PostgreSQL'e aktarıldı: **30.769 satır**; tekrar aktarım
**0** değişiklik; kontrollü yayın **30.769** satır. Kalıcı/canlı katalog etkinleştirilmedi.
Hiçbir özel öğrenci dosyası okunup aktarılmadı. Altı küçük sentetik katalog E2E için kullanıldı.

E2E gerçek Koçluk API/PostgreSQL kullanır. Oturum ve harici Identity sahiplik cevabı
yalnız loopback test taklididir; gerçek Google/Identity giriş veya canlı doğrulaması değildir.
Test ön kontrolü mutlak API assembly yolunu, tek açık geçici DB parametresini ve tek açık
loopback Identity parametresini ve o porttaki tam test betiği sürecini doğrular;
farklı/sonradan eklenen hedefleri reddeder.

## Canlıya geçiş için kalan işletim işleri

Bu kapsamda açık kod geliştirme maddesi bırakılmadı. Canlıya geçiş ayrı onay gerektirir:

1. Sürüm kapsamını, Koçluk/Notification yedeklerini ve ilgili migration sırasını doğrula.
2. Notification eski silme kayıtları için Identity istek → alıcı eşleştirmesini kontrol et;
   gerekiyorsa geçmiş silinmiş hesapların asgari işaret kayıtlarını tamamla.
3. Hakları onaylı katalogları ön kontrolden geçir; doğru Koçluk DB'ye pasif aktar,
   içerik doğrulaması ve ayrı yayın onayıyla etkinleştir.
4. İlgili API/Notification/öğrenci frontend sürümlerini kontrollü yayımla; gerçek oturum,
   ürün yetkisi, abonelik ve bildirimle canlı smoke testi yap.
5. Sağlık kontrollerini izle; sorun varsa doğrulanmış geri dönüş planını uygula.

VPS, canlı veritabanları, Identity/Hızlı Okuma ürün verileri ve GitHub bu turda değiştirilmedi.

## Katalog işletimi

`tools/Coaching.Catalog` bir HTTP endpoint'i veya başlangıç seed'i değildir. Varsayılan
işlem yalnız dosya ön kontrolüdür; DB'ye bağlanmaz. İçe aktarma/yayın için `--apply` ve
`--database` açık doğrulaması; yayın için ayrıca `--publication-authorized` gerekir.
Bağlantı yalnız `COACHING_CATALOG_CONNECTION` ortam değişkeninden alınır, çıktıya yazılmaz.
Araç migration uygulamaz. Veritabanı adı doğrulanır; operatör bağlantıdaki host/ortamı da
ayrıca doğrulamalıdır. Canlı bağlantı vermek başlı başına ayrı yetki gerektiren işlemdir.

Salt okunur örnek:

```powershell
dotnet run --project tools/Coaching.Catalog -- --directory <katalog-klasörü> --source <kaynak>
```

E2E API başlatma betiği `tests/E2E/support/start-coaching-planning-api.ps1`, yalnız
`E2E_DISPOSABLE_ENV=true` ile sabit loopback/geçici ortamı kullanır. Test anahtarları
gerçek ortamda kullanılmamalıdır; gerçek Identity girişini doğrulamaz.
