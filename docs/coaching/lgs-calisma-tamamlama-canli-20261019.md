# Öğrenci tamamlama ve öğretmen toplu takibi — canlı doğrulama

Tarih: 19.10.2026 (Europe/Istanbul).

Aktif plan: `a4cea155-5485-4db6-9f52-1ec4df8080c3` — LGS çalışma planım · 19.10.2026.

## Öğrenci işlemi

Öğrenci tarayıcısında 19.10.2026 18.00–19.00 Din Kültürü **Kader ve Kaza İnancı** çalışması açıldı. Görev kimliği `294578cc-e8e0-431d-a25b-a0a0d1674ca5`; kazanım `8.1.1`, süre sürümü 2, planlanan 60 dakika.

Gerçek süre **55 dakika**, soru sayısı **20** girilerek “Tamamla” seçildi. Uygulama “Çalışma tamamlandı.” mesajını gösterdi. Ay takvimindeki kart “Tamamlandı”, gün takvimindeki kart “1/1 tamamlandı” oldu. Detayda “Tamamlandı · 55 dk” ve “Kaydedilen soru: 20” görüldü. Sayfa tamamen yeniden yüklendiğinde aynı bilgiler korundu. Tamamlanan kayıtta tekrar tamamlama ve tarih değiştirme formu sunulmuyor.

Bu, kullanıcının istediği canlı test kaydıdır; gerçek bir saatlik çalışmanın yapıldığı iddiası değildir. Kayıt, istenen test sonucunu incelemek için tamamlanmış bırakıldı.

## Öğretmen işlemi

Öğretmen oturumu kapalıydı; kullanıcı Chrome personel portalında yeniden giriş yaptı. Öğretmenin kendi oturumunda **Çalışma takibi** sekmesi açıldı.

12.10.2026–26.10.2026 döneminde öğrenci özeti:

- Aktif plan.
- **1 tamamlanan**, **24 bekleyen**, **0 geciken**.
- **55 gerçek dakika**.

“Göster → Tamamlanan çalışmalar” filtresinde öğrenci, 19.10.2026 18.00, **Kader ve Kaza İnancı**, **Tamamlandı**, **60 dk · Gerçek 55 dk** satırı görüldü. Sayımlar seçilen dönem içindir; bütün yılın bekleyen sayısı değildir.

“İlgili planı aç” üzerinden aynı aktif plan, Tamamlanan filtresiyle açıldı. Öğretmen de `8.1.1`, süre sürümü 2, gerçek 55 dakika ve 20 soru ayrıntılarını gördü. Öğretmen tarafında veri düzenlenmedi. Toplu takip, listeyi yüklediğinde/yenilediğinde güncel kaydı okur; bildirim veya anlık push teslimi bu testin konusu değildir.

## Kayıt koruması

Tamamlama öncesi ve tüm tarayıcı kontrolleri sonrası canlı veritabanı yalnız okundu; plan/görev/kazanım bağlantıları satır özetleriyle karşılaştırıldı.

- Aynı aktif plan kaldı; plan oluşturulmadı, arşivlenmedi veya yeniden dağıtılmadı.
- **560 görevden yalnız seçilen görevin tamamlama alanları değişti. Diğer 559 görev birebir aynı.**
- Seçilen görevin konu, tarih, başlangıç saati, planlanan dakika ve diğer sabit alanları aynı.
- **560 çalışma–kazanım bağlantısı birebir aynı.**
- 144 konu, 269 kazanım, 319 süre payı ve toplam **11970 planlanan dakika** korundu; süre payı farkı, çakışma, hedef dışı tarih ve eski süre sürümü bağlantısı sıfır kaldı.
- Tamamlanan görev sürümü 0→1 oldu. Planın eşzamanlılık sürümü normal `RecordTaskChange()` davranışıyla 1→2 oldu; plan kimliği, başlığı, aktifliği, kaynak haftası, hedef tarihi ve dağıtımı aynı kaldı.
- Öğrenci tarayıcısının son hata/uyarı kaydı boş.

**Sonuç:** Öğrenci tamamlama → kalıcı kayıt → takvim işareti → öğretmen toplu özeti → tamamlananlar listesi → ilgili plan ayrıntısı zinciri canlıda geçti. Bu akışta hata bulunmadı; uygulama kodu değişikliği veya yeni dağıtım gerekmedi.

Salt okunur kontrol: `tools/releases/coaching-completion-e2e-20261019/snapshot.sh`. Kanıtlar: `artifacts/coaching-completion-e2e-20261019/` içindeki before/final, assertions.json, öğrenci ve öğretmen ekran görüntüleri.
