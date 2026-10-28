# Koçluk plan sihirbazı

Öğrenci yıllık ve haftalık çalışma planını tek yerden, **Plan sihirbazı** ekranından kurar:
`/coaching-portal/automatic-plan`. Eski `/coaching-portal/study-plan-assistant` adresi bu ekrana yönlendirilir.

## Nasıl çalışır

- Sihirbaz tarayıcıda çalışan, adım adım bir yönlendirmedir; yapay zekâ ya da dış bir sağlayıcı kullanılmaz ve sohbet
  metni saklanmaz.
- Adımlar:
  1. Dersler ve hedef sınav (Koçluk profilinden).
  2. Haftalık çalışma saatleri (`/coaching-portal/study-availability`).
  3. Haftalık taslak.
  4. Yıllık plan önizlemesi.
  5. Yayımlama.
- Plan sunucuda **kural tabanlı plan motoru** (`WeeklyPlanEngine`, sürüm 2) ile hesaplanır. Sihirbaz önizlemeyi
  `engineVersion: 2` ile ister. Sürüm 1 yalnız eski istemciler için korunur.
- Motor şu kurallarla çalışır (roadmap O7):
  - Zorluk süreyi ölçekler.
  - "Çalıştım" kısa tekrara, "tekrar gerek" kısaltılmış tekrara dönüşür.
  - Deneme hatası olan konular öne alınır.
  - 1-7-30 gün aralıklı tekrar yapılır.
  - Görev başına soru hedefi konur.
  - Aynı günün art arda saatleri tek görev olur.
- Süre yetmezse ve öğrenci "öncelikli konuları yerleştir" derse (`prioritizeWhenShort`):
  - Önce deneme hatası olan, sonra sık çıkan konular yerleşir.
  - Dışarıda kalan konular listelenir.
- Plan bitişi sınavdan bir gün öncedir: öğrencinin seçtiği tarih, yoksa yöneticinin sınav türü kaydındaki tarih.
- Önizleme bir parmak iziyle döner. Takvim ya da saatler arada değiştiyse kayıt reddedilir ve önizleme yenilenir.
  Öğrenci açıkça yayımlamadan etkin plan değişmez; tamamlanmış görevler ve sabitlenen işler korunur.

## Yapılandırma

Ek ayar gerekmez.

Eski `Coaching__StudyPlanAssistant__*` (OpenAI) ayarları artık kullanılmaz; ortamdan silinebilir.
