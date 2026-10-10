# Genç eğitim programları v2

13–16 yaş için beş başlangıç seviyesinde 28 günlük program. Tek içerik kaynağı
`infrastructure/data/teen-program-plan-v2.json`; her program 182 bağımsız görev içerir.

| Hafta | Günlük görev | Dikkat/görsel algı | Okuma/anlama/kelime |
|---|---:|---:|---:|
| 1 | 6 | 4 | 2 |
| 2 | 6 | 4 | 2 |
| 3 | 7 | 3 | 4 |
| 4 | 7 | 2 | 5 |

Seviye 3–5 programlarında 15. gün 4. görev RSVP, 19. gün 3. görev Metin Solma,
22. gün 6. görev Göz Gezdirme olur. Seviye 4–5'te 21. gün 6. görev ve 27. gün
7. görev Hata Analizi olur. Seviye 5'te 28. gün 6. görev Sınav Simülasyonu olur.
Bunlar okuma görevlerinin yerine geçer; toplam görev sayısı artmaz.

Gruplama en fazla seviye 4'tür. Serbest Okuma katalogda yalnız 1–3 düzeylerinde
olduğundan seviye 4–5'te ilgili seviyenin Anlama göreviyle değiştirilir.
Regresyon Azaltma ve İç Ses Azaltma katalogdan kaldırılmaz, fakat bu planın zorunlu
görevleri değildir. Seviye tespit programı ve eşikleri değiştirilmez.

Takvim ilerledi diye zorluk artmaz (`WeeksPerDifficultyIncrease=0`). Eski şablonun
ölçüme dayalı kişiselleştirme üst sınırı ve puan aralıkları korunur. Mevcut kayıtlı
egzersiz ayarları kullanılır; süre ve tekrar sayıları bu içerik geçişinde değiştirilmez.
İlk iki hafta 25–30, son iki hafta 30–40 dakika yalnız tasarım hedefidir; gerçek
süre katalog ayarları ve öğrenci yanıtlarıyla ayrıca ölçülmelidir. 182 görev süre
garantisi veya bilimsel etkinlik kanıtı değildir.

Aynı türün tekrarı ayrı sıra numarası, oturum ve sonuçtur. Yeni program oluşturulurken
ikinci tekrar, daha alt seviyede kullanılmamış egzersiz var diye seviye düşürmez.
Önceden atanmış programların sabit takvimi yeniden yazılmaz.

## Kontrollü geçiş

1. Gerçek geçici PostgreSQL ve tarayıcı testleri geçmeden yayımlama.
2. Yalnız `speedreading_owned_db` için yedek al ve doğrula.
3. Tekrarda seviye düşürmeyen Hızlı Okuma API sürümünü yayımla.
4. JSON'u `plan_json` psql değişkeni olarak verip `apply.sql` çalıştır.
5. Script tek transaction içinde yalnız belirlenmiş beş eski Genç eğitim şablonunu
   değiştirir. Yeni kimlik çakışması veya eksik egzersiz geçişi geri alır.
6. Eski şablona ilerleme bağlanmışsa script durur; kullanıcı geçmişini silmez.
   Kullanıcının test geçmişini temizleme izni, bu scriptte toplu silmeye dönüşmez.
7. Egzersiz kataloğu, Genç seviye tespiti, diğer yaş programları ve diğer servisler korunur.

Yeni migration gerekmez. İçerik scripti doğrulanmış aynı planla yeniden çalıştırılabilir.
Programlara kullanıcı kaydolduktan sonra eski şablonları geri getirip yeni şablonları
silmek güvenli değildir. Geri dönüşte kullanıcı sonuçlarını koruyan ileri düzeltme
tercih edilir; tam DB yedeği otomatik olarak canlı üzerine geri yüklenmez.
