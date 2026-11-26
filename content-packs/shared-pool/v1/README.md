# Ortak metin havuzu v1 (26.11.2026)

**Karar (kullanıcı, 26.11.2026):** metinler egzersize göre bölünmez. Düz okuma egzersizlerinin hepsi tek ortak havuzdan,
kendi koşullarıyla (yaş, seviye, kelime aralığı, soru sayısı) metin çeker. Ayrıntı: proje notu `hizli-okuma-metin-havuzu-plani.md`.

## Ne yapar
- **246 metnin egzersiz bağını kaldırır** (`exercise_id` → boş): Serbest Okuma 79, Kelime Gruplama 57, İç Seslendirme 35,
  Geri Dönüş 34, Hızlı Akış 22, Okuduğunu Anlama (çalışma) 16, Metin Solma 3.
- **Taşınmayanlar:**
  - Hata Analizi, Tarama, Gözden Geçirme ve tempolu Hızlı Okuma metinleri (yapısı egzersize göre kurulmuş).
  - Okuduğunu Anlama'nın değerlendirme metinleri ve onlarla aynı öyküden metinler (ölçüm bozulmasın).
  - Aynı öykünün başka bir seçkisi o yaşta havuzda zaten varsa (87 metin). Aynı öykü: 6 kelimelik dizilerin en az %30'u ortak.
- **Kelime Gruplama'nın 20 egzersizinin kelime aralığını genişletir:** mevcut aralık ile havuzdaki o yaş ve seviyenin en kısa
  ve en uzun metni (10'a yuvarlı). Aralıklar yalnız genişler; 17'si değişir. Değerler: `bands.json`.
- Kaynak canlı anlık görüntüdür: `content-packs/live-snapshot/20261126`. Seçimi `generate.mjs` yapar; `moves.json` taşınanları
  ve atlananları nedenleriyle listeler.

## Sonuç (anlık görüntüye göre)
Her yaş ve seviyede, en az 4 sorulu ve bütün düz okuma egzersizlerinin görebildiği metin 11–19'dan 15–27'ye çıkar; sorusuz
taşınan 136 metin (Serbest Okuma ve Kelime Gruplama) soru eklenince bu sayı 21–35 olur.

## Uygulama (yalnız Hızlı Okuma veritabanı)
```sh
docker exec -i postgres sh -c 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d speedreading_owned_db' < content-packs/shared-pool/v1/apply.sql
```
- Tek işlem. 246 metnin hepsi beklenen egzersize bağlı (ya da zaten taşınmış), silinmemiş, etkin ve içeriği aynı (md5)
  değilse, ya da 20 gruplama egzersizinin aralığı beklenen eski ya da yeni değerde değilse hiçbir şey değiştirmeden durur.
- Beklenen çıktı: `246 text(s) moved to the shared pool, 17 Kelime Gruplama range(s) widened`. İkinci çalıştırma `0, 0`.
- Kod değişikliği ya da servis yeniden başlatma gerekmez; yeni oturumlar hemen yeni havuzdan seçer. Açık oturumlar kendi
  metniyle sürer.

## Geri alma
`rollback.sql`: yalnız bu paketin değiştirdiği ve o günden beri değişmemiş satırları eski egzersizine bağlar ve aralıkları
eski değerine döndürür.

## Yerel deneme (26.11.2026, PostgreSQL 16, canlı anlık görüntüyle)
Uygulama 246 / 17; ikinci uygulama 0 / 0; geri alma 246 / 17 ve bütün satırlar ilk hâliyle birebir aynı; içeriği değişmiş
bir metinde "245 of 246" diyerek hiçbir şey değiştirmeden durdu.
