# Serbest Okuma: sorusuz bağımsız pratik

Bu paket 13 mevcut FreeReading kaydını günceller ve eksik yedi kaydı ekler. Dört yaş grubu × beş seviye için toplam 20 kayıt ve hücre başına beş metin seçeneği vardır. Yaş grubu kimlikleri mevcut katalogla aynıdır.

| Seviye | Çocuk: kelime / dakika | Genç: kelime / dakika | Genç yetişkin: kelime / dakika | Yetişkin: kelime / dakika |
|---|---|---|---|---|
| 1 | 150–200 / 2 | 200–300 / 2 | 300–400 / 2–3 | 350–450 / 2–3 |
| 2 | 200–275 / 3 | 300–400 / 3 | 400–550 / 3–4 | 450–600 / 3–4 |
| 3 | 275–350 / 3–4 | 400–500 / 3–4 | 550–700 / 4–5 | 600–800 / 4–5 |
| 4 | 350–450 / 4–5 | 500–650 / 4–5 | 700–850 / 5–6 | 800–1000 / 5–6 |
| 5 | 450–550 / 5–6 | 650–800 / 5–6 | 850–1000 / 6–7 | 1000–1200 / 6–7 |

Süreler öneridir: hız hedefi, bilimsel yaş normu veya otomatik bitirme sınırı değildir. Normal oturumda maksimum süre yoktur; 10 saniyelik alt sınır yalnız yanlışlıkla bitirmeyi azaltır. Okumayı kanıtlamaz. Önizlemede özel süre ayarları ayrıca denenebilir.

## İçerik

`content.json` 100 ayrı kapsamlı metin kaydı içerir; 100 bağımsız hikâye değildir. Aynı özgün kaynak hikâyenin paragraf sonlarında tamamlanan, seviye uzunluğuna uygun seçkileri kullanılır. Çocuk/genç kaynakları mevcut Grouping ve Guided Reading paketlerinden alınır. Uzun yaş grupları için üç özgün devam metni bu pakette bulunur; iki uzun kaynak Comprehension paketindendir. Kaynak yolu ve SHA-256 her kayıtta yer alır. Hücre içinde beş farklı içerik vardır; seviyeler arasında aynı hikâyenin daha uzun seçkisi gelebilir.

Mevcut kaynak metinlere, paylaşılan sorulara ve başka egzersizlerin kayıtlarına dokunulmaz. Yeni metinler yalnız ilgili FreeReading egzersiz kimliğine bağlıdır. Yaş/seviye/uzunluk filtreleri katıdır; uygun metin yoksa örnek veya başka yaş/seviye metnine düşülmez. Kullanıcının iki okuma geçmişindeki kullanım sayıları üzerinden az kullanılanlardan rastgele seçim yapılır. Önizlemenin yerel kullanım geçmişi ayrı tutulur; sunucu kaydı oluşturmaz.

## Sonuçlar

Yeni normal oturumlar `readingPurpose: practice` ile sorusuz biter. Kelime sayısı, metin kimliği ve aktif süre kaydedilir. Duraklatmalar çıkarılarak hesaplanan tempo yalnız okumayı bitirme beyanına dayanır; anlama doğruluğu, puan, ağırlıklı hız, XP, okuma başarımı veya seviye tespit ölçümü değildir. Geçerli tamamlanma günlük görev ve ödev akışında kullanılabilir. Eski evaluation/assessment snapshotları değiştirilmez; varsa soruları ve değerlendirme davranışı korunur.

## Üretim geçişi (bu çalışmada uygulanmadı)

1. Yalnız Hızlı Okuma DB yedeğini alıp geri yüklenebilirliğini doğrulayın; API'yi kontrollü durdurun.
2. `settings.json` eski yapılandırmalarını canlı envanterle karşılaştırın. Uyumsuzluğu otomatik zorlamayın.
3. `psql -v ON_ERROR_STOP=1 -f apply.sql` yalnız Hızlı Okuma veritabanında çalıştırılır. İşlem atomiktir ve tekrar çalıştırılabilir; katalog veya metin kimliği çakışmasında tüm işlem geri alınır.
4. API/frontend birlikte yayımlanır; yaş/seviye listesi, gerçek metin açılması, sorusuz sonuç ve günlük görev kontrol edilir.
5. Hata durumunda API kapalı tutulup doğrulanmış yayın öncesi yedek ve önceki uygulama sürümü geri yüklenir. Yeni kullanıcı geçmişini kalıcı silerek geri dönüş yapılmaz.

Yerel yeniden üretim: `node content-packs/free-reading/v1/generate.mjs`. Katalog dosyalarını günceller; mevcut eski yapılandırma tabanını `settings.json` içinde korur. Sayısal ürün ayarları ve metinlerin yaşa uygunluğu gerçek kullanıcı/pedagojik değerlendirmeyle ayrıca izlenmelidir.
