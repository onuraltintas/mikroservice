# Ortak havuz metinleri v1 (27.11.2026)

Ortak havuza **290 yeni okuma metni** ve her birine **8 anlama sorusu** ekler (toplam 2.320 soru). Metinler, havuzda
eksik kalan yaş, seviye ve kelime aralıklarını (yuvalar, `targets.json`) doldurur. Var olan hiçbir metin ya da soru
değişmez; yalnız `speed_reading.reading_texts` ve `speed_reading.reading_questions` tablolarına satır eklenir.

`shared-pool/v1`, `pool-questions/v1` ve `pool-questions/v2` paketlerinden bağımsızdır; aynı satırlara dokunmaz, sırası önemli değildir.

## Etkisi
Yaş ve seviyeye göre eklenen metin sayısı:

| | S1 | S2 | S3 | S4 | S5 | Toplam |
|---|---|---|---|---|---|---|
| Çocuk | 13 | 17 | 19 | 22 | 25 | 96 |
| Genç | 16 | 14 | 11 | 9 | 14 | 64 |
| Genç yetişkin | 15 | 14 | 14 | 14 | 15 | 72 |
| Yetişkin | 18 | 7 | 8 | 10 | 15 | 58 |

Türler: 169 bilgi/açıklama, 109 öykü, 11 deneme, 1 karma. Metinler yaş grubunun diline ve ilgisine göre yazıldı.
Çocuk metinleri kısa ve somut, yetişkin metinleri 1.000 kelimeye kadar çıkıyor. Hiçbiri canlıdaki bir metinle ya da pakette
başka bir metinle örtüşmüyor (6 sözcüklük kayan pencere, eşik %30). Aynı yaş grubunda iki metin aynı başlığı taşımıyor.

## Soru kuralları
`../../pool-questions/rules.mjs`; paket testleri her soruyu bu kurallardan geçirir.
- Her metinde 8 soru: en az 3 bilgi (doğrudan metinde), en az 3 çıkarım, en az 1 ana fikir sorusu. İlk ve son
  paragraftan da soru var. Her metinde en az bir soru Bloom 4 ya da üstü (Çocuk S1–2'de Bloom 3).
- **Şık uzunluğu:** en uzun ve en kısa şıkkın oranı en çok 1,3; kısa şıklarda 6 harfe kadar fark serbest. Doğru şık tek
  başına en uzun ya da en kısa olamaz.
- **Yasak sözcükler:** “hep, her zaman, asla, hiçbir zaman, kesinlikle, tamamen, hepsi, hiçbiri, yukarıdakiler” gibi
  ipucu veren sözcükler hiçbir şıkta yok (pakette ayrıca denetlendi). Soru kökleri olumsuz değil.
- **Cevap harfleri:** her metinde A, B, C, D ikişer kez (sayısal şıklar sıralandığı için birkaç metinde 1–3 arası).
  Bütün pakette A 578, B 579, C 589, D 574.
- **Okumadan tahmin denetimi (paket geneli, testte):** hep en uzun şıkkı seçen %28,1, hep en kısayı seçen %22,1, hep
  metinle en çok sözcük paylaşan şıkkı seçen %31,9 tutturur. Şans %25, test sınırı %35.

## Dosyalar
- `targets.json` (`targets.py` üretir): 290 yuva; her biri yaş grubu, seviye ve kelime aralığı.
- `source/*.json`: elle yazılan metinler ve sorular, yuva numarasıyla. Doğru şık `correct`, çeldiriciler `wrong`.
- `generate.mjs`: kuralları ve canlı anlık görüntüyle (`content-packs/live-snapshot/20261126`) örtüşmeyi denetler,
  `texts.json`, `questions.json`, `apply.sql` ve `rollback.sql` üretir. `node generate.mjs --check` yalnız denetler.
- Metin kimlikleri `d7b60000-0000-5000-8000-…`, soru kimlikleri `d7b70000-0000-5000-8000-…`, `created_by = 'pool-texts-v1'`.

## Uygulama (yalnız Hızlı Okuma veritabanı)
```sh
docker exec -i postgres sh -c 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d speedreading_owned_db' < content-packs/pool-texts/v1/apply.sql
```
- Tek işlem. Bu kimliklerden biri başka bir kaynaktan ya da başka içerikle (md5) zaten varsa, ya da bu metinlerde bu
  paketten olmayan bir soru varsa hiçbir şey değiştirmeden durur.
- Beklenen çıktı: `290 text(s) added, 0 restored; 2320 question(s) added, 0 restored`. İkinci çalıştırma hepsi 0.
- Kod değişikliği ya da servis yeniden başlatma gerekmez.

## Geri alma
`rollback.sql` yalnız bu paketin metinlerini ve sorularını yumuşak siler (`is_deleted`). `apply.sql` yeniden
çalıştırılırsa geri alma ile silinenler geri gelir.

## Yerel deneme (27.11.2026, PostgreSQL 16, canlı anlık görüntüyle)
Canlı görüntüdeki 831 metin ve 4.350 soru yüklü veritabanında:
- uygula: 290 / 2.320 eklendi;
- tekrar: 0;
- geri al: 290 / 2.320 yumuşak silindi;
- yeniden uygula: 290 / 2.320 geri geldi.

İçeriği değişmiş bir metinde ve başka kaynaktan soru eklenmiş bir metinde “1 conflicting row(s)” diyerek hiçbir şey
değiştirmeden durdu. Canlı satırlara dokunulmadı.
