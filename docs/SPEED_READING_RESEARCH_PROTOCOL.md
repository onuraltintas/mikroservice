# Hızlı Okuma Pilot Ölçüm Protokolü

Bu protokol, platformun eğitim etkisini araştırmak için kullanılır. Platform
sonuçları, bağımsız inceleme tamamlanmadan bilimsel etkililik iddiası olarak
sunulmaz.

## Çalışmayı açma

1. Yönetim panelinde **İçerik yapılandırması > Seviye sözlüğü > Araştırma çalışmaları** bölümünden çalışma oluşturulur.
2. Çalışma kodu, protokol sürümü, kohort kodu, onam belge sürümü ve belge referansı kaydedilir.
3. Katılımcılar, ad veya e-posta ile aranıp yalnız aktif çalışmaya eklenir.
4. Onam zamanı kaydedilmeden katılımcı eklenmez. Yeni katılımda çalışma, kohort ve onam bilgileri katılım kaydına sabitlenir.

## Ölçüm takvimi

| Faz | Zaman | Amaç |
| --- | --- | --- |
| Başlangıç | Eğitime başlamadan önce | Hız, kavrama ve verimlilik başlangıç ölçümü |
| Eğitim sonrası | Başlangıçtan sonra, program tamamlandığında | Kısa dönem değişim |
| Kalıcılık | Eğitim sonrası en erken 7. gün | Öğrenmenin korunması |
| Transfer | Kalıcılıktan en erken 21 gün sonra | Eğitim sonrası en erken 28. gün; görülmemiş içerikte transfer |

Fazlar sunucu tarafından kilitlenir. Öğrenci istemcisi bu süreleri geçersiz
kılamaz. Her assessment girişimi seçilen formu, yaş grubunu, seviye sözlüğünü ve
çalışma bilgisini sabitler.

## İçerik ve soru kalitesi

- Her metin için hedef yaş grubu, dil, önerilen seviye aralığı ve zorluk
  girilir.
- Yönetim panelindeki **İçerik kalite ön kontrolü**, Türkçe metinlerde Ateşman
  okunabilirlik tahmini, cümle/kelime uzunlukları ve Bloom/zorluk dağılımını
  gösterir. Bu kontrol editöre uyarı verir; uzman dil ve eğitim incelemesinin
  yerine geçmez.
- Kavrama değerlendirmesinde en az üç soru, farklı Bloom seviyeleri ve transfer
  fazında başlangıçtan farklı bir metin/form kullanılmalıdır.
- Cevap anahtarı öğrenciye dönmez; puanlama, süre ve ölçüm durumu yalnız
  sunucu kayıtlarından üretilir.

## Analiz ve yayın eşiği

Admin'deki kalibrasyon görünümü; faz, yaş grubu, seviye sözlüğü ve çalışma
kohortuna göre WPM ile kavrama dağılımını gösterir. Bir segment en az 30 geçerli
ölçüm içermedikçe `PilotOnly` olarak kalır; bu eşik norm veya pazarlama iddiası
için tek başına yeterli değildir.

Raporlarda başlangıç-sonuç, kalıcılık ve transfer ayrı gösterilir. WPM, kavrama
ve verimlilik tek bir başarı puanında gizlenmez. Eksik ölçüm, ölçülmemiş motor
veya tamamlanmamış fazlar açıkça belirtilir.

## Veri koruma ve bağımsızlık

Araştırma ekranı kişi adı/e-postasını kalibrasyon segmentlerine taşımaz. Ham
katılımcı verisi yalnız yetkili yönetici tarafından işlenir. Çalışma başlamadan
önce kurumun onam, saklama süresi, erişim yetkisi ve gerekli etik/kurumsal
onaylarını kendi mevzuatına göre tamamlaması gerekir. Bağımsız değerlendirme,
kontrol veya eşleştirilmiş karşılaştırma grubu ile yürütülmelidir.
