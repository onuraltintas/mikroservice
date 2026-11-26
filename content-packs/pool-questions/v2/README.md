# Ortak havuz soruları v2 (26.11.2026)

Ortak havuza taşınan 79 Serbest Okuma metninin hiç sorusu yoktu. Bu paket her birine **8 anlama sorusu** ekler
(toplam 632). Metinler değişmez; yalnız `speed_reading.reading_questions` tablosuna satır eklenir.

**Önce `shared-pool/v1` uygulanmış olmalı** (metinler o paketle havuza geçer). `pool-questions/v1` ile birbirinden
bağımsızdır; ikisi aynı metne dokunmaz, sırası önemli değildir.

## Etkisi
Her yaş ve seviyede 2–5 metin daha sorulu hâle gelir:

| | S1 | S2 | S3 | S4 | S5 |
|---|---|---|---|---|---|
| Çocuk | 4 | 4 | 3 | 3 | 5 |
| Genç | 3 | 5 | 5 | 5 | 5 |
| Genç yetişkin | 4 | 5 | 5 | 3 | 2 |
| Yetişkin | 4 | 5 | 5 | 2 | 2 |

Bu metinler artık en az 4 soru isteyen egzersizlere de (Okuduğunu Anlama'nın çalışma bölümü gibi) çıkabilir.

## Soru kuralları
v1 ile aynı kurallar (`../rules.mjs`); paket testleri her soruyu bu kurallardan geçirir. Ayrıntı: `../v1/README.md`.
- **Cevap harfleri:** her metinde A, B, C, D ikişer kez. Sayısal şıklar küçükten büyüğe dizildiği için üç metinde
  dağılım farklı: 066 ve 071'de 1-3-2-2, 077'de 3-2-1-2. Bütün pakette A 157, B 160, C 157, D 158.
- **Okumadan tahmin denetimi (paket geneli, testte):** hep gözle en uzun şıkkı seçen %27,1, hep en kısayı seçen %23,3,
  hep metinle en çok sözcük paylaşan şıkkı seçen %27,9 tutturur; şans %25, test sınırı %35.

## Dosyalar
- `source/*.json`: elle yazılan sorular, metin numarasına göre (1–79; `TARGETS` kimliğe göre sıralı). Doğru şık `correct`, çeldiriciler `wrong`.
- `generate.mjs`: metinleri canlı anlık görüntüden okur (`content-packs/live-snapshot/20261126`), kuralları denetler,
  `questions.json`, `apply.sql` ve `rollback.sql` üretir. `node generate.mjs --check` yalnız denetler.
- Soru kimlikleri `d7b50000-0000-5000-8000-…`, `created_by = 'pool-questions-v2'`.

## Uygulama (yalnız Hızlı Okuma veritabanı)
```sh
docker exec -i postgres sh -c 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d speedreading_owned_db' < content-packs/pool-questions/v2/apply.sql
```
- Tek işlem. 79 metnin hepsi silinmemiş ve içeriği aynı (md5) değilse, ya da bu metinlerde bu paketten olmayan bir soru
  varsa hiçbir şey değiştirmeden durur.
- Beklenen çıktı: `632 question(s) added, 0 restored`. İkinci çalıştırma `0, 0`.
- Kod değişikliği ya da servis yeniden başlatma gerekmez.

## Geri alma
`rollback.sql` yalnız bu paketin sorularını yumuşak siler (`is_deleted`). `apply.sql` yeniden çalıştırılırsa geri alma ile
silinen sorular geri gelir.

## Yerel deneme (26.11.2026, PostgreSQL 16, canlı anlık görüntüyle)
Uygula 632 / 0; tekrar 0 / 0; geri al 632; yeniden uygula 0 / 632 geri geldi. İçeriği değişmiş bir metinde "78 of 79"
diyerek, başka kaynaktan soru eklenmiş bir metinde "1 other question(s)" diyerek hiçbir şey değiştirmeden durdu.
