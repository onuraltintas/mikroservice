# Genç yetişkin eğitim programları v2

17–21 yaş için başlangıç seviyeleri 1–5 olan beş program. Her biri 28 gün / 182 görev.
Görevlerin tek kaynağı [plan JSON](../../../infrastructure/data/young-adult-program-plan-v2.json).

| Hafta | Günlük görev | Dikkat / görsel algı | Okuma / anlama / kelime |
|---|---:|---:|---:|
| 1 | 6 | 4 | 2 |
| 2 | 6 | 4 | 2 |
| 3 | 7 | 3 | 4 |
| 4 | 7 | 2 | 5 |

Seviye 3–5: 15. gün 4. görev RSVP, 19. gün 3. görev Metin Solma, 22. gün 6. görev
Göz Gezdirme. Seviye 4–5: Serbest Okuma yerine Anlama; 21. gün 6. görev ve 27. gün
7. görev Hata Analizi. Seviye 5: 28. gün 6. görev Sınav Simülasyonu. Bunlar mevcut
okuma görevlerinin yerini alır, toplam görev sayısını artırmaz. Gruplama en fazla seviye 4.

Aynı tür tekrarlanabilir; her tekrar ayrı slot/oturum/sonuçtur. Genç yetişkin kataloğunun
kayıtlı ayarları ve içerik havuzu kullanılır. Regresyon Azaltma / İç Ses Azaltma katalogda
kalır, zorunlu görev değildir. Takvimsel zorluk artışı kapalıdır; ölçüme dayalı kişisel
üst sınırlar korunur. İlk iki hafta 25–30, son iki hafta 30–40 dakika yalnız tasarım
hedefidir; gerçek süre ayrıca ölçülmelidir. Program eğitim etkinliği garantisi vermez.

Yeni migration, API veya frontend sürümü gerekmez. Katalog ve seviye tespiti korunur.
Geçiş yalnız onaylanan bir eski Seviye 1 test ilerlemesi ve üç bağlı oturum/sonucu
temizler; kullanıcı hesabını veya diğer geçmişi silmez. Beklenmeyen bağlantıda abort eder.

Yedekli geçiş sırası, test kanıtları ve canlı sonuçlar için
[yayın raporu](../../../docs/reports/young-adult-programs-v2-live-20261010.md).
