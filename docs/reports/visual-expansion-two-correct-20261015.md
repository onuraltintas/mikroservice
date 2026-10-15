# Görsel genişleme: ilk cevaptan sayım ve iki doğruda ilerleme

> Tarihsel yayın kaydıdır. Kullanıcının sonraki isteğiyle 15 Ekim 20:35'te kural yeniden her doğru cevapta ilerleme olarak değişti. Güncel sürüm: [her doğruda ilerleme raporu](visual-expansion-each-correct-20261015.md).

15 Ekim 2026, Türkiye saati 20:10:20 (`2026-10-15T17:10:20Z`) canlıya alındı. Ürün kaynağı: `51b530423c36d3a848a1a53c28d7025526d68662`. Yayın: `visual-expansion-two-correct-20261015`.

## Düzeltilen davranış

Normal öğrenci çalışması ve önizlemede doğru/yanlış sayımı ilk cevaptan başlar. Normal eğitim ayarlarında bulunan değerlendirme protokolü yanlışlıkla okunuyordu; protokolün üç alıştırma turu puan dışı kalıyor, dördüncü turdan itibaren sayaç ilerliyordu. Protokol ve puan dışı alıştırma etiketi artık yalnız gerçek seviye tespiti modunda kullanılır.

Hedef uzaklığın altında her iki tam doğru cevap, göreli uzaklığı bir birim artırır. Yanlış veya kısmen doğru cevap genişliği değiştirmez; önceki tek doğru da korunur. Örneğin doğru → yanlış → doğru, aynı uzaklık → aynı uzaklık → bir adım artış sonucunu verir. Bu birim fiziksel açı ölçümü değildir; yatay ekranda bir birim yaklaşık 0,75 yüzde puan yerleşim farkıdır.

Hedefe ulaşıldığında mevcut üç ardışık doğruyla gösterim süresini azaltma kuralı korunur. Eski ve bloklu seviye tespitinin sunum, puanlama ve süre kuralları değişmedi. Mevcut eğitim oturumlarının duraklatma/devam ve kayıtlı ayarları korunur. Migration veya katalog güncellemesi yoktur.

## Doğrulama

| Kontrol | Sonuç |
|---|---:|
| Backend birim testleri | 1294/1294 |
| Backend entegrasyon testleri | 190/190 |
| Seçili görsel genişleme/değerlendirme backend testleri | 145/145 |
| Gerçek PostgreSQL görsel genişleme testleri | 8/8 |
| Çalışma alanı öğrenci testleri | 1006/1006 |
| Gerçek canlı tabandan izole öğrenci testleri | 997/997 |
| Masaüstü/mobil tarayıcı senaryoları | 24/24 |
| Aday imajın üretim DB'sinin ayrı kopyasındaki kontrolleri | 146/146 |

Satırlar kısmen aynı testleri kapsar; sayılar toplanarak tekil test toplamı oluşturulmaz. Masaüstü/mobil senaryoları fixture API ile önizlemeyi, yaş/seviye yerleşimlerini, sayımı ve iki doğruda ilerlemeyi doğrular. PostgreSQL kontrolleri gerçek session present/answer, her cevapta sayaç, idempotency, sonuç özeti, mola/devam ve seviye tespitini kapsar.

Değişiklik öncesi RED kayıtları gecikmiş sayaçları, eski ilerleme kuralını ve normal eğitimde çıkan puan dışı alıştırma etiketini yeniden üretti. Frontend ve backend üretim derlemeleri başarılıdır. Egzersiz SCSS dosyasının önceden mevcut 120 KB bütçe uyarısı sürer. Global yüzde 80 kapsam veya bütün egzersizlerin canlıda sınandığı iddia edilmez.

## Canlı öğrenci kontrolü

Yeni Chrome sekmesinde gerçek `testogrenci@eduivme.com` oturumuyla Egzersizler → Görsel Genişleme → Yetişkin → Seviye 1 açıldı. CAPTCHA ayarı açık ve eşik 0,5 olarak korundu.

