# LGS Matematik kazanımları — canlı yayın raporu

Yayın: **16 Ekim 2026 21:34:33 Türkiye saati** (`2026-10-16T18:34:33Z`). Sürüm: `coaching-outcomes-20261016`. Sonuç: canlıya alındı; yönetici ve öğrenci oturumlarıyla doğrulandı.

## Yayınlanan içerik

| Veri | Canlı sonuç |
|---|---:|
| LGS Matematik kazanımı | 52 |
| Resmî konu grubu / program ders saati | 12 / 180 |
| Eşleşen mevcut yerel konu | 13 |
| Onaylı konu–kazanım ilişkisi | 58 / 58 |
| Yayınlanmış bireysel çalışma süresi | 52 / 52 |
| Yayınlanmamış süre / taslak | 0 |
| Doğrulanmış MEBİ kazanım UUID eşleştirmesi | 2 |

Program `meb-2018-lgs-matematik-2026-pilot-v1`; eğitim yılı başlangıcı 2026 (2026–2027) için geçerlidir. Diğer derslerin veya sonraki yılın kazanımları bu paket kapsamında değildir.

Konu bağlantıları ve süreler, kullanıcının açık canlı yayın talimatı dayanak gösterilerek onaylandı. Süreler `ReviewedStudy`, sürüm **2**, güven düzeyi **Low** olarak yayınlandı. İlk paket aktarımının oluşturduğu, hiçbir çalışma tarafından kullanılmayan 52 `OfficialBudgetDraft` sürüm 1 önerisi, yayınlanmış karşılıkları oluşturulduktan sonra kaldırıldı. Önceden var olan çalışma ve kullanıcı verileri silinmedi.

Resmî konu ders saati bütçesini 40 dakika ile çarpıp kazanımlara eşit dağıtma, beş dakikaya yuvarlama ve yaklaşık %40 öğrenme / %50 alıştırma / %10 kontrol ayırma yöntemi başlangıç modelidir. **Bireysel öğrenme süresi resmî MEB ölçümü veya öğretmen ölçümü değildir.** Bu açıklama kayıtların dayanak alanında saklanır. Video ve mola süresi içermez. Toplam başlangıç tahmini 7.215 dakikadır.

Doğrusal Denklemler 1 ve 2, aynı resmî konu grubunun altı kazanımını paylaşır. Onay ortak program kapsamınadır; MEBİ video bölüm kapsamına dair doğrulama iddiası yoktur. Planlayıcı aynı kazanımı iki konudan seçince bir kez sayar.

## Dağıtım ve veritabanı

- Yalnız `coaching-service`, `admin-panel` ve ortak plan görünümü için `staff-portal` güncellendi. Identity ve diğer mevcut konteynerlerin kimlikleri değişmedi.
- Yeni migration: `20261016180740_StudyCatalogOutcomes`. Coaching şemasına yedi tablo ekler; ortak hesapları veya önceki çalışma satırlarını dönüştürmez.
- Çalışma klasöründeki ilgisiz değişiklikler yayına dahil edilmedi. Önceki canlı sürümün kaynakları üzerine `374f79e204060615f46f5d73bce36ae13ec5a282` revizyonunun ilgili farkları uygulandı; mevcut Shared derlemeleri korundu. Staff tarafında yalnız ortak plan görünümünün gerekli farkları taşındı.
- Linux backend, yönetici/öğrenci ve personel arayüzlerinin üretim derlemeleri tamamlandı. Backend katman derlemeleri önceki canlı imaj üzerine yerleştirildi.
- Gerçek PostgreSQL yedeği ayrı `coaching_outcomes_drill_20261016` veritabanına geri yüklendi. Migration iki kez, aktarım/yayın aracı iki kez çalıştırıldı. Tekrarda çoğalma olmadı; önceki iş verileri aynı kaldı. Yalnız bu prova veritabanı kaldırıldı.
- Canlı yazıcılar durduruldu; Identity ve Coaching yedekleri alındı. Migration ve içerik yayını sonrası veri parmak izleri karşılaştırıldı; üç servis açıldıktan sonra yeniden kontrol edildi.

Paket: `artifacts/coaching-outcomes-live-20261016-r2/release-r3.tar.gz` (384 payload dosyası).

```text
SHA256 de34b9f50bc09c030197f07170141da1103e23ae16ece61f7a11279a4c550ae9
```

Yedekler yalnız sunucuda, özel izinli `/var/lib/eduivme/releases/coaching-outcomes-20261016` dizinindedir:

- `identity_db.before-promotion.backup`
- `coaching_db.before-promotion.backup`
- `coaching.before-drill.backup` (geri yükleme provasının kaynağı)
- `live-backups.sha256`, `drill-backup.sha256`

Yedekler, bağlantı dizeleri ve çalışma ortamı dosyaları depoya veya yerel kanıt klasörüne kopyalanmadı. Geri dönüş için önceki üç uygulama imajı ve `rollback.compose.json` sunucuda saklandı. Otomatik veritabanı geri yükleme veya migration Down çalıştırılmaz; eklenen şema korunarak önceki uygulama imajlarına dönülür.

## Veri koruma sonucu

| Yayın öncesi veri | Yayın ve tarayıcı testi sonrası |
|---|---|
| 8 ortak hesap | Hesaplar ve çekirdek alanları korundu |
| Şifre hash/salt, roller, yetkiler, ürün erişimleri | Parmak izleri aynı |
| 3 mevcut plan revizyonu | Kimlik, durum, sürüm ve tüm eski satır içerikleri aynı |
| 4 mevcut görev; 4 tamamlanmış çalışma | Tüm eski satır içerikleri aynı |
| 86 dakika gerçekleşmiş çalışma | Aynı; doğrulama için sahte tamamlanma eklenmedi |

