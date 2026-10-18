# Çakışma ve tamamlanmış çalışma korumaları — canlı test

Tarih: 19.10.2026 (Europe/Istanbul). Öğretmenin kendi açık Chrome oturumunda, mevcut aktif `a4cea155-5485-4db6-9f52-1ec4df8080c3` LGS planında denendi.

## 1. Dolu saate taşıma

Bekleyen Fen Bilimleri / Mevsimlerin Oluşumu, 20.10.2026 17.00–17.30, taşıma formunda seçildi. Hedef 20.10.2026 18.00 yapıldı. Bu saatte Matematik 18.00–19.00 kaydı bulunduğu için “Taşımayı kaydet” işlemi şu uyarıyla engellendi:

> Bu saat başka bir çalışmayla çakışıyor. Boş bir saat seç.

Fen çalışması 17.00–17.30’da kaldı, Matematik değişmedi. Form “Vazgeç” ile kapatıldı. Bu bir beklenen doğrulama uyarısıdır; uygulama hatası değildir.

## 2. Tamamlanan çalışmayı değiştirme

19.10.2026 18.00–19.00 Din Kültürü / Kader ve Kaza İnancı, tamamlandı / 55 gerçek dakika / 20 soru kaydı açıldı. Ayrıntıda tarih/saat değiştirme, yeniden tamamlama veya gerçek süre/soru düzenleme formu sunulmuyor. Tamamlanan çalışma taşıma seçiminde de bulunmuyor.

Tamamlanan kart ayrıca takvimde 20.10.2026 20.00 boş hücresine sürüklenmeye çalışıldı. Kart taşınmadı; 19.10.2026 18.00–19.00 ve 1/1 tamamlandı olarak kaldı. Bu kontrol normal öğretmen arayüzünün korumalarını doğrular; uygulama dışından hazırlanmış bir HTTP isteğiyle sunucu reddi bu testte ayrıca denenmedi.

## Kayıt koruması

İki denemeden önce ve sonra canlı veritabanı yalnız okundu. Aynı salt okunur çıktının tamamı birebir karşılaştırıldı; eşleşti:

- Aynı aktif plan ve aynı sürüm.
- 560 görev; tamamlanan görev dahil her satırın özeti aynı.
- 560 kazanım bağlantısı aynı.
- Tamamlanan kaydın 55 dakika / 20 soru, tamamlanma tarihi, kazanımı, planlanan süre, tarih/saat ve sürümü aynı.
- Bekleyen Fen kaydı 20.10.2026 17.00–17.30 olarak aynı.
- Planlanan toplam 11970 dakika ve tamamlanan toplam 55 dakika aynı.
- Önceki dört bildirim aynı; reddedilen denemeler yeni bildirim üretmedi.

**Sonuç:** Dolu saate taşıma ve tamamlanan çalışmayı değiştirme normal öğretmen arayüzünde engellendi. Hiçbir plan/çalışma/bildirim kaydı değişmedi. Düzeltme veya dağıtım gerekmedi.

Kanıtlar: `artifacts/coaching-guards-e2e-20261019/` (before.txt, after.txt, assertions.json, overlap-blocked.png, completed-protected.png). Salt okunur kontrol için `tools/releases/coaching-move-e2e-20261019/snapshot.sh` kullanıldı.
