# Hızlı Okuma egzersiz yayını — 11 Ekim 2026

Uygulama sürümü `6f315ff3`, yayın araçları `116844de`; `codex/platform-hardening` dalına GitHub üzerinden gönderildi. Yayın 13:45:19 Türkiye saatiyle tamamlandı. GitHub CI sonucu ayrıca doğrulanmadı.

Hızlı Okuma API, öğrenci frontend'i ve Eduİvme admin paneli aynı uygulama kaynağından yayımlandı. Identity, Koçluk, Notification ve diğer servisler yeniden oluşturulmadı. Yaş programları v2 zaten canlıydı; yeniden uygulanmadı.

Tek işlemde uygulanan 11 paket: Takistoskop v2, Hızlı Okuma, Tarama, Gruplama, Metin Solma, Görsel Genişleme, Sabitleme, Sakkad, Regresyon v2, İç Ses Azaltma v2, Görselleştirme v2. Anlama'nın yeni içerik/ayar paketi bu ilk yayına dahil edilmedi; sonraki işlem [ayrı raporda](comprehension-content-20261011.md) izlenir.

## Doğrulama

- Sunucu birim testleri: 1124/1124; arayüz birim testleri: 812/812.
- Gerçek PostgreSQL testleri: 53 C# ve 25 Node testi geçti; katalog kontrolleri 25/25.
- Üretim API ve iki arayüz çıktısı hazırlandı. Mevcut 120 kB SCSS uyarı sınırı aşımı sürüyor; derlemeyi engellemedi.
- Güncel Hızlı Okuma yedeği alındı; geri yüklenmiş geçici veritabanında 11 paket provası geçti ve geçici veritabanı kaldırıldı.
- Canlıda hedeflenmeyen tabloların işlem öncesi/sonrası içerik özetleri eşleşti. Programlar, kullanıcılar ve sonuç geçmişi silinmedi.
- `masterhizliokuma.com`, `eduivme.com`, `onuraltintas.net` HTTPS sağlık kontrolleri 200; oturumsuz program API'si beklenen 401 döndürdü.
- Sunulan Hızlı Okuma `main-GEHHNODC.js`, `chunk-RJVCDS7K.js`, `ngsw.json` ve admin `main-AO7GCLAF.js` dosya özetleri yerel derlemeyle eşleşti.
- Gerçek admin oturumuyla canlı egzersiz kataloğu ve kayıt oluşturmayan Görselleştirme özel önizlemesi incelendi. Tüm platformun oturumlu uçtan uca testi olduğu iddia edilmez.
- 15 dakikalık izleme: 30 sağlıklı API örneği, 0 yeniden başlama, ağır hata işareti yok. Frontend/admin konteynırları healthy. API'nin Docker Health alanı yok; API readiness yanıtı kontrol edildi.

Sunucu yayın/yedek/izleme kayıtları root erişimli `/var/lib/eduivme/releases/exercises-20261011-6f315ff3` altında korunuyor. Kullanıcı sonuçlarını yedekle ezebilecek otomatik tam-veritabanı geri yükleme yapılmadı.
