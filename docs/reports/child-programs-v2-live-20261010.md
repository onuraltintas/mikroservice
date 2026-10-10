# Çocuk programları v2 — kontrollü canlı geçiş

## Kaynak ve kapsam

- Görev sırası/program geçişi: `a6c6cd1e`.
- Önceden canlıya alınmış katalog/önizleme düzeltmelerinin kaynak kaydı: `77618cb9`.
- GitHub dalı: `codex/platform-hardening`.
- Yalnız Hızlı Okuma backend/frontend ve `speedreading_owned_db` değişir.
- Koçluk, Identity, Notification, Gateway ve diğer paneller yeniden başlatılmaz.
- Staging kurulmaz.

## Yayın öncesi kanıt

- 986 Hızlı Okuma birim testi, 14 ilgili PostgreSQL entegrasyon testi geçti.
- Mevcut canlı önizleme/katalog değişiklikleri ile genişletilen 51 Chrome testi geçti.
- 28 gün/182 görev dağılım testi ve backend/frontend üretim derlemeleri geçti.
- Mevcut SCSS boyut uyarısı devam ediyor; derleme başarısızlığı yok.
- Yeni imajlar: `eduivme/speed-reading-service:child-programs-v2-77618cb9`,
  `eduivme/speed-reading-frontend:child-programs-v2-77618cb9`.
- Paket SHA-256: `083f323661040f21dfa9d2709d82ea7e0d9de9a6c13db244d2a62f8b2786b18f`.
- Yayın klasörü: `/var/lib/eduivme/releases/child-programs-v2-20261010-77618cb9`.
- Yedek: aynı klasörde `speedreading-before.dump`; 1.582.751 bayt.
  `pg_restore --list` ve SHA-256 doğrulaması geçti. Tam geri yükleme tatbikatı bu canlı yayında yapılmadı.
- Başlangıç migration geçmişi: 77 kayıt, özet `4d0e4003168d775e55105cd3771bba94`.
  Sadece `20261010064029_AddDailyTaskSlotOrder` SQL'i uygulanır.
- Kesinti/kurtarma fonksiyonu 9 hata/sinyal senaryosunda mock servis çağrılarıyla geçti.
- Korunacak katalog: 366 egzersiz, 21 egzersiz türü, 19 kapsam dışı/seviye tespit programı.

## Güvenli geri dönüş

İçerik geçişi commit edilmeden önce hata olursa eski backend yeniden başlatılır.
İçerik commit edildikten sonra yeni şema korunarak yeni backend/frontend tekrar başlatılır.
Tekrarlı görev sonuçları oluştuktan sonra eski benzersizlik index'ine dönülmez; kullanıcı geçmişi silinmez.
Kurtarma başarısızlığı logda kritik olarak bildirilir ve manuel müdahale gerekir.
Kısmi/bilinmeyen içerik durumunda otomatik sürüm seçimi yapılmaz. Migration commit olup
içerik transaction'ı rollback olursa eski backend açılır; katı ilk yayın kontrolü aynı promote
komutunu tekrar çalıştırmaz. Operatör 78 migration'ı ve hâlâ var olan eski beş programı
doğruladıktan sonra yalnız içerik transaction'ı ve yeni imaj başlangıcını devam ettirir;
migration SQL'i yeniden çalıştırılmaz.

## Bilinen ayrı konu

Mevcut Görselleştirme önizlemesi, sahne ayrıntıları için içerik yönetimi izni gerektiren admin
endpoint'ini kullanıyor. Öğretmen/kurum rollerinde 403 oluşabilir. Bu önceden yayımlanmış
akış bu sürümde korunmuştur; yeni çocuk programları geçişinin parçası değildir.
Bu yayında genel içerik yönetim yetkileri genişletilmez.

## Yayın sonucu

2026-10-10 07:19 UTC (10:19 Türkiye) kontrollü geçiş tamamlandı:

- Görev sırası migration'ı commit edildi; toplam migration sayısı 78.
- Yeni beş şablon eklendi, yalnız eski beş çocuk eğitim şablonu fiziksel silindi.
- Yeni beş şablonun her biri: 28 gün, 182 görev, günlük en az 6/en çok 7.
- Egzersiz kataloğu, türler ve korunacak 19 programın önce/sonra içerik özetleri birebir eşleşti.
- Yalnız Hızlı Okuma backend/frontend container kimlikleri değişti; diğer servisler değişmedi.
- Backend readiness `Healthy`, ana sayfa 200; anonim program/günlük API erişimi 401.
- Frontend service worker yeni `child-programs-v2-77618cb9` release işaretini sunuyor;
  `ngsw.json` önbelleğe alınmaması gereken başlıkları sunuyor.
- Gerçek tarayıcıdaki ana paket `main-7U4USG4K.js`; üretim derlemesindeki paket ile eşleşiyor.
- Yedek izinleri root-only: dosya 600, yayın klasörü 700.
- Gerçek admin oturumuyla Çocuk filtresinde beş yeni program, 28 gün/182 görev açıklaması
  ve mevcut aktif program/günlük liste açılışı doğrulandı; yeni tarayıcı uygulama hatası yok.
  Programa kayıt, egzersiz başlatma veya sonuç yazma işlemi yapılmadı.
- Üretimde yeni çocuk programı kayıt/tamamlama testi canlı kullanıcı verisiyle yapılmadı;
  tekrarlı görev tamamlaması gerçek geçici PostgreSQL entegrasyon testlerinde doğrulandı.

Yayın sonrası 07:20:30–07:35:10 UTC arasında yaklaşık 15 dakikalık izleme tamamlandı.
30 kontrolün tamamında readiness `Healthy`, ana sayfa 200, korumalı API anonim erişimi
401 döndü. İzleme boyunca kontrol edilen backend hata kayıtları ve container yeniden
başlatma sayısı sıfırdı. Bu kontroller tüm olası kullanıcı akışlarının test edildiği
veya yüzde 100 kod kapsaması sağlandığı anlamına gelmez.
