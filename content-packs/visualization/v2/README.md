# Görselleştirme içerik paketi v2

Bu paket yalnız Hızlı Okuma'nın Görselleştirme egzersizini değiştirir. Eğitim programları, kullanıcı profilleri, Koçluk ve Identity veritabanları değişmez.

## İçerik ve akış

- Dört yaş grubu × beş seviye × altı sahne: **120 sahne, 360 soru**.
- Her yaş/seviyede deney, park, mutfak, müze, istasyon ve tasarım bağlamları bulunur.
- Her sahnede ayrıntı, mekânsal ilişki ve işlem sırası için birer soru vardır. Dört seçenekten birinin cevabı sahne metnindeki kanıta dayanır.
- Seviye yükseldikçe ek ayrıntılar, ilişkili konumlar ve sıralama talepleri artar. Bu dağılım pedagojik bir başlangıç ayarıdır; bilimsel olarak doğrulanmış yaş/seviye normu veya klinik ölçüm değildir.
- Sahne içeriği metinseldir; fotoğraf veya yapay zekâ görseli eklenmez. Egzersiz zihinde canlandırma ve soruları yanıtlama üzerine kuruludur.
- Kayıtlı çalışmada mevcut öğrenci ve egzersize ait son 200 oturumdan sahne kullanım sıklığı ve son kullanımı dikkate alınır. Daha az kullanılan/uzun süredir kullanılmayan üç sahne seçilir; eşitlikler ve sunum sırası rastgele belirlenir. Bu, sınırsız tekrar engeli değildir.
- Merkezi sahneli önizleme API'sinden rastgele üç sahne alınır; kullanıcı geçmişi veya sonuç kaydı oluşturulmaz. Dokuz cevap önizlemede puanlanmaz. Elle yapılandırılmış inline sahnelerin önizlemesi kendi içeriğini korur.
- Yeni paketle kayıtlı sonuçta dokuz cevap sunucuda doğrulanır. Bu egzersiz okuma hızı (WPM) ölçmez.

### Korunan sahne süreleri (saniye)

| Yaş grubu | Seviye 1 | Seviye 2 | Seviye 3 | Seviye 4 | Seviye 5 |
| --- | ---: | ---: | ---: | ---: | ---: |
| Çocuk | 40 | 45 | 50 | 55 | 60 |
| Genç | 30 | 35 | 40 | 45 | 50 |
| Genç yetişkin | 25 | 30 | 35 | 40 | 45 |
| Yetişkin | 25 | 30 | 35 | 40 | 45 |

## Üretme ve doğrulama

Depo kökünde:

```powershell
node content-packs/visualization/v2/generate.mjs
node content-packs/visualization/v2/generate.mjs --check
node --test content-packs/visualization/v2/content.test.mjs
```

`generate.mjs`, dört yaş grubunun `v1/scenes.json` dosyalarını ve bu dizindeki `scenes.json`/`apply.sql` dosyalarını üretir. `baseline.json` ilk 60 sahnenin değişmez karşılaştırma kaydıdır; mevcut canlı veritabanının yedeği değildir. Sorulardaki `evidence`, `theme` ve `demandLevel` içerik doğrulama bilgisidir, yeni veritabanı sütunu gerektirmez.

## Kontrollü yayın sırası — bu çalışma canlıya uygulanmadı

1. Hızlı Okuma veritabanının yedeğini alın; yedeğin okunabildiğini ve geri yükleme yöntemini doğrulayın. Mevcut uygulama sürümünü kaydedin.
2. Üç sahne seçen backend'i ve üç sahnelik önizlemeyi yayımlayın. Admin cevapları `answerFormat: text` gönderir; API cevapları A–D anahtarına dönüştürür. Backend'i önce yayımlamak mevcut frontend sözleşmesini korur.
3. `apply.sql` dosyasını yalnız Hızlı Okuma veritabanında `psql -v ON_ERROR_STOP=1` ile çalıştırın. Betik tek transaction içinde kilit alır. Eksik egzersiz, değişmiş eski içerik, eklenmiş aktif soru veya yeni ID çakışmasında işlemin tamamı geri alınır; kontrolü zorla atlamayın.
4. Beklenen sonuç: yeni 120 aktif sahne; önceki 60 sahne arşivlenmiş; eski ve yeni toplam 540 soru korunmuş. Aynı betik değişmemiş veriye ikinci kez güvenle uygulanabilir.
5. Admin düzenleme/arşivleme ve farklı yaş/seviyelerde kayıtlı çalışma/önizlemeyi doğrulayın. Başlatılmış eski oturum, snapshot'ındaki eski sahneler ve soru anahtarlarıyla devam eder. Oturum ve sonuçlar silinmez.

Geçiş sonrası geri dönüş, yedek ve kaydedilmiş uygulama sürümü üzerinden planlanmalıdır. Geçişten sonra yeni oturum oluşmuşsa doğrudan eski yedeği geri yüklemek yeni sonuçları kaybettirir; önce yazmaları durdurun ve yeni veriyi koruyan ayrı geri dönüş planı hazırlayın. Paket canlıya uygulanmadan önce yayın onayı ayrıca alınmalıdır.
