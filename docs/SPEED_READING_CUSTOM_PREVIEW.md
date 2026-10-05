# Özel ayarlarla dene

## Kapsam

Egzersiz kataloğunda Admin, SystemAdmin ve yalnız öğretmen rolüyle kullanılan
önizlemelere geçici ayar formu eklendi. Öğrenci rolü de olan hesaplar mevcut
öğrenci oturumu davranışını korur. Program eğitimi, atama, öğrenme yolu,
tekrar ve gerçek ölçüm oturumlarında özel ayarlar uygulanmaz.

Kullanım: katalogdan egzersiz/seviye seç → Özel ayarlarla dene → değerleri
değiştir → Denemeyi başlat. Varsayılana dön kayıtlı ayarları yeniden yükler;
İptal oturum başlatmaz. Normal başlatma seçeneği korunur.

## Desteklenen kontroller

| Motor | Geçici kontrol |
| --- | --- |
| word_highlight | Hız (20–1500 WPM), kelime grubu (1–10) |
| subvocalization_reduction | Hız (20–1500 WPM), kelime grubu (1–10); süre hızdan türetilir |
| regression_reduction | Hız (20–1500 WPM), kelime grubu (1–10); eski gecikme özel hızın önüne geçmez |
| text_fade | Hız (20–1500 WPM) |
| text_stream | Gösterim (50–5000 ms), bekleme (0–10000 ms); adaptif kurallar korunur |
| motion_path | Tracking: hız seviyesi (1–5); saccade: geçiş (50–10000 ms); fixation: odaklanma (50–10000 ms) |
| scan_find | Süre sınırı (1–3600 saniye) |
| focus | Uyaran süresi (100–10000 ms); N-back zorluğu ve diziler korunur |
| vocabulary_builder | Quiz modunda kelime süresi (0–3600 saniye; 0 sınırsız) |
| visual_expansion | Gösterim (50–5000 ms), bekleme (50–10000 ms) |
| reading_comprehension, free_reading, exam_simulation | Metin boyutu: küçük/orta/büyük |

Grid, hata analizi, görselleştirme ve adaptif akıcılık için bu sürümde özel
kontrol açılmadı. Bunlarda uygun kontrolün içerik üretimi/evre davranışından
ayrılarak tasarlanması gerekir. Desteklenmeyen motor veya kelime öğrenme modu
boş form açmaz; açıklama gösterir. Tüm motorlar için tam ayar editörü değildir.

## Kalıcılık ve güvenlik

- Ayarlar yapılandırmanın klonuna uygulanır; katalog kaydı değiştirilmez.
- Egzersiz kimliğiyle eşleşen yerel gezinme durumu kullanılır; URL'ye içerik yazılmaz.
- Yalnız izinli alanlar, sonlu tam sayılar ve tanımlı metin boyutları kabul edilir.
- Metin, soru, hedef dizisi ve kimlikler kullanıcı ayarlarından alınmaz.
- Önizleme sunucu eğitim oturumu açmaz, sonuç/ilerleme kaydetmez.
- Kelime önizlemesi tarayıcıdaki kalıcı kelime ilerlemesini okumaz veya yazmaz.
- Bu istemci kontrolü sunucu yetkilendirmesinin yerine geçmez; mevcut API
  yetkilendirmesi değiştirilmedi.

## Doğrulama

Testler yeni kontrol sınırlarını önce başarısız durumda gösterip uygulamadan
sonra tekrar çalıştırıldı. Katalog iptali, boş form, rol, atama/ölçüm izolasyonu,
sunucu oturumu açmama, klonlama, eski ayar öncelikleri ve tarayıcı depolama
izolasyonu kapsandı. Motorların mevcut regresyon testleri birlikte çalıştırıldı.

Gerçek Identity oturumuyla uçtan uca görsel/mobil kontrol ve canlı yayın bu
yerel geliştirme doğrulamasından ayrıdır. Bu özellik henüz canlıya alınmadı.
