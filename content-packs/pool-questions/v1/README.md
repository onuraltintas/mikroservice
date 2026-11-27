# Ortak havuz soruları v1 (26.11.2026)

Ortak havuza taşınan 57 Kelime Gruplama metninin hiç sorusu yoktu. Bu paket her birine **8 anlama sorusu** ekler
(toplam 456). Metinler değişmez; yalnız `speed_reading.reading_questions` tablosuna satır eklenir.

**Önce `shared-pool/v1` uygulanmış olmalı** (metinler o paketle havuza geçer). Bu paket metnin egzersiz bağına bakmaz,
ama sorular ancak metin havuzdayken bütün düz okuma egzersizlerine yarar.

## Etkisi
Her yaş ve seviyede 3 metin sorulu hâle gelir (Genç 5. seviye 1, Genç yetişkin 4. seviye 2). Bu metinler artık en az 4
soru isteyen egzersizlere de (Okuduğunu Anlama'nın çalışma bölümü gibi) çıkabilir.

## Soru kuralları
Kurallar `../rules.mjs` içinde, paket testleri her soruyu bu kurallardan geçirir. Proje notu: `hizli-okuma-metin-havuzu-plani.md`.
- **Şıklar:** uzunlukları birbirine yakın (en uzun / en kısa en çok 1,3 ya da fark en çok 6 harf). Doğru şık tek başına en
  uzun ya da en kısa olamaz. Noktalama aynı. "Hepsi", "hiçbiri" gibi şıklar yok.
- **Kök ve cevap:** olumsuz soru kökü yok. "Her zaman, asla, tamamen" gibi kesin sözler yalnız doğru şıkta olamaz. Kökteki
  sözcükler doğru şıkta tekrarlanıp ipucu vermez. Bir sorunun cevabı başka bir sorunun kökünde geçmez.
- **Metin başına:** 8 soru; en az 3 gerçek anlam, 3 çıkarım, 1 eleştirel/yorum sorusu. En az iki Bloom basamağı ve en az
  bir analiz ya da üstü soru (küçük çocukların 1–2. seviyesinde uygulama yeterli). İlk ve son paragraftan da soru var.
- **Cevap harfleri:** her metinde A, B, C, D ikişer kez, sırası metin kimliğinden. Sayısal şıklar küçükten büyüğe dizilir;
  bu yüzden bir metinde (021) dağılım 1-2-2-3. Bütün pakette A 114, B 113, C 114, D 115.
- Her sorunun dayandığı paragraf (`p`) ve kısa bir açıklama (20–300 harf) var; açıklama öğrenciye cevaptan sonra gösterilir.
- **Okumadan tahmin denetimi (paket geneli, testte):** uzunluk gözle algılandığı gibi karşılaştırılır (fark en çok 3 harf
  ya da %10 ise eşit). Hep gözle en uzun şıkkı seçen %28,5, hep en kısayı seçen %20,1, hep metinle en çok sözcük paylaşan
  şıkkı seçen %24,8 tutturur; şans %25, test sınırı %35. Bunun için doğru şıklar metnin cümlesini kopyalamaz, kendi
  sözcükleriyle söyler; çeldiriciler çoğunlukla metindeki başka ayrıntılardan kurulur.

## Dosyalar
- `source/*.json`: elle yazılan sorular, metin numarasına göre (`d7b20000-…-000000000NNN`). Doğru şık `correct`, çeldiriciler `wrong`.
- `generate.mjs`: metinleri canlı anlık görüntüden okur (`content-packs/live-snapshot/20261126`), kuralları denetler,
  `questions.json`, `apply.sql` ve `rollback.sql` üretir. `node generate.mjs --check` yalnız denetler.
- Soru kimlikleri `d7b40000-0000-5000-8000-…`, `created_by = 'pool-questions-v1'`.

## Uygulama (yalnız Hızlı Okuma veritabanı)
```sh
docker exec -i postgres sh -c 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d speedreading_owned_db' < content-packs/pool-questions/v1/apply.sql
```
- Tek işlem. 57 metnin hepsi silinmemiş ve içeriği aynı (md5) değilse, ya da bu metinlerde bu paketten olmayan bir soru
  varsa hiçbir şey değiştirmeden durur.
- Beklenen çıktı: `456 question(s) added, 0 restored`. İkinci çalıştırma `0, 0`.
- Kod değişikliği ya da servis yeniden başlatma gerekmez.

## Geri alma
`rollback.sql` yalnız bu paketin sorularını yumuşak siler (`is_deleted`). `apply.sql` yeniden çalıştırılırsa geri alma ile
silinen sorular geri gelir.

## Yerel deneme (26.11.2026, PostgreSQL 16, canlı anlık görüntüyle)
Uygula 456 / 0; tekrar 0 / 0; geri al 456; yeniden uygula 0 / 456 geri geldi. İçeriği değişmiş bir metinde "56 of 57"
diyerek, başka kaynaktan soru eklenmiş bir metinde "1 other question(s)" diyerek hiçbir şey değiştirmeden durdu.
