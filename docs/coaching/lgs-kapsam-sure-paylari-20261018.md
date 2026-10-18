# LGS kazanım kapsamı ve süre payları — 18.10.2026

## Sonuç ve kapsam

LGS kataloğundaki 269 kazanım korunarak 144 etkin alt konuya 319 sürümlü kapsam/süre payı tanımlandı. Birden fazla konuya bağlı kazanım artık yalnız ilk konuya yerleşmez. Yeni planlarda her konu kendi öğrenme, soru/alıştırma ve kontrol payıyla yerleşir; eski planlar otomatik yeniden hesaplanmaz.

Kazanım kodları resmî programdaki kodları korur. Numaralı alt konuların kapsamları planlama amacıyla yapılan editoryal bölümlendirmedir; MEBİ videosunun içeriğinin birebir dökümü olduğu iddia edilmez. Bu revizyon yalnız LGS içindir; genel 8. sınıf profili veya okul programı eklenmedi.

## Sürelerin anlamı

Süreler bir **ilk çalışma turu** için başlangıç tahminidir. Resmî MEB bireysel öğrenme süreleri değildir; video süreleri veya yıllık ders saatleri doğrudan bireysel dakikaya çevrilmedi. Kapsam ve soru yükü dikkate alınarak öğrenme, soru/alıştırma ve kontrol bileşenleri belirlendi. Süre kayıtları `ReviewedStudy`, güven düzeyi `Low` ve gerekçeleriyle yayımlandı. Onaylı/yayımlanmış olmak ölçülmüş veya uzman doğrulamalı olmak anlamına gelmez.

Tekrar, ek deneme, mola ve öğrencinin seviyesine göre ilave çalışmalar bu bütçenin dışında değerlendirilmelidir. Gerçekleşen çalışma süreleri toplandıkça yeni tahmin sürümleriyle kalibrasyon yapılabilir; bu revizyon öğrenci geçmişini değiştirmez.

| Ders | Önceki toplam dk | Yeni ilk tur toplamı dk |
| --- | ---: | ---: |
| Din Kültürü ve Ahlak Bilgisi | 2880 | 1405 |
| Fen Bilimleri | 5280 | 2430 |
| Matematik | 7215 | 2480 |
| T.C. İnkılap Tarihi ve Atatürkçülük | 2880 | 2320 |
| Türkçe | 3315 | 1840 |
| İngilizce | 4445 | 1495 |
| **Toplam** | **26015** | **11970** |

Yeni toplam 199 saat 30 dakikadır. Her alt konunun üç bileşenli bütçesi, ona bağlı kazanım payları arasında tam dakika olarak dağıtıldı. Her kazanımın payları da yeni kazanım bütçesinin her bileşenini tam karşılar; aynı dakika iki kez sayılmaz. Bu tahminler öğrenciye kişisel başarı veya bitirme garantisi vermez.

## Başlıca kapsam düzeltmeleri

- Paragrafta Anlam 1–4: her bölüm için ayrı kapsam ve 100 dakika bütçe. Konu/ana fikir, anlatım ve yorum, çıkarım, medya/bilgi kaynağı gibi kapsamlar ayrıldı. Eskiden aynı 21 kazanım dört bölüme bağlanıyor ve motor yalnız ilk bölümde tutuyordu.
- Doğrusal Denklemler 1: `M.8.2.2.1–3`; ikinci bölüm: `M.8.2.2.3–6`. Ortak `.3` kazanımı bölüm paylarıyla yerleşir.
- Cümlede Anlam, yazım ve noktalama gibi ortak kazanımlar: farklı uygulama kapsamları ve ayrı dakika payları kullanılır.
- İngilizce: ortak dil işlevleri konu kapsamına göre daraltıldı. Programdaki dil becerisi kodları, LGS yazılı diyalog/metin/soru kapsamında kullanılır; sözlü performans ödevi oluşturulmaz.
- Din Kültürü: `8.4.7` ve `8.4.8`, MEBİ servisindeki video-kazanım bağlantısına uygun olarak `8.4.6` ile birlikte **Hz. Muhammed’in İnsanlara Değer Vermesi** konusuna bağlandı. Fazladan açılmış **Hz. Muhammed’in Örnek Davranışları ve Hikmetli Sözleri** konusu silinmedi; hiçbir çalışma veya öğrenci konu durumunca kullanılmadığı doğrulanarak pasifleştirildi. Eski eşleştirme satırları tarihçe için korundu, yeni kullanım onayları kaldırıldı.
- MEBİ ile yazım farklılığı olan mevcut konu adları değiştirilmedi; eşleşme kaynak konu kimlikleriyle kuruldu.

## Veri modeli ve sürüm seçimi

Yeni `coaching.study_catalog_topic_outcome_shares` tablosu:

| Alan | İşlev |
| --- | --- |
| TopicId, OutcomeId, EstimateId | Birlikte birincil anahtar; konu/kazanım/süre sürümü |
| ScopeDescription | O alt konuda çalışılacak kapsam |
| LearningMinutes | Öğrenme payı |
| PracticeMinutes | Soru/alıştırma payı |
| CheckMinutes | Kontrol payı |

Paylar mevcut konu–kazanım eşleştirmesine ve aynı kazanımın süre sürümüne yabancı anahtarla bağlıdır. Negatif süre ve sıfır toplam reddedilir; bileşen toplamı 1–1440 dakikadır. Silme kısıtlıdır.

