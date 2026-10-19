# 2026 LGS — canlı katalog aktarımı

19 Ekim 2026. Kullanıcının doğrulanan okul/kontenjan verilerini aktarma ve eski kayıtları koruma talebi uygulandı. Türkiye geneli katalog tamamlanmış değildir; yalnız belgelenen kaynak kapsamı aktarıldı.

## Canlı sonuç

| Durum | Kayıt |
|---|---:|
| Kaynak partisi | 8 |
| Toplam kaynak kaydı | 432 |
| Kesin okul eşleşmesi | 274 |
| Birden fazla eski okul karşılığı — Ambiguous | 147 |
| Eksik ilçe — Pending | 9 |
| Yanlış resmî kurum kodu — Conflict | 2 |

430 okul/program/kontenjan metaverisi ve iki aşamalı sonuç kaydı saklandı. Kesin eşleşen 274 kaydın 272'si metaveri, ikisi Menemen'in sonuçlarıdır. İncelemedeki kayıtların okul bağlantıları NULL kaldı; doğrulanmış katalog sorgusu bunları döndürmez. 432 sayısı farklı okul sayısı değildir.

Kaynak kapsamı: İstanbul 322, Samsun 53, Kayseri 52, Bitlis 2, Denizli 1 metaveri satırı; İzmir/Menemen için 2 aşamalı sonuç satırı. Kayseri'nin 17 mesleki satırından dokuzunda ilçe kesinleştirilemedi; başka bir ilçeye tahmini atama yapılmadı. Aynı yayında açıkça ilçe bilgisi olan Merkez, Hürriyet, Talas Atatürk ve Muammer Kocatürk kurumları için yayının kendi kanıtı kullanıldı.

## Eşleştirme ve koruma

- Kanıtlı sekiz kurumun 39 kaynak ad varyantı kullanıldı. **Dokuz kayıt eski okul kimliğine kesin bağlandı**; 30'unda birden fazla eski okul kimliği bulunduğu için rastgele seçim yapılmadı.
- Eski `BEŞİKTAŞ (MERKEZ)` gibi ilçe gösterim ekleri eşleştirme sırasında normalize edildi; eski ilçe alanları değiştirilmedi. Bu kontrol, önceki provanın fark etmediği bazı mükerrer eski karşılıkları ortaya çıkardı. Bu nedenle önceki turdaki 281/320 eşleşme sayısı son aktarım sayısı olarak kullanılmamalıdır.
- Şile Ayet Azer Aran satırlarının kaynakta verilen `751600` kodunun başka kuruma ait olduğu kanıtı korundu. `768129` doğru kurum kimliğidir; ancak iki eski kimlik ve 2026 alan karşılıkları kesin ayrılmadığından bu iki kayıt Conflict kaldı.
- Eski **3.010 okul** ve **21.602 üniversite programı** kaydı değişmedi. Kesin eski karşılığı olmayan 86 okul kaydı eklendi; eski satırlar silinmedi, yeniden adlandırılmadı veya birleştirilmedi.
- Hedefler ve önceki okul/program yabancı anahtarları korunmuştur. Yeni veriler eski MinimumScore/ScoreYear alanlarının üzerine yazılmadı.

## Puan ve yüzdelik

Yalnız aşağıdaki sonuçlar puan/yüzdelik içerir; yıl ve aşama aynı resmî duyuruda açıktır:

| Kurum | Yıl | Aşama | Taban | Yüzdelik |
|---|---:|---|---:|---:|
| Şehit Ahmet Özsoy Fen Lisesi, Menemen | 2026 | Main — ilk yerleştirme | 441,2318 | 4,08 |
| Aynı kurum | 2026 | Transfer1 — birinci nakil | 442,6725 | 3,88 |

Canlı SQL kontrolünde **Catalog aşamasında puan/yüzdelik bulunan satır sayısı sıfırdır**. Sincan'ın tahmini yüzdelikleri, aşaması eksik Menderes/Karacabey/Bitlis değerleri ve Tuzla'nın alanlara ayrılamayan toplu program aralıkları aktarılmadı. Kontenjanı bilinmeyen alanlara eski yılın kontenjanı veya okulun ortak toplamı kopyalanmadı.

## Veritabanı ve uygulama kapsamı

`20261019095937_TargetAdmissions2026` geçişi yalnız `coaching.target_admission_batches` ve `coaching.target_admission_records` tablolarını ve bunların indeks/ilişkilerini ekledi. Kullanıcı/profil/plan/çalışma tablolarına DDL veya veri değişikliği yapılmadı.

