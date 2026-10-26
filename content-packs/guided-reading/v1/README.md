# Sabit tempolu Hızlı Okuma — v1

Bu paket yalnız `SpeedReading` türündeki 20 yaş/seviye egzersizinin ayarlarını günceller. Yeni şema gerektirmez. Programları, atamaları, kullanıcıları, geçmiş oturumları ve diğer egzersizlerin ayarlarını değiştirmez. Yerel olarak doğrulanmıştır; canlıya uygulanmış değildir.

## Çalışma modeli

- Tam metin görünür; tek kelime sırayla vurgulanır. Tempo oturum boyunca sabittir, otomatik uyarlama kapalıdır.
- Kelime gösterim süresi `60000 / speedWpm` milisaniyedir. Zamanlayıcı birikimli hesap kullanır; her kelimede süre yuvarlanmaz.
- Yaş, mevcut egzersiz seviyesi, aktiflik, egzersiz bağlantısı ve kelime aralığı metin seçimini sınırlar. Uygun havuzda en az kullanılmış metin önceliklidir. Önizleme kullanım bilgisi tarayıcıda, kayıtlı oturumlarınki sunucudadır.
- Özel önizlemede hız değişir; seçilmiş yaş/seviye ve metin uzunluğu aralığı değişmez. Başka yaş/seviyeden veya sabit örnek metinden sessiz yedekleme yoktur.
- Hedef dakika aralığı **metin seçimine** dönüşür; egzersizi kesen süre sınırı değildir. Duraklamalar aktif gösterim süresine dahil edilmez.
- Zorunlu anlama sorusu yoktur. Sonuçta gösterim temposu, kelime sayısı, aktif süre ve tamamlanma gösterilir. Bu egzersiz tek başına okuma hızı/anlama ölçmez; kayıtlı sonuç `NotMeasured`, WPM/anlama/doğruluk alanları `null`, XP sıfırdır.
- Mevcut kelime vurgulama protokolünün `grouping*` API alanları geriye uyumluluk için korunmuştur; Hızlı Okuma için öbek boyutu daima 1'dir.

## Onaylanan ayarlar

Seviyeler sütunlarda 1–5 sırasındadır. Dakikalar ürünün pilot parametreleridir; akademik olarak doğrulanmış yaş normu değildir.

| Yaş | Hızlar (kelime/dk) | Kelime aralıkları | Hedef aktif süre |
| --- | --- | --- | --- |
| Çocuk, 9–12 | 100 / 130 / 160 / 200 / 230 | 200–300 / 260–390 / 320–480 / 400–600 / 460–690 | 2–3 dk |
| Genç, 13–16 | 150 / 180 / 210 / 240 / 270 | 375–525 / 450–630 / 525–735 / 600–840 / 675–945 | 2,5–3,5 dk |
| Genç yetişkin, 17–21 | 180 / 210 / 240 / 270 / 300 | 540–720 / 630–840 / 720–960 / 810–1080 / 900–1200 | 3–4 dk |
| Yetişkin, 22+ | 200 / 230 / 260 / 290 / 320 | 600–800 / 690–920 / 780–1040 / 870–1160 / 960–1280 | 3–4 dk |

## İçerik ve dosyalar

- `settings.json`: 20 egzersizin kimliği, eski/yeni ayarları ve hedef dakika aralığı.
- `content.json`: 22 metin eklemesinin kimliği, özgün dosyası veya kaynak listesi.
- `texts/`: 8 yeni, özgün anlatı (6 çocuk, 2 genç). Bilimsel hız artışı iddiası içermez; editoryal/pilot değerlendirme yine yapılmalıdır.
- 14 iki bölümlü seçki: mevcut veritabanındaki aynı yaş/seviye kaynaklarının **tam metinleri** araya iki satır eklenerek birleştirilir. Kaynaklar taşınmaz, kısaltılmaz veya silinmez. Başlıkta iki bölüm belirtilir; eklenen kayıt yalnız ilgili Hızlı Okuma egzersizine bağlanır.
- `apply.sql`: ayarların ve metinlerin kanonik gömülü kopyasıyla tek işlemde uygulama. JSON/dosya ve SQL eşleşmesi birim testinde kontrol edilir. Birini değiştirirken diğerini de güncelleyip testleri yeniden çalıştırmak gerekir.

Salt okunur canlı envanterde 12 yaş/seviye havuzu hedef uzunlukta boştu. Bu eklemeler, mevcut uygun metinlerle birlikte her yaş/seviyede en az iki uygun kayıt bulunmasını sağlar. Bu asgari sayı büyük bir içerik kütüphanesi değildir; pilot geri bildirimine göre çeşitlilik artırılabilir.

## Güvenli uygulama sırası — henüz uygulanmadı

1. Speed Reading veritabanının doğrulanmış yedeğini, 20 egzersizin mevcut ayar/audit envanterini ve API/frontend sürümlerini sakla. Yedeğin geri yüklenebilirliğini doğrula.
2. Yeni API ve frontend'i kontrollü yayımla; diğer servislerin veritabanlarına dokunma. Gerekirse egzersiz başlatmayı kısa bakım penceresinde durdur.
3. Yalnız doğru Speed Reading veritabanına, hata halinde duracak PostgreSQL istemcisiyle uygula: `psql -X -v ON_ERROR_STOP=1 -d <speed-reading-db> -f apply.sql`. Bağlantı sırlarını komut geçmişine koyma.
4. 20 ayarı, 22 yeni metni ve her havuzdaki uygun metin sayısını doğrula. Sağlık kontrolü, yeni kayıtlı oturum ve admin/öğretmen önizlemesini kontrol et. Önbellek varsa ilgili katalog önbelleğini yenile.

SQL işlem/advisory kilidi kullanır. Egzersiz kimlikleri, tür, yaş/seviye, başlık, durum ve önceki ayarlar uyuşmuyorsa durur. Seçki kaynaklarının kimliği, bağlılık durumu, yaş/seviye, dili, gerçek/saklı kelime sayısı ve içerik özeti uyuşmuyorsa durur. MD5 burada yalnız değişiklik tespiti içindir; bir güvenlik imzası değildir.

Yeni kayıt kimliği çakışması veya havuzda yeterli metin bulunmaması tüm işlemi geri alır. Aynı paket ikinci kez çalıştırıldığında mükerrer metin/ayar sürümü oluşturmaz; paket kayıtları sonradan düzenlendiyse yeniden uygulama durur.

## Geri dönüş

SQL başarısızsa işlem tamamen geri alınır. Yayın sonrası geri dönüşte API/frontend'in önceki sürümüne dön; yalnız paketin hedeflediği 20 ayarı yayın öncesi envanterden, mevcut ayarların hâlâ paket sürümü olduğunu doğrulayarak geri yükle. Audit/version güncellemelerini normal yönetim kurallarına uygun kaydet. Yeni metinleri gerekirse pasife al; kullanılmışsa geçmiş sonuçlarını silme. Arada yeni kullanıcı verisi yazılmışsa bütün veritabanını eski yedekle ezme. Bu belgedeki geri dönüş adımları canlıda henüz yürütülmemiştir.

## Doğrulama

Sunucu birim testleri, gerçek geçici PostgreSQL testleri ve Chrome testleri vardır. Playwright gerçek Angular ekran/motorunu masaüstü ve mobilde çalıştırır; Identity/API yanıtları deterministik test düzenekleridir. Canlı Identity oturumu ve canlı yayın sonrası kontrolün yerine geçmez.
