# Hızlı Okuma — kontrollü yayın raporu

## Sürüm ve kapsam

Kaynak: `6fb9722e87360f82f21cd06400744ef089fcfe53`, GitHub
`onuraltintas/mikroservice`, dal `codex/platform-hardening`.
Bekleyen 22 geliştirme commit'i ve bu yayın için iki test/otomasyon commit'i gönderildi.
Canlı geçiş: 12 Ekim 2026 01:21:47 Türkiye saati.

Canlı imaj kimlikleri:

- API: `sha256:96d31f45ae6d3487ceff53ba9936111e64f0985af7a1f33eeb9f931588708b74`
- Frontend: `sha256:985d03ad99e7eaf2f2c69078c2d55fc290b650aa0557115ebd845db742ebab08`

- Göz Gezdirme v1: 12 ayar, 36 metin, 144 soru.
- Göz Takibi v1: 20 yaş/seviye ayarı.
- Sınav Simülasyonu v1: mevcut üç seviye-5 ayarı; sekiz soru, ayrı okuma/soru zamanları.
- Güncel Hızlı Okuma API ve üretim arayüzü birlikte yayımlandı.
- Şema migration'ı yok; Identity, Koçluk ve Notification veritabanları/servisleri değişmedi.
- Eski geçici derleme/arşivler ve güncel v2 programlarla değiştirilmiş v1 taslakları
  kaynak veya canlı sürüm olarak gönderilmedi; mevcut dosyalar silinmedi.

## Yayın öncesi doğrulama

- Backend birim/regresyon: 1.219/1.219.
- Angular birim/regresyon: 923/923.
- Gerçek PostgreSQL Hızlı Okuma entegrasyonu: 188/188.
- Paket testleri: 6/6; yayın güvenlik testleri: 3/3.
- Masaüstü/mobil Playwright: sınav 12/12, Göz Gezdirme 10/10, Göz Takibi 8/8.
- Release API publish ve üretim Angular build başarılı.
- Eski PostgreSQL testindeki reflection çağrısına yeni isteğe bağlı parametre eklendi;
  üretim yerleştirme davranışı bu düzeltmede değiştirilmedi.
- Takipli dosyalarda özel anahtar/AWS anahtarı desenleri ve gerçek .env dosyası yok.
- Bu dalın push'u mevcut CI workflow'larını otomatik tetiklemiyor. GitHub CI geçti
  iddiası yok; belirtilen testler yerelde çalıştırıldı.

## Veritabanı ve geri dönüş güvenliği

VPS sürüm dizini: `/var/lib/eduivme/releases/final-exercises-20261012`.
Kirli `/opt/eduivme` kaynak checkout'u sıfırlanmadı veya ezilmedi.
Önceki frontend'in hashed dosyaları yeni imajda korundu.

- Mühürlenmiş arşiv SHA256:
  `d3fdf46e070b632bcd54454cfcd8c9a24f03443eca53065bc08843704ecb9469`.
- Tam Speed Reading DB yedeği geçici veritabanına geri yüklendi.
- Üç paket tek transaction'da uygulandı; doğrulama ve ikinci uygulama/idempotans geçti.
- Geri dönüş SQL'i geçici kopyada uygulandı ve eski ayarlar doğrulandı.
- Geçiş/geri dönüş boyunca hedef dışı bütün tablolar ve hedef egzersizlerin değişmemesi
  gereken alanları satır içerik hash'leriyle karşılaştırıldı; fark yok.
- Canlı API durdurularak alınan `speedreading-before.dump` yedeğinin SHA256 ve
  `pg_restore --list` kontrolleri geçti. Gerçek öğrenci geçmişi silinmedi.
- Yalnız iki Hızlı Okuma konteynırı değişti; diğer çalışan konteynırların kimlikleri aynı.
- Geçici prova veritabanı otomatik kaldırıldı. Yedek ve önceki imajlar korundu.

## Canlı kontroller

- Ana sayfa HTTPS 200; yetkisiz katalog isteği 401.
- `ngsw.json` canlı/yerel SHA256 eşit:
  `33780b30d46ba5fe67967b83de188c958db115672b66b6c42b63c722eb056926`.