Her partide yıl, aşama, resmî kaynak URL'si ve SHA256 belge/paket özeti saklanır. Okul adı düzeltmeleri ham kaynağı silmeden kanıtlı ad listeleri üzerinden uygulanır. Türetilmiş paket özeti kaynak metaverisini ve gözden geçirilmiş kararları birlikte temsil eder; ham kaynak özetleri araştırma manifestlerinde ayrıca korunur.

Farklı illerin çakışmayan kaynakları aynı yılda kabul edilir. Aynı kaynağın yeni belge sürümü veya başka kaynakla çakışan kabul satırı sessizce ezilmez. Resmî `*.meb.k12.tr` kaynakları kabul edilir; benzer görünen farklı alan adları reddedilir.

Aktarım ayrı operatör sürecinde çalıştı. Mevcut API/ön yüz konteynerleri yeniden dağıtılmadı. Yeni yıl/aşama/kontenjan filtrelerinin ekran dağıtımı bu işlemde yapılmadı; veritabanı aktarımı ile arayüz yayını ayrı işlerdir.

## Prova, yedek ve kontroller

1. Canlı coaching veritabanının 13 MB sıkıştırılmış yedeği sunucuda özel dizine alındı.
2. Yedeğin yerel bilgisayara indirilmesi otomatik güvenlik incelemesince reddedildi. Bu yol kullanılmadı; yedek aynı sunucudaki `coaching_lgs2026_rehearsal_20261019` adlı izole veritabanına geri yüklendi.
3. Eklemeli geçiş ve sekiz paket önce bu tam kopyada çalıştırıldı. Her paket aynı süreçte ikinci kez çalıştırıldı ve aynı parti kimliğiyle idempotent döndü.
4. Korunan bütün coaching tablolarının satır sayısı ve satır içerik özetleri önce/sonra karşılaştırıldı. Yeni katalog tabloları ve geçiş geçmişi bu karşılaştırmanın dışında; eski okullar ayrıca tam içerik özetiyle kontrol edildi. Program tablosu, profiller, hedefler, planlar, görevler, kazanım bağlantıları, ödevler ve çalışma kayıtları değişmedi.
5. Aynı paket canlıya uygulandı. Aynı koruma karşılaştırmaları yeniden PASS verdi; 432/274/147/9/2 sayımları SQL ile doğrulandı.
6. Mevcut coaching servisi `/health/live` yanıtı **Healthy**, konteyner Running=true.
7. Sunucudaki geçici prova veritabanı kaldırıldı; asıl coaching_db ve özel yedek korundu.

Tam backend regresyonu: **135/135 başarılı, atlanan yok**. Python kaynak/paket testleri: **11/11 başarılı**. İlgili C# katalog alan modeli/aktarıcı/sorgu kodunun satır kapsamı **%91,07 — 194/213**; tüm uygulamanın kapsamı değildir.

Prova sırasında `20261017215200_AddAssignmentInstructionAttachments` adlı eski, katalog dışı geçişin canlıda beklediği saptandı. Bu geçiş uygulanmadı. Operatör yalnız kullandığı katalog geçişlerini ve model uyumunu zorunlu tutar; diğer uygulama geçişlerini çalıştırmaz. Canlı kontrol bu ödev eki geçişinin hâlâ beklediğini doğruladı.

## Kanıtlar ve tekrar üretim

- Paketler: `data/target-catalog/2026/lgs-import-reviewed/` — 8 JSON.
- Hazırlayıcı: `tools/target-catalog/prepare_lgs_import_2026.py`.
- Kaynak araştırması ve kararlar: `lgs-national-research-followup-20261019.md`, `lgs-identity-resolutions-20261019.json` ve önceki ham kaynak manifestleri.
- Test logu: `artifacts/target-catalog-20261019/lgs-import-full.log`.
- Sunucu koruma özetleri, yedek, parti logları ve PASS işaretleri: `/var/lib/eduivme/releases/lgs-target-import-20261019/` — özel dizin; kişisel kayıtların kendisi rapora veya çalışma alanına aktarılmadı.
- İlk R1 arşivi SHA256: `0baa23f65d28bdc4462f24885975862ac651087e79ae4d710ff16553cf4dbd20`.
- Katalog dışı geçişi uygulamayan son operatör DLL'i SHA256: `e61f759df7fd57ffa799fec91a68b037565e61de27baf745a01c8ac18daa384d`. R1 çalışma dizinine bu doğrulanmış operatör uygulandı; sunucu betiği de bu ek özeti kontrol eder. R1 arşivi tek başına son operatörü içermez.

Araştırma dosyalarının `NotImported` etiketleri o aşamadaki tarihsel karardır; fiili aktarım durumu bu rapor ve yeni aktarım durum dosyasıdır. Kaynak kanıtı değişmesin diye eski araştırma JSON'ları yeniden yazılmadı.
