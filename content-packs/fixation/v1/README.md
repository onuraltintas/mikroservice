# Sabitleme v1 — yaş/seviye, tur ve harf ayarları

Yalnız Hızlı Okuma'nın dört yaş grubundaki 20 Sabitleme eğitim kaydı güncellenir.
Koçluk/Identity, program atamaları, seviye tespit kayıtları ve sonuç geçmişi değişmez.

| Yaş grubu | Seviyeler 1 → 5: tur | Seviyeler 1 → 5: harf | Özel önizleme harf üst sınırı |
| --- | --- | --- | ---: |
| Çocuk, 9–12 | 20 / 25 / 30 / 35 / 40 | 1 / 1 / 2 / 2 / 2 | 2 |
| Genç, 13–16 | 25 / 30 / 35 / 40 / 45 | 1 / 2 / 2 / 3 / 3 | 3 |
| Genç yetişkin, 17–21 | 30 / 35 / 40 / 45 / 50 | 2 / 2 / 3 / 3 / 4 | 4 |
| Yetişkin, 22+ | 35 / 40 / 45 / 50 / 55 | 2 / 3 / 3 / 4 / 4 | 4 |

Gösterim süreleri değişmez: çocukta 1000 / 900 / 850 / 800 / 750 ms;
diğer yaşlarda 900 / 850 / 800 / 750 / 700 ms. Nokta boyutu 44 px korunur.
Bunlar uygulama eğitim ayarlarıdır; bilimsel olarak doğrulanmış yaş normu değildir.

## İşleyiş

- Harfli Sabitleme tur sayısıyla tamamlanır; cevap beklerken toplam süreyle kesilmez.
  Bu 20 kayıttaki kullanılmayan `durationSeconds` kaldırılır. Gösterim ve cevap
  süresi farklıdır; cevap süresi bu gösterim süresine dahil değildir.
- Yeni eğitim oturumları protokol 2 kullanır. Harfler dört yön arasında dönüşümlü
  sunulur, aynı turda farklıdır ve önceki turun harfleri hemen tekrarlanmaz.
- Harflerin tamamı doğruysa tur doğru sayılır. Sıra önemli değildir; eksik veya
  mükerrer harf tam doğru cevap sayılmaz. Önizleme de aynı tur puanlamasını kullanır.
- Kayıtlı oturumda cevaplar ve sonuçlar sunucu tarafından doğrulanır. Yanlış
  kullanıcı, erken cevap, mükerrer cevap ve tamamlanmamış sonuç reddedilir.
- Önizleme sonuç/ilerleme yazmaz. Özel önizlemede 1–100 tur, yaşın izin verdiği
  harf sayısı ve 50–10000 ms gösterim seçilebilir. Katalog ayarları değişmez.
- Ekranda doğru/yanlış, tamamlanan/planlanan tur ve saniye cinsinden ortalama
  cevap süresi gösterilir. Yerel süre gerçek gizlenme anından başlar; duraklama
  hariçtir. Kayıtlı oturumda ağ gecikmesi cevap süresini etkileyebilir.
- Egzersiz harf tanıma doğruluğunu ölçer; gözün gerçekten sabit kaldığını veya
  okuma hızını ölçmez. Harfsiz izleme alıştırması ölçümlü harf testi değildir.
- Gerçek seviye tespiti ve mevcut protokol 1 oturumları korunur. Açık oturumlar
  kendi ayar anlık görüntüleriyle devam eder; yeni katalog yeni oturumda kullanılır.

## Kontrollü veritabanı uygulaması

`settings.json` 20 açık kimliği, gerçek yaş kimliklerini ve eski/yeni ayarları içerir.
Genç yetişkinin yaş kimliği `...004`, yetişkinin `...003` ile bittiğine dikkat edin;
yaşlar sıra numarasından tahmin edilmez.

```text
node content-packs/fixation/v1/generate.mjs
```

Yayın ayrıca onaylanmalıdır. Önce Hızlı Okuma DB'nin geri yüklenebilir yedeği ve
20 kaydın mevcut ayarları alınır. Uyumlu frontend/backend yayımlandıktan sonra
yalnız Hızlı Okuma veritabanında `psql -v ON_ERROR_STOP=1 -f
content-packs/fixation/v1/apply.sql` çalıştırılır. Parolalar ve bağlantı sırları
sohbete/rapora yazılmaz. Şema migration'ı veya geçmiş silme yoktur.

SQL atomiktir ve tekrar çalıştırılabilir. Eksik/pasif/silinmiş kayıt, farklı yaş,
seviye, motor veya beklenmeyen önceki ayar işlemi tamamen durdurur. Sonrasında
20 ayar, yeni oturum harf/tur sayısı ve açık oturum devamı doğrulanır. Geri dönüşte
yalnız bu kayıtların yedeklenen ayarları ve uyumlu uygulama sürümü geri yüklenir;
programlar ve kullanıcı geçmişi topluca silinmez.

## Yerel doğrulama

- Dört yaş grubunun beş seviyesinde katalog eşleşme testi.
- Motor ve kullanıcı ayarları için birim testleri; eski oturum ve gerçek
  seviye tespiti devam testleri.
- Gerçek PostgreSQL: sahiplik, harf sınırı, tam tur puanlama, sonuç tekilleştirme;
  atomik/idempotent geçiş, değişmiş ayar/motor reddi ve açık oturum korunması.
- Gerçek frontend/motorla sahte Identity/API yanıtları kullanılarak 18 masaüstü/
  mobil tarayıcı kontrolü. Bu testler gerçek üretim Identity oturumu değildir.

11 Ekim 2026 son kontrolü: 1088 backend birim testi, 782 frontend testi,
11 PostgreSQL entegrasyon testi ve 18 tarayıcı testi geçti; başarısız/atlanan yok.
Üretim frontend derlemesi başarılıdır. Mevcut SCSS dosyasının 120 kB bütçesini
8,98 kB aşan uyarısı sürmektedir; ilgisiz stil yeniden düzenlemesi yapılmadı.
Motor ifade kapsaması %81,96, dal kapsaması %74,30'dur; bütün frontend ifade
kapsaması %61,80'dir. Tüm testlerin geçmesi %100 kod kapsamı anlamına gelmez.

Canlı veritabanı henüz güncellenmedi; GitHub'a gönderim ve yayın yapılmadı.