Karşılaştırma, tabloların sıralanmış satır içeriklerinden alınan parmak izleriyle yapıldı. Identity hesabında olağan girişle değişen son giriş/MFA sayaçları ve güncelleme/sürüm metadatası karşılaştırmadan hariçtir. Teknik denetim, outbox/inbox ve yeni katalog tabloları eski iş verisi parmak izine dahil değildir.

Canlı öğrenci testi **bir yeni plan** ve tamamlanmamış bir görev oluşturdu. Bu yeni plan arşivlendi; eski aktif plan değiştirilmedi. Son toplam 4 plan, 5 görev; tamamlanmış çalışma sayısı hâlâ 4 ve gerçekleşmiş süre 86 dakikadır. Eski satırların son kontrolünde yalnız bu yeni test planı ve görevi dışarıda bırakıldı.

## Gerçek oturumlarla doğrulama

### Yönetici

Kullanıcının giriş yaptığı SystemAdmin oturumunda `/dashboard/coaching/catalog` açıldı. 52 kazanımlı program, onaylı konu bağlantısı ve yayınlanmış sürüm 2 süre kaydı görüldü. İlk kazanımın eşleştirme dayanağı yazıldı; onay kaldırılıp yeniden verildi. Son SQL kontrolü 58 ilişkinin tamamının onaylı ve 52 sürenin tamamının yayınlanmış olduğunu doğruladı. Yeni taslak süre oluşturulmadı.

![Canlı yönetici yayın kaydı](../../artifacts/coaching-outcomes-live-20261016-r2/proof/admin-published-final.png)

### Öğrenci

Kullanıcının açık öğrenci oturumuyla şu akış tamamlandı:

1. Mevcut eğitim bağlamı ve plan oluşturma ekranı açıldı; konu için kazanım önerileri yüklendi.
2. `M.8.1.1.1` seçildi. Genel öneri 135 dakika (55 öğrenme + 65 alıştırma + 15 kontrol) iken yalnız öğrenci planının süresi 30 dakikaya değiştirildi.
3. 30 dakikalık önizleme oluşturuldu, plan kaydedildi ve yeniden açıldı. Kazanım kodu/metni, 30 dakika ve kaynak süre sürümü 2 görünür kaldı.
4. Yeni test planı arşivlendi. Mevcut aktif plan yeniden açıldı; önceki tamamlanmış görevler ve gerçekleşen süreler erişilebilir kaldı.
5. Veritabanında yeni görevin kazanım bağlantısı `AllocatedMinutes=30`, `Origin=StudentOverride` olarak doğrulandı. Genel katalog süresi 135 dakika kaldı.

Yeni test planı: `8c4647f1-7f4b-4634-9f47-a0b7f687f3dd`, başlık `Canlı doğrulama · LGS kazanım · 16.10.2026`, son durum **Arşiv**. Katalogdaki taslakların kaldırılması, öğrencinin plan oluştururken taslak kaydetme adımını kaldırmaz; planın aktif edilmesi ayrı kullanıcı kararıdır.

![Kaydedilmiş kazanım ve kişisel süre](../../artifacts/coaching-outcomes-live-20261016-r2/proof/student-saved-outcome.png)

## Kontroller ve sınırlar

Canlı sonrası beş dakikayı aşan izlemede 11 örnek alındı; son örnek `2026-10-16T18:39:52Z`. Üç servis bütün örneklerde çalışıyordu, yeniden başlatma sayıları sıfırdı. `/auth/login`, `/coaching-portal/setup` ve `/staff/` adresleri HTTP 200 döndürdü. Son readiness, imaj kimliği, payload bütünlüğü ve ilgisiz servislerin aynı kaldığı kontrolleri geçti.

Geliştirme testleri: 13 pilot backend testi (ikisi gerçek PostgreSQL), 693 mevcut Coaching regresyon testi, dört kontrollü API ile masaüstü/mobil tarayıcı senaryosu ve ilgili ekran testleri geçti. Bu kümeler kısmen örtüşür; sayıları toplayarak bağımsız test toplamı verilmez. Yeni backend davranışlarında satır kapsamı %91,4. Tüm depo testlerinin geçtiği iddia edilmez: önceki raporda açıklanan yerel Docker fixture erişim sorunu, mevcut Identity testi ve menü bağlantı sayısı beklentisi bu yayın kapsamı dışındadır. Ayrıntılar [pilot raporunda](lgs-matematik-kazanim-pilotu-20261016.md).

Canlı kullanımda engelleyici olmayan iki arayüz iyileştirmesi gözlendi: yönetici kazanım formunda uzun metinler yatay taşma oluşturabiliyor; kurulumun konu düzeyindeki eksik süre uyarısı, yayınlanmış kazanım sürelerini ayrıca hesaba katmıyor. Kazanımları yükleme, süre seçme, önizleme ve kaydetme akışları çalıştı. Bu iki görünüm davranışı bu dağıtımda değiştirilmedi.

Kanıt klasörü: `artifacts/coaching-outcomes-live-20261016-r2/`. `final-production-result.log` yayın/sayım/izleme sonucunu, `final-preservation-result.log` son veri koruma karşılaştırmasını içerir. Sunucudaki `live-result.json`, `browser-checks.json`, `browser-data-verified.txt`, `monitor.log` ve `monitor-verified.txt` ayrıntılı kayıtlardır.
