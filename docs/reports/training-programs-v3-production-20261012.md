# Hızlı Okuma V3 programları — canlı yayın sonucu

12 Ekim 2026, 15:18:23 Türkiye saati. Kaynak sürüm: `04602e85`;
GitHub dalı: `codex/platform-hardening`. Yalnız Hızlı Okuma API, frontend ve
`speedreading_owned_db` değişti. Identity, Koçluk ve Notification kapsam dışı.

## Tamamlananlar

- Dört yaş grubu × beş seviye: 20 program, 560 gün, 3.640 görev.
- 28 gün / 182 görev / ilk iki hafta 6, son iki hafta 7 görev korundu.
- Görselleştirme, Metin Solma ve uygun üst seviye okuma çeşitleri çizelgelere eklendi.
- Serbest Okuma bütün yaş/seviyelerde haftalık yer aldı; eski S4→S3 eşleştirmesi kalktı.
- Anlama öğrenme/değerlendirme modu açık seçiliyor; hafta sonları değerlendirme.
- Sınav Simülasyonu **hiçbir programda yok**; mevcut üç katalog kaydı ve sonuçları korundu.
- 12 kısa seviye tespiti kaydı normal eğitim seçiminden ayrıldı.
- Çocuk Metin Solma için üç özgün hikâyeden 15 kısa metin seçkisi / 60 bağlı soru eklendi.
- Gösterim/çalışma süresi gerçek ayarlardan tahmin ediliyor; cevap süresine bağlı görevlerde
  kesin olmayan süre yerine “Yanıt sürenize bağlı” gösteriliyor.
- Mevcut tek başlanmamış Genç S1 atamasının 37 yanlış ölçüm bağlantısı normal kayda
  taşındı; 182 slotun sırası/haftası/günü ve ilerleme sayaçları korundu.

Yeni V3 gün sırası **yeni atamalara** uygulanır. Mevcut frozen atama toptan yeniden
oluşturulmadı; yalnız yanlış ölçüm kimlikleri onarıldı. Mevcut kayıt için V3 sırasının da
istenmesi ayrı, açık bir geçiş kararıdır. Güncel katalog ayarları aynı kimlikle açılan yeni
oturumlara yansır; eski oturum snapshot'ı veya sonuçlar silinmez.

## Test ve inceleme kanıtı

- Son backend birim/regresyon: **1.246/1.246**, atlanan yok.
- Angular birim/regresyon: **923/923**, üretim derlemesi başarılı.
- Geniş Hızlı Okuma PostgreSQL regresyonu: **201/201**, atlanan yok.
- Son canlı katalog kodlarıyla V3 özel PostgreSQL tekrar doğrulaması: **13/13**.
- Program/içerik Node testleri: **5/5**.
- Masaüstü/mobil günlük ekran Playwright: **2/2**; gerçek Angular, kontrollü Identity/API
  yanıtları. Bu, gerçek kullanıcı oturumuyla bütün 560 günü tamamlamak değildir.
- Kaynak incelemesindeki seed flag, kilit dışı eski oturum izni, pinned onarım ve orphan-slot
  koruma bulguları düzeltildi. Assessment by-ID okunabilir metadata lookup'u seviye tespiti
  oyuncusunu korumak için bırakıldı; normal katalog listesi gizler ve yeni normal session start
  reddeder. Aktif eski session yalnız kendi bağlamında kilit içinde devam edebilir.
- Genel kod kapsaması %100 iddiası yok. CI workflow'unun geçtiği iddia edilmedi;
  belirtilen testler yerelde/geçici gerçek PostgreSQL'de çalıştırıldı.
- Mevcut universal-player SCSS 128,99 kB / 120 kB uyarısı sürüyor; derleme hatası değil.

## Yayın güvenliği ve prova

Sürüm dizini: `/var/lib/eduivme/releases/curriculum-v3-20261012`.

1. Mühürlü payload doğrulandı; kirli `/opt/eduivme` checkout'u sıfırlanmadı.
2. Canlı tam DB yedeği geçici kopyaya geri yüklendi; gerçek uygulama hesabıyla yeni migration
   çalıştırıldı. İlk prova kopyasında atlanan sahiplik/ACL düzeltildi; canlı DB değişmeden
   gerçek yetkilerle tekrar prova tamamlandı.
3. Eski alt seviyelerdeki genel `reading` kodu ile gerçek `TextFading` türü ayrımı yakalandı;
   geçiş gerçek FK türünü doğrulayacak biçimde düzeltildi ve canlı benzeri regresyon geçti.
4. Kısa içerik, 20 program ve pinned atama onarımı tek transaction'da uygulandı.
5. Hedef dışı bütün tablolar/alanların içerik özetleri aynı kaldı. İkinci uygulamada tüm
   tablolar aynı kaldı. Template ve kısa içerik geri dönüşü kopyada prova edildi.
6. API durdurulup son `speedreading-before.dump` alındı; SHA256 ve `pg_restore --list`
   başarılı. Yeni migration ve pack uygulandı; API/frontend birlikte açıldı.
7. Diğer çalışan konteynırların kimliği değişmedi. Geçici prova DB'si kaldırıldı;
   yalnız bizim ürettiğimiz `runtime.env` / `rehearsal.env` kopyaları silindi. Asıl `.env`,
   yedekler ve önceki imajlar korundu.

## Canlı son doğrulama

- `/health/ready`: **Healthy**; API restart sayısı **0**, frontend **healthy**.
- `https://masterhizliokuma.com/`: **200**.
- Oturumsuz katalog erişimi: **401**.
- Canlı `ngsw.json` yerel yayınla birebir:
  `9d7004b061fd5792362eff8530a04fc74b2fe972f9dfda059fde54f38244697c`.
- Üretim API'sinin salt okunur `--audit-training-programs` sonucu:
  `{"Programs":20,"Days":560,"Slots":3640}`. Gerçek seçim motoru yanlış yaş/seviye veya
  assessment-only kayda düşmedi; denetim oturum/atama oluşturmadı.
- SQL doğrulaması: **0 sınav görevi**, **15 kısa metin**, **60 soru**, **12 scoped ölçüm kaydı**.
- Mevcut atama **182 slot**, sıfır tamamlanan görev/log; geçmiş kaydı silinmedi.
- Geçici prova DB'si sayısı **0**. Son başlangıç log taramasında hata satırı **0**.

Canlı gerçek kullanıcıyla yeni atama ve egzersiz ekranlarının kabul kontrolü ayrıca
yapılabilir. Bu yayın teknik seçim, şema, veri geçişi ve sağlık kontrollerini geçmiştir;
akademik etkinlik/pedagojik yaş normu onayı anlamına gelmez.
