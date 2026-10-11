# Serbest Okuma — geliştirme ve doğrulama raporu

Tarih: 11 Ekim 2026. Kapsam: sorusuz bağımsız okuma pratiği. Bu çalışma canlıya uygulanmadı ve GitHub'a gönderilmedi.

## Tamamlanan adımlar

1. Dört yaş grubunun her birinde 1–5. seviyeler tanımlandı. Onaylanan kelime aralıkları ve önerilen süreler `README.md` tablosunda bulunur. Mevcut 13 ayar güncellenir, eksik 7 ayar eklenir.
2. Her yaş/seviye hücresine beş metin seçeneği hazırlandı: toplam 100 egzersize özel metin kaydı. Bunlar 100 bağımsız hikâye değil, özgün kaynakların uzunluğa uygun paragraf seçkileridir. Paylaşılan kaynaklar ve sorular değiştirilmedi.
3. Normal pratik soru göstermeden sonuç ekranına geçer. Önerilen süre otomatik bitirme sınırı değildir. Normal oturumda 10 saniyelik yanlışlıkla bitirme koruması vardır; üst süre sınırı yoktur. İçerik bulunamazsa örnek metin gösterilmez.
4. Metin, kelime sayısı ve duraklatmalar çıkarılmış aktif süre kaydedilir. Tempo yalnız kullanıcının bitirme beyanına dayanan bilgidir; anlama puanı, ölçülmüş doğruluk, ağırlıklı hız, XP veya seviye tespit sonucu oluşturulmaz. Ölçüm raporlarına katılmaz. Geçerli tamamlanma günlük görevi kapatır.
5. Önizleme aynı yaş/seviye/uzunluk kurallarını kullanır ve kayıt oluşturmaz. Eski sorulu değerlendirme oturumlarının sunucu snapshotları korunur. Yeniden açılan kayıtlı oturumun sonuç süresi yerel sayaç yerine sunucunun aktif süresini kullanır.
6. Atomik, tekrar çalıştırılabilir PostgreSQL geçiş paketi hazırlandı. Eksik başlangıç kaydı, ayar sapması, yanlış yaş, pasif tür ve metin kimliği çakışması işlemi bütünüyle geri alır. Canlı geçiş ve geri dönüş adımları `README.md` içindedir.

## Test sonuçları

| Kontrol | Sonuç |
|---|---|
| SpeedReading.Application.UnitTests tam birim testi projesi | 1168 geçti; başarısız/atlanan yok |
| Universal player ve egzersiz motorlarının frontend regresyonları | 561 geçti |
| İlgili frontend testleri, kapsam ölçümüyle | 29 geçti; yukarıdaki 561 testle örtüşür |
| Gerçek PostgreSQL oturum/içerik entegrasyonları | 2 geçti; 20 yaş/seviye hücresinin tamamı açılıp tamamlandı |
| PostgreSQL aktarım/idempotency/geri alma kontrolleri | 7 geçti |
| Katalog ve içerik sözleşmesi | 5 geçti |
| HTML şablon sözleşmesi | Geçti |
| Masaüstü ve mobil Playwright akışları | 10 geçti |
| Frontend üretim derlemesi | Başarılı |
| Git diff boşluk/hatalı yama kontrolü | Geçti |
| Genel, TypeScript/JavaScript ve C# kod incelemeleri | Bulgular düzeltildi; onaylandı |

İlgili okuma motorunun kapsamı: satır %90,32, ifade %90,22, dal %82,14, fonksiyon %95,83. Bu değerler tüm uygulamanın kod kapsamı değildir.

PostgreSQL kontrolleri geçici Docker veritabanlarında gerçek şema üzerinde çalıştı. Tarayıcı testleri kontrollü API yanıtlarıyla öğrenci kayıtlı oturumu, admin/öğretmen önizlemesi, içerik eksikliği ve kayıt hatasını doğruladı; canlı Identity oturumu veya üretim veritabanı testi değildir. Masaüstü/mobil sonuç ekranları görsel olarak da incelendi. Hızlandırılmış önizleme testindeki `00:00`, testin beklemeden tamamlamasından kaynaklanır; kayıtlı sonuç testi 30 saniyelik sunucu süresini ayrıca doğrular.

## Açık yayın ve ürün doğrulama sınırları

- Canlı veritabanına aktarım, API/frontend yayını ve gerçek oturumla canlı kontrol henüz yapılmadı. Yayın için ayrı onay gerekir.
- Üretim derlemesinde önceden bulunan `exercise-player.component.scss` boyut bütçesi uyarısı sürüyor: yaklaşık 128,99 kB, 120 kB bütçesinin üzerinde. Bu çalışmada SCSS değiştirilmedi.
- Kelime/süre aralıkları ürün ayarlarıdır; bilimsel yaş normları veya okumayı/anlamayı kanıtlayan ölçümler değildir. Metinlerin pedagojik uygunluğu gerçek kullanıcı değerlendirmesiyle ayrıca izlenmelidir.
