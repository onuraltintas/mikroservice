# Anlama içerik paketi — 11 Ekim 2026

## Kapsam

40 normal Anlama egzersizi için yaş/seviye/çalışma-değerlendirme ayarları, egzersiz başına iki metin kaydı ve toplam 588 açıklamalı soru tamamlandı. Dört seviye tespit egzersizi, programlar, eski içerik ve kullanıcı geçmişi değiştirilmez.

80 kayıt, altı kaynak hikâyeden seçilen 35 farklı metin gövdesi içerir; 80 bağımsız hikâye değildir. 89 kaynak soru farklı seçkilere eşlenmiştir. Aynı metni/soruyu daha önce görmüş kullanıcı için bağımsız sınav formu sayılmaz. Kaynak, SHA-256, tam paragraf seçimi ve her sorunun metin içi kanıtı manifestte tutulur. Bu pilot paket akademik yaş normu veya standardize ölçme aracı değildir; öğretmen editör değerlendirmesi ve öğrenci pilotu ayrı kalite adımlarıdır. Eski ortak metin havuzunun tamamı editoryal olarak yeniden denetlenmedi.

## Doğrulama ve yayın takibi

- Kanonik içerik/SQL/kaynak/bütünlük kontrolleri: 7/7 geçti.
- Gerçek geçici PostgreSQL içerik testleri: 5/5 geçti; tüm 40 akışın seçimi, soru sayısı, anahtar gizliliği, sahiplik, sonuç kaydı ve metin rotasyonu doğrulandı.
- Geri dönüş: eski ayarlar geri gelir, yeni metinler pasifleştirilir; soru ve sonuçlar silinmez. İkinci geri dönüş metin/egzersiz sürümlerini değiştirmez. Sonradan değişen metin veya metadata'da otomatik geri dönüş durur.
- Mevcut Anlama PostgreSQL regresyonları: 4/4 geçti; yeni paket testleriyle toplam 9/9.
- Playwright: 94/94 geçti (47 masaüstü, 47 mobil); 40 seçeneğin her birinde metin, sorular, açıklamalar ve önizlemede kayıt oluşturmama kontrolü. Tarayıcı API/Identity yanıtları fixture'dır; gerçek canlı kayıt testi değildir.
- Canlı yedeği geri yüklenmiş geçici veritabanında iki uygulama ve iki geri dönüş geçti. Hedef dışındaki her tablo/satırın fingerprint'i eşleşti. Prova veritabanı kaldırıldı; canlı veriler henüz değiştirilmedi.
- JS/SQL/Bash ve C# odaklı kod incelemeleri tamamlandı; geri dönüş metadata koruma bulgusu giderildi. Tüm platform güvenlik denetimi iddiası yok.

Paketin üretim komutu ve sınırlar [README](../../content-packs/comprehension/v1/README.md) içinde. `settings.json` tek başına uygulanmaz. Canlı geçiş, kullanıcı onayıyla yalnız Hızlı Okuma veritabanına yapılacak; Identity ve Koçluk ayrı kalacak.
