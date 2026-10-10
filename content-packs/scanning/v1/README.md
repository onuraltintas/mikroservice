# Tarama: yaş/seviye ayarları ve metne bağlı görevler

Durum: yerelde geliştirildi ve izole PostgreSQL'de test edildi. Canlıya uygulanmadı.

## Parametreler

Her sütundaki sıralama Seviye 1 → 5'tir. Sayılar bilimsel yaş normu veya okuma hızı ölçümü değil, kullanıcı denemeleriyle ayarlanacak pilot değerlerdir.

| Yaş | Metin uzunluğu (kelime) | Hedef sayısı | Azami arama süresi (saniye) | Yazı boyutu |
| --- | --- | --- | --- | --- |
| Çocuk (9–12) | 100–150 / 130–180 / 160–220 / 200–260 / 240–300 | 2 / 3 / 4 / 4 / 5 | 60 / 75 / 90 / 105 / 120 | 24px |
| Genç (13–16) | 150–210 / 190–260 / 230–310 / 270–360 / 320–420 | 3 / 4 / 4 / 5 / 6 | 60 / 75 / 90 / 105 / 120 | 22px |
| Genç yetişkin (17–21) | 180–250 / 230–310 / 280–380 / 340–450 / 400–520 | 3 / 4 / 5 / 6 / 6 | 60 / 75 / 90 / 105 / 120 | 20px |
| Yetişkin (22+) | 200–280 / 260–350 / 320–420 / 380–500 / 450–580 | 3 / 4 / 5 / 6 / 6 | 75 / 90 / 105 / 120 / 135 | 20px |

- Tek oturumda tek metin; otomatik hızlandırma veya zorluk uyarlaması yok.
- S1–S2: gerçek metinden farklı sözcükler rastgele seçilir. En uzun sözcükleri seçme yanlılığı kaldırıldı. Hedefler en az 3 karakter; üst sınır çocukta 12, gençte 15, diğerlerinde 18 karakter.
- S3–S5: kişi, tarih, kontenjan, salon, süre ve malzeme sayısını bulmaya yönelik açık sorular. Soruların cevapları yayımlanan metinde birebir vardır; isim tanıma/NLP tahmini yapılmaz.
- Metni baştan sona okuma veya anlama testi değildir. Sonuçlarda WPM/KDP üretilmez; bulunan hedef, yanlış tıklama, arama süresi ve hedef yanıt süreleri kaydedilir.
- Süre dolarsa kısmi sonuç korunur; tamamlanmış gün/XP kazanımı verilmez.
- Hedef sözcükler bulunmadan önce diğer sözcüklerden farklı vurgulanmaz.
- Eski sonuçlar, programlar, metinler ve egzersiz kimlikleri korunur.

## İçerik

`content.json`: her yaş/seviye egzersizine 3 özgün, kurgusal duyuru; toplam 60 metin. Kitap keşfi, doğa gözlemi ve tasarım atölyesi temaları kullanılır. Metinlerdeki kişiler, tarih ve miktarlar gerçek etkinlik ilanı değildir. Bu başlangıç havuzudur; içerik çeşitliliği yeni metinlerle genişletilebilir.

Metinler normal `reading_texts` kayıtlarıdır, egzersize/yaşa/seviyeye bağlıdır. İlk iki seviyede koşullara uyan önceki metinler de kullanılabilir. Bilgi görevlerinde yalnız `tasksByTextId` ile doğrulanmış metin havuzu seçilir. En az kullanılan uygun metin tercih edilir; eşitlikte rastgele seçim yapılır. Kayıtlı oturum devam ederken metin/hedefler değiştirilmez.

`settings.json`: yirmi mevcut Scanning egzersizinin eski/yeni ayarları. Başka egzersizlerin katalog dosyaları geriye dönük yeniden yazılmaz; bu sürüm kontrollü bir ayar geçişidir.

## Görev düzenleme

Mevcut egzersizin JSON ayarlarında:

```json
{
  "targets": {
    "mode": "find_all",
    "caseSensitive": false,
    "tasksByTextId": {
      "d7a10000-0000-5000-8000-000000000007": [
        { "answer": "Kerem", "prompt": "Hazırlık sorumlusu kim?" }
      ]
    }
  }
}
```

Örnek şemadır; gerçek kimlik/cevap ilgili yayımlanmış metinle eşleşmelidir. Kimlikler küçük harfli UUID olmalı. Cevap tek sözcük/token, soru en fazla 300 karakterdir. Her cevap metinde bulunmalıdır; yinelenen cevaplar reddedilir. Çok sözcüklü cevaplar bu sürümde desteklenmez.

Kayıtlı ayarlarda görev sayısı `targetCount` ile eşit olmalıdır. Özel önizlemede hedef sayısı azaltılırsa ilk görevler kullanılır; artırılırsa kalan sayı gerçek metindeki farklı sözcüklerle tamamlanır. Katalog/veritabanı değişmez ve önizleme sonucu kaydedilmez.

## Yayın ve geri dönüş

1. Hızlı Okuma veritabanı yedeği alınır; mevcut yirmi egzersizin kimlik, durum ve ayarları yeniden kontrol edilir.
2. Hızlı Okuma API ve frontend bu kod sürümüne geçirilir. Ortak Identity veya Koçluk DB'sine değişiklik gerekmez.
3. `apply.sql` yalnız Hızlı Okuma veritabanında, `psql -v ON_ERROR_STOP=1` ile uygulanır. Beklenmedik katalog/ayar değişikliğinde işlemin tamamı geri alınır. Aynı dosya ikinci kez çalışınca ek metin/katalog çoğaltmaz.
4. Sağlık kontrolleri ve öğrenci kayıtlı oturumu, admin/öğretmen önizleme kontrolleri yapılır.
5. Geri dönüş gerekirse `settings.json.previousConfiguration` değerleri yalnız kimliği doğrulanmış yirmi egzersize geri yazılır; yeni `scanning-v1` metinleri arşivlenir/pasifleştirilir. Sonuç ve oturum kayıtları silinmez. Kullanıcıların yeni sonuçlarını kaybetmemek için tüm veritabanını gelişigüzel yedekten geri yüklemeyin. Pasifleştirilmiş paket kendiliğinden yeniden etkinleştirilmez; yeniden yayın ayrıca kontrol gerektirir.