- Yeni service worker sürüm işareti kaynak commit'iyle aynı.
- Eduİvme ve Koçluk ana adresleri mevcut 302 yönlendirmelerini sürdürüyor.
- Gerçek admin/Identity oturumu: yeni sekmede güncel üretim main dosyası doğrulandı.
- Sınav önizlemesi ilk okuma, sekiz soru, bitene kadar gizli dönüt ve sonuç ekranıyla
  tamamlandı; sonuçta “Önizleme sonucu — kaydedilmedi” doğrulandı.
- Göz Gezdirme çocuk seviye 3: yeni 235 kelimelik metin, 75 saniyelik üst sınır,
  minimum inceleme süresi, dört soru ve sonuç ekranı doğrulandı. WPM üretilmedi.
- Canlı test sekmesinde yakalanan konsol hata kaydı yok.
- Göz Takibi çocuk seviye 1: gerçek 45 saniyelik çalışma, hareketli hedef,
  duraklatmada sabit kalan sayaç ve devam sonrası tam 45 saniyelik sonuç doğrulandı.
  Sonuçta göz hareketi/doğruluk/WPM ölçülmediği açık; önizleme sonucu kaydedilmedi.
- Göz Takibi özel önizleme: 10 saniye, 2.000 ms döngü ve daire yolu seçildi;
  tam 10 saniyelik sonuç alındı. Kayıtlı katalog ayarı değiştirilmedi.
- Sınav özel önizleme: minimuma eşit 10 saniyelik üst sınır güvenli uyarıyla reddedildi.
  30 saniyelik okuma, 5 saniyelik soru, büyük yazı (24 px) ve %220 satır aralığı
  (52,8 px) uygulandı. Bir soru timeout'u ve sekiz sorulu sonuç doğrulandı.
- Bütün canlı admin önizlemelerinden sonra hedef dışı kayıt hash'leri değişmedi;
  öğrenci oturumu/sonucu oluşturulmadığı veritabanında da doğrulandı.
- Kullanıcının başlangıçtaki sekmesi test sonunda kataloğa geri getirildi;
  güncel `main-NXG6EVD4.js` dosyasını yüklediği DOM üzerinden doğrulandı.
  Açılan geçici test sekmesi kapatıldı; kullanıcının sekmesi bırakıldı.

Yayın sonrası 15 dakikalık izleme tamamlandı: 30/30 sağlık örneği başarılı,
API restart sayısı 0 ve yeni ağır hata kaydı yok (`MONITOR_OK`).
Bu çalışma için gerekli GitHub gönderimi, kontrollü yayın ve işlev kontrolleri
tamamlandı. Üst çubuk görünüm kusuru aşağıda ayrıca belirtilmiştir.

## Sınırlar

Önceden açık sekme eski service-worker sürümünü çalıştırabilir; yeni sekmede güncel
sürüm doğrulandı. Normal kayıtlı öğrenci sonuç akışları yerel gerçek PostgreSQL'de,
canlı arayüz ise admin önizlemesiyle kontrol edildi; canlıda yapay öğrenci sonucu
üretilmedi. Pedagojik etkililik veya bütün projede %100 kod kapsamı iddiası yok.
Mevcut oyuncu SCSS boyutu 128,99 kB; 120 kB uyarı eşiği aşımı derlemeyi engellemiyor.
Canlı görsel kontrolde Göz Takibi'nin ortak üst çubuğunda kalan “Satır / Görünme
1000 ms” etiketleri tespit edildi. Bunlar gerçek hareket döngüsü/gösterim ölçümü
olarak yorumlanmamalı; bu yayın onları değiştirmedi. Süre ve hareket davranışı
bundan bağımsızdır. Bu küçük görünüm kusuru ayrıca iyileştirilmelidir.
Kod/yayın araçları güvenlik, veri kapsamı, atomiklik, süre/cevap güvenilirliği ve
geri dönüş açısından yapılandırılmış öz incelemeden geçirildi; bağımsız inceleme
olarak sunulmuyor.