| Tamamlanan cevap | Doğru | Yanlış | Göreli uzaklık |
|---|---:|---:|---:|
| İlk cevap yanlış | 0 | 1 | %6 |
| İkinci cevap yanlış | 0 | 2 | %6 |
| Üçüncü cevap doğru | 1 | 2 | %6 |
| Dördüncü cevap yanlış | 1 | 3 | %6 |
| Beşinci cevap doğru | 2 | 3 | %6,8 |

Beşinci cevaptan sonra altıncı turun artmış uzaklığı gösterildi. Sayaçlar ilk cevaptan itibaren ilerledi; yanlış cevap genişliği değiştirmedi ve birikmiş doğruyu sıfırlamadı. Görünen harfler normal arayüzden okunarak yanıtlandı; uygulama saati veya gizli cevap durumu değiştirilmedi. Bu beş cevaplık deneme normal çıkış akışıyla kapatıldı; tam egzersiz sonuç kaydı oluşturulduğu iddia edilmez. Tarayıcı konsolunda bu deneme sırasında hata/uyarı kaydı yoktu.

Ekran kanıtları: `artifacts/visual-expansion-two-correct-20261015/live-first-answer.jpg` ve `live-two-correct.jpg`. Test ve izleme kayıtları aynı artifact dizinindedir.

## Yayın kapsamı ve geri dönüş

Son gerçek canlı kaynak olan `speed-reading-assignment-fixes-20261015` izole edildi. Backend'de yalnız `VisualExpansionRoundRules.cs`; öğrenci arayüzünde yalnız motorun protokol/ilerleme kuralı ve oynatıcının alıştırma etiketi koşulu değişti. Çalışma alanındaki diğer geliştirmeler pakete alınmadı. API'ye yalnız Application DLL/PDB kopyalandı; gerçek canlı Domain ve Infrastructure dosyaları korundu. Öğretmen/kurum personel arayüzü, admin ve Identity yayımlanmadı.

- API: `sha256:4237797b5b28981b44b07d351979eb8f8ec1028a80d47f1dd3cd186b20271185`
- Öğrenci arayüzü: `sha256:918088543109d18261353a321f374fd5685a3c07ef1f033db241ef8e2bcfe880`
- Aktarım arşivi SHA256: `919772eb4902a60c6b3ce523d67f2c866df9053f9af1c5ac53e3ca74096f576c`

Yedek alındı; adayın gerçek DLL'leri yalnız `visual_expansion_two_correct_drill_20261015` adlı üretim DB kopyasında çalıştırıldı ve kopya sonrasında kaldırıldı. Prova onayı aday imaj ve DLL hash'lerine bağlıdır. İki yayın kilidi, önceki imaj kimlikleri, diğer konteynerlerin korunması, hazır olma kontrolleri ve eski iki imaja otomatik geri dönüş betikte bulunur. Önceki hashli arayüz varlıkları korunur; açık eski PWA sekmeleri yeni sürümü görmek için kapatılıp yeniden açılabilir.

Yayın kaynakları: `tools/releases/visual-expansion-two-correct-20261015/`. Üretim paketi ve erişimi kısıtlı yedek sunucuda `/var/lib/eduivme/releases/visual-expansion-two-correct-20261015` altındadır. Parola, bağlantı dizesi, üretim ortamı dosyaları ve DB yedekleri depoya eklenmez.

Yayın sonrası 15 dakikalık izleme `2026-10-15T17:25:34Z` tarihinde tamamlandı: public HTTP 200, API/arayüz restart sayıları 0 ve yayın sonrası 41 istekte sunucu 5xx sayısı 0. Bu düşük trafik örneği bir yük testi değildir. Son kayıt: `artifacts/visual-expansion-two-correct-20261015/production-health-final.log`.
