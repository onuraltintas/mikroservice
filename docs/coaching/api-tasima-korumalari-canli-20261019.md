# Canlı API koruma testleri — 19.10.2026

Aktif plan: `a4cea155-5485-4db6-9f52-1ec4df8080c3`. Geçerli plan sürümü: **3**.

## Yöntem

Testler atanmış öğretmenin açık ve yetkili Chrome oturumunda yapıldı. Gerçek HTTP PUT istekleri `https://onuraltintas.net/api/coaching/staff/students/{studentId}/study/plans/{planId}` uçlarına ulaştı. Yeni erişim belirteci üretilmedi; mevcut öğretmen isteğinin yetkilendirme başlıkları değiştirilmedi ve dışarı yazılmadı.

Arayüzdeki çakışma/tamamlanma engelinden bağımsız sunucu testine ulaşmak için, yalnız bu planın calendar-move XHR/Fetch isteği geçici CDP yakalamasıyla tutuldu. Gövde negatif test senaryosuna dönüştürülerek sunucuya devam ettirildi. Üçüncü testte aynı origin ve plan kapsamındaki gerçek title uç noktası kullanıldı. Yanıtlar üretilmedi veya taklit edilmedi; gerçek Network.responseReceived ve Network.getResponseBody sonuçları okundu. Hiçbir istek 401/403, eski sürüm veya eksik parametre nedeniyle reddedilmedi.

Başlangıçta önerilen geçerli Fen isteği expectedVersion=3, görev `0f302186-f12b-474d-bdae-d4cc74edd68c`, 20.10.2026 17.30 idi; yakalanan istek sunucuya bu geçerli taşıma şeklinde gönderilmedi. Yalnız aşağıdaki geçersiz gövdeler gönderildi.

## Sonuçlar

| Deneme | Gerçek uç | Gövde özeti | Sunucu yanıtı |
| --- | --- | --- | --- |
| Dolu saate taşıma | PUT `.../calendar-move` | Fen görevi; expectedVersion=3; 20.10.2026; startMinute=1080 (18.00, Matematik dolu) | **409**, `StudyPlanning.Conflict`: Bu saat başka bir çalışmayla çakışıyor. Boş bir saat seçin. |
| Tamamlanan çalışmayı taşıma | PUT `.../calendar-move` | Tamamlanan `294578cc-e8e0-431d-a25b-a0a0d1674ca5`; expectedVersion=3; 20.10.2026; startMinute=1200 (20.00, boş) | **409**, `StudyPlanning.Conflict`: Tamamlanmış çalışmalar taşınamaz. |
| Tamamlanan çalışmanın başlığını değiştirme | PUT `.../tasks/294578cc-e8e0-431d-a25b-a0a0d1674ca5/title` | expectedVersion=3; title=Kader ve Kaza İnancı · API koruma testi | **409**, `StudyPlanning.Conflict`: Tamamlanan çalışmanın bilgileri değiştirilemez. |

Üç yanıtta da `success=false`. Sunucu mesajları doğrudan ilgili iş kuralını doğruluyor; eski plan sürümü reddiyle karıştırılmıyor. Test başlığı hiçbir kayda yazılmadı.

## Koruma kontrolü

Tüm HTTP denemelerinden önce ve sonra aynı salt okunur canlı özet alındı. Çıktılar birebir aynı:

- Plan kimliği, aktifliği, verisi ve sürümü aynı; sürüm **3** kaldı.
- **560 görevin tamamı aynı**. Bekleyen Fen, 20.10.2026 17.00–17.30’da kaldı.
- Tamamlanan Kader ve Kaza İnancı, tarih/saat, **55 gerçek dakika / 20 soru**, kazanım ve tamamlanma bilgileriyle aynı.
- **560 çalışma–kazanım bağlantısı aynı**.
- Toplam 11970 planlanan dakika aynı.
- **4 bildirim aynı**; reddedilen istekler yeni plan değişikliği bildirimi üretmedi.

Geçici Fetch desenleri boşaltıldı ve Network gözlemi kapatıldı; tarayıcıda istek değiştirme kuralı bırakılmadı. Uygulama/veritabanı kodu değişmedi, dağıtım gerekmedi.

Arındırılmış HTTP gövdeleri/yanıtları: `artifacts/coaching-api-guards-20261019/http-results.json`. Token, çerez veya istek başlıkları bu dosyada bulunmaz. Önce/sonra ve otomatik sonuç: aynı dizindeki before.txt, after.txt, assertions.json. Salt okunur karşılaştırma: `tools/releases/coaching-move-e2e-20261019/snapshot.sh`.

**Sonuç:** Arayüz engeli aşılarak yapılan üç canlı API denemesi de sunucuda reddedildi; mevcut alan kayıtları değişmedi.
