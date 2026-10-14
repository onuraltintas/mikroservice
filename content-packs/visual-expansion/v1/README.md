# Görsel Genişleme v1 — eğitim tur sayıları ve hedef raporu

Bu paket yalnız Hızlı Okuma'nın dört yaş grubundaki 20 eğitim egzersizinin
tur sayısını günceller. Başlangıç uzaklığı, hedef uzaklık, gösterim süresi,
harf içeriği ve yatay düzen değişmez. Koçluk ve Identity veritabanları kapsam dışıdır.

| Yaş grubu | Seviye 1 | Seviye 2 | Seviye 3 | Seviye 4 | Seviye 5 |
| --- | ---: | ---: | ---: | ---: | ---: |
| Çocuk, 9–12 | 30 | 35 | 40 | 45 | 50 |
| Genç, 13–16 | 35 | 40 | 45 | 50 | 55 |
| Genç yetişkin, 17–21 | 40 | 45 | 50 | 55 | 60 |
| Yetişkin, 22+ | 45 | 50 | 55 | 60 | 65 |

## Çalışma kuralları

- Eğitim oturumları protokol 2 kullanır. 14 Ekim 2026 güncellemesiyle her
  tam doğru cevap uzaklığı bir yapılandırma birimi artırır; gösterim süresi değişmez.
- Hedef uzaklıkta üç ardışık doğru cevap gösterim süresini %5 azaltır;
  uzaklık sabit kalır. Alt sınır 100 ms'dir. Yanlış cevap seriyi sıfırlar,
  uzaklık ve süreyi değiştirmez.
- Mümkün olan uyaran havuzunda önceki turdaki harfler/kelimeler hemen tekrarlanmaz.
- Her 15 tamamlanan turda isteğe bağlı mola önerilir. Mevcut duraklatma/devam
  mekanizması ilerlemeyi korur. Seviye tespitinde mola önerisi gösterilmez.
- Özel önizlemede tur sayısı 1–100 arasında seçilebilir; otomatik uyarlama açıktır.
  Önizleme sonucunu ve ilerlemesini veritabanına yazmaz.
- Sonuç ekranında hedefe ulaşma, hedefte sunulan/doğru turlar, hedef doğruluğu
  ve hedefte doğru cevaplanan en kısa gösterim süresi bulunur. Henüz gösterilmeyen
  sonraki turun hesaplanan uzaklığı başarı sayılmaz. Ölçüm yoksa sıfır yerine
  “Ölçülmedi” gösterilir.
- Uzaklık bir ekran konumudur; fiziksel görüş açısı veya okuma hızı ölçümü değildir.
  Bu sayılar eğitim yapılandırmasıdır, yaşa göre doğrulanmış klinik norm değildir.

Seviye tespiti önceki ilerleme kuralıyla çalışmaya devam eder. Eski eğitim
oturumları da her doğru cevapta bir birim artar. Açık oturumlar kendi ayar anlık görüntüsünü
korur; yeni katalog ayarları yalnız yeni oturumlarda kullanılır.

## Kontrollü uygulama

`settings.json` eski ve yeni ayarları, açık egzersiz kimliklerini ve yaş/seviye
eşleşmelerini içerir. SQL'i yeniden üretmek için depo kökünde:

```text
node content-packs/visual-expansion/v1/generate.mjs
```

Canlı uygulama ayrı yayın onayı gerektirir. Önce Hızlı Okuma veritabanının
geri yüklenebilir yedeği ve 20 egzersizin mevcut ayarları alınmalıdır.
Frontend ve Hızlı Okuma backend'i uyumlu sürümleriyle birlikte yayımlanmalıdır.
Yalnız Hızlı Okuma veritabanına karşı depo kökünde
`psql -v ON_ERROR_STOP=1 -f content-packs/visual-expansion/v1/apply.sql`
çalıştırılır. Bağlantı bilgileri ve parolalar rapora/sohbete yazılmaz.

SQL atomik ve tekrar çalıştırılabilirdir. Eksik egzersiz, pasif/silinmiş kayıt,
farklı yaş/seviye ya da beklenmeyen ayar değişikliği işlemi tamamen durdurur.
Altı turluk seviye tespiti, program atamaları ve sonuç geçmişi değiştirilmez.
Uygulama sonrası 20 ayar, yeni oturum tur sayıları ve mevcut oturum devamı
doğrulanmalıdır. Sorunda otomatik silme/tahmin yapılmaz; önceki sürümler ve
yalnız bu 20 kayıt için yedeklenen ayarlar kontrollü olarak geri alınır.

## Yerel doğrulama — 11 Ekim 2026

- Hızlı Okuma C# birim testleri: 1085 geçti, başarısız/atlanan yok.
- Frontend testleri: 776 geçti; üretim derlemesi başarılı.
- Gerçek PostgreSQL: 5 test; doğrulanmış cevaplar, sonuç tekrarının tekilleştirilmesi,
  sahiplik, atomik/idempotent katalog geçişi, seviye tespiti ve açık oturum korunması.
- Katalog doğrulaması: 20 yaş/seviye kaydında yalnız tur sayıları değişir.
- Tarayıcı: gerçek frontend/motor, sahte Identity/API yanıtları; masaüstü ve
  mobilde dört yaşın 1/5. seviyeleri, uyaran düzenleri, özel turlar, mola/devam
  ve hedef sonuç kartları için 22 test geçti. Sonuç ekranındaki 12 kartın
  yatay taşma kontrolü de masaüstü/mobilde geçti.
  Gerçek üretim Identity oturumu bu testin kapsamı değildir.
- Motor test kapsaması: ifadeler %93,76, dallar %81,03; ayar çözümleyici %100.
  Tüm frontend'in ifade kapsaması %61,68'dir; %100 toplam kod kapsamı iddiası yoktur.
- Üretim derlemesinde mevcut SCSS dosyasının 120 kB sınırını aşan uyarısı sürer;
  bu değişiklikte ilgisiz stil yeniden düzenlemesi yapılmadı.

Bu yerel doğrulama canlı geçişin yapıldığı anlamına gelmez.
