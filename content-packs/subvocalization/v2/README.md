# İç Ses Azaltma v2

Yerel uygulama ve yayın paketi. Bu paket canlı veritabanına henüz uygulanmadı.

## Kapsam

| Yaş grubu | Seviyeler | Gösterim temposu (kelime/dk) | Hedef okuma süresi |
|---|---|---|---|
| Çocuk (9–12) | 4–5 | 160 / 180 | 60 / 75 saniye |
| Genç (13–16) | 3–5 | 210 / 240 / 270 | 120 saniye |
| Genç yetişkin (17–21) | 2–5 | 210 / 240 / 270 / 300 | 120 saniye |
| Yetişkin (22+) | 2–5 | 230 / 260 / 290 / 320 | 120 saniye |

Bu değerler pilot başlangıç ayarlarıdır; yaş normu veya kanıtlanmış öğrenme hedefi değildir. Metin bandı, tempo × hedef süre üzerinden ±%10'dur; metin tam paragraflarla seçilir ve süre doldu diye kesilmez. Soruları yanıtlama süresi hedef okuma süresine dahil değildir.

- Her okumada hafif sesle “bir–iki–üç–dört”; görsel ritim 60 BPM, her saniyede bir sayı. Mikrofon kullanılmaz ve sayı sayma doğrulanmaz.
- Sabit okuma temposu; otomatik zorluk değişimi yok. Seviye 2–4'te iki, seviye 5'te üç kelimelik gruplar. Gruplar satır/cümle sınırlarını aşmaz; metnin tamamı görünür kalır.
- Duraklatma ritmi de durdurur; devamda ritim birden başlar. Anlama sorularında ritim ve sayı sayma biter.
- Beş geçerli soru; kayıtlı oturumda cevap anahtarı istemciye verilmez ve değerlendirme sunucudadır. Önizleme puanlanmaz ve kaydedilmez.
- Gösterim temposu gerçek okuma hızı değildir. İç ses azalması ya da kalıcı gelişim ölçülmez; sayı sayma anlamayı zorlaştırabilir.

## İçerik ve programlar

13 ayar: mevcut 6 kayıt güncellenir, 7 kayıt eklenir. 39 metin kaydı ve 195 soru vardır; her ayar için üç farklı metin bulunur. Bunlar Gruplama paketindeki üç özgün hikâyenin tam paragraf seçkileridir. Hikâye ve sorular başka seviyelerde/egzersizlerde tekrar kullanılabilir; bağımsız öğrenme/transfer ölçümü değildir.

13 uygun 28 günlük programın 23. ve 28. gününde bir okuma görevi yerine bu egzersiz konur. Günlük 6–7 görev ve toplam 182 görev korunur; Regresyon v2'nin 21. ve 26. gün görevleri değişmez. Önceden oluşturulan öğrenci programı takvimleri, oturum anlık görüntüleri ve sonuçlar değiştirilmez.

## Üretim ve yayın sırası

1. `node content-packs/subvocalization/v2/generate.mjs` ile üret; `node --test tests/E2E/subvocalization-catalog-v2.test.mjs` ile doğrula. Üretici tekrar çalıştırıldığında eski yapılandırma tabanını korur.
2. Hızlı Okuma veritabanını yedekle; beklenen mevcut altı ayarı, 13 programı ve aktif egzersiz türünü doğrula. Önce yaş grubu program v2 paketleri ve **Regresyon v2** uygulanmış olmalıdır. Etkilenen eski aktif öğrenci programında `ScheduleJson` yoksa paket işlemi iptal eder. Bu takvimleri eski şablondan mevcut takvim oluşturucuyla kaydet, sonra yeniden dene; yeni şablondan geriye dönük takvim oluşturma veya öğrenci geçmişini silme.
3. API ve frontend uyumlu sürümünü yayımla. Yeni program/oturum başlangıçlarını kısa bakım penceresinde durdurup çalışan başlangıç isteklerini bitir. Tablo yazma kilidi, program şablonunu okuyan eski istekleri tek başına engellemez.
4. Yalnız Hızlı Okuma DB'de `apply.sql` çalıştır. Tek işlem içinde kilit, eski/yeni yapılandırma karşılaştırması, çakışma ve uygun içerik kontrolleri vardır. Beklenmeyen düzenlemelerde tüm işlem geri alınır; başarılı tekrar çalıştırma sürümleri artırmaz.
5. 13 ayar / 39 metin / 195 soru / 13 programı, yeni oturumda yaş-seviye-band ve 5 soruyu kontrol et; bakım penceresini aç.

Yeni şema migration'ı gerekmez. Identity ve Koçluk veritabanları değişmez; hiçbir kullanıcı geçmişi silinmez. Geri dönüşte bakım penceresinde yedekten ilgili tabloları geri yükle; uygulama sürümünü geri al. Yeni oturum/sonuçlar oluşmuşsa yedeği tüm DB üzerine körlemesine yükleme; kimlik bazlı geri dönüş planını kullan.

## Doğrulama

Gerçek PostgreSQL testleri yalnız geçici Testcontainers veritabanını yeniden oluşturur. `SpeedReadingSubvocalizationCatalogPostgresTests` ilk uygulama/tekrar uygulama, değiştirilmiş katalog/program/soru karşısında atomik iptal, mevcut öğrenci takvimlerinin korunması, yeni 182 görevlik takvim, beş sorunun sunucuda değerlendirilmesi ve çift sonuç kaydının önlenmesini kontrol eder.

Tarayıcı kontrolleri yerel frontend + kontrollü API yanıtlarıyla çalışır; gerçek canlı Identity oturumu değildir. Masaüstü/mobil yönerge, duraklatma/ritim, beş soruluk önizleme, kaydetmeme ve sunucu hata/yeniden deneme akışı test edilir.
