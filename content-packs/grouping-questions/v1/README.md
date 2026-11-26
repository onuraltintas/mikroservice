# Kelime Gruplama paket metinleri: anlama soruları v1 (26.11.2026)

Kelime Gruplama her metinden sonra tam 4 anlama sorusu sorar ve yalnız en az 4 geçerli sorusu olan metinleri seçer
(kullanıcı kararı, 26.11.2026). `content-packs/grouping/v1` paketinin 60 metninin sorusu yoktu. Bu paket her birine
4 soru ekler (toplam 240). Metinler ve egzersizler değişmez.

## Nasıl üretildi
- Paketin 60 metni üç öykünün (`grouping/v1/texts/library.txt`, `garden.txt`, `map.txt`) ilk N paragrafıdır (N = 5–21).
- Her öykü için 15–16 soruluk bir havuz yazıldı (`banks/`). Her soru, cevabının bulunduğu son paragrafla etiketlidir (`need`).
- Bir metne yalnız `need <= N` olan sorular düşer. Uzun metinlerde (N ≥ 12) ilk üçte birdeki sorular atlanır; kalanlar
  arasından metnin baştan sonuna yayılan 4 soru seçilir (`generate.mjs`).
- Kurallar: metne dayalı, sade ve 9 yaş ve üstüne uygun dil; tür 1–3 (gerçek anlam, çıkarım, değerlendirme), Bloom 1–6;
  doğru şık tek başına en uzun değil; cevaplar A–D arasında dengeli; her metinde en az bir çıkarım ya da değerlendirme sorusu;
  her sorunun metne dayanan kısa açıklaması var. `pack.test.mjs` bunları denetler (CI'da çalışır).

## Uygulama (yalnız Hızlı Okuma veritabanı)
```sh
docker exec -i postgres sh -c 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d speedreading_owned_db' < content-packs/grouping-questions/v1/apply.sql
```
- Tek işlemdir. 60 metnin hepsi silinmemiş ve içeriği değişmemiş (md5) değilse hiçbir şey değiştirmeden durur.
- Beklenen çıktı: `240 question(s) added`. İkinci çalıştırma `0` ekler.
- Kelime Gruplama'nın 4 soru kuralını içeren sürüm yayınlandıktan hemen sonra uygulanmalıdır.

## Geri alma
`rollback.sql` yalnız bu paketin 240 sorusunu yumuşak siler (`is_deleted`). Geri almadan sonra yeniden uygulamak bu kimlikleri
atlar; gerekirse soruların silinme işareti kaldırılmalıdır.

## Yerel deneme (26.11.2026, PostgreSQL 16)
Uygulama 240 / 60 metin; ikinci uygulama 0; geri alma 240; içeriği değişmiş bir metinde uygulama hiçbir şey yapmadan durdu.
