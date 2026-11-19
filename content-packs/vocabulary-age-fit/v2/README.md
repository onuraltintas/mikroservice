# Kelime havuzu yaş uyumu v2: seviye 3 (19.11.2026)

v1'in devamı (karar K4: "uygunsuz olan kelimeleri uygun kelimelerle değiştir"). v1 seviye 4–5'i düzenledi; bu paket seviye 3'ü düzenler. İki paket birbirinden bağımsızdır.

## Sorun
Seviye 3 ortak havuzunda 9–12 yaşa uygun olmayan iş, ekonomi ve insan kaynakları kelimeleri vardı: Likidite, Cari Açık, Bordro, Damga Vergisi, Freelance gibi. Çocuk programının seviye 3 kelime testleri bu havuzdan soruyordu.

Sayılar 19.11.2026'da admin Kelime havuzu sayfasından salt okunur alındı: seviye 3'te 323 ortak kelime.

## Ne yapar
| Grup | Kelime | Kim görür |
|---|---:|---|
| Ekonomi, İnsan Kaynakları, İş Dünyası, Yönetim, Hukuk | 154 | Yetişkin ve Genç yetişkin |
| Diğer akademik, siyasi ve tarih terimleri (Hipotez, Laiklik, Emperyalizm…) | 130 | Genç ve üstü |
| Coğrafya ve sekiz temel kelime (Anıt, Demokrasi, Taht, Destan, Adalet, Astronomi, Kütleçekimi, Kültür) | 39 | Herkes (ortak kalır) |

Bunlara ek olarak Çocuk için 25 yeni kelime eklenir (Pusula, Gezegen, Hasat, Kervan, Müze…; `words.json`).

**Görünen havuzlar:**
- Çocuk: 64
- Genç: 169
- Genç yetişkin ve Yetişkin: 323 (değişmez)

**Değişmeyenler:** Seviye 1, 2, 4 ve 5; egzersiz ayarları; ders ve müfredat kayıtları. Öğrenci geçmişi silinmez.

Uygulama sonunda her yaşın seviye 3 testi için yeterli ve belirsizliği olmayan kelime olduğu, Çocuk'un hiçbir iş veya ekonomi kelimesi görmediği denetlenir. Denetim tutmazsa işlemin tamamı geri alınır.

## Bilinen etkiler
- Kopyalar yeni kayıt olduğu için Genç yetişkin (ve "Genç ve üstü" grubunda Yetişkin) kullanıcıların bu kelimelerdeki tekrar sırası sıfırdan başlar.
- Çocuk'un bu kelimelerdeki eski tekrarları artık önüne gelmez.

## Uygulama
1. Hızlı Okuma veritabanının yedeğini al.
2. `psql ... -v ON_ERROR_STOP=1 -f apply.sql` çalıştır.
   Çıktıdaki tablo: Çocuk 25, Genç 130, Yetişkin 284, Genç yetişkin 284, ortak 39.
3. Ortak kelime sayıları değişmişse SQL "baseline changed" diye durur; zorlamadan bildir.

## Geri alma
`rollback.sql` kelimeleri ortak havuza döndürür; kopyaları ve yeni kelimeleri yumuşak siler, yedek tablosunu kaldırır.

## Doğrulama
- `node --test content-packs/vocabulary-age-fit/v2/pack.test.mjs`
- Geçici PostgreSQL 16'da, canlıdaki kategori dağılımıyla (323 kelime) denendi:
  - uygulama;
  - Çocuk'un iş ve ekonomi kelimesi görmemesi (0);
  - ikinci uygulamanın reddi;
  - geri alma (323 kelime ortak havuza döndü).
