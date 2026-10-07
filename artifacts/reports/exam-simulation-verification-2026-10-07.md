# Sınav Simülasyonu düzeltmeleri — 7 Ekim 2026

## Uygulanan kapsam

- Soruya özel paragraf yoksa oturumun sunucu kaynaklı ana metni gösterilir.
- Otomatik ve açık metin seçiminde aynı seviye zorunludur; mevcut yaş/egzersiz kapsamı filtreleri korunur. Otomatik seçim daha az kullanılmış metinleri önce getirir.
- Her soru sıralı `exam_question_start` aksiyonuyla sunucuda başlatılır. Yeniden başlangıç saati sıfırlamaz; sunucu kalan süreyi döndürür.
- Süre kök veya engineConfig timing.questionTimeSeconds alanından alınır; varsayılan 60 saniye, sınır 1–3600 saniyedir.
- Cevap süresi sunucuda hesaplanır. Erken timeout reddedilir; geç cevap boş sayılır. Başlatılmamış soru cevaplanamaz. Toplu tamamlama isteği doğrulanmış cevap akışını atlayamaz.
- Sayfa yenilemede kayıtlı cevaplar ve kalan soru korunur. Ekran kapandıktan sonra bekleyen yanıt yeni sayaç kuramaz.
- Özel önizleme ayarlarında soru süresi, yazı boyutu ve satır aralığı bulunur. Öğrenci sınav ekranında süre durdurulamaz.
- Sınav etkinliği okuma hızı olarak raporlanmaz; sunucu WPM üretmez. Yanıltıcı hedef WPM etiketi kaldırıldı. Önizleme sonuç süresi cevaplama sürelerinin toplamından hesaplanır; gerçek cevap süreleri sunucu geri bildiriminden alınır.

## Doğrulama ve sınırlar

Yeni testler önce başarısız çalıştırıldı, ardından düzeltmelerle geçirildi. Seviye reddi, soru başlangıcı, erken/geç timeout, istemci süre manipülasyonu, tekrar başlangıç ve kayıt idempotansı test edildi. Gerçek PostgreSQL testi geçici Docker veritabanında; masaüstü/mobil testler yerel Angular uygulamasında taklit API ile çalıştırıldı.

Backend genel regresyon: 952 test. Frontend genel regresyon: 677 test. Sınav PostgreSQL testi: 1. Masaüstü/mobil akış: 2. Son yürütmelerin durumları sohbet raporunda belirtilir; bu sayılar kod kapsaması yüzdesi değildir.

Bu egzersiz süreli paragraf/anlama pratiğidir; resmî sınav eşdeğerliği veya psikometrik geçerlik iddiası değildir. Soru sayısı mevcut metnin puanlanabilir soruları kadardır. Canlı içerik envanteri bu yerel doğrulamanın kapsamına dahil değildir.

Yeni veritabanı migration yok; zamanlama alanları mevcut oturum JSON'unda tutulur. GitHub gönderimi ve canlı yayın yapılmadı. Önceden mevcut ilgisiz Metin Solma değişiklikleri korunur. Mevcut oyuncu SCSS bütçe uyarısı (128,48 kB / 120 kB) bu çalışmada değiştirilmedi.