269 yeni süre sürümü ve 319 pay eklendi. Önceki sürümler silinmez; eski çalışma–kazanım bağlantıları aynı EstimateId ve kayıtlı dakikaları kullanır. Yeni plan okuyucusu ilgili eğitim yılı için en yüksek yayımlanmış sürüm numarasını alır. Kazanım kodu değişmez; konu payı bu sürüme bağlıdır.

Yönetici yeni süre tahmini oluşturduğunda önceki yayımlanmış paylar oransal olarak yeniden hesaplanır; üç bileşenin toplamı tam korunur. Onaylı konu kümesi değişmişse kapsam payları açıkça düzeltilmeden yayımlamaya izin verilmez. Eksik paylı konu, eski bütün kazanım bütçesiyle sessizce planlanmaz.

Sabitlenmiş eski çalışmaların dakikaları yeniden planlamada yalnız bir kez düşülür. Yeni kapsamlı sürümde sabitlenmiş çalışma yalnız kendi konusunu karşılar. Eski bütün kazanım sürümü gerektiğinde diğer konu paylarını da karşılayabilir; tek kaydın dakikası birden fazla kez kullanılamaz. Tamamlanan çalışmalar değişmez.

## Koruma ve doğrulama

- Migration `20261018203437_TopicOutcomeScopeShares` yalnız yeni tablo ve indeks ekler; eski tablo/veri silmez.
- Identity ve Coaching veritabanı yedekleri geçiş öncesi sunucunun özel release dizinine alındı. Kimlik bilgileri rapora veya pakete yazılmadı.
- Mevcut canlı servis imajının dört Coaching DLL özeti doğrulanarak yalnız koçluk servisi güncellendi. Diğer servislerin konteyner kimlikleri değişmedi.
- Çalışma alanındaki ilgisiz, henüz canlı olmayan ödev eki modeli dağıtıma katılmadı. İlk izole prova bu model farkını yakaladı; canlı temel kaynağına uygun snapshot/designer üretildi. Geçici model kontrol migrationı paketten çıkarıldı. Son pakette `has-pending-model-changes` kontrolü geçti ve yalnız eklemeli migration kaldı.
- Canlı verinin izole kopyasında migration iki kez çalıştırıldı; aktarım iki kez çalıştırıldı. İkinci aktarım 0 yeni sürüm ve 0 metadata değişikliği üretti.
- Eski planlar, görevler, süre sürümleri, kazanım bağlantıları, öğrenci profilleri ve taslaklar satır özetiyle karşılaştırıldı. Katalogda yalnız izin verilen süre/etkinlik/eşleştirme alanları değişti.
- 95 Coaching testi gerçek PostgreSQL ile geçti; atlanan test yok. Eski kazanım/planlama akışları için 13 test ve veri revizyonu için 6 Python testi geçti. Son eklenen Python testi MEBİ ile farklı yazılan mevcut konu adının korunmasını doğrular.
- Yeni pay varlığı ve EF konfigürasyonu satır kapsamı %100; bütçe sınıfı %86,48; yerleştirme sınıfı %97,72.
- 144 konunun 319 payı yayımlanmış katalog okuyucusundan tekrar okundu. Örnek haftalık kapasiteyle 1 Haziran’a kadar yıllık önizleme 11970 dakikanın ve 144 konunun tamamını yerleştirdi; öğrenci için test planı oluşturulmadı.

### Canlı doğrulama sonucu

Geçiş 18.10.2026 20:53 UTC’de tamamlandı. Sağlık kontrolü geçti; ikinci canlı aktarımda 0 yeni süre sürümü ve 0 metadata değişikliği oluştu. 8 ortak hesap korundu. 9 plan, 681 çalışma/görev, 676 kazanım bağlantısı, 6 tamamlanan çalışma ve 115 gerçekleşen dakika değişmedi. Eski satır özetlerinde kayıp/değişiklik yok; diğer servislerin konteynerleri aynı kaldı.

Yeni katalog: 269 kazanım, 319 pay, 144 etkin alt konu. Önceki tahmin sürümleri aynen duruyor; 269 yeni yayımlanmış sürüm eklendi. Canlı katalog okuyucusu ve yıllık önizleme doğrulaması geçti. Öğrenci planı yayımlanmadı veya değiştirilmedi.

## Kaynak ve dosyalar

- MEBİ: https://ogmmateryal.eba.gov.tr/mebi — 18.10.2026 servis karşılaştırması. 144 konu, 32 doğrudan servis kazanımı doğrulanabildi; kalan kazanımların resmî program kaynakları önceki aktarımda kayıtlıdır. Bu revizyon tüm 269 kazanımın MEBİ API’sinden bağımsız doğrulandığı iddiasını taşımaz.
- Revizyon: `data/coaching-outcomes/lgs-scope-revision-2026-v2.json`
- Üretici ve test: `tools/coaching-outcomes/revise_lgs_scopes.py`, `test_revise_lgs_scopes.py`
- Kontrollü geçiş/doğrulayıcı: `tools/releases/coaching-scope-shares-20261018/`
- Yerel test ve arındırılmış sonuçlar: `artifacts/coaching-scope-shares-20261018/`
