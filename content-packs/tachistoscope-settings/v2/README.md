# Takistoskop — yaş/seviye ayarları v2

Bu paket yalnız `settings.json` içindeki 20 mevcut eğitim egzersizini günceller.
Dört seviye tespit egzersizi, program çizelgeleri, oturumlar ve sonuçlar değişmez.
v1 katalog dosyaları tarihsel kaynak olarak korunur. Yeni ayarlar yeni oturumlarda
kullanılır; mevcut oturumlar kendi başlangıç yapılandırmasıyla devam eder.

Hücreler **başlangıç basamak sayısı / tur sayısı / başlangıç gösterim süresi (ms)** şeklindedir.

| Yaş grubu | Seviye 1 | Seviye 2 | Seviye 3 | Seviye 4 | Seviye 5 |
|---|---|---|---|---|---|
| Çocuk 9–12 | 3 / 20 / 1500 | 3 / 25 / 1400 | 4 / 30 / 1300 | 4 / 35 / 1200 | 5 / 40 / 1100 |
| Genç 13–16 | 3 / 25 / 1000 | 4 / 30 / 900 | 4 / 35 / 800 | 5 / 40 / 700 | 6 / 45 / 600 |
| Genç yetişkin 17–21 | 4 / 30 / 800 | 4 / 35 / 700 | 5 / 40 / 600 | 5 / 45 / 550 | 6 / 50 / 500 |
| Yetişkin 22+ | 4 / 30 / 800 | 5 / 35 / 700 | 5 / 40 / 600 | 6 / 45 / 550 | 7 / 50 / 500 |

## Otomatik uyarlama

Eğitim ve önizlemede açıktır. Birbirinden bağımsız beş cevaplık bloklar değerlendirilir:

- 4–5 doğru: süre %10 azalır; 3 doğru: aynı kalır; 0–2 doğru: %10 artar.
- Süre zaten alt sınırdaysa başarılı blokta basamak sayısı bir artar; süre başlangıca döner.
- Süre zaten üst sınırdaysa başarısız blokta basamak sayısı bir azalır; süre başlangıca döner.
- Süre ve basamak sayısı aynı blokta birlikte zorlaştırılmaz. Eksik son blok uyarlama yapmaz.

| Yaş grubu | Basamak sınırı | Süre sınırı |
|---|---|---|
| Çocuk | 3–5 | 400–2000 ms |
| Genç | 3–6 | 300–1500 ms |
| Genç yetişkin | 4–6 | 250–1500 ms |
| Yetişkin | 4–7 | 250–1500 ms |

Admin/öğretmen özel önizlemede başlangıç basamak sayısını ve süreyi bu sınırlar
içinde seçebilir; otomatik uyarlama korunur. Önizleme eğitim kaydı oluşturmaz.
Kayıtlı oturumlarda doğru/yanlış ve her turun gerçekten uygulanan gösterim süresi
sunucu tarafında saklanır. Seviye tespitinde otomatik uyarlama kapalıdır.
Rastgele sayısal uyaranlar başında sıfır içerebilir; kelime uzunluğu ölçümü değildir.

Bu parametreler onaylanan ürün ayarlarıdır; akademik yaş normları veya okuma hızı
ölçümü olarak sunulmaz.

## Kontrollü uygulama

1. Eşleşen API ve frontend sürümlerini birlikte hazırlayın; testlerini çalıştırın.
2. Canlı yayın ayrıca onaylandıktan sonra Hızlı Okuma veritabanını yedekleyin.
3. Yeni kodu yayınlayın; katalog yazımlarını/servisi geçiş sırasında durdurun.
4. `apply.sql` dosyasını hata halinde duracak şekilde PostgreSQL'de çalıştırın.
5. Yirmi yapılandırmayı ve kapsam dışı kayıtların değişmediğini doğrulayın; servisi açın.

SQL tek transaction, tablo kilidi ve advisory lock kullanır. Kimlik, yaş, seviye,
aktiflik ve **tam** eski/yeni yapılandırma kontrolünden geçmeyen kaynakta durur.
İkinci uygulama audit/version dahil değişiklik yapmaz. Geri dönüşte yalnız bu
yirmi satırın yedeklenmiş yapılandırmaları kontrollü olarak geri alınmalıdır;
araya giren kullanıcı verilerini kaybettirecek otomatik tam veritabanı geri yüklemesi yapılmaz.
Bu paket tek başına çalıştırılmamalıdır: API/önizleme motoru değişikliklerini de gerektirir.
