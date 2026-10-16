# Hızlı Okuma kelime puanlama düzeltmesi

Tarih: 16 Ekim 2026. Kapsam: kelime egzersizinin ilk puan kaydında oluşan çift gamification kaydı, sabit kelime listesiyle PostgreSQL başlatma uyumsuzluğu, geçmiş veriyi koruma ve API yayını.

## Hata ve düzeltme

Kelime cevapları tamamlanırken başarı ödülü yeni bir puan satırı oluşturabiliyordu. Oturum tamamlama adımı, henüz kaydedilmemiş bu satırı görmeden ikinci satır oluşturuyordu. InMemory testinde iki satır görüldü; ayrı PostgreSQL kopyasında yalnız XP düzeltmesi çıkarılınca ilk test tamamlama işlemi `23505 / ix_user_gamification_user_id` benzersizlik hatasıyla başarısız oldu. Mevcut puan ve öğrenme senaryoları aynı çalıştırmada geçti (1 RED / 3 GREEN). Artık aynı DbContext içindeki bekleyen satır yeniden kullanılıyor. Var olan puan satırının kimliği ve birikmiş XP'si korunuyor.

PostgreSQL uçtan uca testi ayrıca sabit `vocabularyItemIds` listesinin `.Contains` çağrısında .NET 10 Span overload'u ile EF 9 sorgu çevirisinin uyumsuzluğunu buldu. Çağrı, projedeki mevcut örneklerle uyumlu `Enumerable.Contains` biçimine getirildi. Kelime seçimi ve puan kuralı değişmedi.

Üretim kaynak farkı tek dosyada iki küçük düzeltmedir. Migration, backfill, eski sonuçları yeniden puanlama veya silme yoktur. İlgisiz çalışma ağacı değişiklikleri yayına alınmadı.

## Otomatik doğrulama

| Kontrol | Sonuç |
|---|---|
| Hatanın başarısız testle gösterilmesi | XP satırı 2 yerine 1 beklenirken başarısız; 1 RED / 17 GREEN |
| Kelime birim testleri | 18/18 başarılı |
| Son kaynakla bütün Hızlı Okuma birim testleri | 1317/1317 başarılı |
| Gerçek PostgreSQL ve HTTP testleri | 18/18 başarılı; dört yeni kelime senaryosu dahil |
| Sabit kelime sorgusu RED kanıtı | PostgreSQL testinde dört başlatma hatası; düzeltme sonrası geçti |
| XP hatasının PostgreSQL RED kanıtı | Ayrı eski-XP kopyasında yeni kullanıcı quiz'i `23505` verdi; diğer üç senaryo geçti |
| Üretim veritabanı kopyası ve gerçek aday DLL | Dört senaryo geçti; 86 tablo ve 8583 mevcut satır korundu |
| Yayına geçişte salt okunur veri karşılaştırması | 86 tablonun tamamında hash eşit; bekleyen migration 0 |
| Kod ve yayın incelemesi | Açık bulgu yok; fixture adı ve yayın sırasına ilişkin inceleme bulguları kapatıldı |

PostgreSQL senaryoları öğrenme/test modu ile yeni/mevcut puan satırı kombinasyonlarını sınar. Başlatma, her cevap, tamamlama ve tekrar tamamlama ayrı DbContext'lerden yapılır. Mevcut 400 XP, puan satırı kimliği, en yüksek okuma hızı ve okuma sayısı korunur. Başarım ve oturum XP'si bir kez eklenir; öğrenme modu XP üretmez. Tamamlama tekrarlandığında XP, soru sayısı ve tek sonuç kaydı değişmez.

## Canlı yayın ve veri korunması

Yayın zamanı: `2026-10-16T05:35:14Z` (08:35 Türkiye). Kaynak revizyonu: `e354768142b922f3f0dbe322a3b868e24d8ea0bc`.

API imajı: `sha256:8e9a316cf5ed87e98d4a856bba006fe3fe53291baf469ee333dba4ed6f13f58b`.

