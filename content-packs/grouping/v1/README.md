# Kelime Gruplarıyla Okuma — v1

Gruplama (`Chunking`) için dört yaş grubunun 1–5 seviyelerini tamamlayan içerik/ayar paketi. **Henüz canlıya uygulanmadı.** Identity ve Koçluk veritabanları kapsam dışıdır; yeni şema migration'ı yoktur.

## Çalışma şekli

Metnin tamamı ekranda görünür. Sabit boyuttaki kelime grupları sırayla vurgulanır; grup cümle veya paragraf sınırını geçmez. Kısa grubun gösterim süresi gerçek kelime sayısına göre hesaplanır: `kelime × 60000 / tempo` milisaniye. Duraklama aktif süreye dahil değildir. Otomatik zorluk değişimi ve yeni katalogda zaman aşımı yoktur.

Bu bir gösterim/izleme alıştırmasıdır; anlamsal çözümleme, anlama testi veya göz takibi değildir. Gösterim temposu, kullanıcının ölçülmüş okuma hızı değildir. Anlama, doğruluk ve ölçülmüş WPM üretilmez; XP 0'dır. Geçerli tamamlanma günlük göreve işlenebilir. Özel önizlemede tempo ve grup büyüklüğü değişebilir; yaş/seviye/metin bandı değişmez, sonuç ve ilerleme kaydedilmez.

## Onaylanan ayarlar

Her hücre `gruptaki kelime / kelime-dk tempo / metindeki kelime bandı` biçimindedir.

| Yaş | Seviye 1 | Seviye 2 | Seviye 3 | Seviye 4 | Seviye 5 |
| --- | --- | --- | --- | --- | --- |
| Çocuk 9–12 | 2 / 100 / 150–200 | 3 / 130 / 195–260 | 4 / 160 / 240–320 | 4 / 200 / 300–400 | 4 / 230 / 345–460 |
| Genç 13–16 | 2 / 150 / 300–375 | 3 / 180 / 360–450 | 3 / 210 / 420–525 | 4 / 240 / 480–600 | 4 / 270 / 540–675 |
| Genç yetişkin 17–21 | 2 / 180 / 360–540 | 3 / 210 / 420–630 | 3 / 240 / 480–720 | 4 / 270 / 540–810 | 5 / 300 / 600–900 |
| Yetişkin 22+ | 2 / 200 / 400–600 | 3 / 230 / 460–690 | 3 / 260 / 520–780 | 4 / 290 / 580–870 | 5 / 320 / 640–960 |

Hedef gösterim süresi çocukta 1,5–2; gençte 2–2,5; diğer gruplarda 2–3 dakikadır. Bunlar ürünün pilot ayarlarıdır; akademik olarak doğrulanmış yaş normları değildir. Metinlerin yaşa uygunluğu ve zorluk derecesi eğitimci değerlendirmesi gerektirir.

## İçerik ve değişiklik sınırı

- `settings.json`: 16 mevcut ayar ve dört yeni 5. seviye ayarı; toplam 20.
- `content.json`: her yaş/seviye için üç ayrı metin; toplam 60 egzersize bağlı kayıt. Bunlar `texts/` içindeki **üç özgün anlatının tam paragraf seçkileridir**, 60 bağımsız özgün eser değildir. Kaynak anlatı farklı yaş/seviyelerde tekrar kullanılabilir. Mevcut ortak metinler değiştirilmez.
- Oturum seçiminde aktif, dil/yaş/seviye/egzersiz sahipliği ve kelime bandı uygun gerçek metinler kullanılır. Geçmiş kullanıma göre az kullanılan metinler önceliklidir; uygun içerik yoksa yedek metinle devam edilmez. Bu sıralama her oturumda kesin olarak farklı metin garantisi değildir.
- `programs.json`: dört mevcut 5. seviye v2 şablonunun her birindeki 16 Gruplama görevi 4'ten 5'e yükselir. 28 gün, 182 görev, günlük 6–7 görev ve diğer egzersizlerin sırası korunur.
- Öğrencide kaydedilmiş `ScheduleJson`, geçmiş sonuçlar ve açık oturumlar değiştirilmez. Yeni oturumlar cümle/paragraf sınırlarını sunucudan alır; eski sürümsüz oturumlar eski sabit grup yapısını korur.

## Yeniden üretme ve doğrulama

```powershell
node content-packs/grouping/v1/generate.mjs
git diff -- content-packs/grouping/v1/settings.json content-packs/grouping/v1/content.json content-packs/grouping/v1/programs.json content-packs/grouping/v1/apply.sql
dotnet test tests/Unit/SpeedReading.Application.UnitTests/SpeedReading.Application.UnitTests.csproj --filter FullyQualifiedName~Grouping
dotnet test tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj --filter FullyQualifiedName~SpeedReadingGroupingPostgresTests
```

Üretici yalnız bu paket dosyalarını yeniden oluşturur. Tarihsel yaş kataloğu/v2 program paketlerini değiştirmez. Yeni kurulumda önce mevcut yaş katalogları ve dört v2 program paketi, ardından bu ek paket uygulanır.

## Kontrollü yayın ve geri dönüş

1. Ayrı yayın onayı alın. Kullanılacak API ve frontend sürümlerini sabitleyin. `speedreading_owned_db` hedefini ve `speed_reading` şemasını doğrulayın.
2. Speed Reading veritabanının yedeğini alın; ayrı geçici veritabanında geri yüklenebilirliğini kontrol edin. 20 egzersiz kimliği, 60 yeni metin kimliği, dört şablon ve Chunking türünün mevcut satırlarını ayrıca geri dönüş için dışa aktarın. Yedeklere kısıtlı erişim verin.
3. Kısa kontrollü bakım aralığında yeni API/istemciyi yayımlayın ve aşağıdaki SQL'i **yalnız Speed Reading veritabanına**, `ON_ERROR_STOP` ile çalıştırın. SQL tek transaction'dır; kilit ve süre sınırları vardır. Kaynak ayarlar beklenenden farklıysa durur; denetimi kaldırıp zorla uygulamayın.

   ```sh
   docker exec -i postgres psql -v ON_ERROR_STOP=1 -U eduplatform -d speedreading_owned_db < content-packs/grouping/v1/apply.sql
   ```

4. 20 aktif yaş/seviye ayarını, her birinde en az üç uygun metni ve dört şablondaki 16 yeni 5. seviye görevi doğrulayın. Sağlık, giriş, öğrenci kayıtlı çalışma, admin/öğretmen önizleme ve mobil sonuç ekranlarını kontrol edin. Öğrenci program snapshot'larının değişmediğini kontrol edin.
5. Tekrar uygulama değişiklik yapmaz. Paket sonrasında yönetici hedef ayar/metinleri düzenlemişse yeniden uygulama durur; yeni inceleme gerekir.
6. Geri dönüş gerekirse önce yazımları kontrollü durdurun ve alınan satır yedeğiyle **yalnız hedef ayar/tür/şablonları** geri döndürün; uygun API/frontend sürümüne dönün. Yeni metin/egzersizleri, sonradan oluşmuş oturum referansları kontrol edilmeden kalıcı silmeyin; gerektiğinde pasife alın. Yeni öğrenci verileri oluştuysa tüm canlı DB'yi eski yedekle ezmeyin. Açık/yeni 5. seviye oturumların uyumluluğunu ayrıca doğrulayın.

Bu dosya bir canlı yayın kanıtı değildir. Gerçek oturumla yayın sonrası kabul testi ve öğretmen içerik değerlendirmesi ayrı aşamalardır.
