# Öğretmen çalışma taşıma ve öğrenci bildirimi — canlı test

Tarih: 19.10.2026 (Europe/Istanbul).

Aktif plan: `a4cea155-5485-4db6-9f52-1ec4df8080c3` — LGS çalışma planım · 19.10.2026.

## Gerçekleştirilen işlem

Öğretmenin açık Chrome oturumunda Çalışma takibi → İlgili planı aç → Takvim → Gün ve saat değiştir kullanıldı. Tamamlanan Kader ve Kaza İnancı çalışması taşıma seçim listesinde bulunmuyordu.

Bekleyen **Fen Bilimleri / Mevsimlerin Oluşumu** çalışması (`0f302186-f12b-474d-bdae-d4cc74edd68c`) taşındı:

- Önce: **19.10.2026 19.00–19.30**.
- Sonra: **20.10.2026 17.00–17.30**.
- Süre 30 dakika, kazanım `F.8.1.1.1`, süre sürümü 2 korundu.
- Hedef saat diğer derslerle çakışmıyor. Aynı günün Matematik 18.00 ve İnkılap 19.00 çalışmaları yerinde kaldı.

Kayıt doğrudan veritabanı yazımıyla yapılmadı; öğretmenin normal tarayıcı formundan kaydedildi. Öğretmen takviminde yeni gün ve saat görüldü.

## Öğrenci doğrulaması

Öğrenci planı tamamen yeniden yüklendi. Gün takvimi bir sonraki bekleyen çalışma olan 20 Ekim’e odaklandı. Fen kartında 17.00–17.30; ayrıntıda Mevsimlerin Oluşumu, 20.10.2026, 30 dakika, aynı kazanım ve süre sürümü görüldü. Çalışma bekleyen durumda kaldı; tamamlanmadı.

Bildirimler sayfasında **Çalışma planın güncellendi** / `StudyPlanUpdated` bildirimi görüldü:

> Fen Bilimleri: 19.10.2026 19:00 → 20.10.2026 17:00. Güncel çalışma planını öğrenci panelinden inceleyebilirsin.

Bildirim aynı plan kimliğine bağlı, öğrenciye ulaştı ve okunmamış bırakıldı. Önceki üç bildirime dokunulmadı. Yeni değişiklik bildirimi tam bir adet oluştu; aynı olay tekrar bildirim üretmedi. Bu kontrol bildirimler listesinin yüklenmesi üzerinden yapıldı; açık bir bildirim ekranında canlı push gecikmesi ayrıca ölçülmedi.

## Koruma ve sonuç

Taşıma öncesi ve sonrası canlı satır özetleri otomatik karşılaştırıldı:

- 560 görevden yalnız seçilen görevin tarih/saat ve normal güncelleme/sürüm alanları değişti. Diğer **559 görev birebir aynı**.
- Önceden tamamlanan **Kader ve Kaza İnancı** kaydı, 55 dakika / 20 soru, tarih/saat ve sürümü dahil birebir aynı.
- Taşınan kaydın konu, süre, kazanım ve diğer sabit alanları aynı; görev silinip yeniden oluşturulmadı.
- **560 çalışma–kazanım bağlantısı birebir aynı**.
- Aynı tek aktif plan korundu. Normal eşzamanlılık sürümü 2→3 oldu.
- Toplam planlanan süre **11970 dakika**, kapsam **144 konu / 269 kazanım / 319 pay** olarak korundu.
- Süre payı farkı, çakışan çalışma, hedef tarih aralığı dışında çalışma ve eski süre sürümü bağlantısı: 0.
- Önceki üç bildirimin özetleri aynı; bir yeni `StudyPlanUpdated` kaydı mevcut.
- Öğrenci tarayıcısının hata/uyarı kaydı boş.

**Sonuç:** Öğretmen taşıması → kalıcı kayıt → öğrenci takvimi → öğrenci bildirimi akışı canlıda geçti. Bu akışta hata görülmedi; uygulama değişikliği veya dağıtım gerekmedi. Çalışma yeni gün ve saatte bırakıldı, tamamlanan kayıt korunuyor.

Salt okunur kontrol: `tools/releases/coaching-move-e2e-20261019/snapshot.sh`. Kanıtlar: `artifacts/coaching-move-e2e-20261019/` içindeki önce/sonra özetleri, assertions.json ve ekran görüntüleri.