Yalnız API Infrastructure DLL/PDB değişti. Diğer üretim konteyner kimlikleri korundu. Üretim yedeği alındı; migration uygulanmadı. İki fingerprint de uygulama yazıcısı kapalıyken alındı; bu karşılaştırma yeni uygulama başlamadan veri ve şema mutasyonu yapılmadığını gösterir. Başlangıç sonrası davranış aşağıdaki gerçek öğrenci testiyle ayrıca doğrulandı.

İlk başlatma denemesinde compose bağımlılık hatası oluştu; otomatik geri dönüş eski imajı ve sağlığını doğruladı. Başlatma komutu bağımlılara gitmeden mevcut adayı açacak biçimde düzeltildi; ilk deneme kayıtları saklanarak tekrar yayınlandı.

## Canlı öğrenci testi

Kullanıcının normal CAPTCHA açık girişinden sonra `testogrenci@eduivme.com` hesabıyla Egzersizler → Kelime Hazinesi → Seviye 1 Kelime Bilgisi Testi açıldı. 12 soru arayüzden yanıtlandı. Ekranda **Sonuç kaydedildi**, skor **100**, doğruluk **%100** ve **+60 XP** görüldü. Test sırasında tarayıcı console error kaydı boştu.

Salt okunur önce/sonra karşılaştırması:

| Alan | Önce | Sonra |
|---|---:|---:|
| Toplam XP | 416 | 476 |
| Puanlanan egzersiz sayısı | 11 | 12 |
| Kelime soru sayısı | 0 | 12 |
| Tamamlanmış sonuç sayısı | 20 | 21 |
| Günlük görev kaydı | 12 | 12 |
| Program günü | 3 | 3 |
| Kazanılmış ek gün | 1 | 1 |

Önceki 20 oturum ve sonucun satır hash'leri, günlük görev kayıtları ve programın bütün alanları aynı kaldı. Mevcut gamification kimliği, okuma sayısı ve en yüksek WPM değişmedi. Eklenen tek sonuç yeni kelime testidir; eski puanların yeniden hesaplanması yapılmadı.

Kanıtlar: [Canlı sonuç görüntüsü](../../artifacts/vocabulary-scoring-20261016/live-vocabulary-result.jpg), [korunan üçüncü gün](../../artifacts/vocabulary-scoring-20261016/live-preserved-day-three.jpg), [veri karşılaştırması](../../artifacts/vocabulary-scoring-20261016/live-student-comparison.json), [üretim kaynağı izolasyonu](../../artifacts/vocabulary-scoring-20261016/source-isolation.json). Test ve yayın günlükleri `artifacts/vocabulary-scoring-20261016/` altında, özel yedek ve tam hash dökümleri sunucudaki yayın dizinindedir. [Yayın aracı](../../tools/releases/vocabulary-scoring-20261016/README.md).

Öğrenci raporu sunucudan yenilendi: mevcut başlangıç ölçümü, 20 günlük/okuma aktivitesi ve %86,7 ortalama kavrama korundu. Genel rapor bu sayacı günlük görevler ve okuma kayıtlarından hesaplar; katalogdan başlatılan bağımsız kelime testi günlük göreve eklenmez. Yeni testin tek oturum sonucu ve 60 XP'si ayrı kaydedildi. Günlük Egzersizler ekranında Hafta 1 / Gün 3 ve 0/6 görev durumu görüldü. Rapor kapsamı bu düzeltmede değiştirilmedi.

Son kontrol `2026-10-16T05:50:21Z` (08:50 Türkiye): yayından 15 dakika 7 saniye sonra API çalışıyor, restart sayısı **0**, yeni ERR/FTL/unhandled/duplicate-key/fail/fatal kayıt sayısı **0**. Periyodik readiness kontrolleri geçti. Tam aday imajı ve diğer üretim konteyner kimlikleri yeniden doğrulandı. Kanıt: `artifacts/vocabulary-scoring-20261016/live-monitor.log`.

Son arşiv `vocabulary-scoring-20261016-final.tar.gz` SHA-256: `049a0d20769849c7714044af08ef8f3abb470102297b58fd893f0beac8f42ec9`. Sunucuda arşiv hash'i ve dağıtılan manifest doğrulandı. İlk başarısız deneme kayıtları, eski imaj kimliği ve yedekler korundu.
