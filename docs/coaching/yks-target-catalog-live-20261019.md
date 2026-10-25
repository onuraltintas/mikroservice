# TYT ve AYT hedef kataloğu — canlı aktarım ve doğrulama

19 Ekim 2026. TYT, AYT Sayısal, Eşit Ağırlık ve Sözel kapsamındaki 2026 ana yerleştirme verileri canlıya aktarıldı. Mevcut uygulama sürümü ve şema yeterli olduğu için yeni uygulama dağıtımı veya veritabanı geçişi yapılmadı.

## Son durum

| Puan türü | Kaynak kaydı | Seçilebilir doğrulanmış kayıt | İncelemeye ayrılan |
|---|---:|---:|---:|
| TYT | 9.254 | 9.254 | 0 |
| SAY | 5.744 | 5.744 | 0 |
| EA | 3.900 | 3.899 | 1 |
| SÖZ | 1.914 | 1.914 | 0 |
| **Toplam** | **20.812** | **20.811** | **1** |

Bu işin kapsamı TYT ve AYT'dir. Hazır kaynak paketindeki 681 DİL programı bu aktarımın dışında bırakıldı; eski DİL hedefleri veya katalog satırları silinmedi.

## Kaynak doğrulaması

- [ÖSYM 2026 ana yerleştirme sayısal bilgileri](https://www.osym.gov.tr/2026-yks-yerlestirme-sonuclarina-iliskin-sayisal-bilgiler): Tablo 3 ve Tablo 4; program kodu, derece, puan türü, genel kontenjan, yerleşen sayısı, en küçük/en büyük puan.
- YÖK Atlas'ın kaydedilmiş 215 resmî API sayfası: tam 2026 program koduyla başarı sırası, yerleşim, eğitim dili, burs ve öğretim biçimi karşılaştırması. Sayfa sayısı, toplam eleman sayısı, yıl ve kaynak özetleri doğrulandı.
- İki ÖSYM dosyası ve bütün YÖK sayfaları SHA256 manifestiyle kontrol edildi. Kaynaklardan tekrar ayrıştırma ve birleştirme yapıldı; elde edilen 21.493 satır önceki tam paketle birebir aynı çıktı. Ardından TYT/SAY/EA/SÖZ alt kümesi hazırlandı.
- Geçmiş yıllardan puan veya sıralama alınmadı. Sıralama puandan tahmin edilmedi; resmî kaynakta bulunmayan alanlar boş kaldı. Seçilebilir 20.811 kaydın 17.679'unda başarı sırası var.
- Kaynak kapsamı **genel kontenjan**dır. Özel kontenjan türlerinin toplamı gibi sunulmadı. Katalogdaki yabancı/KKTC programları nedeniyle şehir/yerleşim filtresinde 83 farklı değer bulunması, Türkiye'de 83 il bulunduğu anlamına gelmez.

## Eşleştirmeler ve bulunan hata

İlk kod ve puan türü kontrolü: 19.230 mevcut kod eşleşmesi, 1.582 yeni program; tekrarlanan kod ve puan türü çelişkisi yoktu. Sonraki ad kontrolünde 13 farklı başlık bulundu; [başlık karşılaştırması](../../data/target-catalog/2026/yks-reviewed-20261019/title-differences.json) kaydedildi.

12 fark aynı bölümün adlandırma, parantez sırası, fakülte/bölüm ifadesi veya açıklama değişiklikleri olarak incelendi. Eski adlar güncellenmedi; resmî 2026 adları yıllık kayıtlarında tutuldu.

**409610874 kodu güvenli eşleşme sayılmadı:**

- Eski katalog: `Ekonomi (%25 İndirimli)`.
- ÖSYM ve YÖK 2026: `Gastronomi   (Ücretli)`, EA, 5 kontenjan, 0 yerleşen; aynı üniversite ve kod.
- Bölümün kendisi farklı olduğu için yalnız kod ve puan türü yeterli kabul edilmedi. Yeni yıllık satırın `ProgramId` bağlantısı kaldırılıp `Conflict` durumuna geçirildi; gerekçesi saklandı.
- Eski Ekonomi programı, aktifliği, puanı, kimliği ve ona ait olabilecek mevcut hedefler değiştirilmedi. Bu yeni yıllık kayıt doğrulanmış aramada ve resmî katalog seçiminde görünmüyor. Nihai sonuç: **19.229 onaylı eski kimlik bağlantısı + 1.582 yeni kimlik = 20.811 kullanılabilir kayıt**.

Bu sorun ilk kod kontrolünden sonra, son ad incelemesinde bulundu. Düzeltme önce prova veritabanında, ardından güncel sunucu yedeği alınarak canlıda uygulandı. Kaynak hücreleri değiştirilmedi. Düzeltilmiş inceleme bulgularıyla paket tekrarı yeni kayıt üretmeden çalıştı.

## Veritabanı ve koruma

- `target_university_programs`: mevcut **21.602** satır aynen korundu; **1.582** yeni satır eklendi. Toplam **23.184** kanonik program.
- `target_admission_batches`: YKS / 2026 / Main için kaynak özeti ve yayın bilgisi bulunan bir parti.
- `target_admission_records`: 20.812 yıllık kaynak satırı; 20.811 Verified, 1 Conflict. Kontenjan/puan/sıralama/filtre alanları burada saklanır.
- Hedefler mevcut `target_university_program_id` alanından kanonik programa bağlanır. Yıllık yerleştirme verileri ayrı kayıt olarak kalır; bu işlem hedef tablosuna bir yıllık sonuç anlık görüntüsü eklemez.
- Bütün eski okul satırları, LGS kaynakları, partileri ve bağlantıları aynen korundu. LGS bekleyen eşleşmelerine bu işte müdahale edilmedi. Identity üzerinde işlem yapılmadı.
- Aktarım öncesi sunucu yedeği aynı sunucuda ayrı veritabanına geri yüklenerek prova yapıldı. Tam veritabanı yerel bilgisayara indirilmedi.
- Prova ve canlıda eski program/okul/kaynak kayıtlarının içerik özetleri karşılaştırıldı; diğer Coaching tabloları da satır sayısı ve içerik özetleriyle korundu.
- UI testinden sonra eski **16 hedefin** tamamının içerik özeti ilk yedekle aynı çıktı. Diğer korunan çalışma tablolarının tamamı da değişmedi. İki açık etiketli yeni test hedefi eklendi; eski hedeflerden hiçbiri test amacıyla değiştirilmedi.

## Canlı öğrenci ve öğretmen testleri

| Kontrol | Sonuç |
|---|---|
| Her iki hesapta TYT / SAY / EA / SÖZ arama | Başarılı; kaynak kapsamıyla tutarlı sayımlar |
| Öğrenci: TYT + İstanbul + devlet + ön lisans + Türkçe + Bilgisayar + taban puan / en yüksek sıra | 3 sonuç; kontenjan, 2026 ve ana yerleştirme gösterildi |
| Öğrenci: TYT hedefi seçme/kaydetme | Marmara Bilgisayar Programcılığı `107250331`; yeniden yükleyince korundu |
| Öğrenci: SAY + İstanbul + devlet + lisans + İngilizce + Bilgisayar Mühendisliği + puan aralığı + sıralama | 4 sonuç; başarı sırasıyla doğru sıralandı |
| Öğrenci: Sayısal hedefi kaydetme | İTÜ Bilgisayar Mühendisliği `105510608`; sayfa yenilenince korundu |
| Öğretmen: atanmış öğrenciye yeni hedef oluşturma | Başarılı; önce TYT hedefi kaydedildi ve yeniden yüklendi |
| Öğretmen: EA + İstanbul + vakıf + lisans + İngilizce + burslu + sıra filtresi | 25 sonuç |
| Öğretmen: üniversite + Ataşehir + örgün öğretim + fakülte ile daraltma | Acıbadem İngilizce burslu Psikoloji `200190051`; tek sonuç |
| Öğretmen: Eşit Ağırlık hedefini kaydetme | Başarılı; sunucudan yeniden yüklenince korundu |
| Öğrenci: öğretmenin hedefini görme | Aynı program göründü; yalnız yetkili öğretmenin bağlantıyı düzenleyebileceği belirtildi |
| Veri yılı 2027 | 0 sonuç; 2026 veya yılı belirsiz eski veriler karıştırılmadı |
| Son kimlik incelemesinden sonra EA araması | Öğrencide 3.899 doğrulanmış kayıt |
| İncelemeye ayrılan `409610874` koduyla arama | Öğrencide 0 sonuç; kayıt seçime kapalı |

Öğretmenin kaydetme/yeniden açma ve öğrencide görünme kontrolleri tamamlandıktan sonra öğretmen oturumu giriş ekranına döndü. Son kimlik-ayırma işleminin arama kontrolü öğrenci hesabında ve ortak sunucu sorgusunda tekrarlandı; yeni öğretmen girişi istenmedi.

Canlı testte oluşturulan iki hedef:

- `YKS katalog canlı doğrulama · öğrenci · 19.10.2026` → `105510608`, SAY.
- `YKS katalog canlı doğrulama · öğretmen · 19.10.2026` → `200190051`, EA.

## Test ve işlem kanıtları

- Python veri hazırlama/ayrıştırma testleri: **21/21**.
- Gerçek yerel PostgreSQL kullanan hedef kataloğu aktarım/filtre/API regresyonları: **16/16**; derleme hatası/uyarısı yok.
- Tekrar aktarım yeni parti/satır üretmedi. Kimlik düzeltmesinin tekrar çalışması yeni değişiklik üretmedi. Diğer yıllık kayıtlar ve bütün program satırları aynı kaldı.
- İlk prova koruma kontrolü, yeni programların toplam sayıyı artırmasını da değişiklik olarak yakaladı. Yeni eklemelerden ayrı olarak **bütün eski programları** koruyan kontrol kullanıldı ve prova tekrar geçti; eski program koruması kaldırılmadı.
- Uygulama ön yüzünde ek kod düzeltmesi gerektiren bir akış hatası saptanmadı. Düzeltme veri eşleştirme bulgusuna uygulandı.
- Geçici prova veritabanı ve localhost test PostgreSQL süreci kapatıldı. Sunucu yedekleri korundu.

Kaynak, paket ve nihai durum: `data/target-catalog/2026/yks-reviewed-20261019/`.
Yerel günlükler/görsel: `artifacts/yks-target-import-20261019/`.
Sunucu özel iş klasörü: `/var/lib/eduivme/releases/yks-target-import-20261019/`.
Yedekler: `coaching-before.dump` ve UI hedefleri oluşturulduktan sonraki `coaching-before-identity.dump`.
Son ek işlem manifesti: `identity-payload.sha256`.

![Öğretmen hesabında kaydedilen Eşit Ağırlık hedefi](../../artifacts/yks-target-import-20261019/teacher-saved-goal.png)
