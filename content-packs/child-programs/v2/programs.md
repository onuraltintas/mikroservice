# Çocuk eğitim programları v2

9–12 yaş için beş başlangıç seviyesinde 28 günlük program; her biri 182 görev.
Planın tek kaynağı `infrastructure/data/child-program-plan-v2.json` dosyasıdır.

| Hafta | Günlük görev | Dikkat/görsel algı görevi | Okuma/anlama/kelime görevi |
|---|---:|---:|---:|
| 1 | 6 | 4 | 2 |
| 2 | 6 | 4 | 2 |
| 3 | 7 | 3 | 4 |
| 4 | 7 | 2 | 5 |

Aynı egzersiz aynı günde tekrar edebilir; her tekrar ayrı sıra numarası, oturum ve sonuç kaydıdır.
Görev sayısı çalışma süresi garantisi değildir; gerçek süre egzersiz ayarlarına ve öğrencinin yanıtlarına bağlıdır.
Görsel egzersizler ekran üzerindeki dikkat ve algı çalışmalarıdır; fiziksel göz kası gelişimi veya klinik tedavi iddiası taşımaz.

Takvim haftası tek başına seviye artırmaz (`WeeksPerDifficultyIncrease=0`).
Mevcut ölçüme dayalı kişiselleştirme mekanizması korunur; sabit atanmış görevler kayıtlı egzersiz ayarlarıyla çalışır.
Katalogda olmayan düzeyler zorlanmaz: Gruplama en fazla 4; seviye 4 Serbest Okuma için mevcut seviye 3 kullanılır.
Seviye tespit programı, ölçüm eşikleri ve egzersiz kataloğu değiştirilmez.

## Geçiş ve geri dönüş

1. Hızlı Okuma DB yedeğini al; uygulamanın görev sırasını destekleyen sürümünü yayımla.
2. `AddDailyTaskSlotOrder` migration'ını uygula. Diğer servislerin DB'lerine dokunulmaz.
3. JSON planını `plan_json` psql değişkeni olarak vererek `apply.sql` çalıştır.
4. Yalnız belirlenmiş beş eski program fiziksel silinir. İlişkili ilerleme varsa işlem durur; geçmiş silinmez.
5. Yeni beş programın 28 gün/182 görev olduğunu ve korunan seviye tespit programını doğrula.

Script yeniden çalıştırılabilir; beklenmeyen içerik veya eksik kaynakta bütün işlem geri alınır.
Tekrarlı görev sonuçları oluştuktan sonra şemayı eski benzersizlik kuralına döndürmek güvenli değildir.
Migration `Down` bu durumda durur. Geri dönüşte sonuçları koruyan ileri şema bırakılır; içerik ve uygulama geri dönüşü ayrı değerlendirilir.
